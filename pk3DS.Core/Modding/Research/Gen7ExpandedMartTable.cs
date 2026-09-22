using System;
using System.Collections.Generic;
using System.Linq;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// Structural snapshot of the 28 regular USUM marts stored in Shop.cro.
/// The active table is discovered from CRO relocations instead of fixed file offsets,
/// so a Shop.cro produced by a previous expansion can be opened and expanded again.
/// </summary>
public sealed record Gen7RegularMartSnapshot(
    int CountTableRelocationIndex,
    uint CountTableAddress,
    uint DataStart,
    int DataLength,
    int DataSegment,
    IReadOnlyList<int> PointerRelocationIndexes,
    IReadOnlyList<ushort[]> Inventories)
{
    public int MartCount => Inventories.Count;
    public int TotalSlots => Inventories.Sum(z => z.Length);
}

/// <summary>
/// Audit report for one regular-mart rebuild.
/// </summary>
public sealed record Gen7ExpandedMartBuildReport(
    bool NoChanges,
    int OriginalFileSize,
    int FinalFileSize,
    int OldTotalSlots,
    int NewTotalSlots,
    uint OldDataStart,
    uint NewDataStart,
    uint CountTableAddress,
    int CountTableRelocationIndex,
    int RelocationsEdited,
    int CodeBytesAdded,
    bool ReusedExistingCodeSpace);

/// <summary>
/// Reads and rebuilds the 28 regular USUM marts in Shop.cro.
/// <para>
/// Stock Shop.cro stores all 28 inventories contiguously in .rodata with one type-0x02
/// relocation per mart start and a separate relocation to the 28-byte length table.
/// A rebuild copies the inventories to relocation-safe segment-0 storage, updates the length
/// bytes, and retargets the existing 28 pointer relocations without increasing relocation count.
/// </para>
/// <para>
/// Existing free segment-0 space is reused first. If none is large enough, the CRO code segment
/// is expanded through <see cref="CroPatchSession"/>. This permits a previously expanded Shop.cro
/// to be edited again without forcing one new page per save.
/// </para>
/// </summary>
public static class Gen7ExpandedMartTable
{
    public const int RegularMartCount = 28;
    public const int MaximumSlotsPerMart = 0x7F;

    private const byte LengthTerminator = 0xFF;

    /// <summary>
    /// Discovers the active regular-mart table and returns its current inventories.
    /// No stock absolute table offsets are required.
    /// </summary>
    public static bool TryRead(
        byte[] shopCro,
        out Gen7RegularMartSnapshot snapshot,
        out string error)
    {
        snapshot = null;
        error = string.Empty;

        if (shopCro is null)
        {
            error = "Shop.cro data is null.";
            return false;
        }

        if (!CroRelocationMap.TryCreate(
                shopCro,
                out var map,
                out error))
        {
            return false;
        }

        var candidates =
            new List<LayoutCandidate>();

        foreach (var countReference in map.References)
        {
            if (!countReference.TargetFileBacked)
                continue;

            if (!TryReadLengths(
                    shopCro,
                    countReference.TargetAddress,
                    out int[] lengths))
            {
                continue;
            }

            foreach (var firstReference in map.References)
            {
                if (!firstReference.TargetFileBacked)
                    continue;

                uint dataStart =
                    firstReference.TargetAddress;

                if (!TryMatchMartChain(
                        map,
                        lengths,
                        dataStart,
                        out int dataLength,
                        out int dataSegment,
                        out int[] pointerRelocations))
                {
                    continue;
                }

                candidates.Add(
                    new LayoutCandidate(
                        CountTableRelocationIndex: countReference.Index,
                        CountTableAddress: countReference.TargetAddress,
                        DataStart: dataStart,
                        DataLength: dataLength,
                        DataSegment: dataSegment,
                        Lengths: lengths,
                        PointerRelocationIndexes: pointerRelocations));
            }
        }

        candidates =
            candidates
                .GroupBy(z =>
                    (
                        z.CountTableRelocationIndex,
                        z.DataStart))
                .Select(z => z.First())
                .ToList();

        if (candidates.Count == 0)
        {
            error =
                "Could not discover the 28 regular USUM marts from Shop.cro relocations.";
            return false;
        }

        if (candidates.Count != 1)
        {
            error =
                $"Regular mart layout is ambiguous: {candidates.Count} relocation-consistent candidates were found.";
            return false;
        }

        LayoutCandidate candidate =
            candidates[0];

        var inventories =
            new ushort[RegularMartCount][];

        uint cursor =
            candidate.DataStart;

        for (int mart = 0;
             mart < RegularMartCount;
             mart++)
        {
            int count =
                candidate.Lengths[mart];

            var items =
                new ushort[count];

            for (int slot = 0;
                 slot < count;
                 slot++)
            {
                int at =
                    checked(
                        (int)cursor +
                        (slot * 2));

                items[slot] =
                    BitConverter.ToUInt16(
                        shopCro,
                        at);
            }

            inventories[mart] =
                items;

            cursor =
                checked(
                    cursor +
                    ((uint)count * 2u));
        }

        snapshot =
            new Gen7RegularMartSnapshot(
                CountTableRelocationIndex:
                    candidate.CountTableRelocationIndex,
                CountTableAddress:
                    candidate.CountTableAddress,
                DataStart:
                    candidate.DataStart,
                DataLength:
                    candidate.DataLength,
                DataSegment:
                    candidate.DataSegment,
                PointerRelocationIndexes:
                    Array.AsReadOnly(
                        (int[])candidate.PointerRelocationIndexes.Clone()),
                Inventories:
                    Array.AsReadOnly(
                        inventories
                            .Select(z => (ushort[])z.Clone())
                            .ToArray()));

        return true;
    }

