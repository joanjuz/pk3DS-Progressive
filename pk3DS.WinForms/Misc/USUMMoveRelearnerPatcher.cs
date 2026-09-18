using System;
using System.IO;
using System.Linq;

namespace pk3DS.WinForms;

/// <summary>
/// Applies the supplied CAFE.asm + Learn.asm behavior directly to a decompressed
/// USUM code.bin. The patch is intentionally limited to the known USUM offsets.
/// </summary>
internal static class USUMMoveRelearnerPatcher
{
    private const int CodeBaseAddress = 0x100000;

    private const int CafeHookAddress = 0x39A044;
    private const int CafePayloadAddress = 0x5B9FA0;
    private const int LearnInitHookAddress = 0x441654;
    private const int LearnCheckHookAddress = 0x4417D8;
    private const int LearnPayloadAddress = 0x5B9D04;

    // Original instructions replaced by the supplied ASM hooks.
    private static readonly byte[] CafeHookOriginal = [0xF0, 0x4F, 0x2D, 0xE9];
    private static readonly byte[] LearnInitHookOriginal = [0xF0, 0x43, 0x2D, 0xE9];
    private static readonly byte[] LearnCheckHookOriginal = [0x02, 0x00, 0x50, 0xE3];

    // b 0x5B9FA0
    private static readonly byte[] CafeHookPatched = [0xD5, 0x7F, 0x08, 0xEA];

    // b 0x5B9D04
    private static readonly byte[] LearnInitHookPatched = [0xAA, 0xE1, 0x05, 0xEA];

    // b 0x5B9D14
    private static readonly byte[] LearnCheckHookPatched = [0x4D, 0xE1, 0x05, 0xEA];

    // CAFE.asm detour @ 0x5B9FA0.
    // Café script IDs 0x14AA..0x14B7 are redirected to relearner script 0x157C.
    private static readonly byte[] CafePayload =
    [
        0x04, 0x00, 0x2D, 0xE9, 0x24, 0x20, 0x9F, 0xE5, 0x02, 0x00, 0x51, 0xE1,
        0x04, 0x00, 0x00, 0xBA, 0x1C, 0x20, 0x9F, 0xE5, 0x02, 0x00, 0x51, 0xE1,
        0x01, 0x00, 0x00, 0xCA, 0xFF, 0xFF, 0xFF, 0xEA, 0x10, 0x10, 0x9F, 0xE5,
        0x04, 0x00, 0xBD, 0xE8, 0xF0, 0x4F, 0x2D, 0xE9, 0x1D, 0x80, 0xF7, 0xEA,
        0xAA, 0x14, 0x00, 0x00, 0xB7, 0x14, 0x00, 0x00, 0x7C, 0x15, 0x00, 0x00,
    ];

    // Learn.asm code starts at 0x5B9D04. Address 0x5B9D00 is deliberately
    // left untouched because the ASM uses it as runtime storage (poke_ptr).
    private static readonly byte[] LearnPayload =
    [
        0xF0, 0x43, 0x2D, 0xE9, 0x38, 0x40, 0x9F, 0xE5, 0x00, 0x10, 0x84, 0xE5,
        0x50, 0x1E, 0xFA, 0xEA, 0x1F, 0x40, 0x2D, 0xE9, 0x00, 0x40, 0xA0, 0xE1,
        0x24, 0x00, 0x9F, 0xE5, 0x00, 0x00, 0x90, 0xE5, 0xE5, 0xA6, 0xF5, 0xEB,
        0x04, 0x00, 0x50, 0xE1, 0x00, 0x00, 0x00, 0xBA, 0x01, 0x00, 0x00, 0xEA,
        0x1F, 0x40, 0xBD, 0xE8, 0xE1, 0x1E, 0xFA, 0xEA, 0x1F, 0x40, 0xBD, 0xE8,
        0x02, 0x00, 0x50, 0xE3, 0xA4, 0x1E, 0xFA, 0xEA, 0x00, 0x9D, 0x5B, 0x00,
    ];

