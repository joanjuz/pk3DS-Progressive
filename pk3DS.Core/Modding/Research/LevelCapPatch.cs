using System;
using System.Collections.Generic;
using System.Linq;

namespace pk3DS.Core.Modding.Research;

public enum LevelCapHookState
{
    Stock,
    Applied,
    Unsupported,
}

public sealed record LevelCapSite(
    string Binary,
    bool Success,
    bool Changed,
    uint BlockOffset,
    uint HookOffset,
    string Detail)
{
    public override string ToString()
    {
        if (!Success)
            return $"{Binary}: not applied - {Detail}";

        string verb = Changed ? "installed" : "already installed";
        return $"{Binary}: {verb}; function at 0x{BlockOffset:X6}, hook at 0x{HookOffset:X6} - {Detail}";
    }
}

/// <summary>
/// Atomic in-memory result for the managed Player Level Caps installer.
/// The returned arrays are detached working copies; callers only persist them after every site succeeds.
/// </summary>
public sealed record LevelCapInstallResult(
    byte[] BattleCro,
    byte[] CodeBin,
    IReadOnlyList<LevelCapSite> Sites)
{
    public bool Success =>
        Sites.Count == 2 &&
        Sites.All(z => z.Success);

    public int ChangedCount =>
        Sites.Count(z => z.Changed);
}

/// <summary>
/// Installs the researched USUM player level-cap routine and hooks EXP gain plus Rare Candy.
/// File offsets are used because each hook and injected block keep the same relative displacement
/// in the mapped image. The managed path allocates relocation-safe Battle.cro space and can grow
/// segment 0 automatically when the existing executable padding is insufficient.
/// </summary>
public static class LevelCapPatch
{
    public const uint SaveFlagBase = 0x330138D0;
    public const int PrologueSize = 0x6C;
    public const uint BattleHook = 0x015AD4;
    public const uint CandyHook = 0x225ACC;

    private const int EntryBattle = 0x00;
    private const int EntryCandy = 0x0C;

    private static readonly uint[] Routine =
    [
        0xE2800001, // entry_battle: add  r0, r0, #1
        0xE92D407E, //               push {r1-r6, lr}
        0xEA000003, //               b    body
        0xE92D407E, // entry_candy:  push {r1-r6, lr}
        0xE1550000, //               cmp  r5, r0
        0x0A000010, //               beq  deny
        0xE1A00005, //               mov  r0, r5
        0xE3500064, // body:         cmp  r0, #100
        0x8A00000D, //               bhi  deny
        0xE59F403C, //               ldr  r4, [pc, #0x3C]
        0xE28F503C, //               add  r5, pc, #0x3C
        0xE5D51000, // loop:         ldrb r1, [r5, #0]
        0xE7D41001, //               ldrb r1, [r4, r1]
        0xE5D56001, //               ldrb r6, [r5, #1]
        0xE0111006, //               ands r1, r1, r6
        0x12855003, //               addne r5, r5, #3
        0x1AFFFFF9, //               bne  loop
        0xE5D56002, //               ldrb r6, [r5, #2]
        0xE1500006, //               cmp  r0, r6
        0x8A000002, //               bhi  deny
        0xE3A00001, //               mov  r0, #1
        0xE3500000, //               cmp  r0, #0
        0xE8BD807E, //               pop  {r1-r6, pc}
        0xE3A00000, // deny:         mov  r0, #0
        0xE3500000, //               cmp  r0, #0
        0xE8BD807E, //               pop  {r1-r6, pc}
        SaveFlagBase,
    ];

    private static readonly uint[] BattleOriginal =
    [
        0xE3500064,
        0xE320F000,
        0xAA000004,
    ];

    private static readonly uint[] CandyOriginal =
    [
        0xE3550064,
        0x83A05064,
        0xE1550000,
        0x0A00001E,
    ];