    /// <summary>
    /// Rebuilds all 28 regular marts as one detached CRO transaction.
    /// The caller's input bytes are never mutated.
    /// </summary>
    public static bool TryRebuild(
        byte[] shopCro,
        IReadOnlyList<ushort[]> inventories,
        out byte[] updated,
        out Gen7ExpandedMartBuildReport report,
        out string error)
    {
        updated = null;
        report = null;
        error = string.Empty;

        if (shopCro is null)
        {
            error = "Shop.cro data is null.";
            return false;
        }

        if (!TryNormalizeInventories(
                inventories,
                out ushort[][] requested,
                out error))
        {
            return false;
        }

        if (!TryRead(
                shopCro,
                out Gen7RegularMartSnapshot source,
                out error))
        {
            return false;
        }

        if (InventoriesEqual(
                source.Inventories,
                requested))
        {
            updated =
                (byte[])shopCro.Clone();

            report =
                new Gen7ExpandedMartBuildReport(
                    NoChanges: true,
                    OriginalFileSize: shopCro.Length,
                    FinalFileSize: shopCro.Length,
                    OldTotalSlots: source.TotalSlots,
                    NewTotalSlots: source.TotalSlots,
                    OldDataStart: source.DataStart,
                    NewDataStart: source.DataStart,
                    CountTableAddress: source.CountTableAddress,
                    CountTableRelocationIndex: source.CountTableRelocationIndex,
                    RelocationsEdited: 0,
                    CodeBytesAdded: 0,
                    ReusedExistingCodeSpace: false);

            return true;
        }

        if (!CroRelocationMap.TryCreate(
                shopCro,
                out var sourceMap,
                out error))
        {
            return false;
        }

        if (!TryBuildPayload(
                requested,
                out byte[] payload,
                out int[] counts,
                out error))
        {
            return false;
        }

        int allocationSize;

        try
        {
            allocationSize =
                checked(
                    (payload.Length + 3) &
                    ~3);
        }
        catch (OverflowException)
        {
            error = "Expanded regular mart table is too large to align.";
            return false;
        }

        byte[] staged;
        uint newDataStart;
        int codeBytesAdded = 0;
        bool reusedExistingCodeSpace = false;

        uint? existing =
            sourceMap.FindFirstFreeRun(
                sourceMap.CodeStart,
                sourceMap.CodeEnd,
                allocationSize);

        if (existing is not null)
        {
            staged =
                (byte[])shopCro.Clone();

            newDataStart =
                existing.Value;

            Array.Clear(
                staged,
                checked((int)newDataStart),
                allocationSize);

            payload.CopyTo(
                staged,
                checked((int)newDataStart));

            reusedExistingCodeSpace =
                true;
        }
        else
        {
            if (!CroPatchSession.TryCreate(
                    shopCro,
                    out var session,
                    out error))
            {
                return false;
            }

            if (!session.TryAllocateRelocatableCode(
                    allocationSize,
                    "gen7-expanded-regular-marts",
                    out CroCodeGrant grant,
                    out error))
            {
                return false;
            }

            if (!session.TryWriteCode(
                    grant,
                    payload,
                    out error))
            {
                return false;
            }

            if (!session.TryBuildImage(
                    out staged,
                    out CroPatchSessionReport sessionReport,
                    out error))
            {
                return false;
            }

            newDataStart =
                grant.Offset;

            codeBytesAdded =
                sessionReport.TotalCodeBytesAdded;
        }

        if (!CroRelocationMap.TryCreate(
                staged,
                out var stagedMap,
                out error))
        {
            return false;
        }

        if ((uint)source.CountTableRelocationIndex >=
            stagedMap.PatchTableCount)
        {
            error =
                $"Regular mart length relocation #{source.CountTableRelocationIndex} disappeared after CRO staging.";
            return false;
        }

        var countReference =
            stagedMap.References[
                source.CountTableRelocationIndex];

        if (!countReference.TargetFileBacked)
        {
            error =
                $"Regular mart length relocation #{source.CountTableRelocationIndex} no longer targets file-backed bytes.";
            return false;
        }

        uint countTableAddress =
            countReference.TargetAddress;

        if (!RangeFits(
                staged.Length,
                countTableAddress,
                RegularMartCount + 1))
        {
            error =
                "Regular mart length table lies outside the staged Shop.cro.";
            return false;
        }

        int countAt =
            checked((int)countTableAddress);

        for (int mart = 0;
             mart < RegularMartCount;
             mart++)
        {
            byte expectedOld =
                checked(
                    (byte)source.Inventories[mart].Length);

            if (staged[countAt + mart] != expectedOld)
            {
                error =
                    $"Regular mart #{mart} length byte changed unexpectedly before rebuild.";
                return false;
            }
        }

        if (staged[countAt + RegularMartCount] !=
            LengthTerminator)
        {
            error =
                "Regular mart length table lost its 0xFF terminator before rebuild.";
            return false;
        }

        for (int mart = 0;
             mart < RegularMartCount;
             mart++)
        {
            staged[countAt + mart] =
                checked(
                    (byte)counts[mart]);
        }

        var edits =
            new CroRelocationPointerEdit[
                RegularMartCount];

        uint cursor =
            newDataStart;

        for (int mart = 0;
             mart < RegularMartCount;
             mart++)
        {
            int relocationIndex =
                source.PointerRelocationIndexes[mart];

            if (relocationIndex < 0 ||
                (uint)relocationIndex >= stagedMap.PatchTableCount)
            {
                error =
                    $"Regular mart #{mart} relocation #{relocationIndex} is outside the staged relocation table.";
                return false;
            }

            var currentReference =
                stagedMap.References[
                    relocationIndex];

            if (!currentReference.WriteFileBacked)
            {
                error =
                    $"Regular mart #{mart} relocation #{relocationIndex} no longer has a file-backed write slot.";
                return false;
            }

            edits[mart] =
                new CroRelocationPointerEdit(
                    RelocationIndex: relocationIndex,
                    WriteAddress: currentReference.WriteAddress,
                    TargetAddress: cursor,
                    Purpose: $"regular-mart#{mart}");

            cursor =
                checked(
                    cursor +
                    ((uint)counts[mart] * 2u));
        }

        if (cursor !=
            newDataStart + (uint)payload.Length)
        {
            error =
                "Regular mart payload cursor did not end at the expected byte.";
            return false;
        }

        if (!CroRelocationEditor.TryRewritePointers(
                staged,
                edits,
                out byte[] rebuilt,
                out IReadOnlyList<CroRelocationEditReport> relocationReports,
                out error))
        {
            return false;
        }

        if (!TryRead(
                rebuilt,
                out Gen7RegularMartSnapshot final,
                out error))
        {
            error =
                "Rebuilt Shop.cro could not be rediscovered: " +
                error;
            return false;
        }

        if (!InventoriesEqual(
                final.Inventories,
                requested))
        {
            error =
                "Rebuilt regular mart inventories do not match the requested inventories.";
            return false;
        }

        if (final.DataStart !=
            newDataStart)
        {
            error =
                $"Rebuilt regular mart table resolved to 0x{final.DataStart:X6}, expected 0x{newDataStart:X6}.";
            return false;
        }

        if (relocationReports.Count !=
            RegularMartCount)
        {
            error =
                $"Expected {RegularMartCount} relocation edits, got {relocationReports.Count}.";
            return false;
        }

        updated =
            rebuilt;

        report =
            new Gen7ExpandedMartBuildReport(
                NoChanges: false,
                OriginalFileSize: shopCro.Length,
                FinalFileSize: rebuilt.Length,
                OldTotalSlots: source.TotalSlots,
                NewTotalSlots: final.TotalSlots,
                OldDataStart: source.DataStart,
                NewDataStart: final.DataStart,
                CountTableAddress: final.CountTableAddress,
                CountTableRelocationIndex: final.CountTableRelocationIndex,
                RelocationsEdited: relocationReports.Count,
                CodeBytesAdded: codeBytesAdded,
                ReusedExistingCodeSpace: reusedExistingCodeSpace);

        return true;
    }

