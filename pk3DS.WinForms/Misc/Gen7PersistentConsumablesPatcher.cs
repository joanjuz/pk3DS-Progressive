using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace pk3DS.WinForms;

internal static class Gen7PersistentConsumablesPatcher
{
    internal const string ActionId =
        "battle.persistent-consumables";

    // ------------------------------------------------------------
    // Gate #1: post-battle held-item reconciliation.
    //
    // Stock:
    //   ldrb r0, [r11]
    //   cmp  r0, #1
    //   bne  skip_item_reconciliation
    //   cmp  r4, #0
    //   mov  r5, #0
    //   bls  ...
    //
    // Research identifies battle type values as:
    //   0 = Wild
    //   1 = NPC
    //   2 = Battle Facility
    //   3 = PvP
    //
    // BNE -> BHI after cmp #1 means Wild and NPC both enter the
    // reconciliation loop, while BF/PvP still skip it.
    // ------------------------------------------------------------
    private static readonly byte[] BattleTypeGateSignature =
    [
        0x00, 0x00, 0xDB, 0xE5,
        0x01, 0x00, 0x50, 0xE3,
        0x00, 0x00, 0x00, 0x1A,
        0x00, 0x00, 0x54, 0xE3,
        0x00, 0x50, 0xA0, 0xE3,
        0x00, 0x00, 0x00, 0x9A,
    ];

    private static readonly byte[] BattleTypeGateMask =
    [
        0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF,
        0x00, 0x00, 0x00, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF,
        0x00, 0x00, 0x00, 0xFF,
    ];

    private const int BattleTypeConditionOffset = 11;
    private const byte BattleTypeStockCondition = 0x1A;   // BNE
    private const byte BattleTypePatchedCondition = 0x8A; // BHI

    // ------------------------------------------------------------
    // Gate #2: original held-item restoration.
    //
    // Stock:
    //   mov r0, r6
    //   bl  CoreParam::GetItem
    //   mov r6, r0
    //   mov r1, #17       ; PRM_ID_SPEND
    //   bl  ITEM_MANAGER::GetParam
    //   cmp r0, #0
    //   nop
    //   beq restore_original_item
    //
    // BEQ -> unconditional B makes consumable originals use the
    // same restoration path as non-consumables.
    // ------------------------------------------------------------
    private static readonly byte[] ItemRestoreSignature =
    [
        0x06, 0x00, 0xA0, 0xE1,
        0x00, 0x00, 0x00, 0xEB,
        0x00, 0x60, 0xA0, 0xE1,
        0x11, 0x10, 0xA0, 0xE3,
        0x00, 0x00, 0x00, 0xEB,
        0x00, 0x00, 0x50, 0xE3,
        0x00, 0xF0, 0x20, 0xE3,
        0x00, 0x00, 0x00, 0x0A,
    ];

    private static readonly byte[] ItemRestoreMask =
    [
        0xFF, 0xFF, 0xFF, 0xFF,
        0x00, 0x00, 0x00, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF,
        0x00, 0x00, 0x00, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF,
        0x00, 0x00, 0x00, 0xFF,
    ];

    private const int ItemRestoreConditionOffset = 31;
    private const byte ItemRestoreStockCondition = 0x0A;   // BEQ
    private const byte ItemRestorePatchedCondition = 0xEA; // B

    internal enum PatchState
    {
        MissingBattleCro,
        Stock,
        Partial,
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

            if (!TryGetGateState(
                    data,
                    BattleTypeGateSignature,
                    BattleTypeGateMask,
                    BattleTypeConditionOffset,
                    BattleTypeStockCondition,
                    BattleTypePatchedCondition,
                    out _,
                    out bool battleTypePatched))
            {
                return PatchState.Unsupported;
            }

            if (!TryGetGateState(
                    data,
                    ItemRestoreSignature,
                    ItemRestoreMask,
                    ItemRestoreConditionOffset,
                    ItemRestoreStockCondition,
                    ItemRestorePatchedCondition,
                    out _,
                    out bool itemRestorePatched))
            {
                return PatchState.Unsupported;
            }

            if (battleTypePatched &&
                itemRestorePatched)
            {
                return PatchState.Applied;
            }

            if (!battleTypePatched &&
                !itemRestorePatched)
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

