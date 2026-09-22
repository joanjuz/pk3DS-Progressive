using System;
using System.Collections.Generic;
using System.Linq;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// One requested rewrite of an existing CRO pointer relocation.
/// Addresses are absolute file offsets in the input CRO and are reduced to segment-relative
/// coordinates before any record is changed.
/// </summary>
public sealed record CroRelocationPointerEdit(
    int RelocationIndex,
    uint WriteAddress,
    uint TargetAddress,
    string Purpose = "");

/// <summary>
/// Audit information for one rewritten CRO relocation record.
/// </summary>
public sealed record CroRelocationEditReport(
    int RelocationIndex,
    uint RecordOffset,
    byte PatchType,
    int OldWriteSegment,
    uint OldWriteRelative,
    uint OldWriteAddress,
    int NewWriteSegment,
    uint NewWriteRelative,
    uint NewWriteAddress,
    int OldTargetSegment,
    uint OldTargetRelative,
    uint OldTargetAddress,
    int NewTargetSegment,
    uint NewTargetRelative,
    uint NewTargetAddress,
    string Purpose);

/// <summary>
/// Rewrites existing type-0x02 CRO pointer relocations without changing relocation count, file
/// layout, or any unedited relocation record.
/// <para>
/// This is the missing primitive required to move a master mechanic table: existing handler
/// relocations can move their write slots to the copied table, while inbound table-pointer
/// relocations can retarget the new table base.
/// </para>
/// <para>
/// Phase 1 accepts file-backed segments 0-2 only. BSS needs a segment-relative public API because
/// it has no file address.
/// </para>
/// </summary>
public static class CroRelocationEditor
{
    private const int RelocationEntrySize = 12;

    public static bool TryRewritePointer(
        byte[] cro,
        int relocationIndex,
        uint writeAddress,
        uint targetAddress,
        out byte[] updated,
        out CroRelocationEditReport report,
        out string error)
    {
        updated = null;
        report = null;
        error = string.Empty;

        var edits =
            new[]
            {
                new CroRelocationPointerEdit(
                    relocationIndex,
                    writeAddress,
                    targetAddress),
            };

        if (!TryRewritePointers(
                cro,
                edits,
                out updated,
                out IReadOnlyList<CroRelocationEditReport> reports,
                out error))
        {
            return false;
        }

        report = reports[0];
        return true;
    }

