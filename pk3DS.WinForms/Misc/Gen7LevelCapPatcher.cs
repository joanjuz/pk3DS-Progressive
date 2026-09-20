using pk3DS.Core.Modding.Research;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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

        byte[] battle = (byte[])battleOriginal.Clone();
        byte[] code = (byte[])codeOriginal.Clone();

        List<LevelCapSite> sites = LevelCapPatch.Install(battle, code, table);
        if (sites.Count != 2 || sites.Any(z => !z.Success))
        {
            report = string.Join(Environment.NewLine, sites.Select(z => z.ToString()));
            throw new InvalidDataException(
                "Player Level Caps could not be installed safely. No files were changed." +
                Environment.NewLine + Environment.NewLine + report);
        }

        int changed = sites.Count(z => z.Changed);
        report = string.Join(Environment.NewLine, sites.Select(z => z.ToString()));

        if (changed == 0)
            return 0;

        if (sites.Any(z => z.Binary == "Battle.cro" && z.Changed))
        {
            BackupOnce(battlePath, ".bak_player_level_caps");
            UpdateCroHashes(battle);
        }

        if (sites.Any(z => z.Binary == "code.bin" && z.Changed))
            BackupOnce(codePath, ".bak_player_level_caps");

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

    private static void BackupOnce(string path, string suffix)
    {
        string backup = path + suffix;
        if (!File.Exists(backup))
            File.Copy(path, backup);
    }

    private static void UpdateCroHashes(byte[] data)
    {
        int codeStart = ReadInt32(data, 0xB0);
        int codeSize = ReadInt32(data, 0xB4);
        int dataStart = ReadInt32(data, 0xB8);
        int dataSize = ReadInt32(data, 0xBC);
        int rodataStart = ReadInt32(data, 0xC0);

        ValidateRange(data, 0x80, 0x100, "CRO header");
        ValidateRange(data, codeStart, codeSize, "code segment");

        int rodataSize = dataStart - rodataStart;
        ValidateRange(data, rodataStart, rodataSize, "rodata segment");
        ValidateRange(data, dataStart, dataSize, "data segment");

        byte[][] hashes =
        [
            SHA256.HashData(data.AsSpan(0x80, 0x100)),
            SHA256.HashData(data.AsSpan(codeStart, codeSize)),
            SHA256.HashData(data.AsSpan(rodataStart, rodataSize)),
            SHA256.HashData(data.AsSpan(dataStart, dataSize)),
        ];

        for (int i = 0; i < hashes.Length; i++)
            hashes[i].CopyTo(data, i * 0x20);
    }

    private static int ReadInt32(byte[] data, int offset)
    {
        if (offset < 0 || offset + 4 > data.Length)
            throw new InvalidDataException("Battle.cro has an invalid CRO header.");

        return BitConverter.ToInt32(data, offset);
    }

    private static void ValidateRange(byte[] data, int offset, int length, string name)
    {
        if (offset < 0 || length < 0 || offset > data.Length || length > data.Length - offset)
            throw new InvalidDataException($"Battle.cro contains an invalid {name} range.");
    }
}