    public static byte[] BuildBlock(LevelCapTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var tableBytes = table.ToBytes();
        var block = new byte[PrologueSize + tableBytes.Length];

        for (int i = 0; i < Routine.Length; i++)
            BitConverter.GetBytes(Routine[i]).CopyTo(block, i * 4);

        tableBytes.CopyTo(block, PrologueSize);
        return block;
    }

    public static LevelCapHookState GetBattleState(byte[] cro, out uint blockOffset)
    {
        blockOffset = 0;

        if (WordsMatch(cro, BattleHook, BattleOriginal, out _))
            return LevelCapHookState.Stock;

        if (!TryReadWord(cro, BattleHook, out uint first) ||
            !TryReadWord(cro, BattleHook + 4, out uint second) ||
            !TryReadWord(cro, BattleHook + 8, out uint third))
        {
            return LevelCapHookState.Unsupported;
        }

        if (!IsBranchLink(first) || second != 0xE3500001 || third != 0x1A000004)
            return LevelCapHookState.Unsupported;

        if (!TryDecodeBranchTarget(BattleHook, first, out uint target) ||
            !RoutineMatches(cro, target))
        {
            return LevelCapHookState.Unsupported;
        }

        blockOffset = target;
        return LevelCapHookState.Applied;
    }

    public static bool TryReadInstalledBattleTable(byte[] cro, out LevelCapTable table)
    {
        table = null;
        if (GetBattleState(cro, out uint blockOffset) != LevelCapHookState.Applied)
            return false;

        long at = blockOffset + PrologueSize;
        var entries = new List<LevelCapEntry>();
        var defaults = LevelCapTable.Default().Entries
            .GroupBy(z => (z.FlagOffset, z.FlagBit))
            .ToDictionary(z => z.Key, z => z.First().Label);
        var known = LevelCapTable.KnownFlags
            .GroupBy(z => (z.Offset, z.Bit))
            .ToDictionary(z => z.Key, z => z.First().Label);

        for (int i = 0; i <= LevelCapTable.MaxEntries; i++, at += LevelCapTable.EntrySize)
        {
            if (at < 0 || at + LevelCapTable.EntrySize > cro.Length)
                return false;

            int pos = (int)at;
            byte offset = cro[pos];
            byte bit = cro[pos + 1];
            byte cap = cro[pos + 2];

            if (offset == 0 && bit == 0 && cap == LevelCapTable.HardCeiling)
            {
                if (entries.Count == 0)
                    return false;

                table = new LevelCapTable { Entries = entries };
                return true;
            }

            if (cap is 0 or > LevelCapTable.HardCeiling || bit == 0 || (bit & (bit - 1)) != 0)
                return false;

            string label = defaults.TryGetValue((offset, bit), out string stockLabel)
                ? stockLabel
                : known.TryGetValue((offset, bit), out string knownLabel)
                    ? knownLabel
                    : $"Flag 0x{offset:X2}/0x{bit:X2}";

            entries.Add(new LevelCapEntry(label, offset, bit, cap));
        }

        return false;
    }

    public static LevelCapHookState GetCandyState(byte[] code, out uint blockOffset)
    {
        blockOffset = 0;

        if (WordsMatch(code, CandyHook, CandyOriginal, out _))
            return LevelCapHookState.Stock;

        if (!TryReadWord(code, CandyHook, out uint first) ||
            !TryReadWord(code, CandyHook + 4, out uint second) ||
            !TryReadWord(code, CandyHook + 8, out uint third) ||
            !TryReadWord(code, CandyHook + 12, out uint fourth))
        {
            return LevelCapHookState.Unsupported;
        }

        if (first != CandyOriginal[0] || second != CandyOriginal[1] ||
            !IsBranchLink(third) || fourth != CandyOriginal[3])
        {
            return LevelCapHookState.Unsupported;
        }

        if (!TryDecodeBranchTarget(CandyHook + 8, third, out uint entry) ||
            entry < EntryCandy)
        {
            return LevelCapHookState.Unsupported;
        }

        uint start = entry - EntryCandy;
        if (!RoutineMatches(code, start))
            return LevelCapHookState.Unsupported;

        blockOffset = start;
        return LevelCapHookState.Applied;
    }

