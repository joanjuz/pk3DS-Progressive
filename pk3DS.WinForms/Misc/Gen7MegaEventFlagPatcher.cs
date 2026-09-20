using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace pk3DS.WinForms;

/// <summary>
/// Removes the USUM Mega Evolution story/event-flag gate.
///
/// Verified source patch:
/// Battle.cro + 0x9CE24
///   stock   01 00 00 0A
///   patched 00 F0 20 E3  (ARM NOP)
///
/// The same offset and stock instruction are documented for Ultra Sun and
/// Ultra Moon. The patch is intentionally restricted to USUM.
/// </summary>
internal static class Gen7MegaEventFlagPatcher
{
    internal const string ActionId =
        "mega-evolution.unlock-from-start";

    private const int PatchOffset = 0x0009CE24;

    private static readonly byte[] StockInstruction =
    [
        0x01, 0x00, 0x00, 0x0A,
    ];

    private static readonly byte[] PatchedInstruction =
    [
        0x00, 0xF0, 0x20, 0xE3,
    ];

    internal enum PatchState
    {
        MissingBattleCro,
        Stock,
        Applied,
        Unsupported,
    }

    internal static PatchState GetState()
    {
        if (!Main.Config.USUM)
            return PatchState.Unsupported;

        string path = FindBattleCro();

        if (string.IsNullOrWhiteSpace(path) ||
            !File.Exists(path))
        {
            return PatchState.MissingBattleCro;
        }

        try
        {
            byte[] data = File.ReadAllBytes(path);

            if (!IsExpectedCro(data))
                return PatchState.Unsupported;

            if (Match(
                    data,
                    PatchOffset,
                    PatchedInstruction))
            {
                return PatchState.Applied;
            }

            return Match(
                    data,
                    PatchOffset,
                    StockInstruction)
                ? PatchState.Stock
                : PatchState.Unsupported;
        }
        catch
        {
            return PatchState.Unsupported;
        }
    }

    /// <summary>
    /// Applies the patch.
    /// Returns 1 when bytes were changed, or 0 when already applied.
    /// Throws when the input is not the verified USUM Battle.cro state.
    /// </summary>
    internal static int Apply()
    {
        if (!Main.Config.USUM)
        {
            throw new NotSupportedException(
                "Mega Evolution from Start is currently validated only for Ultra Sun / Ultra Moon.");
        }

        string path = FindBattleCro();

        if (string.IsNullOrWhiteSpace(path) ||
            !File.Exists(path))
        {
            throw new FileNotFoundException(
                "Battle.cro was not found in the loaded USUM project.");
        }

        byte[] data = File.ReadAllBytes(path);

        if (!IsExpectedCro(data))
        {
            throw new InvalidDataException(
                "Battle.cro is not a supported USUM CRO file.");
        }

        if (Match(
                data,
                PatchOffset,
                PatchedInstruction))
        {
            return 0;
        }

        if (!Match(
                data,
                PatchOffset,
                StockInstruction))
        {
            string actual =
                Convert.ToHexString(
                    data.AsSpan(
                        PatchOffset,
                        StockInstruction.Length));

            throw new InvalidDataException(
                $"Battle.cro offset 0x{PatchOffset:X} does not contain the expected USUM instruction. " +
                $"Expected {Convert.ToHexString(StockInstruction)}, found {actual}. No bytes were changed.");
        }

        BackupOnce(
            path,
            ".bak_mega_event_flag");

        PatchedInstruction.CopyTo(
            data,
            PatchOffset);

        UpdateCroHashes(data);

        File.WriteAllBytes(
            path,
            data);

        return 1;
    }

    private static bool IsExpectedCro(byte[] data)
    {
        if (data is null ||
            data.Length < PatchOffset +
                StockInstruction.Length ||
            data.Length < 0x180)
        {
            return false;
        }

        return Encoding.ASCII.GetString(
                   data,
                   0x80,
                   4) == "CRO0";
    }

