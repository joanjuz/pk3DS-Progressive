using System;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// Describes one conservative enlargement of the file area between the CRO relocation table
/// and the file-backed data segment.
/// </summary>
public sealed record CroRelocationHeadroomReport(
    int RequestedExtraRelocations,
    int RequiredBytes,
    int ExistingHeadroomBytes,
    int BytesInserted,
    int NewHeadroomBytes,
    int OldFileSize,
    int NewFileSize,
    uint PatchTableOffset,
    uint PatchTableCount,
    uint PatchTableEnd,
    uint OldDataStart,
    uint NewDataStart,
    int ShiftedHeaderPointers,
    int ShiftedInternalPointers,
    CroFreeRange InsertedHeadroomRange)
{
    public bool Changed => BytesInserted > 0;
}

/// <summary>
/// Creates physical room for additional CRO relocation records without changing the existing
/// relocation records themselves.
/// <para>
/// USUM Battle.cro stores the relocation table immediately before segment 2 (.data), with no
/// spare bytes between them. Relocation records are 12 bytes each. Growing that table in place
/// would therefore overwrite .data.
/// </para>
/// <para>
/// This helper inserts whole pages immediately before .data. Segment 2 moves with its bytes while
/// its size stays unchanged. Existing relocations remain byte-identical because their write offsets
/// and target addends are segment-relative; moving segment 2's base automatically moves every
/// existing segment-2 write/target with it.
/// </para>
/// </summary>
public static class CroRelocationHeadroom
{
    private const int PageSize = 0x1000;
    private const int RelocationEntrySize = 12;