    private static bool TryReadLengths(
        byte[] data,
        uint address,
        out int[] lengths)
    {
        lengths =
            Array.Empty<int>();

        if (!RangeFits(
                data.Length,
                address,
                RegularMartCount + 1))
        {
            return false;
        }

        int at =
            checked((int)address);

        var candidate =
            new int[RegularMartCount];

        for (int i = 0;
             i < RegularMartCount;
             i++)
        {
            int value =
                data[at + i];

            if (value <= 0 ||
                value > MaximumSlotsPerMart)
            {
                return false;
            }

            candidate[i] =
                value;
        }

        if (data[at + RegularMartCount] !=
            LengthTerminator)
        {
            return false;
        }

        lengths =
            candidate;

        return true;
    }

    private static bool TryMatchMartChain(
        CroRelocationMap map,
        IReadOnlyList<int> lengths,
        uint dataStart,
        out int dataLength,
        out int dataSegment,
        out int[] pointerRelocations)
    {
        dataLength = 0;
        dataSegment = -1;
        pointerRelocations =
            Array.Empty<int>();

        if ((dataStart & 1u) != 0 ||
            lengths is null ||
            lengths.Count != RegularMartCount)
        {
            return false;
        }

        long totalSlots = 0;

        for (int i = 0;
             i < lengths.Count;
             i++)
        {
            int count =
                lengths[i];

            if (count <= 0 ||
                count > MaximumSlotsPerMart)
            {
                return false;
            }

            totalSlots +=
                count;
        }

        long bytes =
            totalSlots * 2L;

        if (bytes <= 0 ||
            bytes > int.MaxValue)
        {
            return false;
        }

        dataLength =
            (int)bytes;

        if (!TryLocateRangeInFileBackedSegment(
                map,
                dataStart,
                dataLength,
                out dataSegment))
        {
            return false;
        }

        var relocations =
            new int[RegularMartCount];

        var expectedStarts =
            new HashSet<uint>();

        uint cursor =
            dataStart;

        for (int mart = 0;
             mart < RegularMartCount;
             mart++)
        {
            expectedStarts.Add(
                cursor);

            var matches =
                map.References
                    .Where(z =>
                        z.TargetFileBacked &&
                        z.TargetAddress == cursor)
                    .ToArray();

            if (matches.Length != 1)
                return false;

            relocations[mart] =
                matches[0].Index;

            cursor =
                checked(
                    cursor +
                    ((uint)lengths[mart] * 2u));
        }

        uint dataEnd =
            checked(
                dataStart +
                (uint)dataLength);

        int targetsInside =
            map.References.Count(z =>
                z.TargetFileBacked &&
                z.TargetAddress >= dataStart &&
                z.TargetAddress < dataEnd);

        if (targetsInside !=
            RegularMartCount)
        {
            return false;
        }

        foreach (var reference in map.References)
        {
            if (!reference.TargetFileBacked ||
                reference.TargetAddress < dataStart ||
                reference.TargetAddress >= dataEnd)
            {
                continue;
            }

            if (!expectedStarts.Contains(
                    reference.TargetAddress))
            {
                return false;
            }
        }

        bool hasWriteInside =
            map.References.Any(z =>
                z.WriteFileBacked &&
                z.WriteAddress < dataEnd &&
                z.WriteAddress + 4u > dataStart);

        if (hasWriteInside)
            return false;

        pointerRelocations =
            relocations;

        return true;
    }