    /// <summary>
    /// Recomputes the four SHA-256 hashes stored at 0x00-0x7F.
    /// This matches the CRO layout used by pk3DS.Core.CTR.CRO and the
    /// verified USUM Battle.cro supplied with the reference project.
    /// </summary>
    private static void UpdateCroHashes(byte[] data)
    {
        int codeStart = ReadInt32(data, 0xB0);
        int codeSize = ReadInt32(data, 0xB4);
        int dataStart = ReadInt32(data, 0xB8);
        int dataSize = ReadInt32(data, 0xBC);
        int rodataStart = ReadInt32(data, 0xC0);

        ValidateRange(
            data,
            0x80,
            0x100,
            "CRO header");

        ValidateRange(
            data,
            codeStart,
            codeSize,
            "code segment");

        int rodataSize =
            dataStart - rodataStart;

        ValidateRange(
            data,
            rodataStart,
            rodataSize,
            "rodata segment");

        ValidateRange(
            data,
            dataStart,
            dataSize,
            "data segment");

        byte[][] hashes =
        [
            SHA256.HashData(
                data.AsSpan(
                    0x80,
                    0x100)),
            SHA256.HashData(
                data.AsSpan(
                    codeStart,
                    codeSize)),
            SHA256.HashData(
                data.AsSpan(
                    rodataStart,
                    rodataSize)),
            SHA256.HashData(
                data.AsSpan(
                    dataStart,
                    dataSize)),
        ];

        for (int i = 0; i < hashes.Length; i++)
        {
            hashes[i].CopyTo(
                data,
                i * 0x20);
        }
    }

    private static int ReadInt32(
        byte[] data,
        int offset)
    {
        if (offset < 0 ||
            offset + 4 > data.Length)
        {
            throw new InvalidDataException(
                "Battle.cro has an invalid CRO header.");
        }

        return BitConverter.ToInt32(
            data,
            offset);
    }

    private static void ValidateRange(
        byte[] data,
        int offset,
        int length,
        string name)
    {
        if (offset < 0 ||
            length < 0 ||
            offset > data.Length ||
            length > data.Length - offset)
        {
            throw new InvalidDataException(
                $"Battle.cro contains an invalid {name} range.");
        }
    }

    private static bool Match(
        byte[] data,
        int offset,
        byte[] expected)
    {
        if (offset < 0 ||
            offset + expected.Length > data.Length)
        {
            return false;
        }

        return data
            .AsSpan(
                offset,
                expected.Length)
            .SequenceEqual(expected);
    }

    private static string FindBattleCro()
    {
        var roots = new List<string>();

        if (!string.IsNullOrWhiteSpace(Main.RomFSPath))
            roots.Add(Main.RomFSPath);

        if (!string.IsNullOrWhiteSpace(Main.ExeFSPath))
            roots.Add(Main.ExeFSPath);

        roots.Add(
            Environment.CurrentDirectory);

        roots.Add(
            AppDomain.CurrentDomain.BaseDirectory);

        foreach (string root in roots
                     .Where(z =>
                         !string.IsNullOrWhiteSpace(z))
                     .Distinct(
                         StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (!Directory.Exists(root))
                    continue;

                string[] directCandidates =
                [
                    Path.Combine(
                        root,
                        "Battle.cro"),
                    Path.Combine(
                        root,
                        "dll",
                        "Battle.cro"),
                    Path.Combine(
                        root,
                        "ExtractedRomFS",
                        "Battle.cro"),
                    Path.Combine(
                        root,
                        "ExtractedRomFS",
                        "dll",
                        "Battle.cro"),
                ];

                string direct =
                    directCandidates.FirstOrDefault(
                        File.Exists);

                if (!string.IsNullOrWhiteSpace(direct))
                    return direct;

                string nested =
                    Directory
                        .EnumerateFiles(
                            root,
                            "Battle.cro",
                            SearchOption.AllDirectories)
                        .FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(nested))
                    return nested;
            }
            catch
            {
                // Try the next root.
            }
        }

        return string.Empty;
    }

    private static void BackupOnce(
        string path,
        string suffix)
    {
        string backup =
            path + suffix;

        if (!File.Exists(backup))
        {
            File.Copy(
                path,
                backup);
        }
    }
}