    /// <summary>
    /// Applies both halves to detached in-memory copies. Battle.cro uses
    /// <see cref="CroCodeSpaceManager"/> for new placement and can therefore grow .text when needed.
    /// No caller-owned array is mutated.
    /// </summary>
    public static LevelCapInstallResult InstallManaged(
        byte[] battleCro,
        byte[] codeBin,
        LevelCapTable table)
    {
        ArgumentNullException.ThrowIfNull(battleCro);
        ArgumentNullException.ThrowIfNull(codeBin);
        ArgumentNullException.ThrowIfNull(table);

        byte[] battle = (byte[])battleCro.Clone();
        byte[] code = (byte[])codeBin.Clone();

        var problems = table.Validate();
        if (problems.Count != 0)
        {
            return new LevelCapInstallResult(
                battle,
                code,
                [
                    new LevelCapSite(
                        "(table)",
                        false,
                        false,
                        0,
                        0,
                        "table is not valid: " + problems[0]),
                ]);
        }

        byte[] block = BuildBlock(table);

        LevelCapSite battleSite =
            InstallBattleManaged(
                ref battle,
                block);

        LevelCapSite candySite =
            InstallCandy(
                code,
                block);

        if (battleSite.Success &&
            battleSite.Changed &&
            !CroSegmentExpander.TryUpdateHashes(
                battle,
                out string hashError))
        {
            battleSite = new LevelCapSite(
                "Battle.cro",
                false,
                false,
                battleSite.BlockOffset,
                BattleHook,
                "the CRO was modified in memory, but its integrity hashes could not be finalized: " +
                hashError);
        }

        return new LevelCapInstallResult(
            battle,
            code,
            [battleSite, candySite]);
    }

    private static LevelCapSite InstallBattleManaged(
        ref byte[] cro,
        byte[] block)
    {
        LevelCapHookState state =
            GetBattleState(
                cro,
                out uint existing);

        // New installations always use the managed allocator. This is the production caller that
        // exercises allocation -> optional expansion -> write -> final CRO image.
        if (state == LevelCapHookState.Stock)
        {
            return InstallBattleViaSpaceManager(
                ref cro,
                block,
                existing: 0,
                oldLength: 0,
                fallbackReason: string.Empty);
        }

        // Unsupported / malformed installs retain the existing diagnostics.
        if (state != LevelCapHookState.Applied)
            return InstallBattle(cro, block);

        if (!TryGetInstalledBlockLength(
                cro,
                existing,
                out int oldLength))
        {
            return InstallBattle(cro, block);
        }

        // Preserve the proven in-place / legacy-migration behavior when it succeeds.
        // Work on a clone so a failed attempt can never contaminate the managed fallback.
        byte[] direct = (byte[])cro.Clone();
        LevelCapSite directSite =
            InstallBattle(
                direct,
                block);

        if (directSite.Success)
        {
            cro = direct;
            return directSite;
        }

        return InstallBattleViaSpaceManager(
            ref cro,
            block,
            existing,
            oldLength,
            directSite.Detail);
    }