    /// <summary>
    /// Rewrites a batch atomically and refreshes CRO hashes once.
    /// The caller's input buffer is never mutated.
    /// </summary>
    public static bool TryRewritePointers(
        byte[] cro,
        IReadOnlyList<CroRelocationPointerEdit> edits,
        out byte[] updated,
        out IReadOnlyList<CroRelocationEditReport> reports,
        out string error)
    {
        updated = null;
        reports = Array.Empty<CroRelocationEditReport>();
        error = string.Empty;

        if (cro is null)
        {
            error = "CRO data is null.";
            return false;
        }

        if (edits is null ||
            edits.Count == 0)
        {
            error = "at least one relocation edit is required.";
            return false;
        }

        if (!CroRelocationMap.TryCreate(
                cro,
                out var oldMap,
                out error))
        {
            return false;
        }

        var seenIndexes =
            new HashSet<int>();

        var plannedWriteWords =
            new HashSet<uint>();

        var prepared =
            new List<PreparedEdit>(
                edits.Count);

        foreach (var edit in edits)
        {
            if (edit is null)
            {
                error = "relocation edit is null.";
                return false;
            }

            if (!seenIndexes.Add(edit.RelocationIndex))
            {
                error =
                    $"relocation #{edit.RelocationIndex} is edited more than once.";
                return false;
            }

            if (edit.RelocationIndex < 0 ||
                (uint)edit.RelocationIndex >= oldMap.PatchTableCount)
            {
                error =
                    $"relocation index {edit.RelocationIndex} is outside the CRO relocation table.";
                return false;
            }

            if ((edit.WriteAddress & 3u) != 0)
            {
                error =
                    $"new write slot 0x{edit.WriteAddress:X} for relocation #{edit.RelocationIndex} is not 4-byte aligned.";
                return false;
            }

            if (!TryLocateFileBackedAddress(
                    oldMap,
                    edit.WriteAddress,
                    requiredBytes: 4,
                    out int newWriteSegment,
                    out uint newWriteRelative))
            {
                error =
                    $"new write slot 0x{edit.WriteAddress:X} for relocation #{edit.RelocationIndex} " +
                    "is not fully inside a declared file-backed CRO segment.";
                return false;
            }

            if (!TryLocateFileBackedAddress(
                    oldMap,
                    edit.TargetAddress,
                    requiredBytes: 1,
                    out int newTargetSegment,
                    out uint newTargetRelative))
            {
                error =
                    $"new target 0x{edit.TargetAddress:X} for relocation #{edit.RelocationIndex} " +
                    "is not inside a declared file-backed CRO segment.";
                return false;
            }

            if (newWriteRelative > 0x0FFFFFFFu)
            {
                error =
                    $"new write-relative offset 0x{newWriteRelative:X} does not fit the CRO word0 encoding.";
                return false;
            }

            if (newWriteSegment == newTargetSegment &&
                newTargetRelative >= newWriteRelative &&
                newTargetRelative < newWriteRelative + 4u)
            {
                error =
                    $"relocation #{edit.RelocationIndex} target lies inside its new loader write slot.";
                return false;
            }

            uint newWriteWord =
                edit.WriteAddress & ~3u;

            if (!plannedWriteWords.Add(newWriteWord))
            {
                error =
                    $"more than one edited relocation would write word 0x{newWriteWord:X}.";
                return false;
            }

            int recordOffset =
                checked(
                    (int)(
                        oldMap.PatchTableOffset +
                        ((uint)edit.RelocationIndex * RelocationEntrySize)));

            if (recordOffset < 0 ||
                recordOffset + RelocationEntrySize > cro.Length)
            {
                error =
                    $"relocation #{edit.RelocationIndex} record lies outside the CRO.";
                return false;
            }

            uint oldWord0 =
                BitConverter.ToUInt32(
                    cro,
                    recordOffset);

            uint oldWord1 =
                BitConverter.ToUInt32(
                    cro,
                    recordOffset + 4);

            uint oldAddend =
                BitConverter.ToUInt32(
                    cro,
                    recordOffset + 8);

            byte patchType =
                (byte)(oldWord1 & 0xFFu);

            if (patchType != CroRelocationWriter.PointerPatchType)
            {
                error =
                    $"relocation #{edit.RelocationIndex} has patch type 0x{patchType:X2}; " +
                    $"phase 1 only rewrites pointer type 0x{CroRelocationWriter.PointerPatchType:X2}.";
                return false;
            }

            int oldWriteSegment =
                (int)(oldWord0 & 0xFu);

            uint oldWriteRelative =
                oldWord0 >> 4;

            int oldTargetSegment =
                (int)((oldWord1 >> 8) & 0xFFu);

            uint oldTargetRelative =
                oldAddend;

            var oldReference =
                oldMap.References[edit.RelocationIndex];

            if (!oldReference.WriteFileBacked ||
                !oldReference.TargetFileBacked)
            {
                error =
                    $"relocation #{edit.RelocationIndex} is not file-backed on both sides; " +
                    "phase 1 cannot rewrite BSS references.";
                return false;
            }

            bool keepsExistingWrite =
                edit.WriteAddress == oldReference.WriteAddress;

            // A new loader-owned slot must be blank on disk. Keeping the same write slot is also
            // validated; type-0x02 pointer slots are expected to be zero/CC until the loader runs.
            if (!WordIsBlank(
                    cro,
                    edit.WriteAddress))
            {
                error =
                    $"new write slot 0x{edit.WriteAddress:X} for relocation #{edit.RelocationIndex} is not blank.";
                return false;
            }

            // If the write slot is unchanged, preserve the CRO's existing relocation topology.
            // Some valid CROs intentionally have another relocation target that points at this
            // loader-owned word (Shop.cro does this for its mart pointer table). That relationship
            // is not a collision when this edit only retargets the existing pointer relocation.
            if (!keepsExistingWrite)
            {
                foreach (var reference in oldMap.References)
                {
                    if (reference.Index == edit.RelocationIndex)
                        continue;

                    bool writeOverlaps =
                        reference.WriteFileBacked &&
                        reference.WriteAddress < edit.WriteAddress + 4u &&
                        reference.WriteAddress + 4u > edit.WriteAddress;

                    bool targetInside =
                        reference.TargetFileBacked &&
                        reference.TargetAddress >= edit.WriteAddress &&
                        reference.TargetAddress < edit.WriteAddress + 4u;

                    if (writeOverlaps ||
                        targetInside)
                    {
                        error =
                            $"new write slot 0x{edit.WriteAddress:X} for relocation #{edit.RelocationIndex} " +
                            $"overlaps relocation reference #{reference.Index}.";
                        return false;
                    }
                }
            }

            uint newWord0 =
                (newWriteRelative << 4) |
                (uint)newWriteSegment;

            uint newWord1 =
                (oldWord1 & 0xFFFF00FFu) |
                ((uint)newTargetSegment << 8);

            prepared.Add(
                new PreparedEdit(
                    Request: edit,
                    RecordOffset: (uint)recordOffset,
                    PatchType: patchType,
                    OldWord0: oldWord0,
                    OldWord1: oldWord1,
                    OldAddend: oldAddend,
                    NewWord0: newWord0,
                    NewWord1: newWord1,
                    NewAddend: newTargetRelative,
                    OldWriteSegment: oldWriteSegment,
                    OldWriteRelative: oldWriteRelative,
                    OldWriteAddress: oldReference.WriteAddress,
                    NewWriteSegment: newWriteSegment,
                    NewWriteRelative: newWriteRelative,
                    NewWriteAddress: edit.WriteAddress,
                    OldTargetSegment: oldTargetSegment,
                    OldTargetRelative: oldTargetRelative,
                    OldTargetAddress: oldReference.TargetAddress,
                    NewTargetSegment: newTargetSegment,
                    NewTargetRelative: newTargetRelative,
                    NewTargetAddress: edit.TargetAddress));
        }

        byte[] working =
            (byte[])cro.Clone();

        foreach (var edit in prepared)
        {
            int at =
                checked((int)edit.RecordOffset);

            WriteU32(
                working,
                edit.NewWord0,
                at);

            WriteU32(
                working,
                edit.NewWord1,
                at + 4);

            WriteU32(
                working,
                edit.NewAddend,
                at + 8);

            // A moved relocation needs a fresh blank loader-owned word. A retarget-only edit
            // deliberately preserves its existing write slot and any relocation topology around it.
            if (edit.NewWriteAddress != edit.OldWriteAddress)
            {
                Array.Clear(
                    working,
                    checked((int)edit.NewWriteAddress),
                    4);
            }
        }

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

        if (working.Length != cro.Length ||
            finalMap.PatchTableOffset != oldMap.PatchTableOffset ||
            finalMap.PatchTableCount != oldMap.PatchTableCount ||
            finalMap.SegmentTableOffset != oldMap.SegmentTableOffset ||
            finalMap.SegmentTableCount != oldMap.SegmentTableCount)
        {
            error =
                "relocation editing changed CRO layout or relocation metadata unexpectedly.";
            return false;
        }

        foreach (var edit in prepared)
        {
            var finalReference =
                finalMap.References[edit.Request.RelocationIndex];

            if (!finalReference.WriteFileBacked ||
                !finalReference.TargetFileBacked ||
                finalReference.WriteAddress != edit.NewWriteAddress ||
                finalReference.TargetAddress != edit.NewTargetAddress)
            {
                error =
                    $"relocation #{edit.Request.RelocationIndex} did not resolve to the requested final addresses.";
                return false;
            }
        }

        var editedIndexes =
            prepared
                .Select(z => z.Request.RelocationIndex)
                .ToHashSet();

        for (uint i = 0;
             i < oldMap.PatchTableCount;
             i++)
        {
            if (editedIndexes.Contains((int)i))
                continue;

            int at =
                checked(
                    (int)(
                        oldMap.PatchTableOffset +
                        (i * RelocationEntrySize)));

            if (!cro.AsSpan(
                    at,
                    RelocationEntrySize)
                .SequenceEqual(
                    working.AsSpan(
                        at,
                        RelocationEntrySize)))
            {
                error =
                    $"unedited relocation #{i} changed unexpectedly.";
                return false;
            }
        }

        updated =
            working;

        reports =
            prepared
                .Select(z =>
                    new CroRelocationEditReport(
                        RelocationIndex: z.Request.RelocationIndex,
                        RecordOffset: z.RecordOffset,
                        PatchType: z.PatchType,
                        OldWriteSegment: z.OldWriteSegment,
                        OldWriteRelative: z.OldWriteRelative,
                        OldWriteAddress: z.OldWriteAddress,
                        NewWriteSegment: z.NewWriteSegment,
                        NewWriteRelative: z.NewWriteRelative,
                        NewWriteAddress: z.NewWriteAddress,
                        OldTargetSegment: z.OldTargetSegment,
                        OldTargetRelative: z.OldTargetRelative,
                        OldTargetAddress: z.OldTargetAddress,
                        NewTargetSegment: z.NewTargetSegment,
                        NewTargetRelative: z.NewTargetRelative,
                        NewTargetAddress: z.NewTargetAddress,
                        Purpose: z.Request.Purpose ?? string.Empty))
                .ToArray();

        return true;
    }