    /// <summary>
    /// Ensures enough contiguous file space exists after the current relocation table for
    /// <paramref name="extraRelocations"/> additional 12-byte records.
    /// The caller's buffer is never mutated.
    /// </summary>
    public static bool TryEnsure(
        byte[] cro,
        int extraRelocations,
        out byte[] prepared,
        out CroRelocationHeadroomReport report,
        out string error)
    {
        prepared = null;
        report = null;
        error = string.Empty;

        if (cro is null)
        {
            error = "CRO data is null.";
            return false;
        }

        if (extraRelocations <= 0)
        {
            error = "extra relocation count must be positive.";
            return false;
        }

        if (!CroRelocationMap.TryCreate(
                cro,
                out var oldMap,
                out error))
        {
            return false;
        }

        if (oldMap.Segments.Count < 3 ||
            !oldMap.Segments[2].FileBacked)
        {
            error = "CRO does not expose a file-backed data segment.";
            return false;
        }

        if (ReadU32(cro, 0x90) != (uint)cro.Length)
        {
            error =
                $"CRO header file size 0x{ReadU32(cro, 0x90):X} does not match actual size 0x{cro.Length:X}.";
            return false;
        }

        uint oldDataStart = oldMap.DataStart;
        uint inlineDataStart = ReadU32(cro, 0xB8);

        if (inlineDataStart != oldDataStart)
        {
            error =
                $"Inline data start 0x{inlineDataStart:X} disagrees with segment-table data start 0x{oldDataStart:X}.";
            return false;
        }

        ulong patchBytes =
            (ulong)oldMap.PatchTableCount *
            RelocationEntrySize;

        ulong patchEnd64 =
            (ulong)oldMap.PatchTableOffset +
            patchBytes;

        if (patchEnd64 > oldDataStart)
        {
            error =
                $"CRO relocation table ends at 0x{patchEnd64:X}, past .data start 0x{oldDataStart:X}.";
            return false;
        }

        uint patchEnd = (uint)patchEnd64;

        int requiredBytes;
        try
        {
            requiredBytes =
                checked(extraRelocations * RelocationEntrySize);
        }
        catch (OverflowException)
        {
            error = "requested relocation headroom is too large.";
            return false;
        }

        int existingHeadroom =
            checked((int)(oldDataStart - patchEnd));

        if (existingHeadroom >= requiredBytes)
        {
            prepared = (byte[])cro.Clone();

            report = new CroRelocationHeadroomReport(
                RequestedExtraRelocations: extraRelocations,
                RequiredBytes: requiredBytes,
                ExistingHeadroomBytes: existingHeadroom,
                BytesInserted: 0,
                NewHeadroomBytes: existingHeadroom,
                OldFileSize: cro.Length,
                NewFileSize: cro.Length,
                PatchTableOffset: oldMap.PatchTableOffset,
                PatchTableCount: oldMap.PatchTableCount,
                PatchTableEnd: patchEnd,
                OldDataStart: oldDataStart,
                NewDataStart: oldDataStart,
                ShiftedHeaderPointers: 0,
                ShiftedInternalPointers: 0,
                InsertedHeadroomRange: default);

            return true;
        }

        int missing =
            requiredBytes - existingHeadroom;

        int bytesToInsert;
        try
        {
            bytesToInsert =
                checked(
                    (missing + (PageSize - 1)) &
                    ~(PageSize - 1));
        }
        catch (OverflowException)
        {
            error = "relocation headroom growth overflowed.";
            return false;
        }

        if (bytesToInsert <= 0)
        {
            error = "calculated relocation headroom growth is invalid.";
            return false;
        }

        int newLength;
        try
        {
            newLength =
                checked(cro.Length + bytesToInsert);
        }
        catch (OverflowException)
        {
            error = "expanded CRO would exceed the maximum array size.";
            return false;
        }

        int insertion =
            checked((int)oldDataStart);

        prepared = new byte[newLength];

        Array.Copy(
            cro,
            0,
            prepared,
            0,
            insertion);

        Array.Clear(
            prepared,
            insertion,
            bytesToInsert);

        Array.Copy(
            cro,
            insertion,
            prepared,
            insertion + bytesToInsert,
            cro.Length - insertion);

        uint newDataStart =
            checked(oldDataStart + (uint)bytesToInsert);

        // File size and the inline data/hash boundary are the two direct header changes.
        WriteU32(
            prepared,
            checked((uint)newLength),
            0x90);

        WriteU32(
            prepared,
            newDataStart,
            0xB8);

        int shiftedHeaderPointers = 0;

        shiftedHeaderPointers += ShiftHeaderPointer(
            prepared,
            0x84,
            oldDataStart,
            bytesToInsert);

        // The first dword of these offset/count pairs is a file offset on the audited Battle.cro.
        // 0x128 is deliberately excluded: it is the relocation table being given headroom and must
        // stay at its current file offset even if an empty table happens to start exactly at .data.
        for (int pointer = 0xC0;
             pointer <= 0x130;
             pointer += 8)
        {
            if (pointer == 0x128)
                continue;

            shiftedHeaderPointers += ShiftHeaderPointer(
                prepared,
                pointer,
                oldDataStart,
                bytesToInsert);
        }

        // Rewrite the segment table from the validated old map. Only file-backed segments at or
        // after the insertion point move. Segment 2 is the expected mover for Battle.cro.
        uint segmentTableOffset =
            ReadU32(prepared, 0xC8);

        if (segmentTableOffset != oldMap.SegmentTableOffset)
        {
            error =
                "segment table pointer moved unexpectedly while inserting relocation headroom.";
            prepared = null;
            return false;
        }

        for (int i = 0;
             i < oldMap.Segments.Count;
             i++)
        {
            var segment =
                oldMap.Segments[i];

            int entry =
                checked(
                    (int)(
                        segmentTableOffset +
                        ((uint)i * 12u)));

            if (entry < 0 ||
                entry + 12 > prepared.Length)
            {
                error =
                    $"segment table entry #{i} is outside the prepared CRO.";
                prepared = null;
                return false;
            }

            uint newStart =
                segment.Start;

            if (segment.FileBacked &&
                segment.Start >= oldDataStart)
            {
                newStart =
                    checked(
                        segment.Start +
                        (uint)bytesToInsert);
            }

            WriteU32(
                prepared,
                newStart,
                entry);

            WriteU32(
                prepared,
                segment.Size,
                entry + 4);

            WriteU32(
                prepared,
                segment.Id,
                entry + 8);
        }

        int shiftedInternalPointers = 0;

        if (!TryShiftPointerTable(
                prepared,
                headerPointerOffset: 0xD0,
                entrySize: 0x08,
                fieldOffsets: [0x00],
                oldDataStart,
                bytesToInsert,
                ref shiftedInternalPointers,
                out error) ||
            !TryShiftPointerTable(
                prepared,
                headerPointerOffset: 0xF0,
                entrySize: 0x14,
                fieldOffsets: [0x00, 0x04, 0x0C],
                oldDataStart,
                bytesToInsert,
                ref shiftedInternalPointers,
                out error) ||
            !TryShiftPointerTable(
                prepared,
                headerPointerOffset: 0x100,
                entrySize: 0x08,
                fieldOffsets: [0x00, 0x04],
                oldDataStart,
                bytesToInsert,
                ref shiftedInternalPointers,
                out error) ||
            !TryShiftPointerTable(
                prepared,
                headerPointerOffset: 0x110,
                entrySize: 0x08,
                fieldOffsets: [0x04],
                oldDataStart,
                bytesToInsert,
                ref shiftedInternalPointers,
                out error))
        {
            prepared = null;
            return false;
        }

        // The relocation table itself remains at the same offset and every existing record should
        // stay byte-identical. Their absolute segment-2 addresses move only because segment 2 moved.
        if (ReadU32(prepared, 0x128) != oldMap.PatchTableOffset ||
            ReadU32(prepared, 0x12C) != oldMap.PatchTableCount)
        {
            error =
                "relocation table metadata changed unexpectedly.";
            prepared = null;
            return false;
        }

        if (patchBytes > int.MaxValue)
        {
            error =
                "relocation table is too large to compare safely.";
            prepared = null;
            return false;
        }

        if (!cro.AsSpan(
                checked((int)oldMap.PatchTableOffset),
                checked((int)patchBytes))
            .SequenceEqual(
                prepared.AsSpan(
                    checked((int)oldMap.PatchTableOffset),
                    checked((int)patchBytes))))
        {
            error =
                "existing relocation records changed unexpectedly while creating headroom.";
            prepared = null;
            return false;
        }

        if (!CroSegmentExpander.TryUpdateHashes(
                prepared,
                out error))
        {
            prepared = null;
            return false;
        }

        if (!CroRelocationMap.TryCreate(
                prepared,
                out var newMap,
                out error))
        {
            prepared = null;
            return false;
        }

        if (newMap.CodeStart != oldMap.CodeStart ||
            newMap.CodeSize != oldMap.CodeSize ||
            newMap.RodataStart != oldMap.RodataStart ||
            newMap.RodataSize != oldMap.RodataSize)
        {
            error =
                "code or rodata segment changed while only .data was supposed to move.";
            prepared = null;
            return false;
        }

        if (newMap.DataStart != newDataStart ||
            newMap.DataSize != oldMap.DataSize)
        {
            error =
                "prepared data segment does not match the requested relocation-headroom move.";
            prepared = null;
            return false;
        }

        if (newMap.PatchTableOffset != oldMap.PatchTableOffset ||
            newMap.PatchTableCount != oldMap.PatchTableCount)
        {
            error =
                "prepared relocation table no longer matches the original table.";
            prepared = null;
            return false;
        }

        int newHeadroom =
            checked((int)(newMap.DataStart - patchEnd));

        if (newHeadroom < requiredBytes)
        {
            error =
                $"prepared CRO has only 0x{newHeadroom:X} bytes of relocation headroom, need 0x{requiredBytes:X}.";
            prepared = null;
            return false;
        }

        report = new CroRelocationHeadroomReport(
            RequestedExtraRelocations: extraRelocations,
            RequiredBytes: requiredBytes,
            ExistingHeadroomBytes: existingHeadroom,
            BytesInserted: bytesToInsert,
            NewHeadroomBytes: newHeadroom,
            OldFileSize: cro.Length,
            NewFileSize: prepared.Length,
            PatchTableOffset: oldMap.PatchTableOffset,
            PatchTableCount: oldMap.PatchTableCount,
            PatchTableEnd: patchEnd,
            OldDataStart: oldDataStart,
            NewDataStart: newMap.DataStart,
            ShiftedHeaderPointers: shiftedHeaderPointers,
            ShiftedInternalPointers: shiftedInternalPointers,
            InsertedHeadroomRange:
                new CroFreeRange(
                    oldDataStart,
                    bytesToInsert));

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

        uint tableOffset =
            ReadU32(
                data,
                headerPointerOffset);

        uint count =
            ReadU32(
                data,
                headerPointerOffset + 4);

        if (tableOffset == 0 ||
            count == 0)
        {
            return true;
        }

        long tableBytes =
            (long)count *
            entrySize;

        if (tableOffset > data.Length ||
            tableBytes < 0 ||
            tableOffset + tableBytes > data.Length)
        {
            error =
                $"pointer table at header 0x{headerPointerOffset:X} lies outside the prepared CRO.";
            return false;
        }

        for (uint i = 0;
             i < count;
             i++)
        {
            long entry =
                tableOffset +
                ((long)i * entrySize);

            foreach (int fieldOffset in fieldOffsets)
            {
                long location =
                    entry +
                    fieldOffset;

                if (location < 0 ||
                    location + 4 > data.Length)
                {
                    error =
                        $"pointer-table field 0x{location:X} lies outside the prepared CRO.";
                    return false;
                }

                uint value =
                    ReadU32(
                        data,
                        checked((int)location));

                if (value == 0 ||
                    value < insertionOffset)
                {
                    continue;
                }

                if (!TryAdd(
                        value,
                        (uint)delta,
                        out uint shifted))
                {
                    error =
                        $"pointer 0x{value:X} overflows while shifting by 0x{delta:X}.";
                    return false;
                }

                WriteU32(
                    data,
                    shifted,
                    checked((int)location));

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
        uint value =
            ReadU32(
                data,
                pointerLocation);

        if (value == 0 ||
            value < insertionOffset)
        {
            return 0;
        }

        WriteU32(
            data,
            checked(
                value +
                (uint)delta),
            pointerLocation);

        return 1;
    }

    private static uint ReadU32(
        byte[] data,
        int offset) =>
        BitConverter.ToUInt32(
            data,
            offset);

    private static void WriteU32(
        byte[] data,
        uint value,
        int offset) =>
        BitConverter.GetBytes(value)
            .CopyTo(
                data,
                offset);

    private static bool TryAdd(
        uint left,
        uint right,
        out uint value)
    {
        ulong sum =
            (ulong)left +
            right;

        if (sum > uint.MaxValue)
        {
            value = 0;
            return false;
        }

        value = (uint)sum;
        return true;
    }
}
