using System;
using System.Collections.Generic;
using System.Linq;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// Structural description of one fixed-width Shop.cro table whose rows are grouped by shop.
/// </summary>
public sealed record Gen7ShopGroupedTableSnapshot(
    int CountTableRelocationIndex,
    uint CountTableAddress,
    uint DataStart,
    int DataLength,
    int DataSegment,
    int EntrySize,
    IReadOnlyList<byte> Counts,
    IReadOnlyList<int> PointerRelocationIndexes)
{
    public int ShopCount => Counts.Count;
    public int TotalEntries => Counts.Sum(z => z);
}

/// <summary>
/// Relocation-derived layout of the USUM Shop.cro structures used by the mart and tutor editors.
/// </summary>
public sealed record Gen7ShopCroLayoutSnapshot(
    Gen7RegularMartSnapshot RegularMarts,
    Gen7ShopGroupedTableSnapshot BPItems,
    Gen7ShopGroupedTableSnapshot BPTutors);

/// <summary>
/// Discovers the active USUM Shop.cro layout from CRO relocations.
/// <para>
/// The three count tables remain adjacent in .rodata:
/// 4 BP Tutor counts, 7 BP Item counts, then 28 regular-mart counts plus FF.
/// Their absolute file offsets may move when segment 0 is expanded, so callers must use the
/// resolved addresses in this snapshot instead of the stock 0x52D2/0x52FA/0x54DE constants.
/// </para>
/// </summary>
public static class Gen7ShopCroLayout
{
    public const int BPTutorShopCount = 4;
    public const int BPItemShopCount = 7;
    public const int GroupedEntrySize = 4;

    private const int MetadataPrefixLength =
        BPTutorShopCount +
        BPItemShopCount;

    public static bool TryRead(
        byte[] shopCro,
        out Gen7ShopCroLayoutSnapshot snapshot,
        out string error)
    {
        snapshot = null;
        error = string.Empty;

        if (shopCro is null)
        {
            error = "Shop.cro data is null.";
            return false;
        }

        if (!Gen7ExpandedMartTable.TryRead(
                shopCro,
                out Gen7RegularMartSnapshot regular,
                out error))
        {
            return false;
        }

        if (!CroRelocationMap.TryCreate(
                shopCro,
                out CroRelocationMap map,
                out error))
        {
            return false;
        }

        if (regular.CountTableAddress < MetadataPrefixLength)
        {
            error =
                "Regular mart count table is too early in Shop.cro to contain the BP metadata prefix.";
            return false;
        }

        uint bpItemCountAddress =
            regular.CountTableAddress -
            BPItemShopCount;

        uint bpTutorCountAddress =
            bpItemCountAddress -
            BPTutorShopCount;

        if (!RangeInsideSegment(
                map,
                segmentIndex: 1,
                bpTutorCountAddress,
                MetadataPrefixLength +
                Gen7ExpandedMartTable.RegularMartCount +
                1))
        {
            error =
                "USUM shop count metadata is not fully inside the declared .rodata segment.";
            return false;
        }

        if (!TryFindSingleRelocationTarget(
                map,
                bpItemCountAddress,
                out int bpItemCountRelocation,
                out error))
        {
            error =
                "BP Item count table: " +
                error;
            return false;
        }

        if (!TryFindSingleRelocationTarget(
                map,
                bpTutorCountAddress,
                out int bpTutorCountRelocation,
                out error))
        {
            error =
                "BP Tutor count table: " +
                error;
            return false;
        }

        if (!TryReadCountBytes(
                shopCro,
                bpItemCountAddress,
                BPItemShopCount,
                out byte[] bpItemCounts,
                out error))
        {
            error =
                "BP Item count table: " +
                error;
            return false;
        }

        if (!TryReadCountBytes(
                shopCro,
                bpTutorCountAddress,
                BPTutorShopCount,
                out byte[] bpTutorCounts,
                out error))
        {
            error =
                "BP Tutor count table: " +
                error;
            return false;
        }

        if (!TryDiscoverGroupedTable(
                map,
                bpItemCounts,
                GroupedEntrySize,
                requiredSegment: 1,
                out uint bpItemDataStart,
                out int bpItemDataLength,
                out int bpItemDataSegment,
                out int[] bpItemPointerRelocations,
                out error))
        {
            error =
                "BP Item data table: " +
                error;
            return false;
        }

        if (!TryDiscoverGroupedTable(
                map,
                bpTutorCounts,
                GroupedEntrySize,
                requiredSegment: 1,
                out uint bpTutorDataStart,
                out int bpTutorDataLength,
                out int bpTutorDataSegment,
                out int[] bpTutorPointerRelocations,
                out error))
        {
            error =
                "BP Tutor data table: " +
                error;
            return false;
        }

        if (RangesOverlap(
                bpItemDataStart,
                bpItemDataLength,
                bpTutorDataStart,
                bpTutorDataLength))
        {
            error =
                "BP Item and BP Tutor data tables overlap.";
            return false;
        }

        snapshot =
            new Gen7ShopCroLayoutSnapshot(
                RegularMarts:
                    regular,
                BPItems:
                    new Gen7ShopGroupedTableSnapshot(
                        CountTableRelocationIndex:
                            bpItemCountRelocation,
                        CountTableAddress:
                            bpItemCountAddress,
                        DataStart:
                            bpItemDataStart,
                        DataLength:
                            bpItemDataLength,
                        DataSegment:
                            bpItemDataSegment,
                        EntrySize:
                            GroupedEntrySize,
                        Counts:
                            Array.AsReadOnly(
                                (byte[])bpItemCounts.Clone()),
                        PointerRelocationIndexes:
                            Array.AsReadOnly(
                                (int[])bpItemPointerRelocations.Clone())),
                BPTutors:
                    new Gen7ShopGroupedTableSnapshot(
                        CountTableRelocationIndex:
                            bpTutorCountRelocation,
                        CountTableAddress:
                            bpTutorCountAddress,
                        DataStart:
                            bpTutorDataStart,
                        DataLength:
                            bpTutorDataLength,
                        DataSegment:
                            bpTutorDataSegment,
                        EntrySize:
                            GroupedEntrySize,
                        Counts:
                            Array.AsReadOnly(
                                (byte[])bpTutorCounts.Clone()),
                        PointerRelocationIndexes:
                            Array.AsReadOnly(
                                (int[])bpTutorPointerRelocations.Clone())));

        return true;
    }