    private static LevelCapSite InstallBattleViaSpaceManager(
        ref byte[] cro,
        byte[] block,
        uint existing,
        int oldLength,
        string fallbackReason)
    {
        int originalLength = cro.Length;

        if (!CroCodeSpaceManager.TryCreate(
                cro,
                out var manager,
                out string managerError))
        {
            return new LevelCapSite(
                "Battle.cro",
                false,
                false,
                existing,
                BattleHook,
                "managed CRO space could not be initialized: " + managerError);
        }

        if (!manager.TryAllocate(
                block.Length,
                "Player Level Caps",
                out CroCodeGrant grant,
                out string allocationError))
        {
            return new LevelCapSite(
                "Battle.cro",
                false,
                false,
                existing,
                BattleHook,
                "managed CRO space could not reserve the level-cap routine: " +
                allocationError);
        }

        if (!manager.TryWrite(
                grant,
                block,
                out string writeError))
        {
            return new LevelCapSite(
                "Battle.cro",
                false,
                false,
                existing,
                BattleHook,
                "managed CRO space reserved a block but could not write it: " +
                writeError);
        }

        if (!manager.TryBuildImage(
                out byte[] managed,
                out string buildError))
        {
            return new LevelCapSite(
                "Battle.cro",
                false,
                false,
                existing,
                BattleHook,
                "managed CRO image could not be finalized: " + buildError);
        }

        if (oldLength > 0)
        {
            if ((ulong)existing + (uint)oldLength > (ulong)managed.Length)
            {
                return new LevelCapSite(
                    "Battle.cro",
                    false,
                    false,
                    existing,
                    BattleHook,
                    "the existing level-cap block lies outside the managed CRO image");
            }

            Array.Clear(
                managed,
                checked((int)existing),
                oldLength);
        }

        WriteWord(
            managed,
            BattleHook,
            BranchLink(
                BattleHook,
                grant.Offset + EntryBattle));

        WriteWord(
            managed,
            BattleHook + 4,
            0xE3500001);

        WriteWord(
            managed,
            BattleHook + 8,
            0x1A000004);

        int added = managed.Length - originalLength;

        string detail;
        if (oldLength > 0)
        {
            detail =
                "relocated Player Level Caps through managed CRO space";

            if (!string.IsNullOrWhiteSpace(fallbackReason))
                detail += " after the direct update was rejected: " + fallbackReason;
        }
        else
        {
            detail =
                $"{block.Length} bytes; EXP gain now asks the player level cap through managed CRO space";
        }

        if (added > 0)
        {
            detail +=
                $"; Battle.cro auto-expanded by 0x{added:X} bytes";
        }

        cro = managed;

        return new LevelCapSite(
            "Battle.cro",
            true,
            true,
            grant.Offset,
            BattleHook,
            detail);
    }

    /// <summary>
    /// Legacy in-place installer retained for compatibility and focused regression tests.
    /// It never replaces the caller's Battle.cro array with a larger image.
    /// </summary>
    public static List<LevelCapSite> Install(byte[] battleCro, byte[] codeBin, LevelCapTable table)
    {
        ArgumentNullException.ThrowIfNull(battleCro);
        ArgumentNullException.ThrowIfNull(codeBin);
        ArgumentNullException.ThrowIfNull(table);

        var problems = table.Validate();
        if (problems.Count != 0)
        {
            return
            [
                new LevelCapSite("(table)", false, false, 0, 0, "table is not valid: " + problems[0]),
            ];
        }

        byte[] block = BuildBlock(table);
        return
        [
            InstallBattle(battleCro, block),
            InstallCandy(codeBin, block),
        ];
    }

