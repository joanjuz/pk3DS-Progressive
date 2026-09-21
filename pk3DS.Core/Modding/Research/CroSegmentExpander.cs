using System;
using System.Security.Cryptography;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// Describes one in-memory expansion of CRO segment 0.
/// </summary>
public sealed record CroCodeExpansionReport(
    int BytesAdded,
    uint InsertionOffset,
    int OldFileSize,
    int NewFileSize,
    uint OldCodeEnd,
    uint NewCodeEnd,
    uint OldNextSegmentStart,
    uint NewNextSegmentStart,
    uint OldSegmentTableOffset,
    uint NewSegmentTableOffset,
    uint OldPatchTableOffset,
    uint NewPatchTableOffset,
    int ShiftedHeaderPointers,
    int ShiftedInternalPointers,
    int RelocationReferencesInExpandedRange,
    CroFreeRange ExpandedCodeRange,
    CroFreeRange InsertedFileRange);

/// <summary>
/// First conservative CRO segment expander.
/// <para>
/// This phase only grows segment 0 (.text), only in page-sized increments, and never mutates
/// the caller's buffer. Physical bytes are inserted at the start of the next file-backed segment
/// rather than at the old .text end. That preserves the existing executable padding addresses
/// (including already-installed code caves) and preserves the original gap/alignment before the
/// following segment.
/// </para>
/// <para>
/// CRO hashes are intentionally recalculated from the inline hash regions (0xB0-0xBC). Those
/// regions are not the same thing as the real runtime segment table in USUM Battle.cro.
/// Runtime segment ownership comes from the segment table; integrity hash boundaries come from
/// the inline fields.
/// </para>
/// </summary>
public static class CroSegmentExpander
{
    private const int PageSize = 0x1000;

