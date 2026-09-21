using System;
using System.Collections.Generic;
using System.Linq;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// Structural description of a CRO master mechanic table whose entries contain one loader-owned
/// pointer field.
/// </summary>
public enum CroMasterTableBoundEncoding
{
    UInt32,
    ArmDataProcessingImmediate,
}

public sealed record CroMasterTableBoundPatch(
    uint Address,
    uint StockInstruction,
    uint ExpandedInstruction,
    CroMasterTableBoundEncoding Encoding = CroMasterTableBoundEncoding.UInt32);

public sealed record CroMasterTableLayout(
    string Name,
    uint TableStart,
    int EntryCount,
    int EntrySize,
    int IdFieldOffset,
    int PointerFieldOffset,
    int TerminatorBytes,
    IReadOnlyList<int> InboundRelocationIndexes,
    IReadOnlyList<CroMasterTableBoundPatch> AppendOneBoundPatches);

/// <summary>
/// One entry appended to a master table. The handler target is an absolute file-backed address in
/// the input CRO; it is captured segment-relative before any structural expansion.
/// </summary>
public sealed record CroMasterTableAppendRequest(
    uint Id,
    uint HandlerTarget,
    string Purpose = "");

/// <summary>
/// Result of appending exactly one entry to a stock master table.
/// </summary>
public sealed record CroMasterTableAppendReport(
    string Name,
    uint NewEntryId,
    uint OriginalTableStart,
    uint FinalOriginalTableStart,
    uint NewTableStart,
    uint NewEntryStart,
    uint NewPointerSlot,
    uint NewSentinelStart,
    uint HandlerTarget,
    int OriginalEntryCount,
    int FinalEntryCount,
    int TableBytes,
    int HandlerRelocationsMoved,
    int InboundPointersRetargeted,
    int ExistingRelocationsEdited,
    int NewRelocationIndex,
    uint OriginalPatchCount,
    uint FinalPatchCount,
    int OriginalFileSize,
    int FinalFileSize,
    int CodeBytesAdded,
    CroCodeGrant Grant,
    CroRelocationWriteReport NewRelocation,
    IReadOnlyList<CroRelocationEditReport> RelocationEdits);

/// <summary>
/// One row appended by a multi-entry master-table expansion.
/// </summary>
public sealed record CroMasterTableAppendedEntryReport(
    int RequestIndex,
    uint Id,
    uint EntryStart,
    uint PointerSlot,
    uint HandlerTarget,
    int RelocationIndex,
    CroRelocationWriteReport Relocation);

/// <summary>
/// Result of appending one or more entries to an audited stock master table in one expansion.
/// </summary>
public sealed record CroMasterTableMultiAppendReport(
    string Name,
    uint OriginalTableStart,
    uint FinalOriginalTableStart,
    uint NewTableStart,
    uint NewSentinelStart,
    int OriginalEntryCount,
    int AppendedEntryCount,
    int FinalEntryCount,
    int TableBytes,
    int HandlerRelocationsMoved,
    int InboundPointersRetargeted,
    int ExistingRelocationsEdited,
    uint OriginalPatchCount,
    uint FinalPatchCount,
    int OriginalFileSize,
    int FinalFileSize,
    int CodeBytesAdded,
    CroCodeGrant Grant,
    IReadOnlyList<CroMasterTableAppendedEntryReport> AppendedEntries,
    IReadOnlyList<CroRelocationEditReport> RelocationEdits);

/// <summary>
/// Audited stock layouts for the Battle.cro currently supported by the progressive branch.
/// These are intentionally explicit: a mismatch fails closed instead of guessing a nearby table.
/// </summary>
public static class CroMasterTableLayouts
{
    /// <summary>
    /// USUM item master table discovered from the relocation graph:
    /// 190 [id,pointer] entries followed by one zero 8-byte sentinel.
    /// Two stock code relocations point exactly at the table start.
    /// </summary>
    public static CroMasterTableLayout ItemUsumStock { get; } =
        new(
            Name: "Item",
            TableStart: 0x001031BCu,
            EntryCount: 190,
            EntrySize: 8,
            IdFieldOffset: 0,
            PointerFieldOffset: 4,
            TerminatorBytes: 8,
            InboundRelocationIndexes: [345, 537],
            AppendOneBoundPatches:
            [
                new CroMasterTableBoundPatch(
                    Address: 0x00082390u,
                    StockInstruction: 0xE35400BFu,
                    ExpandedInstruction: 0xE35400C0u,
                    Encoding: CroMasterTableBoundEncoding.ArmDataProcessingImmediate),
                new CroMasterTableBoundPatch(
                    Address: 0x000BF824u,
                    StockInstruction: 0xE35400BFu,
                    ExpandedInstruction: 0xE35400C0u,
                    Encoding: CroMasterTableBoundEncoding.ArmDataProcessingImmediate),
            ]);

    /// <summary>
    /// USUM ability master table discovered from the relocation graph:
    /// 226 [id,pointer] entries with no trailing sentinel. The structure beginning exactly at
    /// 0x104E20 is separate and has its own inbound relocation.
    /// </summary>
    public static CroMasterTableLayout AbilityUsumStock { get; } =
        new(
            Name: "Ability",
            TableStart: 0x00104710u,
            EntryCount: 226,
            EntrySize: 8,
            IdFieldOffset: 0,
            PointerFieldOffset: 4,
            TerminatorBytes: 0,
            InboundRelocationIndexes: [542],
            AppendOneBoundPatches:
            [
                new CroMasterTableBoundPatch(
                    Address: 0x0008497Cu,
                    StockInstruction: 0xE35400E2u,
                    ExpandedInstruction: 0xE35400E3u,
                    Encoding: CroMasterTableBoundEncoding.ArmDataProcessingImmediate),
            ]);

    /// <summary>
    /// USUM move master table discovered from the relocation graph and confirmed in-game:
    /// 343 [id,pointer] entries with no trailing sentinel. Relocation #755 points to the table.
    /// The consumer at 0x87304 loads the entry count from 0x873EC, which changes from
    /// 343 to 344 when exactly one entry is appended.
    /// </summary>
    public static CroMasterTableLayout MoveUsumStock { get; } =
        new(
            Name: "Move",
            TableStart: 0x00105DF0u,
            EntryCount: 343,
            EntrySize: 8,
            IdFieldOffset: 0,
            PointerFieldOffset: 4,
            TerminatorBytes: 0,
            InboundRelocationIndexes: [755],
            AppendOneBoundPatches:
            [
                new CroMasterTableBoundPatch(
                    Address: 0x000873ECu,
                    StockInstruction: 0x00000157u,
                    ExpandedInstruction: 0x00000158u),
            ]);
}