    private static LevelCapSite InstallBattle(byte[] cro, byte[] block)
    {
        LevelCapHookState state = GetBattleState(cro, out uint existing);
        if (state == LevelCapHookState.Applied)
        {
            uint installedTextEnd = CroTextEnd(cro);
            uint installedExecutableEnd = CroExecutablePaddingEnd(cro, installedTextEnd);
            if (!TryGetInstalledBlockLength(cro, existing, out int oldLength))
            {
                return new LevelCapSite(
                    "Battle.cro", false, false, existing, BattleHook,
                    "the existing level-cap routine was found, but its table terminator is invalid");
            }

            // Older builds searched zero runs inside .text. In USUM the tail of .text
            // contains relocation placeholders, so those zero bytes are not a safe code cave.
            // A block already in executable page padding must also be relocation-free: a CRO
            // relocation target means the region belongs to the original module even when the
            // loader does not overwrite the bytes themselves.
            bool inExecutablePadding =
                existing >= installedTextEnd &&
                (long)existing + oldLength <= installedExecutableEnd;

            if (!CroRelocationMap.TryCreate(cro, out var relocationMap, out string relocationError))
            {
                return new LevelCapSite(
                    "Battle.cro", false, false, existing, BattleHook,
                    "the CRO relocation table could not be validated: " + relocationError);
            }

            int relocationReferences = relocationMap.CountReferencesInRange(existing, oldLength);
            bool relocationSafe = relocationReferences == 0;

            if (!inExecutablePadding || !relocationSafe)
            {
                uint? migratedSpot = FindCroExecutablePadding(cro, block.Length, installedTextEnd);
                if (migratedSpot is not { } migratedAt)
                {
                    string unsafeReason = !inExecutablePadding
                        ? "is inside .text"
                        : $"overlaps {relocationReferences} CRO relocation reference(s)";

                    return new LevelCapSite(
                        "Battle.cro", false, false, existing, BattleHook,
                        $"the installed block {unsafeReason} and no safe executable page padding is available");
                }

                // Clearing the old injected block restores the bytes that were blank before
                // Player Level Caps. Existing CRO relocation metadata is intentionally untouched.
                Array.Clear(cro, (int)existing, oldLength);
                block.CopyTo(cro, (int)migratedAt);
                WriteWord(cro, BattleHook, BranchLink(BattleHook, migratedAt + EntryBattle));
                WriteWord(cro, BattleHook + 4, 0xE3500001);
                WriteWord(cro, BattleHook + 8, 0x1A000004);

                string migrationReason = !inExecutablePadding
                    ? "migrated Player Level Caps out of .text"
                    : $"migrated Player Level Caps away from {relocationReferences} CRO relocation reference(s)";

                return new LevelCapSite(
                    "Battle.cro", true, true, migratedAt, BattleHook,
                    $"{migrationReason}; {block.Length} bytes in safe executable page padding");
            }

            if (BlockEquals(cro, existing, block))
            {
                return new LevelCapSite(
                    "Battle.cro", true, false, existing, BattleHook,
                    "EXP gain already uses this Player Level Caps table");
            }

            if (!CanReplaceInstalledBlock(cro, existing, oldLength, block.Length, installedExecutableEnd, out string why))
            {
                return new LevelCapSite(
                    "Battle.cro", false, false, existing, BattleHook,
                    "the installed table cannot be resized safely: " + why);
            }

            ReplaceInstalledBlock(cro, existing, oldLength, block);
            return new LevelCapSite(
                "Battle.cro", true, true, existing, BattleHook,
                $"existing Player Level Caps table updated to {block.Length} bytes");
        }

        if (state != LevelCapHookState.Stock)
        {
            WordsMatch(cro, BattleHook, BattleOriginal, out string why);
            return new LevelCapSite(
                "Battle.cro", false, false, 0, BattleHook,
                (string.IsNullOrWhiteSpace(why) ? "hook is neither stock nor this level-cap patch" : why) +
                " - this is not a supported USUM Battle.cro state");
        }

        uint textEnd = CroTextEnd(cro);
        uint? spot = FindCroExecutablePadding(cro, block.Length, textEnd);
        if (spot is not { } at)
        {
            return new LevelCapSite(
                "Battle.cro", false, false, 0, BattleHook,
                $"no run of {block.Length} free bytes exists in the executable page padding after .text");
        }

        block.CopyTo(cro, (int)at);
        WriteWord(cro, BattleHook, BranchLink(BattleHook, at + EntryBattle));
        WriteWord(cro, BattleHook + 4, 0xE3500001);
        WriteWord(cro, BattleHook + 8, 0x1A000004);

        return new LevelCapSite(
            "Battle.cro", true, true, at, BattleHook,
            $"{block.Length} bytes; EXP gain now asks the player level cap");
    }

