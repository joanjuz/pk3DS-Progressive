using System;
using System.Collections.Generic;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// Lightweight structural view of a CRO relocation table.
/// It intentionally parses only the information needed to decide whether
/// file-backed bytes are genuinely free for injected code/data.
/// </summary>
public sealed class CroRelocationMap
{
    private const uint CroMagic = 0x304F5243; // "CRO0" little-endian.
    private const int SegmentEntrySize = 12;
    private const int RelocationEntrySize = 12;
    private const int RuntimeSegmentCount = 4;

    private readonly byte[] data;
    private readonly List<CroSegmentInfo> segments;
    private readonly List<CroRelocationReference> references;
    private readonly SortedSet<uint> blockedWords;

    public uint CodeStart => segments[0].Start;
    public uint CodeSize => segments[0].Size;
    public uint CodeEnd => CodeStart + CodeSize;
    public uint RodataStart => segments[1].Start;
    public uint RodataSize => segments[1].Size;
    public uint DataStart => segments[2].Start;
    public uint DataSize => segments[2].Size;

    public uint SegmentTableOffset { get; }
    public uint SegmentTableCount { get; }
    public uint PatchTableOffset { get; }
    public uint PatchTableCount { get; }

    public IReadOnlyList<CroSegmentInfo> Segments => segments;
    public IReadOnlyList<CroRelocationReference> References => references;

    private CroRelocationMap(
        byte[] data,
        uint segmentTableOffset,
        uint segmentTableCount,
        uint patchTableOffset,
        uint patchTableCount,
        List<CroSegmentInfo> segments,
        List<CroRelocationReference> references,
        SortedSet<uint> blockedWords)
    {
        this.data = data;
        SegmentTableOffset = segmentTableOffset;
        SegmentTableCount = segmentTableCount;
        PatchTableOffset = patchTableOffset;
        PatchTableCount = patchTableCount;
        this.segments = segments;
        this.references = references;
        this.blockedWords = blockedWords;
    }

    /// <summary>
    /// Parses the CRO segment table and relocation patch table.
    /// The segment table is the source of truth for file-backed segment boundaries.
    /// Inline header sizes are deliberately not used as .text/.rodata boundaries because
    /// USUM Battle.cro can describe a larger mapped code region there than segment 0 actually owns.
    /// Invalid/truncated metadata fails closed instead of being ignored.
    /// </summary>
    public static bool TryCreate(byte[] data, out CroRelocationMap map, out string error)
    {
        map = null;
        error = string.Empty;

        if (data is null)
        {
            error = "CRO data is null.";
            return false;
        }

        if (data.Length < 0x130)
        {
            error = "CRO is too small to contain the required header.";
            return false;
        }

        if (BitConverter.ToUInt32(data, 0x80) != CroMagic)
        {
            error = "CRO0 magic was not found at 0x80.";
            return false;
        }

        uint segmentTableOffset = BitConverter.ToUInt32(data, 0xC8);
        uint segmentTableCount = BitConverter.ToUInt32(data, 0xCC);
        uint patchTableOffset = BitConverter.ToUInt32(data, 0x128);
        uint patchTableCount = BitConverter.ToUInt32(data, 0x12C);

        if (segmentTableCount < RuntimeSegmentCount)
        {
            error = $"CRO segment table has only {segmentTableCount} entries; at least {RuntimeSegmentCount} are required.";
            return false;
        }

        long segmentBytes = (long)segmentTableCount * SegmentEntrySize;
        if (segmentTableOffset > data.Length ||
            segmentBytes < 0 ||
            segmentTableOffset + segmentBytes > data.Length)
        {
            error = "CRO segment table lies outside the file.";
            return false;
        }

        long relocationBytes = (long)patchTableCount * RelocationEntrySize;
        if (patchTableOffset > data.Length ||
            relocationBytes < 0 ||
            patchTableOffset + relocationBytes > data.Length)
        {
            error = "CRO relocation table lies outside the file.";
            return false;
        }

        var segments = new List<CroSegmentInfo>((int)Math.Min(segmentTableCount, int.MaxValue));
        for (uint i = 0; i < segmentTableCount; i++)
        {
            int entry = checked((int)(segmentTableOffset + (i * SegmentEntrySize)));
            uint start = BitConverter.ToUInt32(data, entry);
            uint size = BitConverter.ToUInt32(data, entry + 4);
            uint id = BitConverter.ToUInt32(data, entry + 8);

            bool fileBacked = i < 3 && start != 0 && size != 0;
            if (fileBacked && !RangeFits(data.Length, start, size))
            {
                error = $"CRO segment #{i} lies outside the file.";
                return false;
            }

            segments.Add(new CroSegmentInfo((int)i, start, size, id, fileBacked));
        }

        if (!segments[0].FileBacked || !segments[1].FileBacked || !segments[2].FileBacked)
        {
            error = "CRO code/rodata/data segments are not file-backed as expected.";
            return false;
        }

        var references = new List<CroRelocationReference>((int)Math.Min(patchTableCount, int.MaxValue));
        var blockedWords = new SortedSet<uint>();

        for (uint i = 0; i < patchTableCount; i++)
        {
            long entryLong = (long)patchTableOffset + ((long)i * RelocationEntrySize);
            if (entryLong < 0 || entryLong + RelocationEntrySize > data.Length)
            {
                error = $"Relocation #{i} is truncated.";
                return false;
            }

            int entry = (int)entryLong;
            uint word0 = BitConverter.ToUInt32(data, entry);
            uint word1 = BitConverter.ToUInt32(data, entry + 4);
            uint addend = BitConverter.ToUInt32(data, entry + 8);

            int writeSegment = (int)(word0 & 0xF);
            uint writeOffset = word0 >> 4;
            int targetSegment = (int)((word1 >> 8) & 0xFF);

            if ((uint)writeSegment >= RuntimeSegmentCount ||
                (uint)targetSegment >= RuntimeSegmentCount)
            {
                error = $"Relocation #{i} references unsupported segment {writeSegment}->{targetSegment}.";
                return false;
            }

            bool writeFileBacked = TryResolveFileAddress(
                data.Length, segments, writeSegment, writeOffset, out uint writeAddress);

            bool targetFileBacked = TryResolveFileAddress(
                data.Length, segments, targetSegment, addend, out uint targetAddress);

            // A write slot in code/rodata/data must resolve to actual file bytes.
            // BSS (segment 3) is memory-only and intentionally has no file address.
            if (writeSegment < 3 && !writeFileBacked)
            {
                error = $"Relocation #{i} writes outside the file-backed bytes of segment {writeSegment}.";
                return false;
            }

            references.Add(new CroRelocationReference(
                (int)i,
                writeAddress,
                targetAddress,
                writeFileBacked,
                targetFileBacked));

            // Code allocations are 4-byte aligned. Block the complete word containing either
            // a loader write slot or a file-backed relocation target.
            if (writeFileBacked)
                blockedWords.Add(writeAddress & ~3u);

            if (targetFileBacked)
                blockedWords.Add(targetAddress & ~3u);
        }

        map = new CroRelocationMap(
            data,
            segmentTableOffset,
            segmentTableCount,
            patchTableOffset,
            patchTableCount,
            segments,
            references,
            blockedWords);

        return true;
    }

