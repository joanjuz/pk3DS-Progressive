using System;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// Describes one loader pointer appended to a CRO relocation table.
/// </summary>
public sealed record CroRelocationWriteReport(
    int RelocationIndex,
    uint RecordOffset,
    byte PatchType,
    uint OldPatchCount,
    uint NewPatchCount,
    int WriteSegment,
    uint WriteRelative,
    uint OriginalWriteAddress,
    uint FinalWriteAddress,
    int TargetSegment,
    uint TargetRelative,
    uint OriginalTargetAddress,
    uint FinalTargetAddress,
    CroRelocationHeadroomReport Headroom);

/// <summary>
/// Appends relocation type 0x02 pointer records without overwriting the data segment.
/// <para>
/// Addresses supplied by callers are absolute file offsets in the input CRO. They are first reduced
/// to segment-relative coordinates. If creating relocation headroom moves .data, the final absolute
/// addresses are reconstructed from those same relative coordinates against the new segment bases.
/// </para>
/// <para>
/// Phase 1 deliberately supports file-backed segments 0-2 only. BSS targets need an explicit
/// segment-relative API because BSS has no file offset.
/// </para>
/// </summary>
public static class CroRelocationWriter
{
    public const byte PointerPatchType = 0x02;

    private const int RelocationEntrySize = 12;