    private static LevelCapSite InstallCandy(byte[] code, byte[] block)
    {
        LevelCapHookState state = GetCandyState(code, out uint existing);
        if (state == LevelCapHookState.Applied)
        {
            uint installedTextEnd = CodeTextEnd(code);
            if (!TryGetInstalledBlockLength(code, existing, out int oldLength))
            {
                return new LevelCapSite(
                    "code.bin", false, false, existing, CandyHook + 8,
                    "the existing level-cap routine was found, but its table terminator is invalid");
            }

            if (BlockEquals(code, existing, block))
            {
                return new LevelCapSite(
                    "code.bin", true, false, existing, CandyHook + 8,
                    "Rare Candy already uses this Player Level Caps table");
            }

            if (!CanReplaceInstalledBlock(code, existing, oldLength, block.Length, installedTextEnd, out string why))
            {
                return new LevelCapSite(
                    "code.bin", false, false, existing, CandyHook + 8,
                    "the installed table cannot be resized safely: " + why);
            }

            ReplaceInstalledBlock(code, existing, oldLength, block);
            return new LevelCapSite(
                "code.bin", true, true, existing, CandyHook + 8,
                $"existing Player Level Caps table updated to {block.Length} bytes");
        }

        if (state != LevelCapHookState.Stock)
        {
            WordsMatch(code, CandyHook, CandyOriginal, out string why);
            return new LevelCapSite(
                "code.bin", false, false, 0, CandyHook,
                (string.IsNullOrWhiteSpace(why) ? "hook is neither stock nor this level-cap patch" : why) +
                " - this is not a supported USUM code.bin state");
        }

        uint textEnd = CodeTextEnd(code);
        uint? spot = FindFreeSpace(code, block.Length, textEnd, CandyHook, 0);
        if (spot is not { } at)
        {
            return new LevelCapSite(
                "code.bin", false, false, 0, CandyHook,
                $"no run of {block.Length} free bytes inside executable .text");
        }

        block.CopyTo(code, (int)at);
        WriteWord(code, CandyHook + 8, BranchLink(CandyHook + 8, at + EntryCandy));

        return new LevelCapSite(
            "code.bin", true, true, at, CandyHook + 8,
            $"{block.Length} bytes; Rare Candy now asks the player level cap");
    }

    private static uint CroTextEnd(byte[] cro)
    {
        if (cro.Length < 0xD0 || BitConverter.ToUInt32(cro, 0x80) != 0x304F5243)
            return 0;

        uint segTable = BitConverter.ToUInt32(cro, 0xC8);
        uint segCount = BitConverter.ToUInt32(cro, 0xCC);
        for (uint i = 0; i < segCount; i++)
        {
            long e = segTable + (i * 12);
            if (e < 0 || e + 12 > cro.Length)
                break;

            if (BitConverter.ToUInt32(cro, (int)e + 8) != 0)
                continue;

            uint off = BitConverter.ToUInt32(cro, (int)e);
            uint size = BitConverter.ToUInt32(cro, (int)e + 4);
            if (off != 0 && size != 0 && off + size <= cro.Length)
                return off + size;
        }

        return 0;
    }

    private static uint CroExecutablePaddingEnd(byte[] cro, uint textEnd)
    {
        if (textEnd == 0 || textEnd >= (uint)cro.Length)
            return textEnd;

        uint pageEnd = (textEnd + 0xFFFu) & ~0xFFFu;
        if (pageEnd < textEnd || pageEnd > (uint)cro.Length)
            pageEnd = (uint)cro.Length;

        // Do not cross into another file-backed segment if a non-standard CRO
        // starts one before the end of the current executable page.
        if (cro.Length >= 0xD0 && BitConverter.ToUInt32(cro, 0x80) == 0x304F5243)
        {
            uint segTable = BitConverter.ToUInt32(cro, 0xC8);
            uint segCount = BitConverter.ToUInt32(cro, 0xCC);
            for (uint i = 0; i < segCount; i++)
            {
                long e = segTable + (i * 12L);
                if (e < 0 || e + 12 > cro.Length)
                    break;

                uint off = BitConverter.ToUInt32(cro, (int)e);
                uint size = BitConverter.ToUInt32(cro, (int)e + 4);
                uint type = BitConverter.ToUInt32(cro, (int)e + 8);
                if (type == 3 || size == 0 || off <= textEnd)
                    continue;

                if (off < pageEnd)
                    pageEnd = off;
            }
        }

        return pageEnd;
    }