    /// <summary>
    /// Expands real segment 0 by <paramref name="bytesToAdd"/> bytes.
    /// The input is left unchanged; the expanded CRO is returned separately.
    /// </summary>
    public static bool TryExpandCodeSegment(
        byte[] cro,
        int bytesToAdd,
        out byte[] expanded,
        out CroCodeExpansionReport report,
        out string error)
    {
        expanded = null;
        report = null;
        error = string.Empty;

        if (cro is null)
        {
            error = "CRO data is null.";
            return false;
        }

        if (bytesToAdd <= 0)
        {
            error = "Code expansion size must be positive.";
            return false;
        }

        if ((bytesToAdd & (PageSize - 1)) != 0)
        {
            error = $"Code expansion size must be a multiple of 0x{PageSize:X}.";
            return false;
        }

        if (!CroRelocationMap.TryCreate(cro, out var oldMap, out error))
            return false;

        if (oldMap.Segments.Count < 3 ||
            !oldMap.Segments[0].FileBacked ||
            !oldMap.Segments[1].FileBacked ||
            !oldMap.Segments[2].FileBacked)
        {
            error = "CRO does not expose the expected file-backed code/rodata/data segments.";
            return false;
        }

        if (ReadU32(cro, 0x90) != (uint)cro.Length)
        {
            error =
                $"CRO header file size 0x{ReadU32(cro, 0x90):X} does not match actual size 0x{cro.Length:X}.";
            return false;
        }

        uint oldCodeEnd = oldMap.CodeEnd;
        uint nextSegmentStart = uint.MaxValue;

        foreach (var segment in oldMap.Segments)
        {
            if (!segment.FileBacked || segment.Start <= oldCodeEnd)
                continue;

            if (segment.Start < nextSegmentStart)
                nextSegmentStart = segment.Start;
        }

        if (nextSegmentStart == uint.MaxValue)
        {
            error = "No file-backed segment follows .text.";
            return false;
        }

        if (nextSegmentStart > (uint)cro.Length)
        {
            error = "The next file-backed segment starts past the end of the CRO.";
            return false;
        }

        // Preserve page alignment of the following segment. This is why physical insertion
        // happens at its current start rather than at oldCodeEnd.
        if ((nextSegmentStart & (PageSize - 1)) != 0)
        {
            error =
                $"The segment following .text starts at 0x{nextSegmentStart:X}, which is not page-aligned.";
            return false;
        }

        uint inlineCodeStart = ReadU32(cro, 0xB0);
        uint inlineCodeSize = ReadU32(cro, 0xB4);
        uint inlineDataStart = ReadU32(cro, 0xB8);
        uint inlineDataSize = ReadU32(cro, 0xBC);

        if (inlineCodeStart != oldMap.CodeStart)
        {
            error =
                $"Inline code start 0x{inlineCodeStart:X} disagrees with segment-table code start 0x{oldMap.CodeStart:X}.";
            return false;
        }

        if (!TryAdd(inlineCodeStart, inlineCodeSize, out uint inlineCodeEnd) ||
            inlineCodeEnd < nextSegmentStart ||
            inlineCodeEnd > (uint)cro.Length)
        {
            error = "Inline code/hash region does not contain the requested insertion point.";
            return false;
        }

        if (inlineDataStart < inlineCodeEnd ||
            !RangeFits(cro.Length, inlineDataStart, inlineDataSize))
        {
            error = "Inline data/hash region is invalid.";
            return false;
        }

        int newLength;
        try
        {
            newLength = checked(cro.Length + bytesToAdd);
        }
        catch (OverflowException)
        {
            error = "Expanded CRO would exceed the maximum array size.";
            return false;
        }

        int insertion = checked((int)nextSegmentStart);
        expanded = new byte[newLength];

        Array.Copy(cro, 0, expanded, 0, insertion);
        Array.Clear(expanded, insertion, bytesToAdd);
        Array.Copy(
            cro,
            insertion,
            expanded,
            insertion + bytesToAdd,
            cro.Length - insertion);

        // Header fields whose meaning is known and independently validated on Battle.cro.
        WriteU32(expanded, checked((uint)newLength), 0x90);
        WriteU32(expanded, checked(inlineCodeSize + (uint)bytesToAdd), 0xB4);
        WriteU32(expanded, ShiftIfAtOrAfter(inlineDataStart, nextSegmentStart, bytesToAdd), 0xB8);

        int shiftedHeaderPointers = 0;

        shiftedHeaderPointers += ShiftHeaderPointer(
            expanded,
            0x84,
            nextSegmentStart,
            bytesToAdd);

        // The first dword of every offset/count pair from 0xC0 through 0x130 is a
        // file offset on the audited USUM Battle.cro.
        for (int pointer = 0xC0; pointer <= 0x130; pointer += 8)
        {
            shiftedHeaderPointers += ShiftHeaderPointer(
                expanded,
                pointer,
                nextSegmentStart,
                bytesToAdd);
        }

        uint newSegmentTableOffset = ReadU32(expanded, 0xC8);
        uint newPatchTableOffset = ReadU32(expanded, 0x128);

        if (newSegmentTableOffset != oldMap.SegmentTableOffset + (uint)bytesToAdd)
        {
            error = "Segment table pointer did not shift by the expected amount.";
            expanded = null;
            return false;
        }

        if (newPatchTableOffset != oldMap.PatchTableOffset + (uint)bytesToAdd)
        {
            error = "Relocation table pointer did not shift by the expected amount.";
            expanded = null;
            return false;
        }

        // Rewrite the moved segment table from the already-validated old map.
        for (int i = 0; i < oldMap.Segments.Count; i++)
        {
            var segment = oldMap.Segments[i];
            int entry = checked((int)(newSegmentTableOffset + ((uint)i * 12u)));

            if (entry < 0 || entry + 12 > expanded.Length)
            {
                error = $"Expanded segment table entry #{i} is outside the file.";
                expanded = null;
                return false;
            }

            uint newStart = segment.Start;
            uint newSize = segment.Size;

            if (i == 0)
            {
                newSize = checked(segment.Size + (uint)bytesToAdd);
            }
            else if (segment.FileBacked && segment.Start >= nextSegmentStart)
            {
                newStart = checked(segment.Start + (uint)bytesToAdd);
            }

            WriteU32(expanded, newStart, entry);
            WriteU32(expanded, newSize, entry + 4);
            WriteU32(expanded, segment.Id, entry + 8);
        }

        // Known CRO tables that contain absolute file offsets.
        int shiftedInternalPointers = 0;

        if (!TryShiftPointerTable(
                expanded,
                headerPointerOffset: 0xD0,
                entrySize: 0x08,
                fieldOffsets: [0x00],
                nextSegmentStart,
                bytesToAdd,
                ref shiftedInternalPointers,
                out error) ||
            !TryShiftPointerTable(
                expanded,
                headerPointerOffset: 0xF0,
                entrySize: 0x14,
                fieldOffsets: [0x00, 0x04, 0x0C],
                nextSegmentStart,
                bytesToAdd,
                ref shiftedInternalPointers,
                out error) ||
            !TryShiftPointerTable(
                expanded,
                headerPointerOffset: 0x100,
                entrySize: 0x08,
                fieldOffsets: [0x00, 0x04],
                nextSegmentStart,
                bytesToAdd,
                ref shiftedInternalPointers,
                out error) ||
            !TryShiftPointerTable(
                expanded,
                headerPointerOffset: 0x110,
                entrySize: 0x08,
                fieldOffsets: [0x04],
                nextSegmentStart,
                bytesToAdd,
                ref shiftedInternalPointers,
                out error))
        {
            expanded = null;
            return false;
        }

        // Because insertion happens at the next segment start:
        // - segment 0 start is unchanged;
        // - later segment starts move with their bytes;
        // - all existing relocation offsets/addends therefore remain segment-relative.
        // Verify that the relocation table body really stayed byte-identical.
        long relocationBytes = (long)oldMap.PatchTableCount * 12L;
        if (relocationBytes > int.MaxValue ||
            oldMap.PatchTableOffset + relocationBytes > cro.Length ||
            newPatchTableOffset + relocationBytes > expanded.Length)
        {
            error = "Relocation table bounds are invalid after expansion.";
            expanded = null;
            return false;
        }

        if (!cro.AsSpan(
                checked((int)oldMap.PatchTableOffset),
                checked((int)relocationBytes))
            .SequenceEqual(
                expanded.AsSpan(
                    checked((int)newPatchTableOffset),
                    checked((int)relocationBytes))))
        {
            error =
                "Relocation table contents changed unexpectedly while shifting the CRO.";
            expanded = null;
            return false;
        }

        if (!TryUpdateHashes(expanded, out error))
        {
            expanded = null;
            return false;
        }

        if (!CroRelocationMap.TryCreate(expanded, out var newMap, out error))
        {
            expanded = null;
            return false;
        }

        uint expectedNewCodeEnd = checked(oldCodeEnd + (uint)bytesToAdd);
        uint expectedNextStart = checked(nextSegmentStart + (uint)bytesToAdd);

        if (newMap.CodeStart != oldMap.CodeStart ||
            newMap.CodeSize != oldMap.CodeSize + (uint)bytesToAdd ||
            newMap.CodeEnd != expectedNewCodeEnd)
        {
            error = "Expanded segment 0 does not match the requested growth.";
            expanded = null;
            return false;
        }

        uint actualNextStart = uint.MaxValue;
        foreach (var segment in newMap.Segments)
        {
            if (!segment.FileBacked || segment.Start <= newMap.CodeEnd)
                continue;

            if (segment.Start < actualNextStart)
                actualNextStart = segment.Start;
        }

        if (actualNextStart != expectedNextStart)
        {
            error =
                $"Following segment moved to 0x{actualNextStart:X}, expected 0x{expectedNextStart:X}.";
            expanded = null;
            return false;
        }

        var expandedRange = new CroFreeRange(oldCodeEnd, bytesToAdd);
        var insertedRange = new CroFreeRange(nextSegmentStart, bytesToAdd);

        report = new CroCodeExpansionReport(
            BytesAdded: bytesToAdd,
            InsertionOffset: nextSegmentStart,
            OldFileSize: cro.Length,
            NewFileSize: expanded.Length,
            OldCodeEnd: oldCodeEnd,
            NewCodeEnd: newMap.CodeEnd,
            OldNextSegmentStart: nextSegmentStart,
            NewNextSegmentStart: actualNextStart,
            OldSegmentTableOffset: oldMap.SegmentTableOffset,
            NewSegmentTableOffset: newMap.SegmentTableOffset,
            OldPatchTableOffset: oldMap.PatchTableOffset,
            NewPatchTableOffset: newMap.PatchTableOffset,
            ShiftedHeaderPointers: shiftedHeaderPointers,
            ShiftedInternalPointers: shiftedInternalPointers,
            RelocationReferencesInExpandedRange:
                newMap.CountReferencesInRange(expandedRange.Offset, expandedRange.Length),
            ExpandedCodeRange: expandedRange,
            InsertedFileRange: insertedRange);

        return true;
    }