    /// <summary>
    /// True only when every byte is blank (00/CC) and no CRO relocation writes to or targets
    /// any 4-byte word in the requested range.
    /// </summary>
    public bool IsRangeFree(uint start, int length)
    {
        if (length <= 0)
            return false;

        if (!RangeFits(data.Length, start, (uint)length))
            return false;

        uint end = start + (uint)length;

        for (uint p = start; p < end; p++)
        {
            byte value = data[(int)p];
            if (value != 0x00 && value != 0xCC)
                return false;
        }

        uint firstWord = start & ~3u;
        uint lastWord = (end - 1) & ~3u;
        return blockedWords.GetViewBetween(firstWord, lastWord).Count == 0;
    }

    /// <summary>
    /// Finds the first aligned free run inside [start, end).
    /// Blank bytes that are relocation slots or file-backed targets are automatically excluded.
    /// </summary>
    public uint? FindFirstFreeRun(uint start, uint end, int minimumLength)
    {
        if (minimumLength <= 0 || start >= end || end > data.Length)
            return null;

        int wanted = (minimumLength + 3) & ~3;
        long runStart = -1;

        for (long p = start; p <= end; p++)
        {
            bool blank =
                p < end &&
                (data[(int)p] == 0x00 || data[(int)p] == 0xCC);

            if (blank)
            {
                if (runStart < 0)
                    runStart = p;
                continue;
            }

            if (runStart >= 0)
            {
                uint? found = FindInsideBlankRun((uint)runStart, (uint)p, wanted);
                if (found is not null)
                    return found;
            }

            runStart = -1;
        }

        return null;
    }

