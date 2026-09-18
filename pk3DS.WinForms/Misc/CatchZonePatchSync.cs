using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace pk3DS.WinForms;

/// <summary>
/// Applies compact Catch Zone patch manifests to files extracted from the user's own ROM dump.
/// Production builds require only catch_zone_patches/XY.json and ORAS.json; complete modified
/// game template files are not required.
/// </summary>
internal static class CatchZonePatchSync
{
    private const int CurrentFormat = 2;
    private const string PatchRootName = "catch_zone_patches";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    internal static bool HasPatchForCurrentGame()
    {
        string path = GetPatchManifestPath();
        return path.Length != 0 && File.Exists(path);
    }

    internal static string ApplyCurrentGameWorkingDirectoryPatches()
    {
        return ApplyCurrentGamePatches(
            scope => scope.Equals("encdata", StringComparison.OrdinalIgnoreCase)
                  || scope.Equals("mapGR", StringComparison.OrdinalIgnoreCase),
            scope => Path.GetFullPath(scope));
    }

    internal static string ApplyCurrentGameRawPatches(string romfsPath)
    {
        if (string.IsNullOrWhiteSpace(romfsPath) || !Directory.Exists(romfsPath))
            throw new DirectoryNotFoundException("The loaded RomFS folder could not be found.");

        return ApplyCurrentGamePatches(
            scope => scope.Equals("raw", StringComparison.OrdinalIgnoreCase),
            _ => Path.GetFullPath(romfsPath));
    }

