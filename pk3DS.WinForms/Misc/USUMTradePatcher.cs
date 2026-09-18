using pk3DS.Core;
using System;
using System.IO;
using System.Linq;

namespace pk3DS.WinForms;

internal static class USUMTradePatcher
{
    // Ultra Sun/Ultra Moon field-trade selector patch.
    //
    // CallBoxFieldTrade eventually calls the selector-filter setter at 0x2DE7A4.
    // The vanilla setter enables the species filter even when the requested
    // species is 0, so "(None)" is NOT a wildcard by itself.
    //
    // Hook file offset 0x2843F4:
    //   BL 0x2DE7A4  ->  BL wrapper
    //
    // Wrapper file offset 0x4B9C00 (runtime 0x5B9C00):
    //   cmp   r1, #0
    //   bxeq  lr          ; request 0 = do not enable species filter
    //   b     0x2DE7A4    ; non-zero request = vanilla behavior
    //
    // The wrapper is intentionally placed before the existing USUM Move
    // Relearner payload area and does not overlap it.
    private const int HookOffset = 0x2843F4;
    private const int WrapperOffset = 0x4B9C00;

    private static readonly byte[] CleanHook =
    [
        0xEA, 0x68, 0xFD, 0xEB,
    ];

    private static readonly byte[] PatchedHook =
    [
        0x01, 0xD6, 0x08, 0xEB,
    ];

    private static readonly byte[] CleanWrapper = new byte[12];

    private static readonly byte[] PatchedWrapper =
    [
        0x00, 0x00, 0x51, 0xE3, // cmp r1, #0
        0x1E, 0xFF, 0x2F, 0x01, // bxeq lr
        0xE5, 0x92, 0xF4, 0xEA, // b 0x2DE7A4
    ];

    public static bool ApplyAcceptAnyPokemon(string exefsPath, GameConfig config)
    {
        if (!config.USUM)
            throw new InvalidOperationException("This trade selector patch is only supported for USUM.");

        string codePath = GetCodePath(exefsPath);
        byte[] data = File.ReadAllBytes(codePath);

        if (data.Length < WrapperOffset + PatchedWrapper.Length)
            throw new InvalidOperationException("code.bin is too small for the known USUM trade selector patch.");

        bool hookClean = Matches(data, HookOffset, CleanHook);
        bool hookPatched = Matches(data, HookOffset, PatchedHook);
        if (!hookClean && !hookPatched)
            throw new InvalidOperationException(
                $"Unexpected bytes at USUM trade hook 0x{HookOffset:X}. " +
                "The loaded code.bin does not match the tested USUM build.");

        bool wrapperClean = Matches(data, WrapperOffset, CleanWrapper);
        bool wrapperPatched = Matches(data, WrapperOffset, PatchedWrapper);
        if (!wrapperClean && !wrapperPatched)
            throw new InvalidOperationException(
                $"The USUM trade code-cave area at 0x{WrapperOffset:X} is already in use. " +
                "No changes were written.");

        if (hookPatched && wrapperPatched)
            return false;

        string backupPath = codePath + ".pk3ds-usum-trade.bak";
        if (!File.Exists(backupPath))
            File.Copy(codePath, backupPath);

        PatchedWrapper.CopyTo(data, WrapperOffset);
        PatchedHook.CopyTo(data, HookOffset);
        File.WriteAllBytes(codePath, data);
        return true;
    }

    private static bool Matches(byte[] data, int offset, byte[] expected)
    {
        if (offset < 0 || offset + expected.Length > data.Length)
            return false;
        return data.AsSpan(offset, expected.Length).SequenceEqual(expected);
    }

    private static string GetCodePath(string exefsPath)
    {
        if (string.IsNullOrWhiteSpace(exefsPath) || !Directory.Exists(exefsPath))
            throw new DirectoryNotFoundException("ExeFS folder is not loaded.");

        string[] candidates =
        [
            Path.Combine(exefsPath, ".code.bin"),
            Path.Combine(exefsPath, "code.bin"),
        ];

        foreach (string candidate in candidates)
            if (File.Exists(candidate))
                return candidate;

        string found = Directory.GetFiles(exefsPath, "*code*.bin", SearchOption.TopDirectoryOnly).FirstOrDefault();
        return found ?? throw new FileNotFoundException("Could not find code.bin or .code.bin in the loaded ExeFS folder.");
    }
}
