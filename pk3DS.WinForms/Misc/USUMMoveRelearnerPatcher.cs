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
    internal const string ActionId = "usum.move-relearner";

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
    // Ultra Moon exact-delta integration.
    //
    // Reference pair:
    //   clean  SHA-256: 0aa12056d403a7d747d9c299e1dfbd3c0d5e4ce147675bc8300a8d61e999919b
    //   patched SHA-256: 2539ea304d7a428cbf8e9c0591900e07aa97449590b4c438ff701395a49ea41b
    //
    // These are the 143 contiguous byte-difference runs between the two
    // validated binaries: 516 changed bytes total. Only bytes that actually
    // differ are written; unchanged neighboring bytes are left untouched.
    // Offsets below are file offsets in decompressed code.bin.
    private static readonly (int Offset, byte[] Original, byte[] Patched)[] MoonExactPatches =
    [
        (0x00225AD4, Hex("000055E1"), Hex("CA4F0AEB")),
        (0x002843F4, Hex("EA68FD"), Hex("01D608")),
        (0x0029A044, Hex("F04F2DE9"), Hex("D57F08EA")),
        (0x002A72D4, Hex("08"), Hex("00")),
        (0x002A72D7, Hex("0A"), Hex("00")),
        (0x002D0058, Hex("01"), Hex("03")),
        (0x002D005B, Hex("E1"), Hex("E3")),
        (0x002D08FC, Hex("00"), Hex("03")),
        (0x002D08FE, Hex("D0E5"), Hex("A0E3")),
        (0x00341658, Hex("F0432DE9"), Hex("A9E105EA")),
        (0x003417DC, Hex("020050E3"), Hex("4CE105EA")),
        (0x003455F8, Hex("40"), Hex("00")),
        (0x003455FA, Hex("9DE5"), Hex("A0E3")),
        (0x003456D0, Hex("CE02"), Hex("EC01")),
        (0x003AC4B8, Hex("DC"), Hex("A0")),
        (0x003AC4C4, Hex("DC"), Hex("A0")),
        (0x003AC4DC, Hex("DC"), Hex("A0")),
        (0x004B99F8, Hex("00"), Hex("01")),
        (0x004B99FA, Hex("00000000000000"), Hex("80E27E402DE903")),
        (0x004B9A03, Hex("0000000000"), Hex("EA7E402DE9")),
        (0x004B9A0A, Hex("000000"), Hex("55E120")),
        (0x004B9A0F, Hex("0000"), Hex("0A05")),
        (0x004B9A12, Hex("000000"), Hex("A0E164")),
        (0x004B9A16, Hex("000000"), Hex("50E31D")),
        (0x004B9A1B, Hex("00000000000000000000000000"), Hex("8A7C409FE57C309FE57C508FE2")),
        (0x004B9A29, Hex("00000000"), Hex("10D5E5FF")),
        (0x004B9A2E, Hex("000000"), Hex("51E30F")),
        (0x004B9A33, Hex("00"), Hex("0A")),
        (0x004B9A36, Hex("000000"), Hex("51E308")),
        (0x004B9A3B, Hex("0000"), Hex("0A01")),
        (0x004B9A3E, Hex("000000"), Hex("51E313")),
        (0x004B9A43, Hex("000000000000000000000000000000000000"), Hex("1AB210D5E1811083E0B010D1E1B460D5E106")),
        (0x004B9A56, Hex("000000"), Hex("51E10B")),
        (0x004B9A5B, Hex("0000"), Hex("2A04")),
        (0x004B9A5F, Hex("0000000000000000000000000000"), Hex("EAB210D5E10110D4E70460D5E506")),
        (0x004B9A6E, Hex("000000"), Hex("11E105")),
        (0x004B9A73, Hex("000000000000"), Hex("1A0160D5E506")),
        (0x004B9A7A, Hex("000000"), Hex("50E104")),
        (0x004B9A7F, Hex("0000"), Hex("8A01")),
        (0x004B9A82, Hex("0000"), Hex("A0E3")),
        (0x004B9A86, Hex("0000000000000000000000000000"), Hex("50E37E80BDE8065085E2E4FFFFEA")),
        (0x004B9A96, Hex("0000"), Hex("A0E3")),
        (0x004B9A9A, Hex("0000000000000000000000000000"), Hex("50E37E80BDE8D4360133042F0133")),
        (0x004B9AA9, Hex("0000"), Hex("0E10")),
        (0x004B9AAC, Hex("00"), Hex("08")),
        (0x004B9AAF, Hex("0000"), Hex("1311")),
        (0x004B9AB2, Hex("00"), Hex("08")),
        (0x004B9AB5, Hex("0000"), Hex("1810")),
        (0x004B9AB8, Hex("00"), Hex("10")),
        (0x004B9ABB, Hex("0000"), Hex("1A10")),
        (0x004B9ABE, Hex("00"), Hex("40")),
        (0x004B9AC1, Hex("0000"), Hex("1D10")),
        (0x004B9AC4, Hex("00"), Hex("20")),
        (0x004B9AC7, Hex("0000"), Hex("2211")),
        (0x004B9ACA, Hex("00"), Hex("10")),
        (0x004B9ACD, Hex("0000"), Hex("2810")),
        (0x004B9AD0, Hex("00"), Hex("80")),
        (0x004B9AD3, Hex("0000"), Hex("2A11")),
        (0x004B9AD6, Hex("00"), Hex("01")),
        (0x004B9AD9, Hex("00000000"), Hex("33980180")),
        (0x004B9ADF, Hex("0000"), Hex("3511")),
        (0x004B9AE2, Hex("00"), Hex("20")),
        (0x004B9AE5, Hex("00000000"), Hex("388B0180")),
        (0x004B9AEB, Hex("0000"), Hex("3B11")),
        (0x004B9AEE, Hex("00"), Hex("02")),
        (0x004B9AF0, Hex("000000"), Hex("013C44")),
        (0x004B9AF4, Hex("0000"), Hex("0407")),
        (0x004B9AF7, Hex("0000"), Hex("4213")),
        (0x004B9AFA, Hex("00"), Hex("20")),
        (0x004B9AFD, Hex("0000"), Hex("4311")),
        (0x004B9B00, Hex("00"), Hex("40")),
        (0x004B9B02, Hex("000000"), Hex("014444")),
        (0x004B9B06, Hex("0000"), Hex("3A07")),
        (0x004B9B09, Hex("00000000"), Hex("488F0101")),
        (0x004B9B0F, Hex("00000000"), Hex("48B80120")),
        (0x004B9B15, Hex("00000000"), Hex("488E0120")),
        (0x004B9B1B, Hex("00000000"), Hex("488E0102")),
        (0x004B9B20, Hex("000000"), Hex("014B44")),
        (0x004B9B24, Hex("00000000"), Hex("D007FF64")),
        (0x004B9C02, Hex("00000000000000000000"), Hex("51E31EFF2F01E592F4EA")),
        (0x004B9D04, Hex("0000000000000000"), Hex("F0432DE938409FE5")),
        (0x004B9D0D, Hex("0000000000000000000000"), Hex("1084E5511EFAEA1F402DE9")),
        (0x004B9D19, Hex("00000000"), Hex("40A0E124")),
        (0x004B9D1E, Hex("0000"), Hex("9FE5")),
        (0x004B9D22, Hex("00000000000000"), Hex("90E5E5A6F5EB04")),
        (0x004B9D2A, Hex("0000"), Hex("50E1")),
        (0x004B9D2F, Hex("0000"), Hex("BA01")),
        (0x004B9D33, Hex("0000000000000000000000000000"), Hex("EA1F40BDE8E21EFAEA1F40BDE802")),
        (0x004B9D42, Hex("000000000000"), Hex("50E3A51EFAEA")),
        (0x004B9D49, Hex("0000"), Hex("9D5B")),
        (0x004B9FA0, Hex("00"), Hex("04")),
        (0x004B9FA2, Hex("00000000000000"), Hex("2DE924209FE502")),
        (0x004B9FAA, Hex("000000"), Hex("51E104")),
        (0x004B9FAF, Hex("000000000000"), Hex("BA1C209FE502")),
        (0x004B9FB6, Hex("000000"), Hex("51E101")),
        (0x004B9FBB, Hex("00000000000000000000"), Hex("CAFFFFFFEA10109FE504")),
        (0x004B9FC6, Hex("000000000000000000000000"), Hex("BDE8F04F2DE91D80F7EAAA14")),
        (0x004B9FD4, Hex("0000"), Hex("B714")),
        (0x004B9FD8, Hex("0000"), Hex("7C15")),
        (0x004BB98E, Hex("0E0251"), Hex("10009F")),
        (0x004BB992, Hex("D9015B012E005C"), Hex("350275003902F2")),
        (0x004BB99A, Hex("02015301DA"), Hex("84002B0036")),
        (0x004BB9A0, Hex("ED"), Hex("DE")),
        (0x004BB9A2, Hex("F1"), Hex("C1")),
        (0x004BB9A4, Hex("0D"), Hex("98")),
        (0x004BB9A6, Hex("3A003B003F"), Hex("2A021802D3")),
        (0x004BB9AC, Hex("71"), Hex("08")),
        (0x004BB9AE, Hex("B600F0"), Hex("CA02F8")),
        (0x004BB9B2, Hex("63"), Hex("A7")),
        (0x004BB9B4, Hex("DB00DA004C"), Hex("B4021F02F1")),
        (0x004BB9BA, Hex("DF"), Hex("77")),
        (0x004BB9BC, Hex("5500570059"), Hex("9C01B502D1")),
        (0x004BB9C2, Hex("D8008D005E"), Hex("C901090240")),
        (0x004BB9C8, Hex("F7001801680073"), Hex("63029C021A019C")),
        (0x004BB9D0, Hex("E2"), Hex("B7")),
        (0x004BB9D2, Hex("3500BC"), Hex("C10237")),
        (0x004BB9D6, Hex("C9007E"), Hex("690154")),
        (0x004BB9DA, Hex("3D"), Hex("A8")),
        (0x004BB9DC, Hex("4C0103"), Hex("690090")),
        (0x004BB9E0, Hex("07"), Hex("F9")),
        (0x004BB9E2, Hex("E8"), Hex("28")),
        (0x004BB9E4, Hex("9C"), Hex("53")),
        (0x004BB9E6, Hex("D5"), Hex("58")),
        (0x004BB9E8, Hex("A800EA01F001F1013B01D3009B"), Hex("4F021B022D0012026002B30254")),
        (0x004BB9F6, Hex("9C"), Hex("79")),
        (0x004BB9F8, Hex("CE00F7017601C3"), Hex("6702B600B00073")),
        (0x004BBA00, Hex("FB"), Hex("26")),
        (0x004BBA02, Hex("B502FF01050100"), Hex("4200B2026A000D")),
        (0x004BBA0A, Hex("75"), Hex("6B")),
        (0x004BBA0C, Hex("99"), Hex("EB")),
        (0x004BBA0E, Hex("A5"), Hex("E5")),
        (0x004BBA10, Hex("7301AC02A0"), Hex("AF005C01D5")),
        (0x004BBA18, Hex("B602BC0109"), Hex("C700E100CB")),
        (0x004BBA1E, Hex("560068"), Hex("2401A1")),
        (0x004BBA22, Hex("0E001300F4"), Hex("9E029301B9")),
        (0x004BBA28, Hex("0B020C029D"), Hex("AE01BD00E7")),
        (0x004BBA2E, Hex("94"), Hex("97")),
        (0x004BBA30, Hex("0D0263028E018A00BF01CF00D6007101A400AE01B10110"), Hex("35015D015802C001AE002402C101BF008801CC02A6025B")),
        (0x004BBA48, Hex("39"), Hex("41")),
        (0x004BBA4A, Hex("2B020B"), Hex("FB0129")),
        (0x004BBA4E, Hex("8F"), Hex("3C")),
        (0x004BBA50, Hex("7F"), Hex("92")),
        (0x004BBA52, Hex("5D024E02"), Hex("03010E01")),
    ];

    // Stable Ultra Moon instructions immediately before the shifted Learn hooks.
    // These bytes are not changed by the patch and distinguish Luna from Sol.
    private static readonly byte[] MoonLayoutMarker1 = Hex("F081BDE8");
    private static readonly byte[] MoonLayoutMarker2 = Hex("1BA701EB");


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

        if (IsUltraMoonLayout(code))
            return ApplyUltraMoonExact(codePath, code);
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

    private static bool IsUltraMoonLayout(byte[] code)
    {
        return
            Matches(code, 0x341654, MoonLayoutMarker1) &&
            Matches(code, 0x3417D8, MoonLayoutMarker2);
    }

    private static string ApplyUltraMoonExact(string codePath, byte[] code)
    {
        bool allPatched = true;

        // Validate the entire recipe before writing anything.
        foreach (var patch in MoonExactPatches)
            allPatched &= ValidateMoonExactPatch(code, patch);

        if (allPatched)
        {
            return
                "The verified Ultra Moon Move Relearner patch is already applied. " +
                "No files were changed.";
        }

        string backupPath =
            codePath +
            ".pk3ds-usum-relearner-ultra-moon-clean.bak";

        if (!File.Exists(backupPath))
            File.Copy(codePath, backupPath);

        byte[] updated = (byte[])code.Clone();

        foreach (var patch in MoonExactPatches)
            Write(updated, patch.Offset, patch.Patched);

        foreach (var patch in MoonExactPatches)
        {
            if (!Matches(updated, patch.Offset, patch.Patched))
            {
                throw new InvalidDataException(
                    $"Internal verification failed for Ultra Moon patch at file offset 0x{patch.Offset:X8}. " +
                    "The code.bin was not written.");
            }
        }

        File.WriteAllBytes(codePath, updated);

        return
            "Ultra Moon Pokémon Center cafés now open the Move Relearner." +
            Environment.NewLine +
            "Applied the exact validated clean-to-working Ultra Moon delta " +
            "(143 changed runs / 516 bytes)." +
            Environment.NewLine +
            Environment.NewLine +
            "Backup: " +
            backupPath;
    }

    private static bool ValidateMoonExactPatch(
        byte[] code,
        (int Offset, byte[] Original, byte[] Patched) patch)
    {
        if (Matches(code, patch.Offset, patch.Patched))
            return true;

        if (Matches(code, patch.Offset, patch.Original))
            return false;

        int length = Math.Min(patch.Patched.Length, 16);

        string actual =
            patch.Offset >= 0 &&
            patch.Offset <= code.Length - patch.Patched.Length
                ? string.Join(
                    " ",
                    code
                        .Skip(patch.Offset)
                        .Take(length)
                        .Select(z => z.ToString("X2")))
                : "out-of-range";

        throw new InvalidDataException(
            $"Ultra Moon code.bin does not match the validated clean/already-patched bytes at file offset 0x{patch.Offset:X8}. " +
            $"Actual: {actual}. Nothing was changed.");
    }

    private static byte[] Hex(string value) =>
        Convert.FromHexString(value);

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