    /// <summary>
    /// Adds one loader-written pointer relocation:
    /// <paramref name="writeAddress"/> is the 4-byte file slot the loader will fill and
    /// <paramref name="targetAddress"/> is the file-backed address it should point to.
    /// The caller's CRO buffer is never mutated.
    /// </summary>
    public static bool TryAddPointer(
        byte[] cro,
        uint writeAddress,
        uint targetAddress,
        out byte[] updated,
        out CroRelocationWriteReport report,
        out string error)
    {
        updated = null;
        report = null;
        error = string.Empty;

        if (cro is null)
        {
            error = "CRO data is null.";
            return false;
        }

        if ((writeAddress & 3u) != 0)
        {
            error =
                $"relocation write slot 0x{writeAddress:X} is not 4-byte aligned.";
            return false;
        }

        if (!CroRelocationMap.TryCreate(
                cro,
                out var oldMap,
                out error))
        {
            return false;
        }

        if (!TryLocateFileBackedAddress(
                oldMap,
                writeAddress,
                requiredBytes: 4,
                out int writeSegment,
                out uint writeRelative))
        {
            error =
                $"write slot 0x{writeAddress:X} is not fully inside a declared file-backed CRO segment.";
            return false;
        }

        if (!TryLocateFileBackedAddress(
                oldMap,
                targetAddress,
                requiredBytes: 1,
                out int targetSegment,
                out uint targetRelative))
        {
            error =
                $"target 0x{targetAddress:X} is not inside a declared file-backed CRO segment.";
            return false;
        }

        if (targetAddress >= writeAddress &&
            targetAddress < writeAddress + 4u)
        {
            error =
                "target address lies inside the loader write slot.";
            return false;
        }

        if (!WordIsBlank(
                cro,
                writeAddress))
        {
            error =
                $"relocation write slot 0x{writeAddress:X} is not blank.";
            return false;
        }

        if (oldMap.CountReferencesInRange(
                writeAddress,
                4) != 0)
        {
            error =
                $"relocation write slot 0x{writeAddress:X} already overlaps a CRO relocation reference.";
            return false;
        }

        if (writeRelative > 0x0FFFFFFFu)
        {
            error =
                $"write-relative offset 0x{writeRelative:X} cannot fit the CRO relocation encoding.";
            return false;
        }

        if (!CroRelocationHeadroom.TryEnsure(
                cro,
                1,
                out byte[] working,
                out CroRelocationHeadroomReport headroom,
                out error))
        {
            return false;
        }

        if (!CroRelocationMap.TryCreate(
                working,
                out var preparedMap,
                out error))
        {
            return false;
        }

        if (preparedMap.PatchTableOffset != oldMap.PatchTableOffset ||
            preparedMap.PatchTableCount != oldMap.PatchTableCount)
        {
            error =
                "relocation table metadata changed before the new record was appended.";
            return false;
        }

        if (!TryResolvePreparedAddress(
                preparedMap,
                writeSegment,
                writeRelative,
                requiredBytes: 4,
                out uint finalWriteAddress))
        {
            error =
                "write slot no longer resolves inside its declared segment after headroom preparation.";
            return false;
        }

        if (!TryResolvePreparedAddress(
                preparedMap,
                targetSegment,
                targetRelative,
                requiredBytes: 1,
                out uint finalTargetAddress))
        {
            error =
                "target no longer resolves inside its declared segment after headroom preparation.";
            return false;
        }

        if (!WordIsBlank(
                working,
                finalWriteAddress))
        {
            error =
                $"prepared relocation write slot 0x{finalWriteAddress:X} is not blank.";
            return false;
        }

        if (preparedMap.CountReferencesInRange(
                finalWriteAddress,
                4) != 0)
        {
            error =
                $"prepared relocation write slot 0x{finalWriteAddress:X} overlaps an existing relocation reference.";
            return false;
        }

        uint oldCount =
            preparedMap.PatchTableCount;

        ulong recordOffset64 =
            (ulong)preparedMap.PatchTableOffset +
            ((ulong)oldCount * RelocationEntrySize);

        if (recordOffset64 > uint.MaxValue)
        {
            error =
                "new relocation record offset overflows 32-bit file addressing.";
            return false;
        }

        uint recordOffset =
            (uint)recordOffset64;

        ulong recordEnd =
            recordOffset64 +
            RelocationEntrySize;

        if (recordEnd > preparedMap.DataStart ||
            recordEnd > (ulong)working.Length)
        {
            error =
                "prepared CRO does not have a complete 12-byte relocation slot before .data.";
            return false;
        }

        if (!RangeIsBlank(
                working,
                recordOffset,
                RelocationEntrySize))
        {
            error =
                $"new relocation record area 0x{recordOffset:X}-0x{recordOffset + RelocationEntrySize:X} is not blank.";
            return false;
        }

        uint word0 =
            (writeRelative << 4) |
            (uint)writeSegment;

        uint word1 =
            PointerPatchType |
            ((uint)targetSegment << 8);

        uint word2 =
            targetRelative;

        WriteU32(
            working,
            word0,
            checked((int)recordOffset));

        WriteU32(
            working,
            word1,
            checked((int)recordOffset + 4));

        WriteU32(
            working,
            word2,
            checked((int)recordOffset + 8));

        // Loader-owned pointer slots are stored blank on disk.
        Array.Clear(
            working,
            checked((int)finalWriteAddress),
            4);

        uint newCount =
            checked(oldCount + 1u);

        WriteU32(
            working,
            newCount,
            0x12C);

        if (!CroSegmentExpander.TryUpdateHashes(
                working,
                out error))
        {
            return false;
        }

        if (!CroRelocationMap.TryCreate(
                working,
                out var finalMap,
                out error))
        {
            return false;
        }

        if (finalMap.PatchTableOffset != oldMap.PatchTableOffset ||
            finalMap.PatchTableCount != newCount)
        {
            error =
                $"final relocation table metadata is invalid: offset 0x{finalMap.PatchTableOffset:X}, count {finalMap.PatchTableCount}.";
            return false;
        }

        if (finalMap.References.Count != newCount)
        {
            error =
                $"final relocation parser returned {finalMap.References.Count} references, expected {newCount}.";
            return false;
        }

        var appended =
            finalMap.References[
                checked((int)oldCount)];

        if (!appended.WriteFileBacked ||
            appended.WriteAddress != finalWriteAddress ||
            !appended.TargetFileBacked ||
            appended.TargetAddress != finalTargetAddress)
        {
            error =
                "the appended relocation does not resolve to the requested write slot and target.";
            return false;
        }

        ulong oldRelocationBytes =
            (ulong)oldMap.PatchTableCount *
            RelocationEntrySize;

        if (oldRelocationBytes > int.MaxValue)
        {
            error =
                "existing relocation table is too large to compare safely.";
            return false;
        }

        if (!cro.AsSpan(
                checked((int)oldMap.PatchTableOffset),
                checked((int)oldRelocationBytes))
            .SequenceEqual(
                working.AsSpan(
                    checked((int)finalMap.PatchTableOffset),
                    checked((int)oldRelocationBytes))))
        {
            error =
                "an existing relocation record changed while appending the new pointer.";
            return false;
        }

        updated = working;

        report = new CroRelocationWriteReport(
            RelocationIndex: checked((int)oldCount),
            RecordOffset: recordOffset,
            PatchType: PointerPatchType,
            OldPatchCount: oldCount,
            NewPatchCount: newCount,
            WriteSegment: writeSegment,
            WriteRelative: writeRelative,
            OriginalWriteAddress: writeAddress,
            FinalWriteAddress: finalWriteAddress,
            TargetSegment: targetSegment,
            TargetRelative: targetRelative,
            OriginalTargetAddress: targetAddress,
            FinalTargetAddress: finalTargetAddress,
            Headroom: headroom);

        return true;
    }