    private static uint? FindCroExecutablePadding(byte[] cro, int need, uint textEnd)
    {
        if (!CroCodeAllocator.TryCreate(cro, out var allocator, out _))
            return null;

        // Fail closed if the legacy helper and the allocator disagree about where .text ends.
        // Once all CRO callers use CroCodeAllocator directly, this compatibility check can go.
        if (allocator.CodeEnd != textEnd)
            return null;

        CroCodeGrant grant = allocator.Allocate(need, "Player Level Caps");
        return grant.Success ? grant.Offset : null;
    }

    private static uint CodeTextEnd(byte[] code)
    {
        const long MinPadding = 0x100;

        for (long page = 0x1000; page < code.Length; page += 0x1000)
        {
            if (code[page] == 0)
                continue;

            long i = page - 1;
            while (i >= 0 && code[i] == 0)
                i--;

            if (page - (i + 1) >= MinPadding)
                return (uint)page;
        }

        return (uint)code.Length;
    }

    private static uint? FindFreeSpace(byte[] bin, int need, uint limit, uint after, uint preferredEnd)
    {
        if (limit == 0 || limit > bin.Length)
            return null;

        int want = (need + 3) & ~3;
        var runs = new List<(uint Start, uint End)>();
        long runStart = -1;

        for (long i = 0; i <= limit; i++)
        {
            if (i < limit && bin[i] == 0)
            {
                if (runStart < 0)
                    runStart = i;
                continue;
            }

            if (runStart >= 0)
            {
                long start = (runStart + 3) & ~3L;
                if (start > after && i - start >= want)
                    runs.Add(((uint)start, (uint)i));
            }

            runStart = -1;
        }

        if (runs.Count == 0)
            return null;

        foreach (var r in runs)
        {
            if (EndsFunction(bin, r.Start))
                return r.Start;
        }

        foreach (var r in runs)
        {
            if (r.End == preferredEnd)
                return r.Start;
        }

        foreach (var r in runs)
        {
            if ((r.End & 0xFFF) == 0)
                return r.Start;
        }

        return runs.OrderByDescending(r => r.End - r.Start).First().Start;
    }

    private static bool TryGetInstalledBlockLength(byte[] bin, uint at, out int length)
    {
        length = 0;
        long tableStart = (long)at + PrologueSize;
        if (tableStart < 0 || tableStart + LevelCapTable.EntrySize > bin.Length)
            return false;

        for (int i = 0; i <= LevelCapTable.MaxEntries; i++)
        {
            long pos = tableStart + (i * LevelCapTable.EntrySize);
            if (pos + LevelCapTable.EntrySize > bin.Length)
                return false;

            int p = (int)pos;
            byte offset = bin[p];
            byte bit = bin[p + 1];
            byte cap = bin[p + 2];

            if (offset == 0 && bit == 0 && cap == LevelCapTable.HardCeiling)
            {
                length = PrologueSize + ((i + 1) * LevelCapTable.EntrySize);
                return i != 0;
            }

            if (cap is 0 or > LevelCapTable.HardCeiling ||
                bit == 0 || (bit & (bit - 1)) != 0)
            {
                return false;
            }
        }

        return false;
    }