/// <summary>
/// Result of moving one stock master table without changing its logical contents.
/// Phase 1 deliberately does not add entries or patch loop bounds.
/// </summary>
public sealed record CroMasterTableRelocationReport(
    string Name,
    uint OriginalTableStart,
    uint FinalOriginalTableStart,
    uint NewTableStart,
    int EntryCount,
    int EntrySize,
    int TableBytes,
    uint NewSentinelStart,
    int HandlerRelocationsMoved,
    int InboundPointersRetargeted,
    int TotalRelocationsEdited,
    uint OriginalPatchCount,
    uint FinalPatchCount,
    int OriginalFileSize,
    int FinalFileSize,
    int CodeBytesAdded,
    CroCodeGrant Grant,
    IReadOnlyList<CroRelocationEditReport> RelocationEdits);

/// <summary>
/// Moves master mechanic tables into allocator-owned relocatable segment-0 storage.
/// <para>
/// The stock table bytes are copied unchanged. Existing handler relocation records are reused by
/// moving their write slots into the copied table, and every audited inbound table pointer is
/// retargeted to the new table. No relocation records are appended in this phase.
/// </para>
/// <para>
/// This relocation-only phase is intentionally separate from entry expansion. It lets us validate
/// in-game that the engine consumes the moved table before changing counts, sentinels, or adding a
/// custom handler.
/// </para>
/// </summary>
public static class CroMasterTableExpander
{
    /// <summary>
    /// Appends one entry to an audited stock master table, reuses all existing handler relocation
    /// records, appends exactly one new pointer relocation for the new entry, and applies the
    /// layout's audited one-entry loop-bound patches.
    /// </summary>
    public static bool TryAppendEntry(
        byte[] cro,
        CroMasterTableLayout layout,
        CroMasterTableAppendRequest request,
        out byte[] updated,
        out CroMasterTableAppendReport report,
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

        if (layout is null)
        {
            error = "master-table layout is null.";
            return false;
        }

        if (request is null)
        {
            error = "master-table append request is null.";
            return false;
        }

        if (layout.AppendOneBoundPatches is null ||
            layout.AppendOneBoundPatches.Count == 0)
        {
            error =
                $"master-table layout '{layout.Name}' has no audited append-one bound patches.";
            return false;
        }

        if (!TryValidateLayout(
                cro,
                layout,
                out CroRelocationMap originalMap,
                out int sourceSegment,
                out uint sourceRelative,
                out int stockTableBytes,
                out int[] handlerRelocationIndexes,
                out int[] inboundRelocationIndexes,
                out byte[] stockPayload,
                out error))
        {
            return false;
        }

        if (!TryLocate(
                originalMap,
                request.HandlerTarget,
                requiredBytes: 1,
                out int handlerSegment,
                out uint handlerRelative))
        {
            error =
                $"new handler target 0x{request.HandlerTarget:X6} is not inside a declared file-backed CRO segment.";
            return false;
        }

        for (int i = 0; i < layout.EntryCount; i++)
        {
            uint idAddress =
                checked(
                    layout.TableStart +
                    (uint)(i * layout.EntrySize) +
                    (uint)layout.IdFieldOffset);

            uint existingId =
                BitConverter.ToUInt32(
                    cro,
                    checked((int)idAddress));

            if (existingId == request.Id)
            {
                error =
                    $"master table '{layout.Name}' already contains id 0x{request.Id:X8} at entry #{i}.";
                return false;
            }
        }

        int stockEntryBytes;
        int expandedTableBytes;

        try
        {
            stockEntryBytes =
                checked(
                    layout.EntryCount *
                    layout.EntrySize);

            expandedTableBytes =
                checked(
                    stockEntryBytes +
                    layout.EntrySize +
                    layout.TerminatorBytes);
        }
        catch (OverflowException)
        {
            error = "expanded master-table byte length overflowed.";
            return false;
        }

        byte[] expandedPayload =
            new byte[expandedTableBytes];

        stockPayload
            .AsSpan(0, stockEntryBytes)
            .CopyTo(expandedPayload);

        int newEntryRelative =
            stockEntryBytes;

        WriteU32(
            expandedPayload,
            request.Id,
            newEntryRelative + layout.IdFieldOffset);

        // The new pointer field and the new terminator remain zero; the loader owns the pointer.
        if (!CroPatchSession.TryCreate(
                cro,
                out var session,
                out error))
        {
            return false;
        }

        string purpose =
            string.IsNullOrWhiteSpace(request.Purpose)
                ? $"master-table:{layout.Name}:append-one"
                : request.Purpose.Trim();

        if (!session.TryAllocateRelocatableCode(
                expandedTableBytes,
                purpose,
                out CroCodeGrant grant,
                out error))
        {
            return false;
        }

        if (!session.TryWriteCode(
                grant,
                expandedPayload,
                out error))
        {
            return false;
        }

        if (!session.TryBuildImage(
                out byte[] built,
                out CroPatchSessionReport sessionReport,
                out error))
        {
            return false;
        }

        if (sessionReport.Relocations.Count != 0)
        {
            error =
                "master-table expansion unexpectedly appended relocations before the explicit new-entry relocation.";
            return false;
        }

        if (!CroRelocationMap.TryCreate(
                built,
                out var builtMap,
                out error))
        {
            return false;
        }

        if (!TryResolve(
                builtMap,
                sourceSegment,
                sourceRelative,
                stockTableBytes,
                out uint finalOriginalTableStart))
        {
            error =
                "original master table no longer resolves after code-segment expansion.";
            return false;
        }

        if (!TryResolve(
                builtMap,
                handlerSegment,
                handlerRelative,
                requiredBytes: 1,
                out uint finalHandlerTarget))
        {
            error =
                "new-entry handler target no longer resolves after code-segment expansion.";
            return false;
        }

        uint newTableStart =
            grant.Offset;

        uint newEntryStart =
            checked(
                newTableStart +
                (uint)stockEntryBytes);

        uint newPointerSlot =
            checked(
                newEntryStart +
                (uint)layout.PointerFieldOffset);

        uint newSentinelStart =
            checked(
                newEntryStart +
                (uint)layout.EntrySize);

        ulong newTableEnd =
            (ulong)newTableStart +
            (uint)expandedTableBytes;

        if (newTableStart < builtMap.CodeStart ||
            newTableEnd > builtMap.CodeEnd)
        {
            error =
                $"expanded master table 0x{newTableStart:X6}+0x{expandedTableBytes:X} is not fully inside declared segment 0.";
            return false;
        }

        var edits =
            new List<CroRelocationPointerEdit>(
                handlerRelocationIndexes.Length +
                inboundRelocationIndexes.Length);

        for (int i = 0; i < handlerRelocationIndexes.Length; i++)
        {
            int relocationIndex =
                handlerRelocationIndexes[i];

            var current =
                builtMap.References[relocationIndex];

            uint expectedOldWrite =
                checked(
                    finalOriginalTableStart +
                    (uint)(i * layout.EntrySize) +
                    (uint)layout.PointerFieldOffset);

            if (!current.WriteFileBacked ||
                !current.TargetFileBacked ||
                current.WriteAddress != expectedOldWrite)
            {
                error =
                    $"handler relocation #{relocationIndex} no longer writes expected stock slot 0x{expectedOldWrite:X6}.";
                return false;
            }

            uint newWrite =
                checked(
                    newTableStart +
                    (uint)(i * layout.EntrySize) +
                    (uint)layout.PointerFieldOffset);

            edits.Add(
                new CroRelocationPointerEdit(
                    RelocationIndex: relocationIndex,
                    WriteAddress: newWrite,
                    TargetAddress: current.TargetAddress,
                    Purpose: $"{layout.Name}:entry#{i}"));
        }

        foreach (int relocationIndex in inboundRelocationIndexes)
        {
            var current =
                builtMap.References[relocationIndex];

            if (!current.WriteFileBacked ||
                !current.TargetFileBacked ||
                current.TargetAddress != finalOriginalTableStart)
            {
                error =
                    $"inbound relocation #{relocationIndex} no longer targets stock table 0x{finalOriginalTableStart:X6}.";
                return false;
            }

            edits.Add(
                new CroRelocationPointerEdit(
                    RelocationIndex: relocationIndex,
                    WriteAddress: current.WriteAddress,
                    TargetAddress: newTableStart,
                    Purpose: $"{layout.Name}:table-pointer"));
        }

        if (!CroRelocationEditor.TryRewritePointers(
                built,
                edits,
                out byte[] edited,
                out IReadOnlyList<CroRelocationEditReport> editReports,
                out error))
        {
            return false;
        }

        if (!CroRelocationWriter.TryAddPointer(
                edited,
                newPointerSlot,
                finalHandlerTarget,
                out byte[] withNewRelocation,
                out CroRelocationWriteReport newRelocation,
                out error))
        {
            return false;
        }

        byte[] working =
            withNewRelocation;

        foreach (var bound in layout.AppendOneBoundPatches)
        {
            if ((bound.Address & 3u) != 0 ||
                (ulong)bound.Address + 4u > (ulong)working.Length)
            {
                error =
                    $"bound patch address 0x{bound.Address:X6} is invalid.";
                return false;
            }

            uint actual =
                BitConverter.ToUInt32(
                    working,
                    checked((int)bound.Address));

            if (actual != bound.StockInstruction)
            {
                error =
                    $"bound patch at 0x{bound.Address:X6} expected 0x{bound.StockInstruction:X8}, found 0x{actual:X8}.";
                return false;
            }

            WriteU32(
                working,
                bound.ExpandedInstruction,
                checked((int)bound.Address));
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

        if (finalMap.PatchTableCount !=
            originalMap.PatchTableCount + 1u)
        {
            error =
                $"expanded master table changed patch count {originalMap.PatchTableCount} -> {finalMap.PatchTableCount}; expected exactly +1.";
            return false;
        }

        int oldWrites =
            finalMap.References.Count(r =>
                r.WriteFileBacked &&
                r.WriteAddress >= finalOriginalTableStart &&
                (ulong)r.WriteAddress <
                    (ulong)finalOriginalTableStart +
                    (uint)stockTableBytes);

        if (oldWrites != 0)
        {
            error =
                $"old master-table range still contains {oldWrites} relocation write slots.";
            return false;
        }

        int newWrites =
            finalMap.References.Count(r =>
                r.WriteFileBacked &&
                r.WriteAddress >= newTableStart &&
                (ulong)r.WriteAddress <
                    (ulong)newTableStart +
                    (uint)expandedTableBytes);

        if (newWrites != layout.EntryCount + 1)
        {
            error =
                $"expanded master table has {newWrites} relocation write slots; expected {layout.EntryCount + 1}.";
            return false;
        }

        int oldStartTargets =
            finalMap.References.Count(r =>
                r.TargetFileBacked &&
                r.TargetAddress == finalOriginalTableStart);

        if (oldStartTargets != 0)
        {
            error =
                $"old master-table start still has {oldStartTargets} inbound relocation target(s).";
            return false;
        }

        int newStartTargets =
            finalMap.References.Count(r =>
                r.TargetFileBacked &&
                r.TargetAddress == newTableStart);

        if (newStartTargets != inboundRelocationIndexes.Length)
        {
            error =
                $"expanded master-table start has {newStartTargets} inbound relocation target(s); " +
                $"expected {inboundRelocationIndexes.Length}.";
            return false;
        }

        var appendedReference =
            finalMap.References[newRelocation.RelocationIndex];

        if (!appendedReference.WriteFileBacked ||
            !appendedReference.TargetFileBacked ||
            appendedReference.WriteAddress != newPointerSlot ||
            appendedReference.TargetAddress != finalHandlerTarget)
        {
            error =
                "new-entry relocation does not resolve to the expected pointer slot and handler target.";
            return false;
        }

        if (layout.TerminatorBytes > 0 &&
            !IsZeroRange(
                working,
                newSentinelStart,
                layout.TerminatorBytes))
        {
            error =
                $"expanded {layout.Name} sentinel at 0x{newSentinelStart:X6} is not zero.";
            return false;
        }

        foreach (var bound in layout.AppendOneBoundPatches)
        {
            uint actual =
                BitConverter.ToUInt32(
                    working,
                    checked((int)bound.Address));

            if (actual != bound.ExpandedInstruction)
            {
                error =
                    $"expanded bound patch at 0x{bound.Address:X6} is 0x{actual:X8}, expected 0x{bound.ExpandedInstruction:X8}.";
                return false;
            }
        }

        report =
            new CroMasterTableAppendReport(
                Name: layout.Name ?? string.Empty,
                NewEntryId: request.Id,
                OriginalTableStart: layout.TableStart,
                FinalOriginalTableStart: finalOriginalTableStart,
                NewTableStart: newTableStart,
                NewEntryStart: newEntryStart,
                NewPointerSlot: newPointerSlot,
                NewSentinelStart: newSentinelStart,
                HandlerTarget: finalHandlerTarget,
                OriginalEntryCount: layout.EntryCount,
                FinalEntryCount: layout.EntryCount + 1,
                TableBytes: expandedTableBytes,
                HandlerRelocationsMoved: handlerRelocationIndexes.Length,
                InboundPointersRetargeted: inboundRelocationIndexes.Length,
                ExistingRelocationsEdited: editReports.Count,
                NewRelocationIndex: newRelocation.RelocationIndex,
                OriginalPatchCount: originalMap.PatchTableCount,
                FinalPatchCount: finalMap.PatchTableCount,
                OriginalFileSize: cro.Length,
                FinalFileSize: working.Length,
                CodeBytesAdded: sessionReport.TotalCodeBytesAdded,
                Grant: grant,
                NewRelocation: newRelocation,
                RelocationEdits: editReports.ToArray());

        updated =
            working;

        return true;
    }

    /// <summary>
    /// Appends one or more entries to an audited stock master table in one structural expansion.
    /// Existing handler relocations are moved once, inbound table pointers are retargeted once,
    /// and one new pointer relocation is appended for each requested entry.
    /// </summary>
    public static bool TryAppendEntries(
        byte[] cro,
        CroMasterTableLayout layout,
        IReadOnlyList<CroMasterTableAppendRequest> requests,
        out byte[] updated,
        out CroMasterTableMultiAppendReport report,
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

        if (layout is null)
        {
            error = "master-table layout is null.";
            return false;
        }

        if (requests is null)
        {
            error = "master-table append request list is null.";
            return false;
        }

        if (requests.Count == 0)
        {
            error = "master-table append request list is empty.";
            return false;
        }

        if (layout.AppendOneBoundPatches is null ||
            layout.AppendOneBoundPatches.Count == 0)
        {
            error =
                $"master-table layout '{layout.Name}' has no audited append-one bound patches.";
            return false;
        }

        if (!TryValidateLayout(
                cro,
                layout,
                out CroRelocationMap originalMap,
                out int sourceSegment,
                out uint sourceRelative,
                out int stockTableBytes,
                out int[] handlerRelocationIndexes,
                out int[] inboundRelocationIndexes,
                out byte[] stockPayload,
                out error))
        {
            return false;
        }

        CroMasterTableAppendRequest[] requestArray =
            requests.ToArray();

        int appendedCount =
            requestArray.Length;

        var seenIds =
            new HashSet<uint>();

        var handlerSegments =
            new int[appendedCount];

        var handlerRelatives =
            new uint[appendedCount];

        for (int requestIndex = 0;
             requestIndex < appendedCount;
             requestIndex++)
        {
            CroMasterTableAppendRequest request =
                requestArray[requestIndex];

            if (request is null)
            {
                error =
                    $"master-table append request #{requestIndex} is null.";
                return false;
            }

            if (!seenIds.Add(request.Id))
            {
                error =
                    $"master-table append request list contains duplicate id 0x{request.Id:X8}.";
                return false;
            }

            for (int i = 0; i < layout.EntryCount; i++)
            {
                uint idAddress =
                    checked(
                        layout.TableStart +
                        (uint)(i * layout.EntrySize) +
                        (uint)layout.IdFieldOffset);

                uint existingId =
                    BitConverter.ToUInt32(
                        cro,
                        checked((int)idAddress));

                if (existingId == request.Id)
                {
                    error =
                        $"master table '{layout.Name}' already contains id 0x{request.Id:X8} at entry #{i}.";
                    return false;
                }
            }

            if (!TryLocate(
                    originalMap,
                    request.HandlerTarget,
                    requiredBytes: 1,
                    out handlerSegments[requestIndex],
                    out handlerRelatives[requestIndex]))
            {
                error =
                    $"new handler target 0x{request.HandlerTarget:X6} for request #{requestIndex} " +
                    "is not inside a declared file-backed CRO segment.";
                return false;
            }
        }

        int stockEntryBytes;
        int finalEntryCount;
        int expandedTableBytes;

        try
        {
            stockEntryBytes =
                checked(
                    layout.EntryCount *
                    layout.EntrySize);

            finalEntryCount =
                checked(
                    layout.EntryCount +
                    appendedCount);

            expandedTableBytes =
                checked(
                    stockEntryBytes +
                    (appendedCount * layout.EntrySize) +
                    layout.TerminatorBytes);
        }
        catch (OverflowException)
        {
            error =
                "expanded master-table dimensions overflowed.";
            return false;
        }

        byte[] expandedPayload =
            new byte[expandedTableBytes];

        stockPayload
            .AsSpan(0, stockEntryBytes)
            .CopyTo(expandedPayload);

        for (int i = 0; i < appendedCount; i++)
        {
            int newEntryRelative =
                checked(
                    stockEntryBytes +
                    (i * layout.EntrySize));

            WriteU32(
                expandedPayload,
                requestArray[i].Id,
                newEntryRelative + layout.IdFieldOffset);
        }

        if (!CroPatchSession.TryCreate(
                cro,
                out var session,
                out error))
        {
            return false;
        }

        string purpose;

        if (appendedCount == 1)
        {
            purpose =
                string.IsNullOrWhiteSpace(requestArray[0].Purpose)
                    ? $"master-table:{layout.Name}:append-one"
                    : requestArray[0].Purpose.Trim();
        }
        else
        {
            purpose =
                $"master-table:{layout.Name}:append-{appendedCount}";
        }

        if (!session.TryAllocateRelocatableCode(
                expandedTableBytes,
                purpose,
                out CroCodeGrant grant,
                out error))
        {
            return false;
        }

        if (!session.TryWriteCode(
                grant,
                expandedPayload,
                out error))
        {
            return false;
        }

        if (!session.TryBuildImage(
                out byte[] built,
                out CroPatchSessionReport sessionReport,
                out error))
        {
            return false;
        }

        if (sessionReport.Relocations.Count != 0)
        {
            error =
                "master-table multi-expansion unexpectedly appended relocations before explicit new-entry relocations.";
            return false;
        }

        if (!CroRelocationMap.TryCreate(
                built,
                out var builtMap,
                out error))
        {
            return false;
        }

        if (!TryResolve(
                builtMap,
                sourceSegment,
                sourceRelative,
                stockTableBytes,
                out uint finalOriginalTableStart))
        {
            error =
                "original master table no longer resolves after code-segment expansion.";
            return false;
        }

        uint newTableStart =
            grant.Offset;

        uint newSentinelStart;

        try
        {
            newSentinelStart =
                checked(
                    newTableStart +
                    (uint)(finalEntryCount * layout.EntrySize));
        }
        catch (OverflowException)
        {
            error =
                "expanded master-table sentinel address overflowed.";
            return false;
        }

        ulong newTableEnd =
            (ulong)newTableStart +
            (uint)expandedTableBytes;

        if (newTableStart < builtMap.CodeStart ||
            newTableEnd > builtMap.CodeEnd)
        {
            error =
                $"expanded master table 0x{newTableStart:X6}+0x{expandedTableBytes:X} " +
                "is not fully inside declared segment 0.";
            return false;
        }

        var edits =
            new List<CroRelocationPointerEdit>(
                handlerRelocationIndexes.Length +
                inboundRelocationIndexes.Length);

        for (int i = 0; i < handlerRelocationIndexes.Length; i++)
        {
            int relocationIndex =
                handlerRelocationIndexes[i];

            var current =
                builtMap.References[relocationIndex];

            uint expectedOldWrite =
                checked(
                    finalOriginalTableStart +
                    (uint)(i * layout.EntrySize) +
                    (uint)layout.PointerFieldOffset);

            if (!current.WriteFileBacked ||
                !current.TargetFileBacked ||
                current.WriteAddress != expectedOldWrite)
            {
                error =
                    $"handler relocation #{relocationIndex} no longer writes expected stock slot " +
                    $"0x{expectedOldWrite:X6}.";
                return false;
            }

            uint newWrite =
                checked(
                    newTableStart +
                    (uint)(i * layout.EntrySize) +
                    (uint)layout.PointerFieldOffset);

            edits.Add(
                new CroRelocationPointerEdit(
                    RelocationIndex: relocationIndex,
                    WriteAddress: newWrite,
                    TargetAddress: current.TargetAddress,
                    Purpose: $"{layout.Name}:entry#{i}"));
        }

        foreach (int relocationIndex in inboundRelocationIndexes)
        {
            var current =
                builtMap.References[relocationIndex];

            if (!current.WriteFileBacked ||
                !current.TargetFileBacked ||
                current.TargetAddress != finalOriginalTableStart)
            {
                error =
                    $"inbound relocation #{relocationIndex} no longer targets stock table " +
                    $"0x{finalOriginalTableStart:X6}.";
                return false;
            }

            edits.Add(
                new CroRelocationPointerEdit(
                    RelocationIndex: relocationIndex,
                    WriteAddress: current.WriteAddress,
                    TargetAddress: newTableStart,
                    Purpose: $"{layout.Name}:table-pointer"));
        }

        if (!CroRelocationEditor.TryRewritePointers(
                built,
                edits,
                out byte[] edited,
                out IReadOnlyList<CroRelocationEditReport> editReports,
                out error))
        {
            return false;
        }

        byte[] working =
            edited;

        var newEntryStarts =
            new uint[appendedCount];

        var newPointerSlots =
            new uint[appendedCount];

        var newRelocations =
            new CroRelocationWriteReport[appendedCount];

        for (int i = 0; i < appendedCount; i++)
        {
            try
            {
                newEntryStarts[i] =
                    checked(
                        newTableStart +
                        (uint)((layout.EntryCount + i) * layout.EntrySize));

                newPointerSlots[i] =
                    checked(
                        newEntryStarts[i] +
                        (uint)layout.PointerFieldOffset);
            }
            catch (OverflowException)
            {
                error =
                    $"new entry #{i} address overflowed.";
                return false;
            }

            if (!CroRelocationMap.TryCreate(
                    working,
                    out var currentMap,
                    out error))
            {
                return false;
            }

            if (!TryResolve(
                    currentMap,
                    handlerSegments[i],
                    handlerRelatives[i],
                    requiredBytes: 1,
                    out uint currentHandlerTarget))
            {
                error =
                    $"handler target for request #{i} no longer resolves before relocation insertion.";
                return false;
            }

            if (!CroRelocationWriter.TryAddPointer(
                    working,
                    newPointerSlots[i],
                    currentHandlerTarget,
                    out byte[] withNewRelocation,
                    out CroRelocationWriteReport newRelocation,
                    out error))
            {
                return false;
            }

            working =
                withNewRelocation;

            newRelocations[i] =
                newRelocation;
        }

        foreach (var bound in layout.AppendOneBoundPatches)
        {
            if ((bound.Address & 3u) != 0 ||
                (ulong)bound.Address + 4u > (ulong)working.Length)
            {
                error =
                    $"bound patch address 0x{bound.Address:X6} is invalid.";
                return false;
            }

            uint actual =
                BitConverter.ToUInt32(
                    working,
                    checked((int)bound.Address));

            if (actual != bound.StockInstruction)
            {
                error =
                    $"bound patch at 0x{bound.Address:X6} expected " +
                    $"0x{bound.StockInstruction:X8}, found 0x{actual:X8}.";
                return false;
            }

            if (!TryGetExpandedBoundWord(
                    bound,
                    appendedCount,
                    out uint expandedBound,
                    out error))
            {
                error =
                    $"bound patch at 0x{bound.Address:X6}: {error}";
                return false;
            }

            WriteU32(
                working,
                expandedBound,
                checked((int)bound.Address));
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

        uint expectedPatchCount;

        try
        {
            expectedPatchCount =
                checked(
                    originalMap.PatchTableCount +
                    (uint)appendedCount);
        }
        catch (OverflowException)
        {
            error =
                "final relocation count overflowed.";
            return false;
        }

        if (finalMap.PatchTableCount != expectedPatchCount)
        {
            error =
                $"expanded master table changed patch count " +
                $"{originalMap.PatchTableCount} -> {finalMap.PatchTableCount}; " +
                $"expected +{appendedCount}.";
            return false;
        }

        int oldWrites =
            finalMap.References.Count(r =>
                r.WriteFileBacked &&
                r.WriteAddress >= finalOriginalTableStart &&
                (ulong)r.WriteAddress <
                    (ulong)finalOriginalTableStart +
                    (uint)stockTableBytes);

        if (oldWrites != 0)
        {
            error =
                $"old master-table range still contains {oldWrites} relocation write slots.";
            return false;
        }

        int newWrites =
            finalMap.References.Count(r =>
                r.WriteFileBacked &&
                r.WriteAddress >= newTableStart &&
                (ulong)r.WriteAddress <
                    (ulong)newTableStart +
                    (uint)expandedTableBytes);

        if (newWrites != finalEntryCount)
        {
            error =
                $"expanded master table has {newWrites} relocation write slots; " +
                $"expected {finalEntryCount}.";
            return false;
        }

        int oldStartTargets =
            finalMap.References.Count(r =>
                r.TargetFileBacked &&
                r.TargetAddress == finalOriginalTableStart);

        if (oldStartTargets != 0)
        {
            error =
                $"old master-table start still has {oldStartTargets} inbound relocation target(s).";
            return false;
        }

        int newStartTargets =
            finalMap.References.Count(r =>
                r.TargetFileBacked &&
                r.TargetAddress == newTableStart);

        if (newStartTargets != inboundRelocationIndexes.Length)
        {
            error =
                $"expanded master-table start has {newStartTargets} inbound relocation target(s); " +
                $"expected {inboundRelocationIndexes.Length}.";
            return false;
        }

        var appendedReports =
            new CroMasterTableAppendedEntryReport[appendedCount];

        for (int i = 0; i < appendedCount; i++)
        {
            int relocationIndex =
                newRelocations[i].RelocationIndex;

            if (relocationIndex < 0 ||
                (uint)relocationIndex >= finalMap.PatchTableCount)
            {
                error =
                    $"new-entry relocation #{relocationIndex} for request #{i} " +
                    "is outside the final patch table.";
                return false;
            }

            var appendedReference =
                finalMap.References[relocationIndex];

            if (!appendedReference.WriteFileBacked ||
                !appendedReference.TargetFileBacked ||
                appendedReference.WriteAddress != newPointerSlots[i])
            {
                error =
                    $"new-entry relocation #{relocationIndex} for request #{i} " +
                    "does not resolve to the expected pointer slot.";
                return false;
            }

            uint finalId =
                BitConverter.ToUInt32(
                    working,
                    checked(
                        (int)(
                            newEntryStarts[i] +
                            (uint)layout.IdFieldOffset)));

            if (finalId != requestArray[i].Id)
            {
                error =
                    $"new entry #{i} id is 0x{finalId:X8}; " +
                    $"expected 0x{requestArray[i].Id:X8}.";
                return false;
            }

            appendedReports[i] =
                new CroMasterTableAppendedEntryReport(
                    RequestIndex: i,
                    Id: requestArray[i].Id,
                    EntryStart: newEntryStarts[i],
                    PointerSlot: newPointerSlots[i],
                    HandlerTarget: appendedReference.TargetAddress,
                    RelocationIndex: relocationIndex,
                    Relocation: newRelocations[i]);
        }

        if (layout.TerminatorBytes > 0 &&
            !IsZeroRange(
                working,
                newSentinelStart,
                layout.TerminatorBytes))
        {
            error =
                $"expanded {layout.Name} sentinel at 0x{newSentinelStart:X6} is not zero.";
            return false;
        }

        foreach (var bound in layout.AppendOneBoundPatches)
        {
            if (!TryGetExpandedBoundWord(
                    bound,
                    appendedCount,
                    out uint expectedBound,
                    out error))
            {
                return false;
            }

            uint actual =
                BitConverter.ToUInt32(
                    working,
                    checked((int)bound.Address));

            if (actual != expectedBound)
            {
                error =
                    $"expanded bound patch at 0x{bound.Address:X6} is " +
                    $"0x{actual:X8}, expected 0x{expectedBound:X8}.";
                return false;
            }
        }

        report =
            new CroMasterTableMultiAppendReport(
                Name: layout.Name ?? string.Empty,
                OriginalTableStart: layout.TableStart,
                FinalOriginalTableStart: finalOriginalTableStart,
                NewTableStart: newTableStart,
                NewSentinelStart: newSentinelStart,
                OriginalEntryCount: layout.EntryCount,
                AppendedEntryCount: appendedCount,
                FinalEntryCount: finalEntryCount,
                TableBytes: expandedTableBytes,
                HandlerRelocationsMoved: handlerRelocationIndexes.Length,
                InboundPointersRetargeted: inboundRelocationIndexes.Length,
                ExistingRelocationsEdited: editReports.Count,
                OriginalPatchCount: originalMap.PatchTableCount,
                FinalPatchCount: finalMap.PatchTableCount,
                OriginalFileSize: cro.Length,
                FinalFileSize: working.Length,
                CodeBytesAdded: sessionReport.TotalCodeBytesAdded,
                Grant: grant,
                AppendedEntries: appendedReports,
                RelocationEdits: editReports.ToArray());

        updated =
            working;

        return true;
    }
    public static bool TryRelocateStockTable(
        byte[] cro,
        CroMasterTableLayout layout,
        out byte[] updated,
        out CroMasterTableRelocationReport report,
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

        if (layout is null)
        {
            error = "master-table layout is null.";
            return false;
        }

        if (!TryValidateLayout(
                cro,
                layout,
                out CroRelocationMap originalMap,
                out int sourceSegment,
                out uint sourceRelative,
                out int tableBytes,
                out int[] handlerRelocationIndexes,
                out int[] inboundRelocationIndexes,
                out byte[] payload,
                out error))
        {
            return false;
        }

        if (!CroPatchSession.TryCreate(
                cro,
                out var session,
                out error))
        {
            return false;
        }

        if (!session.TryAllocateRelocatableCode(
                tableBytes,
                $"master-table:{layout.Name}",
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
                out byte[] built,
                out CroPatchSessionReport sessionReport,
                out error))
        {
            return false;
        }

        if (sessionReport.Relocations.Count != 0)
        {
            error =
                "master-table relocation unexpectedly appended new CRO relocations before the edit phase.";
            return false;
        }

        if (!CroRelocationMap.TryCreate(
                built,
                out var builtMap,
                out error))
        {
            return false;
        }

        if (!TryResolve(
                builtMap,
                sourceSegment,
                sourceRelative,
                tableBytes,
                out uint finalOriginalTableStart))
        {
            error =
                "original master table no longer resolves after code-segment expansion.";
            return false;
        }

        if (!built.AsSpan(
                checked((int)finalOriginalTableStart),
                tableBytes)
            .SequenceEqual(payload))
        {
            error =
                "original master-table bytes changed unexpectedly while allocating the relocated copy.";
            return false;
        }

        uint newTableStart =
            grant.Offset;

        ulong newTableEnd =
            (ulong)newTableStart +
            (uint)tableBytes;

        if (newTableStart < builtMap.CodeStart ||
            newTableEnd > builtMap.CodeEnd)
        {
            error =
                $"new master table 0x{newTableStart:X6}+0x{tableBytes:X} is not fully inside declared segment 0.";
            return false;
        }

        var edits =
            new List<CroRelocationPointerEdit>(
                handlerRelocationIndexes.Length +
                inboundRelocationIndexes.Length);

        for (int i = 0; i < handlerRelocationIndexes.Length; i++)
        {
            int relocationIndex =
                handlerRelocationIndexes[i];

            var current =
                builtMap.References[relocationIndex];

            uint expectedOldWrite =
                checked(
                    finalOriginalTableStart +
                    (uint)(i * layout.EntrySize) +
                    (uint)layout.PointerFieldOffset);

            if (!current.WriteFileBacked ||
                !current.TargetFileBacked ||
                current.WriteAddress != expectedOldWrite)
            {
                error =
                    $"handler relocation #{relocationIndex} no longer writes expected stock slot 0x{expectedOldWrite:X6}.";
                return false;
            }

            uint newWrite =
                checked(
                    newTableStart +
                    (uint)(i * layout.EntrySize) +
                    (uint)layout.PointerFieldOffset);

            edits.Add(
                new CroRelocationPointerEdit(
                    RelocationIndex: relocationIndex,
                    WriteAddress: newWrite,
                    TargetAddress: current.TargetAddress,
                    Purpose: $"{layout.Name}:entry#{i}"));
        }

        foreach (int relocationIndex in inboundRelocationIndexes)
        {
            var current =
                builtMap.References[relocationIndex];

            if (!current.WriteFileBacked ||
                !current.TargetFileBacked ||
                current.TargetAddress != finalOriginalTableStart)
            {
                error =
                    $"inbound relocation #{relocationIndex} no longer targets stock table 0x{finalOriginalTableStart:X6}.";
                return false;
            }

            edits.Add(
                new CroRelocationPointerEdit(
                    RelocationIndex: relocationIndex,
                    WriteAddress: current.WriteAddress,
                    TargetAddress: newTableStart,
                    Purpose: $"{layout.Name}:table-pointer"));
        }

        if (!CroRelocationEditor.TryRewritePointers(
                built,
                edits,
                out byte[] edited,
                out IReadOnlyList<CroRelocationEditReport> editReports,
                out error))
        {
            return false;
        }

        if (!CroRelocationMap.TryCreate(
                edited,
                out var finalMap,
                out error))
        {
            return false;
        }

        if (finalMap.PatchTableCount != originalMap.PatchTableCount)
        {
            error =
                $"master-table relocation changed patch count {originalMap.PatchTableCount} -> {finalMap.PatchTableCount}.";
            return false;
        }

        int writesInsideOld =
            finalMap.References.Count(r =>
                r.WriteFileBacked &&
                r.WriteAddress >= finalOriginalTableStart &&
                (ulong)r.WriteAddress <
                    (ulong)finalOriginalTableStart +
                    (uint)tableBytes);

        if (writesInsideOld != 0)
        {
            error =
                $"old master-table range still contains {writesInsideOld} relocation write slots.";
            return false;
        }

        int writesInsideNew =
            finalMap.References.Count(r =>
                r.WriteFileBacked &&
                r.WriteAddress >= newTableStart &&
                (ulong)r.WriteAddress <
                    (ulong)newTableStart +
                    (uint)tableBytes);

        if (writesInsideNew != layout.EntryCount)
        {
            error =
                $"new master table has {writesInsideNew} relocation write slots; expected {layout.EntryCount}.";
            return false;
        }

        int targetsOldStart =
            finalMap.References.Count(r =>
                r.TargetFileBacked &&
                r.TargetAddress == finalOriginalTableStart);

        if (targetsOldStart != 0)
        {
            error =
                $"old master-table start still has {targetsOldStart} inbound relocation target(s).";
            return false;
        }

        int targetsNewStart =
            finalMap.References.Count(r =>
                r.TargetFileBacked &&
                r.TargetAddress == newTableStart);

        if (targetsNewStart != inboundRelocationIndexes.Length)
        {
            error =
                $"new master-table start has {targetsNewStart} inbound relocation target(s); " +
                $"expected {inboundRelocationIndexes.Length}.";
            return false;
        }

        uint sentinel =
            checked(
                newTableStart +
                (uint)(layout.EntryCount * layout.EntrySize));

        if (layout.TerminatorBytes > 0 &&
            !IsZeroRange(
                edited,
                sentinel,
                layout.TerminatorBytes))
        {
            error =
                $"relocated {layout.Name} sentinel at 0x{sentinel:X6} is not zero.";
            return false;
        }

        report =
            new CroMasterTableRelocationReport(
                Name: layout.Name ?? string.Empty,
                OriginalTableStart: layout.TableStart,
                FinalOriginalTableStart: finalOriginalTableStart,
                NewTableStart: newTableStart,
                EntryCount: layout.EntryCount,
                EntrySize: layout.EntrySize,
                TableBytes: tableBytes,
                NewSentinelStart: sentinel,
                HandlerRelocationsMoved: handlerRelocationIndexes.Length,
                InboundPointersRetargeted: inboundRelocationIndexes.Length,
                TotalRelocationsEdited: editReports.Count,
                OriginalPatchCount: originalMap.PatchTableCount,
                FinalPatchCount: finalMap.PatchTableCount,
                OriginalFileSize: cro.Length,
                FinalFileSize: edited.Length,
                CodeBytesAdded: sessionReport.TotalCodeBytesAdded,
                Grant: grant,
                RelocationEdits: editReports.ToArray());

        updated =
            edited;

        return true;
    }

    private static bool TryGetExpandedBoundWord(
        CroMasterTableBoundPatch bound,
        int appendedCount,
        out uint expanded,
        out string error)
    {
        expanded = 0;
        error = string.Empty;

        if (appendedCount <= 0)
        {
            error =
                "appended entry count must be positive.";
            return false;
        }

        switch (bound.Encoding)
        {
            case CroMasterTableBoundEncoding.UInt32:
            {
                uint expectedAppendOne;

                try
                {
                    expectedAppendOne =
                        checked(
                            bound.StockInstruction +
                            1u);
                }
                catch (OverflowException)
                {
                    error =
                        "stock UInt32 bound cannot be incremented.";
                    return false;
                }

                if (bound.ExpandedInstruction != expectedAppendOne)
                {
                    error =
                        $"audited append-one UInt32 value is 0x{bound.ExpandedInstruction:X8}; " +
                        $"expected 0x{expectedAppendOne:X8}.";
                    return false;
                }

                try
                {
                    expanded =
                        checked(
                            bound.StockInstruction +
                            (uint)appendedCount);
                }
                catch (OverflowException)
                {
                    error =
                        "expanded UInt32 bound overflowed.";
                    return false;
                }

                return true;
            }

            case CroMasterTableBoundEncoding.ArmDataProcessingImmediate:
            {
                if ((bound.StockInstruction & 0xFFFFF000u) !=
                    (bound.ExpandedInstruction & 0xFFFFF000u))
                {
                    error =
                        "audited ARM append-one instruction changes bits outside Operand2.";
                    return false;
                }

                if (!TryDecodeArmDataProcessingImmediate(
                        bound.StockInstruction,
                        out uint stockImmediate) ||
                    !TryDecodeArmDataProcessingImmediate(
                        bound.ExpandedInstruction,
                        out uint appendOneImmediate))
                {
                    error =
                        "audited ARM bound is not a data-processing immediate instruction.";
                    return false;
                }

                uint expectedAppendOne;

                try
                {
                    expectedAppendOne =
                        checked(
                            stockImmediate +
                            1u);
                }
                catch (OverflowException)
                {
                    error =
                        "stock ARM immediate cannot be incremented.";
                    return false;
                }

                if (appendOneImmediate != expectedAppendOne)
                {
                    error =
                        $"audited ARM append-one immediate is {appendOneImmediate}; " +
                        $"expected {expectedAppendOne}.";
                    return false;
                }

                uint finalImmediate;

                try
                {
                    finalImmediate =
                        checked(
                            stockImmediate +
                            (uint)appendedCount);
                }
                catch (OverflowException)
                {
                    error =
                        "expanded ARM immediate overflowed.";
                    return false;
                }

                if (!TryEncodeArmDataProcessingImmediate(
                        bound.StockInstruction,
                        finalImmediate,
                        out expanded))
                {
                    error =
                        $"ARM immediate {finalImmediate} is not representable by the audited instruction.";
                    return false;
                }

                return true;
            }

            default:
                error =
                    $"unsupported master-table bound encoding: {bound.Encoding}.";
                return false;
        }
    }

    private static bool TryDecodeArmDataProcessingImmediate(
        uint instruction,
        out uint value)
    {
        value = 0;

        if ((instruction & 0x0C000000u) != 0 ||
            (instruction & 0x02000000u) == 0)
        {
            return false;
        }

        uint operand2 =
            instruction &
            0x00000FFFu;

        int rotation =
            checked(
                (int)(
                    ((operand2 >> 8) & 0xFu) *
                    2u));

        uint immediate =
            operand2 &
            0xFFu;

        value =
            RotateRight32(
                immediate,
                rotation);

        return true;
    }

    private static bool TryEncodeArmDataProcessingImmediate(
        uint template,
        uint value,
        out uint instruction)
    {
        instruction = 0;

        if ((template & 0x0C000000u) != 0 ||
            (template & 0x02000000u) == 0)
        {
            return false;
        }

        for (int rotate = 0; rotate < 16; rotate++)
        {
            int rotation =
                rotate *
                2;

            uint candidate =
                RotateLeft32(
                    value,
                    rotation);

            if ((candidate & 0xFFFFFF00u) != 0)
                continue;

            uint operand2 =
                ((uint)rotate << 8) |
                candidate;

            uint encoded =
                (template & 0xFFFFF000u) |
                operand2;

            if (!TryDecodeArmDataProcessingImmediate(
                    encoded,
                    out uint decoded) ||
                decoded != value)
            {
                continue;
            }

            instruction =
                encoded;

            return true;
        }

        return false;
    }

    private static uint RotateRight32(
        uint value,
        int bits)
    {
        int shift =
            bits &
            31;

        if (shift == 0)
            return value;

        return
            (value >> shift) |
            (value << (32 - shift));
    }

    private static uint RotateLeft32(
        uint value,
        int bits)
    {
        int shift =
            bits &
            31;

        if (shift == 0)
            return value;

        return
            (value << shift) |
            (value >> (32 - shift));
    }
    private static bool TryValidateLayout(
        byte[] cro,
        CroMasterTableLayout layout,
        out CroRelocationMap map,
        out int sourceSegment,
        out uint sourceRelative,
        out int tableBytes,
        out int[] handlerRelocationIndexes,
        out int[] inboundRelocationIndexes,
        out byte[] payload,
        out string error)
    {
        map = null;
        sourceSegment = -1;
        sourceRelative = 0;
        tableBytes = 0;
        handlerRelocationIndexes = Array.Empty<int>();
        inboundRelocationIndexes = Array.Empty<int>();
        payload = Array.Empty<byte>();
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(layout.Name))
        {
            error = "master-table name is empty.";
            return false;
        }

        if (layout.EntryCount <= 0 ||
            layout.EntrySize <= 0 ||
            layout.IdFieldOffset < 0 ||
            layout.IdFieldOffset + 4 > layout.EntrySize ||
            layout.PointerFieldOffset < 0 ||
            layout.PointerFieldOffset + 4 > layout.EntrySize ||
            layout.IdFieldOffset == layout.PointerFieldOffset ||
            layout.TerminatorBytes < 0)
        {
            error = "master-table layout dimensions are invalid.";
            return false;
        }

        if (((layout.TableStart + (uint)layout.PointerFieldOffset) & 3u) != 0)
        {
            error = "master-table pointer field is not 4-byte aligned.";
            return false;
        }

        try
        {
            tableBytes =
                checked(
                    (layout.EntryCount * layout.EntrySize) +
                    layout.TerminatorBytes);
        }
        catch (OverflowException)
        {
            error = "master-table byte length overflowed.";
            return false;
        }

        if (!CroRelocationMap.TryCreate(
                cro,
                out map,
                out error))
        {
            return false;
        }

        if (!TryLocate(
                map,
                layout.TableStart,
                tableBytes,
                out sourceSegment,
                out sourceRelative))
        {
            error =
                $"stock {layout.Name} table 0x{layout.TableStart:X6}+0x{tableBytes:X} " +
                "is not fully inside a declared file-backed CRO segment.";
            return false;
        }

        payload =
            cro.AsSpan(
                    checked((int)layout.TableStart),
                    tableBytes)
                .ToArray();

        uint sentinel =
            checked(
                layout.TableStart +
                (uint)(layout.EntryCount * layout.EntrySize));

        if (layout.TerminatorBytes > 0 &&
            !IsZeroRange(
                cro,
                sentinel,
                layout.TerminatorBytes))
        {
            error =
                $"stock {layout.Name} terminator at 0x{sentinel:X6} does not match the expected zero sentinel.";
            return false;
        }

        var handlerIndexes =
            new int[layout.EntryCount];

        for (int i = 0; i < layout.EntryCount; i++)
        {
            uint slot =
                checked(
                    layout.TableStart +
                    (uint)(i * layout.EntrySize) +
                    (uint)layout.PointerFieldOffset);

            if (!IsZeroRange(
                    cro,
                    slot,
                    4))
            {
                error =
                    $"stock {layout.Name} pointer slot 0x{slot:X6} is not zero on disk.";
                return false;
            }

            var matches =
                map.References
                    .Where(r =>
                        r.WriteFileBacked &&
                        r.WriteAddress == slot)
                    .ToArray();

            if (matches.Length != 1)
            {
                error =
                    $"stock {layout.Name} pointer slot 0x{slot:X6} has {matches.Length} relocation writers; expected exactly 1.";
                return false;
            }

            if (!matches[0].TargetFileBacked)
            {
                error =
                    $"stock {layout.Name} relocation #{matches[0].Index} targets memory-only space; phase 1 cannot move it.";
                return false;
            }

            handlerIndexes[i] =
                matches[0].Index;
        }

        int[] expectedInbound =
            (layout.InboundRelocationIndexes ?? Array.Empty<int>())
                .OrderBy(z => z)
                .ToArray();

        if (expectedInbound.Length == 0)
        {
            error =
                $"stock {layout.Name} layout has no audited inbound relocation indexes.";
            return false;
        }

        if (expectedInbound.Distinct().Count() != expectedInbound.Length)
        {
            error =
                $"stock {layout.Name} inbound relocation list contains duplicates.";
            return false;
        }

        foreach (int index in expectedInbound)
        {
            if (index < 0 ||
                (uint)index >= map.PatchTableCount)
            {
                error =
                    $"stock {layout.Name} inbound relocation index {index} is outside the patch table.";
                return false;
            }

            var reference =
                map.References[index];

            if (!reference.WriteFileBacked ||
                !reference.TargetFileBacked ||
                reference.TargetAddress != layout.TableStart)
            {
                error =
                    $"stock {layout.Name} inbound relocation #{index} does not target 0x{layout.TableStart:X6}.";
                return false;
            }
        }

        uint tableEnd =
            checked(
                layout.TableStart +
                (uint)tableBytes);

        int[] actualTargetsInside =
            map.References
                .Where(r =>
                    r.TargetFileBacked &&
                    r.TargetAddress >= layout.TableStart &&
                    r.TargetAddress < tableEnd)
                .Select(r => r.Index)
                .OrderBy(z => z)
                .ToArray();

        if (!actualTargetsInside.SequenceEqual(expectedInbound))
        {
            error =
                $"stock {layout.Name} has unexpected relocation targets inside its table range. " +
                $"Expected [{string.Join(", ", expectedInbound)}], found [{string.Join(", ", actualTargetsInside)}].";
            return false;
        }

        handlerRelocationIndexes =
            handlerIndexes;

        inboundRelocationIndexes =
            expectedInbound;

        return true;
    }

    private static bool TryLocate(
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

    private static bool TryResolve(
        CroRelocationMap map,
        int segment,
        uint relative,
        int requiredBytes,
        out uint absolute)
    {
        absolute = 0;

        if (requiredBytes <= 0 ||
            segment < 0 ||
            segment >= 3 ||
            segment >= map.Segments.Count)
        {
            return false;
        }

        var info =
            map.Segments[segment];

        if (!info.FileBacked)
            return false;

        ulong relativeEnd =
            (ulong)relative +
            (uint)requiredBytes;

        if (relativeEnd > info.Size)
            return false;

        ulong resolved =
            (ulong)info.Start +
            relative;

        if (resolved > uint.MaxValue)
            return false;

        absolute =
            (uint)resolved;

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

    private static bool IsZeroRange(
        byte[] data,
        uint start,
        int length)
    {
        if (length <= 0 ||
            (ulong)start + (uint)length > (ulong)data.Length)
        {
            return false;
        }

        int at =
            checked((int)start);

        for (int i = 0; i < length; i++)
        {
            if (data[at + i] != 0)
                return false;
        }

        return true;
    }
}