    private static bool TryShiftPointerTable(
        byte[] data,
        int headerPointerOffset,
        int entrySize,
        int[] fieldOffsets,
        uint insertionOffset,
        int delta,
        ref int shiftedCount,
        out string error)
    {
        error = string.Empty;

        uint tableOffset = ReadU32(data, headerPointerOffset);
        uint count = ReadU32(data, headerPointerOffset + 4);

        if (tableOffset == 0 || count == 0)
            return true;

        long tableBytes = (long)count * entrySize;
        if (tableOffset > data.Length ||
            tableBytes < 0 ||
            tableOffset + tableBytes > data.Length)
        {
            error =
                $"Pointer table at header 0x{headerPointerOffset:X} lies outside the expanded CRO.";
            return false;
        }

        for (uint i = 0; i < count; i++)
        {
            long entry = tableOffset + ((long)i * entrySize);

            foreach (int fieldOffset in fieldOffsets)
            {
                long location = entry + fieldOffset;
                if (location < 0 || location + 4 > data.Length)
                {
                    error =
                        $"Pointer-table field 0x{location:X} lies outside the expanded CRO.";
                    return false;
                }

                uint value = ReadU32(data, checked((int)location));
                if (value == 0 || value < insertionOffset)
                    continue;

                if (!TryAdd(value, (uint)delta, out uint shifted))
                {
                    error =
                        $"Pointer 0x{value:X} overflows while shifting by 0x{delta:X}.";
                    return false;
                }

                WriteU32(data, shifted, checked((int)location));
                shiftedCount++;
            }
        }

        return true;
    }