    /// <summary>
    /// Finds the largest relocation-safe blank reserve inside segment 0 (.text).
    /// Unlike the inline 0xB0/0xB4 header fields, this boundary comes from the actual
    /// CRO segment table and therefore cannot accidentally scan .rodata/alignment space.
    /// </summary>
    public CroFreeRange FindLargestCodeReserve(int minimumLength = 0x40)
    {
        uint start = CodeStart;
        uint end = Math.Min(CodeEnd, (uint)data.Length);

        if (end <= start)
            return default;

        CroFreeRange best = default;
        long runStart = -1;

        for (long p = start; p <= end; p++)
        {
            bool blank =
                p < end &&
                (data[(int)p] == 0x00 || data[(int)p] == 0xCC);

            if (blank)
            {
                if (runStart < 0)
                    runStart = p;
                continue;
            }

            if (runStart >= 0)
                ConsiderBlankRun((uint)runStart, (uint)p, ref best);

            runStart = -1;
        }

        return best.Length >= minimumLength ? best : default;
    }

    public int CountReferencesInRange(uint start, int length)
    {
        if (length <= 0)
            return 0;

        ulong end = (ulong)start + (uint)length;
        int count = 0;

        foreach (var reference in references)
        {
            bool writeOverlaps =
                reference.WriteFileBacked &&
                (ulong)reference.WriteAddress < end &&
                (ulong)reference.WriteAddress + 4 > start;

            bool targetInside =
                reference.TargetFileBacked &&
                reference.TargetAddress >= start &&
                (ulong)reference.TargetAddress < end;

            if (writeOverlaps || targetInside)
                count++;
        }

        return count;
    }

    private uint? FindInsideBlankRun(uint from, uint to, int wanted)
    {
        uint cursor = Align4(from);
        if (to <= cursor)
            return null;

        uint firstWord = cursor & ~3u;
        uint lastWord = (to - 1) & ~3u;

        foreach (uint blocked in blockedWords.GetViewBetween(firstWord, lastWord))
        {
            if (blocked > cursor && blocked - cursor >= wanted)
                return cursor;

            uint afterBlocked = blocked <= uint.MaxValue - 4 ? blocked + 4 : uint.MaxValue;
            if (afterBlocked > cursor)
                cursor = Align4(afterBlocked);

            if (cursor >= to)
                return null;
        }

        return to - cursor >= wanted ? cursor : null;
    }

    private void ConsiderBlankRun(uint from, uint to, ref CroFreeRange best)
    {
        uint cursor = Align4(from);
        if (to <= cursor)
            return;

        uint firstWord = cursor & ~3u;
        uint lastWord = (to - 1) & ~3u;

        foreach (uint blocked in blockedWords.GetViewBetween(firstWord, lastWord))
        {
            Take(cursor, Math.Min(blocked, to), ref best);

            uint afterBlocked = blocked <= uint.MaxValue - 4 ? blocked + 4 : uint.MaxValue;
            if (afterBlocked > cursor)
                cursor = Align4(afterBlocked);

            if (cursor >= to)
                return;
        }

        Take(cursor, to, ref best);
    }

    private static void Take(uint from, uint to, ref CroFreeRange best)
    {
        uint aligned = Align4(from);
        if (to <= aligned)
            return;

        int length = checked((int)(to - aligned));
        if (length > best.Length)
            best = new CroFreeRange(aligned, length);
    }

    private static bool TryResolveFileAddress(
        int fileLength,
        IReadOnlyList<CroSegmentInfo> segments,
        int segment,
        uint relative,
        out uint address)
    {
        address = 0;

        // Segment 3 is BSS: it exists only after loading and therefore has no file offset.
        if (segment < 0 || segment >= 3 || segment >= segments.Count)
            return false;

        uint start = segments[segment].Start;
        if (!TryAdd(start, relative, out uint absolute))
            return false;

        // A relocation target can legally land at the first byte after the declared segment
        // (USUM Battle.cro does this at 0xFC974), so resolve by file bounds rather than by the
        // segment's declared size. The allocator still treats that target word conservatively.
        if (absolute >= (uint)fileLength)
            return false;

        address = absolute;
        return true;
    }

    private static uint Align4(uint value) => (value + 3u) & ~3u;

    private static bool RangeFits(int fileLength, uint start, uint length) =>
        start <= (uint)fileLength &&
        length <= (uint)fileLength - start;

    private static bool TryAdd(uint left, uint right, out uint value)
    {
        ulong sum = (ulong)left + right;
        if (sum > uint.MaxValue)
        {
            value = 0;
            return false;
        }

        value = (uint)sum;
        return true;
    }
}

public readonly record struct CroSegmentInfo(
    int Index,
    uint Start,
    uint Size,
    uint Id,
    bool FileBacked);

public readonly record struct CroRelocationReference(
    int Index,
    uint WriteAddress,
    uint TargetAddress,
    bool WriteFileBacked,
    bool TargetFileBacked);

public readonly record struct CroFreeRange(
    uint Offset,
    int Length);