    private static bool TryLocateRangeInFileBackedSegment(
        CroRelocationMap map,
        uint start,
        int length,
        out int segment)
    {
        segment = -1;

        if (length <= 0)
            return false;

        ulong requestedEnd =
            (ulong)start +
            (uint)length;

        for (int i = 0;
             i < 3 &&
             i < map.Segments.Count;
             i++)
        {
            var candidate =
                map.Segments[i];

            if (!candidate.FileBacked)
                continue;

            ulong segmentStart =
                candidate.Start;

            ulong segmentEnd =
                segmentStart +
                candidate.Size;

            if ((ulong)start < segmentStart ||
                requestedEnd > segmentEnd)
            {
                continue;
            }

            segment =
                i;

            return true;
        }

        return false;
    }

    private static bool TryNormalizeInventories(
        IReadOnlyList<ushort[]> inventories,
        out ushort[][] normalized,
        out string error)
    {
        normalized =
            Array.Empty<ushort[]>();

        error =
            string.Empty;

        if (inventories is null)
        {
            error =
                "Regular mart inventories are null.";
            return false;
        }

        if (inventories.Count !=
            RegularMartCount)
        {
            error =
                $"Expected {RegularMartCount} regular marts, got {inventories.Count}.";
            return false;
        }

        var result =
            new ushort[RegularMartCount][];

        for (int mart = 0;
             mart < RegularMartCount;
             mart++)
        {
            ushort[] inventory =
                inventories[mart];

            if (inventory is null)
            {
                error =
                    $"Regular mart #{mart} inventory is null.";
                return false;
            }

            if (inventory.Length <= 0)
            {
                error =
                    $"Regular mart #{mart} cannot be empty because 0 terminates the USUM length table.";
                return false;
            }

            if (inventory.Length >
                MaximumSlotsPerMart)
            {
                error =
                    $"Regular mart #{mart} has {inventory.Length} slots; the USUM signed length byte supports at most {MaximumSlotsPerMart}.";
                return false;
            }

            result[mart] =
                (ushort[])inventory.Clone();
        }

        normalized =
            result;

        return true;
    }