    private static bool TryLocateFileBackedAddress(
        CroRelocationMap map,
        uint address,
        int requiredBytes,
        out int segment,
        out uint relative)
    {
        segment = -1;
        relative = 0;

        if (requiredBytes <= 0)
            return false;

        for (int i = 0;
             i < 3 &&
             i < map.Segments.Count;
             i++)
        {
            var candidate =
                map.Segments[i];

            if (!candidate.FileBacked)
                continue;

            ulong start =
                candidate.Start;

            ulong end =
                start +
                candidate.Size;

            ulong requestedEnd =
                (ulong)address +
                (uint)requiredBytes;

            if ((ulong)address < start ||
                requestedEnd > end)
            {
                continue;
            }

            segment = i;
            relative =
                address -
                candidate.Start;

            return true;
        }

        return false;
    }

    private static bool WordIsBlank(
        byte[] data,
        uint address)
    {
        if ((ulong)address + 4u > (ulong)data.Length)
            return false;

        int at =
            checked((int)address);

        for (int i = 0; i < 4; i++)
        {
            byte value =
                data[at + i];

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

    private sealed record PreparedEdit(
        CroRelocationPointerEdit Request,
        uint RecordOffset,
        byte PatchType,
        uint OldWord0,
        uint OldWord1,
        uint OldAddend,
        uint NewWord0,
        uint NewWord1,
        uint NewAddend,
        int OldWriteSegment,
        uint OldWriteRelative,
        uint OldWriteAddress,
        int NewWriteSegment,
        uint NewWriteRelative,
        uint NewWriteAddress,
        int OldTargetSegment,
        uint OldTargetRelative,
        uint OldTargetAddress,
        int NewTargetSegment,
        uint NewTargetRelative,
        uint NewTargetAddress);
}