    private static bool TryLocateFileBackedAddress(
        CroRelocationMap map,
        uint address,
        int requiredBytes,
        out int segmentIndex,
        out uint relative)
    {
        segmentIndex = -1;
        relative = 0;

        if (requiredBytes <= 0)
            return false;

        for (int i = 0;
             i < 3 &&
             i < map.Segments.Count;
             i++)
        {
            var segment =
                map.Segments[i];

            if (!segment.FileBacked)
                continue;

            ulong start =
                segment.Start;

            ulong end =
                start +
                segment.Size;

            ulong requestedEnd =
                (ulong)address +
                (uint)requiredBytes;

            if ((ulong)address < start ||
                requestedEnd > end)
            {
                continue;
            }

            segmentIndex = i;
            relative =
                address -
                segment.Start;

            return true;
        }

        return false;
    }

    private static bool TryResolvePreparedAddress(
        CroRelocationMap map,
        int segmentIndex,
        uint relative,
        int requiredBytes,
        out uint address)
    {
        address = 0;

        if (requiredBytes <= 0 ||
            segmentIndex < 0 ||
            segmentIndex >= 3 ||
            segmentIndex >= map.Segments.Count)
        {
            return false;
        }

        var segment =
            map.Segments[segmentIndex];

        if (!segment.FileBacked ||
            relative > segment.Size)
        {
            return false;
        }

        ulong endRelative =
            (ulong)relative +
            (uint)requiredBytes;

        if (endRelative > segment.Size)
            return false;

        ulong absolute =
            (ulong)segment.Start +
            relative;

        if (absolute > uint.MaxValue ||
            absolute + (uint)requiredBytes > (ulong)int.MaxValue + 1u)
        {
            return false;
        }

        address =
            (uint)absolute;

        return true;
    }

    private static bool WordIsBlank(
        byte[] data,
        uint address) =>
        RangeIsBlank(
            data,
            address,
            4);

    private static bool RangeIsBlank(
        byte[] data,
        uint address,
        int length)
    {
        if (length <= 0 ||
            address > (uint)data.Length ||
            (ulong)address + (uint)length > (ulong)data.Length)
        {
            return false;
        }

        int start =
            checked((int)address);

        for (int i = 0;
             i < length;
             i++)
        {
            byte value =
                data[start + i];

            if (value != 0x00 &&
                value != 0xCC)
            {
                return false;
            }
        }

        return true;
    }

    private static void WriteU32(
        byte[] data,
        uint value,
        int offset) =>
        BitConverter.GetBytes(value)
            .CopyTo(
                data,
                offset);
}