    private static int ShiftHeaderPointer(
        byte[] data,
        int pointerLocation,
        uint insertionOffset,
        int delta)
    {
        uint value = ReadU32(data, pointerLocation);
        if (value == 0 || value < insertionOffset)
            return 0;

        WriteU32(
            data,
            checked(value + (uint)delta),
            pointerLocation);

        return 1;
    }

    private static uint ShiftIfAtOrAfter(
        uint value,
        uint insertionOffset,
        int delta) =>
        value >= insertionOffset
            ? checked(value + (uint)delta)
            : value;

    private static bool TryUpdateHashes(
        byte[] data,
        out string error)
    {
        error = string.Empty;

        uint codeStart = ReadU32(data, 0xB0);
        uint codeSize = ReadU32(data, 0xB4);
        uint dataStart = ReadU32(data, 0xB8);
        uint dataSize = ReadU32(data, 0xBC);

        if (codeStart < 0x80 ||
            !RangeFits(data.Length, codeStart, codeSize) ||
            !RangeFits(data.Length, dataStart, dataSize))
        {
            error = "Inline CRO hash boundaries are outside the expanded file.";
            return false;
        }

        if (!TryAdd(codeStart, codeSize, out uint codeEnd) ||
            dataStart < codeEnd)
        {
            error = "Inline CRO code/data hash boundaries overlap or overflow.";
            return false;
        }

        uint headerSize = codeStart - 0x80;
        uint middleSize = dataStart - codeEnd;

        byte[] headerHash = SHA256.HashData(
            data.AsSpan(0x80, checked((int)headerSize)));

        byte[] codeHash = SHA256.HashData(
            data.AsSpan(
                checked((int)codeStart),
                checked((int)codeSize)));

        byte[] middleHash = SHA256.HashData(
            data.AsSpan(
                checked((int)codeEnd),
                checked((int)middleSize)));

        byte[] dataHash = SHA256.HashData(
            data.AsSpan(
                checked((int)dataStart),
                checked((int)dataSize)));

        headerHash.CopyTo(data, 0x00);
        codeHash.CopyTo(data, 0x20);
        middleHash.CopyTo(data, 0x40);
        dataHash.CopyTo(data, 0x60);

        return true;
    }

    private static uint ReadU32(
        byte[] data,
        int offset) =>
        BitConverter.ToUInt32(data, offset);

    private static void WriteU32(
        byte[] data,
        uint value,
        int offset) =>
        BitConverter.GetBytes(value).CopyTo(data, offset);

    private static bool RangeFits(
        int fileLength,
        uint start,
        uint length) =>
        start <= (uint)fileLength &&
        length <= (uint)fileLength - start;

    private static bool TryAdd(
        uint left,
        uint right,
        out uint value)
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