    private static bool TryFindSingleRelocationTarget(
        CroRelocationMap map,
        uint targetAddress,
        out int relocationIndex,
        out string error)
    {
        relocationIndex = -1;
        error = string.Empty;

        CroRelocationReference[] matches =
            map.References
                .Where(z =>
                    z.TargetFileBacked &&
                    z.TargetAddress == targetAddress)
                .ToArray();

        if (matches.Length != 1)
        {
            error =
                $"expected exactly one relocation target at 0x{targetAddress:X6}, found {matches.Length}.";
            return false;
        }

        relocationIndex =
            matches[0].Index;

        return true;
    }

    private static bool TryReadCountBytes(
        byte[] data,
        uint address,
        int count,
        out byte[] values,
        out string error)
    {
        values =
            Array.Empty<byte>();

        error =
            string.Empty;

        if (count <= 0)
        {
            error =
                "count length must be positive.";
            return false;
        }

        if (!RangeFits(
                data.Length,
                address,
                count))
        {
            error =
                $"range 0x{address:X6}+0x{count:X} lies outside Shop.cro.";
            return false;
        }

        var result =
            new byte[count];

        int at =
            checked((int)address);

        for (int i = 0;
             i < count;
             i++)
        {
            byte value =
                data[at + i];

            if (value == 0 ||
                value >= 0x80)
            {
                error =
                    $"entry #{i} has unsupported signed count byte 0x{value:X2}.";
                return false;
            }

            result[i] =
                value;
        }

        values =
            result;

        return true;
    }