    public static string Apply(string exefsPath)
    {
        string codePath = FindCodeBinary(exefsPath);
        var info = new FileInfo(codePath);

        if ((info.Length & 0x1FF) != 0)
        {
            throw new InvalidDataException(
                "code.bin appears to be compressed. Decompress it before applying the USUM Move Relearner patch.");
        }

        byte[] code = File.ReadAllBytes(codePath);
        EnsureRange(code, CafePayloadAddress, CafePayload.Length);
        EnsureRange(code, LearnPayloadAddress, LearnPayload.Length);

        bool cafeHookDone = ValidateHook(
            code, CafeHookAddress, CafeHookOriginal, CafeHookPatched, "CAFE.asm hook");
        bool learnInitDone = ValidateHook(
            code, LearnInitHookAddress, LearnInitHookOriginal, LearnInitHookPatched, "Learn.asm init hook");
        bool learnCheckDone = ValidateHook(
            code, LearnCheckHookAddress, LearnCheckHookOriginal, LearnCheckHookPatched, "Learn.asm check hook");

        bool cafePayloadDone = Matches(code, ToOffset(CafePayloadAddress), CafePayload);
        bool learnPayloadDone = Matches(code, ToOffset(LearnPayloadAddress), LearnPayload);

        if (cafeHookDone && !cafePayloadDone)
            throw new InvalidDataException("The café hook is already modified, but its payload does not match this patch.");
        if ((learnInitDone || learnCheckDone) && !learnPayloadDone)
            throw new InvalidDataException("A Learn.asm hook is already modified, but its payload does not match this patch.");

        if (cafeHookDone && learnInitDone && learnCheckDone && cafePayloadDone && learnPayloadDone)
            return "The USUM Move Relearner patch is already applied. No files were changed.";

        string backupPath = codePath + ".pk3ds-usum-relearner.bak";
        if (!File.Exists(backupPath))
            File.Copy(codePath, backupPath);

        // Write detours first, then hooks.
        Write(code, ToOffset(CafePayloadAddress), CafePayload);
        Write(code, ToOffset(LearnPayloadAddress), LearnPayload);
        Write(code, ToOffset(CafeHookAddress), CafeHookPatched);
        Write(code, ToOffset(LearnInitHookAddress), LearnInitHookPatched);
        Write(code, ToOffset(LearnCheckHookAddress), LearnCheckHookPatched);

        File.WriteAllBytes(codePath, code);

        return
            "Pokémon Center cafés now open the Move Relearner." + Environment.NewLine +
            "Future level-up moves are filtered out; only moves available at the Pokémon's current level or earlier are offered." +
            Environment.NewLine + Environment.NewLine +
            "Backup: " + backupPath;
    }

    private static string FindCodeBinary(string exefsPath)
    {
        if (string.IsNullOrWhiteSpace(exefsPath) || !Directory.Exists(exefsPath))
            throw new DirectoryNotFoundException("The loaded ExeFS folder could not be found.");

        string dotCode = Path.Combine(exefsPath, ".code.bin");
        if (File.Exists(dotCode))
            return dotCode;

        string code = Path.Combine(exefsPath, "code.bin");
        if (File.Exists(code))
            return code;

        string fallback = Directory.GetFiles(exefsPath)
            .FirstOrDefault(z => Path.GetFileName(z).Contains("code", StringComparison.OrdinalIgnoreCase));

        return fallback ?? throw new FileNotFoundException(
            "Could not find code.bin in the loaded ExeFS folder.");
    }

    private static bool ValidateHook(
        byte[] code,
        int address,
        byte[] original,
        byte[] patched,
        string name)
    {
        int offset = ToOffset(address);
        EnsureRange(code, address, patched.Length);

        if (Matches(code, offset, patched))
            return true;
        if (Matches(code, offset, original))
            return false;

        throw new InvalidDataException(
            $"{name} at 0x{address:X8} does not match the expected clean USUM instruction. " +
            "The code.bin may be a different revision or another patch may already use this hook.");
    }

    private static int ToOffset(int address) => address - CodeBaseAddress;

    private static void EnsureRange(byte[] code, int address, int length)
    {
        int offset = ToOffset(address);
        if (offset < 0 || offset > code.Length - length)
        {
            throw new InvalidDataException(
                $"code.bin is too small for the required patch address 0x{address:X8}.");
        }
    }

    private static bool Matches(byte[] code, int offset, byte[] expected)
    {
        if (offset < 0 || offset > code.Length - expected.Length)
            return false;

        return code.AsSpan(offset, expected.Length).SequenceEqual(expected);
    }

    private static void Write(byte[] code, int offset, byte[] value) =>
        value.CopyTo(code, offset);
}
