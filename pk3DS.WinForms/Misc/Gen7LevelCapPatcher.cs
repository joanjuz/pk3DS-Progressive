using pk3DS.Core.Modding.Research;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace pk3DS.WinForms;

internal static class Gen7LevelCapPatcher
{
    internal const string ActionId = "player.level-caps";

    internal enum PatchState
    {
        MissingFiles,
        Stock,
        Partial,
        Applied,
        Unsupported,
    }

    internal static PatchState GetState()
    {
        if (Main.Config?.USUM != true)
            return PatchState.Unsupported;

        string battlePath = FindBattleCro();
        string codePath = FindCodeBinary();
        if (string.IsNullOrWhiteSpace(battlePath) || string.IsNullOrWhiteSpace(codePath) ||
            !File.Exists(battlePath) || !File.Exists(codePath))
        {
            return PatchState.MissingFiles;
        }

        try
        {
            byte[] battle = File.ReadAllBytes(battlePath);
            byte[] code = File.ReadAllBytes(codePath);

            if (!IsExpectedCro(battle) || !IsDecompressedCode(code))
                return PatchState.Unsupported;

            var battleState = LevelCapPatch.GetBattleState(battle, out _);
            var candyState = LevelCapPatch.GetCandyState(code, out _);

            if (battleState == LevelCapHookState.Unsupported ||
                candyState == LevelCapHookState.Unsupported)
            {
                return PatchState.Unsupported;
            }

            if (battleState == LevelCapHookState.Applied &&
                candyState == LevelCapHookState.Applied)
            {
                return PatchState.Applied;
            }

            if (battleState == LevelCapHookState.Stock &&
                candyState == LevelCapHookState.Stock)
            {
                return PatchState.Stock;
            }

            return PatchState.Partial;
        }
        catch
        {
            return PatchState.Unsupported;
        }
    }

    internal static bool TryReadInstalledTable(out LevelCapTable table)
    {
        table = null;

        string battlePath = FindBattleCro();
        if (string.IsNullOrWhiteSpace(battlePath) || !File.Exists(battlePath))
            return false;

        try
        {
            byte[] battle = File.ReadAllBytes(battlePath);
            return LevelCapPatch.TryReadInstalledBattleTable(battle, out table);
        }
        catch
        {
            table = null;
            return false;
        }
    }

    /// <summary>
    /// Applies both the Battle.cro EXP hook and code.bin Rare Candy hook.
    /// Returns the number of binaries changed (0, 1 or 2).
    /// </summary>
    internal static int Apply(LevelCapTable table, out string report)
    {
        if (Main.Config?.USUM != true)
        {
            throw new NotSupportedException(
                "Player Level Caps are currently validated only for Ultra Sun / Ultra Moon.");
        }

        ArgumentNullException.ThrowIfNull(table);

        var problems = table.Validate();
        if (problems.Count != 0)
            throw new InvalidDataException("Invalid Player Level Caps table: " + problems[0]);

        string battlePath = FindBattleCro();
        if (string.IsNullOrWhiteSpace(battlePath) || !File.Exists(battlePath))
            throw new FileNotFoundException("Battle.cro was not found in the loaded USUM RomFS.");

        string codePath = FindCodeBinary();
        if (string.IsNullOrWhiteSpace(codePath) || !File.Exists(codePath))
        {
            throw new FileNotFoundException(
                "code.bin was not found in the loaded USUM ExeFS. Load an unpacked ExeFS with decompressed code.bin first.");
        }

        byte[] battleOriginal = File.ReadAllBytes(battlePath);
        byte[] codeOriginal = File.ReadAllBytes(codePath);

        if (!IsExpectedCro(battleOriginal))
            throw new InvalidDataException("Battle.cro is not a supported CRO file.");

        if (!IsDecompressedCode(codeOriginal))
        {
            throw new InvalidDataException(
                "code.bin appears to be compressed. Decompress it before enabling Player Level Caps.");
        }

        LevelCapInstallResult install =
            LevelCapPatch.InstallManaged(
                battleOriginal,
                codeOriginal,
                table);

        var sites = install.Sites;
        if (!install.Success)
        {
            report = string.Join(Environment.NewLine, sites.Select(z => z.ToString()));
            throw new InvalidDataException(
                "Player Level Caps could not be installed safely. No files were changed." +
                Environment.NewLine + Environment.NewLine + report);
        }

        byte[] battle = install.BattleCro;
        byte[] code = install.CodeBin;

        int changed = install.ChangedCount;
        report = string.Join(Environment.NewLine, sites.Select(z => z.ToString()));

        if (changed == 0)
            return 0;

        if (sites.Any(z => z.Binary == "Battle.cro" && z.Changed))
            PatchBackupManager.BackupOnce(battlePath, "player-level-caps");

        if (sites.Any(z => z.Binary == "code.bin" && z.Changed))
            PatchBackupManager.BackupOnce(codePath, "player-level-caps");

        // Both in-memory binaries were validated before either disk file is written.
        // This avoids the common partial-install case where Battle.cro is changed before
        // an incompatible code.bin is discovered.
        if (sites.Any(z => z.Binary == "Battle.cro" && z.Changed))
            File.WriteAllBytes(battlePath, battle);

        if (sites.Any(z => z.Binary == "code.bin" && z.Changed))
            File.WriteAllBytes(codePath, code);

        return changed;
    }

    private static bool IsExpectedCro(byte[] data) =>
        data is { Length: >= 0x180 } &&
        Encoding.ASCII.GetString(data, 0x80, 4) == "CRO0";

    private static bool IsDecompressedCode(byte[] data) =>
        data is { Length: > 0 } && (data.Length & 0x1FF) == 0;

    private static string FindBattleCro()
    {
        if (string.IsNullOrWhiteSpace(Main.RomFSPath) || !Directory.Exists(Main.RomFSPath))
            return string.Empty;

        string[] direct =
        [
            Path.Combine(Main.RomFSPath, "Battle.cro"),
            Path.Combine(Main.RomFSPath, "dll", "Battle.cro"),
        ];

        string exact = direct.FirstOrDefault(File.Exists);
        if (!string.IsNullOrWhiteSpace(exact))
            return exact;

        try
        {
            return Directory
                .EnumerateFiles(Main.RomFSPath, "Battle.cro", SearchOption.AllDirectories)
                .FirstOrDefault() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string FindCodeBinary()
    {
        if (string.IsNullOrWhiteSpace(Main.ExeFSPath) || !Directory.Exists(Main.ExeFSPath))
            return string.Empty;

        string dotCode = Path.Combine(Main.ExeFSPath, ".code.bin");
        if (File.Exists(dotCode))
            return dotCode;

        string code = Path.Combine(Main.ExeFSPath, "code.bin");
        if (File.Exists(code))
            return code;

        try
        {
            return Directory
                .EnumerateFiles(Main.ExeFSPath, "*", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(z => Path.GetFileName(z).Contains("code", StringComparison.OrdinalIgnoreCase))
                ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

}