    private static bool BlockEquals(byte[] bin, uint at, byte[] block)
    {
        if ((long)at + block.Length > bin.Length)
            return false;

        return bin.AsSpan((int)at, block.Length).SequenceEqual(block);
    }

    private static bool CanReplaceInstalledBlock(
        byte[] bin,
        uint at,
        int oldLength,
        int newLength,
        uint limit,
        out string why)
    {
        why = string.Empty;

        long newEnd = (long)at + newLength;
        if (newEnd > bin.Length || newEnd > limit)
        {
            why = "the new block would extend past executable .text";
            return false;
        }

        if (newLength <= oldLength)
            return true;

        long extensionStart = (long)at + oldLength;
        for (long i = extensionStart; i < newEnd; i++)
        {
            if (bin[(int)i] != 0)
            {
                why = $"byte 0x{i:X} after the current block is already in use";
                return false;
            }
        }

        return true;
    }

    private static void ReplaceInstalledBlock(byte[] bin, uint at, int oldLength, byte[] block)
    {
        int clear = Math.Max(oldLength, block.Length);
        Array.Clear(bin, (int)at, clear);
        block.CopyTo(bin, (int)at);
    }

    private static bool EndsFunction(byte[] bin, uint at)
    {
        if (at < 4)
            return false;

        uint w = BitConverter.ToUInt32(bin, (int)at - 4);
        if (w == 0xE12FFF1E || w == 0xE1A0F00E)
            return true;

        return (w & 0x0FFF8000) == 0x08BD8000;
    }

    private static bool RoutineMatches(byte[] bin, uint at)
    {
        if ((long)at > bin.Length - (Routine.Length * 4L))
            return false;

        for (int i = 0; i < Routine.Length; i++)
        {
            if (BitConverter.ToUInt32(bin, (int)at + (i * 4)) != Routine[i])
                return false;
        }

        return true;
    }

    private static bool WordsMatch(byte[] bin, uint at, uint[] expected, out string why)
    {
        why = string.Empty;
        if ((long)at > bin.Length - (expected.Length * 4L))
        {
            why = $"0x{at:X6} is past the end of the file";
            return false;
        }

        for (int i = 0; i < expected.Length; i++)
        {
            uint got = BitConverter.ToUInt32(bin, (int)at + (i * 4));
            if (got == expected[i])
                continue;

            why = $"0x{at + (i * 4):X6} holds {got:X8}, expected {expected[i]:X8}";
            return false;
        }

        return true;
    }

    private static bool TryReadWord(byte[] bin, uint at, out uint value)
    {
        value = 0;
        if ((long)at > bin.Length - 4L)
            return false;

        value = BitConverter.ToUInt32(bin, (int)at);
        return true;
    }

    private static bool IsBranchLink(uint word) => (word & 0xFF000000) == 0xEB000000;

    private static bool TryDecodeBranchTarget(uint from, uint word, out uint target)
    {
        target = 0;
        if (!IsBranchLink(word))
            return false;

        int imm24 = (int)(word & 0x00FFFFFF);
        if ((imm24 & 0x00800000) != 0)
            imm24 |= unchecked((int)0xFF000000);

        long decoded = (long)from + 8 + ((long)imm24 << 2);
        if (decoded < 0 || decoded > uint.MaxValue)
            return false;

        target = (uint)decoded;
        return true;
    }

    private static uint BranchLink(uint from, uint to)
    {
        long delta = (long)to - (from + 8L);
        if ((delta & 3) != 0)
            throw new InvalidOperationException("ARM branch target is not 4-byte aligned.");

        long words = delta >> 2;
        if (words < -0x800000 || words > 0x7FFFFF)
            throw new InvalidOperationException("ARM branch target is outside BL range.");

        return 0xEB000000u | ((uint)words & 0x00FFFFFF);
    }

    private static void WriteWord(byte[] bin, uint at, uint word) =>
        BitConverter.GetBytes(word).CopyTo(bin, (int)at);
}