    /// <summary>
    /// Returns the number of ARM instructions changed:
    /// 0 = already fully applied
    /// 1 = completing a partial install
    /// 2 = stock -> full install
    /// </summary>
    internal static int Apply()
    {
        if (!Main.Config.USUM)
        {
            throw new NotSupportedException(
                "Persistent Battle Consumables are currently validated only for Ultra Sun / Ultra Moon.");
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

        if (!TryGetGateState(
                data,
                BattleTypeGateSignature,
                BattleTypeGateMask,
                BattleTypeConditionOffset,
                BattleTypeStockCondition,
                BattleTypePatchedCondition,
                out int battleTypeOffset,
                out bool battleTypePatched))
        {
            throw new InvalidDataException(
                "The USUM battle-type reconciliation gate was not found exactly once. No bytes were changed.");
        }

        if (!TryGetGateState(
                data,
                ItemRestoreSignature,
                ItemRestoreMask,
                ItemRestoreConditionOffset,
                ItemRestoreStockCondition,
                ItemRestorePatchedCondition,
                out int itemRestoreOffset,
                out bool itemRestorePatched))
        {
            throw new InvalidDataException(
                "The USUM held-item restoration gate was not found exactly once. No bytes were changed.");
        }

        int changed = 0;

        if (!battleTypePatched)
            changed++;

        if (!itemRestorePatched)
            changed++;

        if (changed == 0)
            return 0;

        BackupOnce(
            path,
            ".bak_persistent_consumables");

        if (!battleTypePatched)
        {
            data[
                battleTypeOffset +
                BattleTypeConditionOffset] =
                BattleTypePatchedCondition;
        }

        if (!itemRestorePatched)
        {
            data[
                itemRestoreOffset +
                ItemRestoreConditionOffset] =
                ItemRestorePatchedCondition;
        }

        UpdateCroHashes(data);

        File.WriteAllBytes(
            path,
            data);

        return changed;
    }

    private static bool TryGetGateState(
        byte[] data,
        byte[] signature,
        byte[] mask,
        int conditionOffset,
        byte stockCondition,
        byte patchedCondition,
        out int offset,
        out bool patched)
    {
        int[] stock =
            FindMatches(
                data,
                signature,
                mask,
                conditionOffset,
                stockCondition);

        int[] modified =
            FindMatches(
                data,
                signature,
                mask,
                conditionOffset,
                patchedCondition);

        if (stock.Length == 1 &&
            modified.Length == 0)
        {
            offset = stock[0];
            patched = false;
            return true;
        }

        if (stock.Length == 0 &&
            modified.Length == 1)
        {
            offset = modified[0];
            patched = true;
            return true;
        }

        offset = -1;
        patched = false;
        return false;
    }

    private static int[] FindMatches(
        byte[] data,
        byte[] signature,
        byte[] mask,
        int conditionOffset,
        byte condition)
    {
        if (data is null ||
            signature is null ||
            mask is null ||
            signature.Length != mask.Length ||
            data.Length < signature.Length)
        {
            return [];
        }

        var result = new List<int>();

        for (int offset = 0;
             offset <= data.Length - signature.Length;
             offset += 4)
        {
            bool match = true;

            for (int i = 0;
                 i < signature.Length;
                 i++)
            {
                if (i == conditionOffset)
                {
                    if (data[offset + i] != condition)
                    {
                        match = false;
                        break;
                    }

                    continue;
                }

                byte m = mask[i];

                if (m == 0)
                    continue;

                if ((data[offset + i] & m) !=
                    (signature[i] & m))
                {
                    match = false;
                    break;
                }
            }

            if (match)
                result.Add(offset);
        }

        return [.. result];
    }

    private static bool IsExpectedCro(byte[] data)
    {
        return data is not null &&
               data.Length >= 0x180 &&
               Encoding.ASCII.GetString(
                   data,
                   0x80,
                   4) == "CRO0";
    }

    private static void UpdateCroHashes(
        byte[] data)
    {
        int codeStart =
            ReadInt32(data, 0xB0);

        int codeSize =
            ReadInt32(data, 0xB4);

        int dataStart =
            ReadInt32(data, 0xB8);

        int dataSize =
            ReadInt32(data, 0xBC);

        int rodataStart =
            ReadInt32(data, 0xC0);

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
            dataStart -
            rodataStart;

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

        for (int i = 0;
             i < hashes.Length;
             i++)
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
            offset + 4 >
            data.Length)
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
            length >
            data.Length - offset)
        {
            throw new InvalidDataException(
                $"Battle.cro contains an invalid {name} range.");
        }
    }

    private static string FindBattleCro()
    {
        var roots = new List<string>();

        if (!string.IsNullOrWhiteSpace(
                Main.RomFSPath))
        {
            roots.Add(
                Main.RomFSPath);
        }

        if (!string.IsNullOrWhiteSpace(
                Main.ExeFSPath))
        {
            roots.Add(
                Main.ExeFSPath);
        }

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

                if (!string.IsNullOrWhiteSpace(
                        direct))
                {
                    return direct;
                }

                string nested =
                    Directory
                        .EnumerateFiles(
                            root,
                            "Battle.cro",
                            SearchOption.AllDirectories)
                        .FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(
                        nested))
                {
                    return nested;
                }
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
            path +
            suffix;

        if (!File.Exists(backup))
        {
            File.Copy(
                path,
                backup);
        }
    }
}