    private static bool TryDiscoverGroupedTable(
        CroRelocationMap map,
        IReadOnlyList<byte> counts,
        int entrySize,
        int requiredSegment,
        out uint dataStart,
        out int dataLength,
        out int dataSegment,
        out int[] pointerRelocations,
        out string error)
    {
        dataStart = 0;
        dataLength = 0;
        dataSegment = -1;
        pointerRelocations =
            Array.Empty<int>();
        error =
            string.Empty;

        if (counts is null ||
            counts.Count == 0)
        {
            error =
                "count table is empty.";
            return false;
        }

        if (entrySize <= 0)
        {
            error =
                "entry size must be positive.";
            return false;
        }

        long totalEntries = 0;

        foreach (byte count in counts)
        {
            if (count == 0 ||
                count >= 0x80)
            {
                error =
                    $"unsupported signed count byte 0x{count:X2}.";
                return false;
            }

            totalEntries +=
                count;
        }

        long totalBytes =
            totalEntries *
            entrySize;

        if (totalBytes <= 0 ||
            totalBytes > int.MaxValue)
        {
            error =
                "grouped table length is invalid.";
            return false;
        }

        int expectedLength =
            (int)totalBytes;

        var candidates =
            new List<GroupedTableCandidate>();

        foreach (CroRelocationReference first in map.References)
        {
            if (!first.TargetFileBacked)
                continue;

            uint candidateStart =
                first.TargetAddress;

            if (!TryLocateRangeInDeclaredSegment(
                    map,
                    candidateStart,
                    expectedLength,
                    out int candidateSegment))
            {
                continue;
            }

            if (candidateSegment !=
                requiredSegment)
            {
                continue;
            }

            var relocations =
                new int[counts.Count];

            var expectedStarts =
                new HashSet<uint>();

            uint cursor =
                candidateStart;

            bool valid =
                true;

            for (int shop = 0;
                 shop < counts.Count;
                 shop++)
            {
                expectedStarts.Add(
                    cursor);

                CroRelocationReference[] matches =
                    map.References
                        .Where(z =>
                            z.TargetFileBacked &&
                            z.TargetAddress == cursor)
                        .ToArray();

                if (matches.Length != 1)
                {
                    valid =
                        false;
                    break;
                }

                relocations[shop] =
                    matches[0].Index;

                cursor =
                    checked(
                        cursor +
                        ((uint)counts[shop] *
                         (uint)entrySize));
            }

            if (!valid)
                continue;

            uint candidateEnd =
                checked(
                    candidateStart +
                    (uint)expectedLength);

            CroRelocationReference[] targetsInside =
                map.References
                    .Where(z =>
                        z.TargetFileBacked &&
                        z.TargetAddress >= candidateStart &&
                        z.TargetAddress < candidateEnd)
                    .ToArray();

            if (targetsInside.Length !=
                counts.Count)
            {
                continue;
            }

            if (targetsInside.Any(z =>
                    !expectedStarts.Contains(
                        z.TargetAddress)))
            {
                continue;
            }

            bool writeOverlaps =
                map.References.Any(z =>
                    z.WriteFileBacked &&
                    z.WriteAddress < candidateEnd &&
                    z.WriteAddress + 4u > candidateStart);

            if (writeOverlaps)
                continue;

            candidates.Add(
                new GroupedTableCandidate(
                    DataStart:
                        candidateStart,
                    DataLength:
                        expectedLength,
                    DataSegment:
                        candidateSegment,
                    PointerRelocationIndexes:
                        relocations));
        }

        candidates =
            candidates
                .GroupBy(z => z.DataStart)
                .Select(z => z.First())
                .ToList();

        if (candidates.Count == 0)
        {
            error =
                "no relocation-consistent grouped table was found.";
            return false;
        }

        if (candidates.Count != 1)
        {
            error =
                $"layout is ambiguous: {candidates.Count} relocation-consistent grouped tables were found.";
            return false;
        }

        GroupedTableCandidate chosen =
            candidates[0];

        dataStart =
            chosen.DataStart;

        dataLength =
            chosen.DataLength;

        dataSegment =
            chosen.DataSegment;

        pointerRelocations =
            (int[])chosen.PointerRelocationIndexes.Clone();

        return true;
    }

    private static bool TryLocateRangeInDeclaredSegment(
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
            CroSegmentInfo candidate =
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

    private static bool RangeInsideSegment(
        CroRelocationMap map,
        int segmentIndex,
        uint start,
        int length)
    {
        if (segmentIndex < 0 ||
            segmentIndex >= map.Segments.Count ||
            length <= 0)
        {
            return false;
        }

        CroSegmentInfo segment =
            map.Segments[segmentIndex];

        if (!segment.FileBacked)
            return false;

        ulong end =
            (ulong)start +
            (uint)length;

        return (ulong)start >= segment.Start &&
               end <=
               (ulong)segment.Start +
               segment.Size;
    }

    private static bool RangesOverlap(
        uint leftStart,
        int leftLength,
        uint rightStart,
        int rightLength)
    {
        ulong leftEnd =
            (ulong)leftStart +
            (uint)leftLength;

        ulong rightEnd =
            (ulong)rightStart +
            (uint)rightLength;

        return (ulong)leftStart < rightEnd &&
               (ulong)rightStart < leftEnd;
    }

    private static bool RangeFits(
        int fileLength,
        uint start,
        int length) =>
        length >= 0 &&
        start <= (uint)fileLength &&
        (uint)length <=
        (uint)fileLength -
        start;

    private sealed record GroupedTableCandidate(
        uint DataStart,
        int DataLength,
        int DataSegment,
        int[] PointerRelocationIndexes);
}