    private static bool TryBuildPayload(
        IReadOnlyList<ushort[]> inventories,
        out byte[] payload,
        out int[] counts,
        out string error)
    {
        payload =
            null;

        counts =
            Array.Empty<int>();

        error =
            string.Empty;

        long totalSlots = 0;

        var lengths =
            new int[RegularMartCount];

        for (int mart = 0;
             mart < RegularMartCount;
             mart++)
        {
            int count =
                inventories[mart].Length;

            lengths[mart] =
                count;

            totalSlots +=
                count;
        }

        long bytes =
            totalSlots * 2L;

        if (bytes <= 0 ||
            bytes > int.MaxValue)
        {
            error =
                "Expanded regular mart payload is too large.";
            return false;
        }

        var result =
            new byte[(int)bytes];

        int at = 0;

        for (int mart = 0;
             mart < RegularMartCount;
             mart++)
        {
            ushort[] inventory =
                inventories[mart];

            for (int slot = 0;
                 slot < inventory.Length;
                 slot++)
            {
                BitConverter.GetBytes(
                        inventory[slot])
                    .CopyTo(
                        result,
                        at);

                at +=
                    2;
            }
        }

        if (at !=
            result.Length)
        {
            error =
                "Expanded regular mart payload length was calculated incorrectly.";
            return false;
        }

        payload =
            result;

        counts =
            lengths;

        return true;
    }

    private static bool InventoriesEqual(
        IReadOnlyList<ushort[]> left,
        IReadOnlyList<ushort[]> right)
    {
        if (left is null ||
            right is null ||
            left.Count != right.Count)
        {
            return false;
        }

        for (int i = 0;
             i < left.Count;
             i++)
        {
            if (left[i] is null ||
                right[i] is null ||
                !left[i].AsSpan()
                    .SequenceEqual(
                        right[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool RangeFits(
        int fileLength,
        uint start,
        int length) =>
        length >= 0 &&
        start <= (uint)fileLength &&
        (uint)length <= (uint)fileLength - start;

    private sealed record LayoutCandidate(
        int CountTableRelocationIndex,
        uint CountTableAddress,
        uint DataStart,
        int DataLength,
        int DataSegment,
        int[] Lengths,
        int[] PointerRelocationIndexes);
}