    private static string ApplyCurrentGamePatches(
        Func<string, bool> scopeFilter,
        Func<string, string> rootResolver)
    {
        CatchZonePatchManifest manifest = LoadCurrentGameManifest();

        int patched = 0;
        int alreadyPatched = 0;
        int deleted = 0;

        foreach (CatchZonePatchFile file in manifest.Files.Where(z => scopeFilter(z.Scope)))
        {
            string root = rootResolver(file.Scope);
            string path = ResolveSafePath(root, file.Path);

            if (file.Operation.Equals("delete", StringComparison.OrdinalIgnoreCase))
            {
                if (!File.Exists(path))
                {
                    alreadyPatched++;
                    continue;
                }

                byte[] existing = File.ReadAllBytes(path);
                VerifyExpectedSource(file, existing);
                File.Delete(path);
                deleted++;
                continue;
            }

            if (!file.Operation.Equals("patch", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    $"Unknown catch-zone patch operation '{file.Operation}' for {file.Scope}/{file.Path}.");

            if (!File.Exists(path))
                throw new FileNotFoundException(
                    $"Required clean file was not found: {file.Scope}/{file.Path}", path);

            byte[] data = File.ReadAllBytes(path);
            string currentHash = ComputeSha256(data);

            if (currentHash.Equals(file.TargetSha256, StringComparison.OrdinalIgnoreCase))
            {
                alreadyPatched++;
                continue;
            }

            VerifyExpectedSource(file, data);

            if (file.PatchMode.Equals("delta", StringComparison.OrdinalIgnoreCase))
            {
                data = ApplyDelta(data, file);
            }
            else
            {
                foreach (CatchZonePatchHunk hunk in file.Hunks.OrderBy(z => z.Offset))
                {
                    byte[] replacement = Convert.FromBase64String(hunk.Data);
                    if (hunk.Offset < 0 || hunk.Offset > data.Length - replacement.Length)
                        throw new InvalidDataException(
                            $"Patch hunk is outside the file bounds: {file.Scope}/{file.Path} @ 0x{hunk.Offset:X}.");

                    Buffer.BlockCopy(replacement, 0, data, hunk.Offset, replacement.Length);
                }

                if (file.TargetLength >= 0 && data.LongLength != file.TargetLength)
                    throw new InvalidDataException(
                        $"Catch-zone hunk patch produced an unexpected length for {file.Scope}/{file.Path}. " +
                        $"Expected {file.TargetLength.ToString(CultureInfo.InvariantCulture)}, " +
                        $"got {data.LongLength.ToString(CultureInfo.InvariantCulture)}.");
            }

            string resultHash = ComputeSha256(data);
            if (!resultHash.Equals(file.TargetSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    $"Catch-zone patch verification failed for {file.Scope}/{file.Path}. " +
                    $"Expected {file.TargetSha256}, got {resultHash}.");

            File.WriteAllBytes(path, data);
            patched++;
        }

        if (patched == 0 && deleted == 0 && alreadyPatched == 0)
            return string.Empty;

        return $"Catch-zone patch: {patched.ToString(CultureInfo.InvariantCulture)} files patched, " +
               $"{deleted.ToString(CultureInfo.InvariantCulture)} files deleted, " +
               $"{alreadyPatched.ToString(CultureInfo.InvariantCulture)} already patched.";
    }

    private static void VerifyExpectedSource(CatchZonePatchFile file, byte[] data)
    {
        if (file.Length >= 0 && data.LongLength != file.Length)
            throw new InvalidDataException(
                $"Unexpected file length for {file.Scope}/{file.Path}. " +
                $"Expected {file.Length.ToString(CultureInfo.InvariantCulture)}, " +
                $"got {data.LongLength.ToString(CultureInfo.InvariantCulture)}.");

        string currentHash = ComputeSha256(data);
        if (!currentHash.Equals(file.SourceSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                $"The file does not match the clean source used to build this patch: {file.Scope}/{file.Path}. " +
                $"Expected SHA-256 {file.SourceSha256}, got {currentHash}. Use a clean compatible dump.");
    }

    private static CatchZonePatchManifest LoadCurrentGameManifest()
    {
        string path = GetPatchManifestPath();
        if (path.Length == 0 || !File.Exists(path))
            throw new FileNotFoundException(
                "No catch-zone patch manifest exists for the currently loaded game.", path);

        CatchZonePatchManifest manifest =
            JsonSerializer.Deserialize<CatchZonePatchManifest>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("The catch-zone patch manifest is empty or invalid.");

        if (manifest.Format != CurrentFormat)
            throw new InvalidDataException(
                $"Unsupported catch-zone patch format {manifest.Format}. Expected {CurrentFormat}.");

        string game = GetGameFolderName();
        if (!manifest.Game.Equals(game, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                $"Catch-zone patch manifest is for {manifest.Game}, but the loaded game family is {game}.");

        return manifest;
    }

    private static byte[] ApplyDelta(byte[] source, CatchZonePatchFile file)
    {
        if (file.TargetLength < 0 || file.TargetLength > int.MaxValue)
            throw new InvalidDataException(
                $"Invalid target length for {file.Scope}/{file.Path}: {file.TargetLength}.");

        using var output = new MemoryStream((int)file.TargetLength);

        foreach (CatchZoneDeltaOperation op in file.Delta)
        {
            if (op.CopyOffset >= 0)
            {
                if (op.Length < 0 || op.CopyOffset > source.Length - op.Length)
                    throw new InvalidDataException(
                        $"Invalid delta copy range for {file.Scope}/{file.Path}: " +
                        $"source 0x{op.CopyOffset:X}, length {op.Length}.");

                output.Write(source, op.CopyOffset, op.Length);
                continue;
            }

            byte[] literal = Convert.FromBase64String(op.Data ?? string.Empty);
            if (op.Length != literal.Length)
                throw new InvalidDataException(
                    $"Invalid delta literal length for {file.Scope}/{file.Path}: " +
                    $"manifest says {op.Length}, data contains {literal.Length}.");

            output.Write(literal, 0, literal.Length);
        }

        byte[] result = output.ToArray();
        if (result.LongLength != file.TargetLength)
            throw new InvalidDataException(
                $"Catch-zone delta produced an unexpected length for {file.Scope}/{file.Path}. " +
                $"Expected {file.TargetLength}, got {result.LongLength}.");

        return result;
    }

    private static string ResolveSafePath(string root, string relative)
    {
        string fullRoot = Path.GetFullPath(root);
        string normalizedRelative = relative.Replace('/', Path.DirectorySeparatorChar);
        string fullPath = Path.GetFullPath(Path.Combine(fullRoot, normalizedRelative));

        string prefix = fullRoot.EndsWith(Path.DirectorySeparatorChar)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                $"Unsafe path in catch-zone patch manifest: {relative}");

        return fullPath;
    }

    private static string ComputeSha256(byte[] data) =>
        Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    private static string GetPatchManifestPath()
    {
        string game = GetGameFolderName();
        if (game.Length == 0)
            return string.Empty;

        return Path.Combine(AppContext.BaseDirectory, PatchRootName, game + ".json");
    }

    private static string GetGameFolderName()
    {
        if (Main.Config?.ORAS == true)
            return "ORAS";
        if (Main.Config?.XY == true)
            return "XY";
        return string.Empty;
    }
}

internal sealed class CatchZonePatchManifest
{
    public int Format { get; set; }
    public string Game { get; set; } = string.Empty;
    public List<CatchZonePatchFile> Files { get; set; } = [];
}

internal sealed class CatchZonePatchFile
{
    public string Scope { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Operation { get; set; } = "patch";
    public string PatchMode { get; set; } = "hunks";
    public long Length { get; set; } = -1;
    public long TargetLength { get; set; } = -1;
    public string SourceSha256 { get; set; } = string.Empty;
    public string TargetSha256 { get; set; } = string.Empty;
    public List<CatchZonePatchHunk> Hunks { get; set; } = [];
    public List<CatchZoneDeltaOperation> Delta { get; set; } = [];
}

internal sealed class CatchZonePatchHunk
{
    public int Offset { get; set; }
    public string Data { get; set; } = string.Empty;
}

internal sealed class CatchZoneDeltaOperation
{
    public int CopyOffset { get; set; } = -1;
    public int Length { get; set; }
    public string Data { get; set; } = string.Empty;
}
