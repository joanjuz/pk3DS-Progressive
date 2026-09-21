using System;
using System.Collections.Generic;
using System.Linq;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// Audited CRO master-table domains that may participate in one mechanic recipe.
/// </summary>
public enum CroMechanicRecipeDomain
{
    Item,
    Ability,
    Move,
}

/// <summary>
/// One requested custom mechanic inside a recipe.
/// </summary>
public sealed class CroMechanicRecipeEntry
{
    public CroMechanicRecipeDomain Domain { get; set; }
    public uint Id { get; set; }
    public CroMechanicRequest Mechanic { get; set; } = new();
}

/// <summary>
/// One transactional set of CRO mechanic changes.
/// </summary>
public sealed class CroMechanicRecipe
{
    public string Name { get; set; } = string.Empty;
    public List<CroMechanicRecipeEntry> Entries { get; set; } = [];
}

/// <summary>
/// Audit information for one master-table domain expanded by a recipe.
/// </summary>
public sealed record CroMechanicRecipeDomainReport(
    CroMechanicRecipeDomain Domain,
    string DomainName,
    CroMasterTableMultiAppendReport Table);

/// <summary>
/// Audit information for one recipe entry after its master-table row has been attached to the
/// generated mechanic package.
/// </summary>
public sealed record CroMechanicRecipeEntryReport(
    int RecipeIndex,
    CroMechanicRecipeDomain Domain,
    uint EntryId,
    uint PlaceholderHandlerTarget,
    uint EntryStart,
    uint PointerSlot,
    int EntryRelocationIndex,
    uint HandlerOffset,
    uint TimingTableOffset,
    int EffectCount,
    CroCodeGrant MechanicGrant,
    IReadOnlyList<CroMechanicEffectPlacement> Effects,
    IReadOnlyList<CroRelocationWriteReport> MechanicRelocations,
    CroRelocationEditReport Attachment);

/// <summary>
/// End-to-end audit report for one mechanic recipe installation.
/// </summary>
public sealed record CroMechanicRecipeReport(
    string Name,
    int EntryCount,
    IReadOnlyList<CroMechanicRecipeDomainReport> Domains,
    IReadOnlyList<CroMechanicRecipeEntryReport> Entries,
    CroPatchSessionReport MechanicSession,
    IReadOnlyList<CroRelocationEditReport> Attachments,
    uint OriginalPatchCount,
    uint FinalPatchCount,
    int OriginalFileSize,
    int FinalFileSize);

/// <summary>
/// Installs several custom Item / Ability / Move mechanics as one detached CRO transaction.
/// Stock addresses are captured segment-relative before structural work so later code/data movement
/// cannot stale table, handler, bound-patch or ExistingFunction references.
/// </summary>
public static class CroMechanicRecipeEngine
{
    private static readonly CroMechanicRecipeDomain[] DomainOrder =
    [
        CroMechanicRecipeDomain.Item,
        CroMechanicRecipeDomain.Ability,
        CroMechanicRecipeDomain.Move,
    ];

    public static bool TryInstall(
        byte[] cro,
        CroMechanicRecipe recipe,
        out byte[] updated,
        out CroMechanicRecipeReport report,
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

        if (recipe is null)
        {
            error = "mechanic recipe is null.";
            return false;
        }

        if (recipe.Entries is null ||
            recipe.Entries.Count == 0)
        {
            error = "mechanic recipe has no entries.";
            return false;
        }

        byte[] inputSnapshot =
            (byte[])cro.Clone();

        if (!CroRelocationMap.TryCreate(
                cro,
                out CroRelocationMap originalMap,
                out error))
        {
            error =
                "could not parse input CRO: " +
                error;
            return false;
        }

        if (!TryCaptureRecipeEntries(
                originalMap,
                recipe,
                out CapturedRecipeEntry[] capturedEntries,
                out error))
        {
            return false;
        }

        var capturedDomains =
            new Dictionary<CroMechanicRecipeDomain, CapturedDomain>();

        foreach (CroMechanicRecipeDomain domain in DomainOrder)
        {
            if (!capturedEntries.Any(z => z.Domain == domain))
                continue;

            if (!TryGetDomainSpec(
                    domain,
                    out CroMechanicDomainSpec spec))
            {
                error =
                    $"unsupported mechanic recipe domain: {domain}.";
                return false;
            }

            if (!TryCaptureDomain(
                    originalMap,
                    spec,
                    out CapturedDomain capturedDomain,
                    out error))
            {
                error =
                    $"could not capture {spec.Name} domain: " +
                    error;
                return false;
            }

            capturedDomains.Add(
                domain,
                capturedDomain);
        }

        byte[] working =
            (byte[])cro.Clone();

        var domainReports =
            new List<CroMechanicRecipeDomainReport>(
                capturedDomains.Count);

        var expandedEntries =
            new ExpandedRecipeEntry[capturedEntries.Length];

        foreach (CroMechanicRecipeDomain domain in DomainOrder)
        {
            CapturedRecipeEntry[] group =
                capturedEntries
                    .Where(z => z.Domain == domain)
                    .OrderBy(z => z.RecipeIndex)
                    .ToArray();

            if (group.Length == 0)
                continue;

            CapturedDomain capturedDomain =
                capturedDomains[domain];

            if (!CroRelocationMap.TryCreate(
                    working,
                    out CroRelocationMap currentMap,
                    out error))
            {
                error =
                    $"could not parse CRO before {capturedDomain.Spec.Name} table expansion: " +
                    error;
                return false;
            }

            if (!TryBuildCurrentLayout(
                    currentMap,
                    capturedDomain,
                    out CroMasterTableLayout currentLayout,
                    out error))
            {
                error =
                    $"could not rebase {capturedDomain.Spec.Name} layout: " +
                    error;
                return false;
            }

            if (!TryResolveAddress(
                    currentMap,
                    capturedDomain.PlaceholderHandler,
                    out uint placeholderHandler))
            {
                error =
                    $"{capturedDomain.Spec.Name} placeholder handler no longer resolves.";
                return false;
            }

            var requests =
                new CroMasterTableAppendRequest[group.Length];

            for (int i = 0; i < group.Length; i++)
            {
                string mechanicName =
                    string.IsNullOrWhiteSpace(group[i].Mechanic.Name)
                        ? $"{capturedDomain.Spec.PurposePrefix}-0x{group[i].Id:X4}"
                        : group[i].Mechanic.Name.Trim();

                requests[i] =
                    new CroMasterTableAppendRequest(
                        Id: group[i].Id,
                        HandlerTarget: placeholderHandler,
                        Purpose:
                            $"{capturedDomain.Spec.PurposePrefix}-mechanic:{mechanicName}:master-entry");
            }

            if (!CroMasterTableExpander.TryAppendEntries(
                    working,
                    currentLayout,
                    requests,
                    out byte[] expanded,
                    out CroMasterTableMultiAppendReport tableReport,
                    out error))
            {
                error =
                    $"could not expand {capturedDomain.Spec.Name} master table: " +
                    error;
                return false;
            }

            if (tableReport.AppendedEntries.Count !=
                group.Length)
            {
                error =
                    $"{capturedDomain.Spec.Name} table expansion returned " +
                    $"{tableReport.AppendedEntries.Count} appended row(s); expected {group.Length}.";
                return false;
            }

            if (!CroRelocationMap.TryCreate(
                    expanded,
                    out CroRelocationMap expandedMap,
                    out error))
            {
                error =
                    $"could not parse CRO after {capturedDomain.Spec.Name} table expansion: " +
                    error;
                return false;
            }

            for (int i = 0; i < group.Length; i++)
            {
                CroMasterTableAppendedEntryReport tableEntry =
                    tableReport.AppendedEntries[i];

                if (tableEntry.RequestIndex != i ||
                    tableEntry.Id != group[i].Id)
                {
                    error =
                        $"{capturedDomain.Spec.Name} appended row #{i} does not match recipe entry " +
                        $"#{group[i].RecipeIndex}.";
                    return false;
                }

                if (!TryCaptureAddress(
                        expandedMap,
                        tableEntry.EntryStart,
                        currentLayout.EntrySize,
                        out CroPatchAddress entryAddress) ||
                    !TryCaptureAddress(
                        expandedMap,
                        tableEntry.PointerSlot,
                        4,
                        out CroPatchAddress pointerAddress))
                {
                    error =
                        $"{capturedDomain.Spec.Name} appended row #{i} is not fully file-backed.";
                    return false;
                }

                expandedEntries[group[i].RecipeIndex] =
                    new ExpandedRecipeEntry(
                        RecipeIndex: group[i].RecipeIndex,
                        Domain: domain,
                        Layout: currentLayout,
                        TableEntry: tableEntry,
                        EntryAddress: entryAddress,
                        PointerAddress: pointerAddress,
                        PlaceholderHandler: capturedDomain.PlaceholderHandler);
            }

            domainReports.Add(
                new CroMechanicRecipeDomainReport(
                    Domain: domain,
                    DomainName: capturedDomain.Spec.Name,
                    Table: tableReport));

            working =
                expanded;
        }

        for (int i = 0; i < expandedEntries.Length; i++)
        {
            if (expandedEntries[i] is null)
            {
                error =
                    $"recipe entry #{i} did not receive a master-table row.";
                return false;
            }
        }

        if (!CroPatchSession.TryCreate(
                working,
                out CroPatchSession session,
                out error))
        {
            error =
                "could not create recipe mechanic patch session: " +
                error;
            return false;
        }

        var plannedEntries =
            new PlannedRecipeEntry[capturedEntries.Length];

        for (int i = 0; i < capturedEntries.Length; i++)
        {
            CapturedRecipeEntry captured =
                capturedEntries[i];

            if (!TryBuildCurrentMechanic(
                    session.Map,
                    captured,
                    out CroMechanicRequest currentMechanic,
                    out error))
            {
                error =
                    $"recipe entry #{i} could not rebase ExistingFunction addresses: " +
                    error;
                return false;
            }

            int pointerStart =
                session.PointerPlans.Count;

            if (!CroMechanicInstaller.TryPlan(
                    session,
                    currentMechanic,
                    out CroMechanicPlan plan,
                    out error))
            {
                error =
                    $"recipe entry #{i} could not plan mechanic package: " +
                    error;
                return false;
            }

            int pointerCount =
                session.PointerPlans.Count -
                pointerStart;

            int expectedPointers =
                checked(
                    currentMechanic.Effects.Count +
                    1);

            if (pointerCount != expectedPointers)
            {
                error =
                    $"recipe entry #{i} queued {pointerCount} mechanic pointer(s); " +
                    $"expected {expectedPointers}.";
                return false;
            }

            plannedEntries[i] =
                new PlannedRecipeEntry(
                    Captured: captured,
                    Expanded: expandedEntries[i],
                    Plan: plan,
                    Mechanic: currentMechanic,
                    RelocationStart: pointerStart,
                    RelocationCount: pointerCount);
        }

        CroRelocationMap plannedMap =
            session.Map;

        for (int i = 0; i < plannedEntries.Length; i++)
        {
            PlannedRecipeEntry planned =
                plannedEntries[i];

            if (!TryCaptureAddress(
                    plannedMap,
                    planned.Plan.HandlerOffset,
                    1,
                    out CroPatchAddress handlerAddress) ||
                !TryCaptureAddress(
                    plannedMap,
                    planned.Plan.TimingTableOffset,
                    1,
                    out CroPatchAddress timingAddress))
            {
                error =
                    $"recipe entry #{i} generated package is not fully file-backed.";
                return false;
            }

            var functionAddresses =
                new CroPatchAddress[planned.Plan.Effects.Count];

            for (int effectIndex = 0;
                 effectIndex < functionAddresses.Length;
                 effectIndex++)
            {
                CroPatchAddress? existing =
                    planned.Captured.ExistingFunctions[effectIndex];

                if (existing.HasValue)
                {
                    functionAddresses[effectIndex] =
                        existing.Value;
                    continue;
                }

                if (!TryCaptureAddress(
                        plannedMap,
                        planned.Plan.Effects[effectIndex].FunctionOffset,
                        1,
                        out functionAddresses[effectIndex]))
                {
                    error =
                        $"recipe entry #{i} generated effect #{effectIndex} is not file-backed.";
                    return false;
                }
            }

            planned.HandlerAddress =
                handlerAddress;

            planned.TimingAddress =
                timingAddress;

            planned.FunctionAddresses =
                functionAddresses;
        }

        for (int i = 0; i < plannedEntries.Length; i++)
        {
            if (!plannedEntries[i].Plan.TryWrite(
                    out error))
            {
                error =
                    $"recipe entry #{i} could not write mechanic package: " +
                    error;
                return false;
            }
        }

        if (!session.TryBuildImage(
                out byte[] mechanicImage,
                out CroPatchSessionReport mechanicReport,
                out error))
        {
            error =
                "could not finalize recipe mechanic session: " +
                error;
            return false;
        }

        int expectedMechanicRelocations =
            plannedEntries.Sum(z => z.RelocationCount);

        if (mechanicReport.Relocations.Count !=
            expectedMechanicRelocations)
        {
            error =
                $"recipe mechanic session generated {mechanicReport.Relocations.Count} relocation(s); " +
                $"expected {expectedMechanicRelocations}.";
            return false;
        }

        if (!CroRelocationMap.TryCreate(
                mechanicImage,
                out CroRelocationMap mechanicMap,
                out error))
        {
            error =
                "recipe mechanic image is invalid: " +
                error;
            return false;
        }

        var attachmentEdits =
            new CroRelocationPointerEdit[plannedEntries.Length];

        for (int i = 0; i < plannedEntries.Length; i++)
        {
            PlannedRecipeEntry planned =
                plannedEntries[i];

            if (!TryResolveAddress(
                    mechanicMap,
                    planned.Expanded.PointerAddress,
                    out uint pointerSlot) ||
                !TryResolveAddress(
                    mechanicMap,
                    planned.HandlerAddress,
                    out uint handlerOffset))
            {
                error =
                    $"recipe entry #{i} attachment addresses no longer resolve.";
                return false;
            }

            attachmentEdits[i] =
                new CroRelocationPointerEdit(
                    RelocationIndex:
                        planned.Expanded.TableEntry.RelocationIndex,
                    WriteAddress:
                        pointerSlot,
                    TargetAddress:
                        handlerOffset,
                    Purpose:
                        $"recipe-entry#{i}:{planned.Captured.Domain}:0x{planned.Captured.Id:X4}");
        }

        if (!CroRelocationEditor.TryRewritePointers(
                mechanicImage,
                attachmentEdits,
                out byte[] attached,
                out IReadOnlyList<CroRelocationEditReport> attachmentReports,
                out error))
        {
            error =
                "could not attach recipe mechanics to master-table rows: " +
                error;
            return false;
        }

        if (attachmentReports.Count !=
            plannedEntries.Length)
        {
            error =
                $"recipe attachment returned {attachmentReports.Count} report(s); " +
                $"expected {plannedEntries.Length}.";
            return false;
        }

        if (!CroRelocationMap.TryCreate(
                attached,
                out CroRelocationMap finalMap,
                out error))
        {
            error =
                "final recipe CRO is invalid: " +
                error;
            return false;
        }

        uint expectedPatchCount;

        try
        {
            expectedPatchCount =
                checked(
                    originalMap.PatchTableCount +
                    (uint)capturedEntries.Length +
                    (uint)expectedMechanicRelocations);
        }
        catch (OverflowException)
        {
            error =
                "final recipe relocation count overflowed.";
            return false;
        }

        if (finalMap.PatchTableCount !=
            expectedPatchCount)
        {
            error =
                $"final recipe patch count is {finalMap.PatchTableCount}; " +
                $"expected {expectedPatchCount}.";
            return false;
        }

        var entryReports =
            new CroMechanicRecipeEntryReport[plannedEntries.Length];

        for (int i = 0; i < plannedEntries.Length; i++)
        {
            PlannedRecipeEntry planned =
                plannedEntries[i];

            if (!TryBuildFinalEntryReport(
                    attached,
                    finalMap,
                    mechanicReport,
                    attachmentReports[i],
                    planned,
                    out CroMechanicRecipeEntryReport entryReport,
                    out error))
            {
                error =
                    $"recipe entry #{i} final validation failed: " +
                    error;
                return false;
            }

            entryReports[i] =
                entryReport;
        }

        if (!cro.AsSpan().SequenceEqual(inputSnapshot))
        {
            error =
                "recipe installation mutated the caller's input CRO.";
            return false;
        }

        report =
            new CroMechanicRecipeReport(
                Name:
                    string.IsNullOrWhiteSpace(recipe.Name)
                        ? string.Empty
                        : recipe.Name.Trim(),
                EntryCount:
                    entryReports.Length,
                Domains:
                    domainReports.ToArray(),
                Entries:
                    entryReports,
                MechanicSession:
                    mechanicReport,
                Attachments:
                    attachmentReports.ToArray(),
                OriginalPatchCount:
                    originalMap.PatchTableCount,
                FinalPatchCount:
                    finalMap.PatchTableCount,
                OriginalFileSize:
                    cro.Length,
                FinalFileSize:
                    attached.Length);

        updated =
            attached;

        return true;
    }

    private static bool TryCaptureRecipeEntries(
        CroRelocationMap originalMap,
        CroMechanicRecipe recipe,
        out CapturedRecipeEntry[] captured,
        out string error)
    {
        captured =
            Array.Empty<CapturedRecipeEntry>();

        error =
            string.Empty;

        var seenIds =
            new HashSet<(CroMechanicRecipeDomain Domain, uint Id)>();

        var result =
            new CapturedRecipeEntry[recipe.Entries.Count];

        for (int i = 0;
             i < recipe.Entries.Count;
             i++)
        {
            CroMechanicRecipeEntry entry =
                recipe.Entries[i];

            if (entry is null)
            {
                error =
                    $"recipe entry #{i} is null.";
                return false;
            }

            if (!TryGetDomainSpec(
                    entry.Domain,
                    out _))
            {
                error =
                    $"recipe entry #{i} has unsupported domain value {(int)entry.Domain}.";
                return false;
            }

            if (!seenIds.Add(
                    (entry.Domain, entry.Id)))
            {
                error =
                    $"recipe contains duplicate {entry.Domain} id 0x{entry.Id:X8}.";
                return false;
            }

            if (entry.Mechanic is null)
            {
                error =
                    $"recipe entry #{i} mechanic is null.";
                return false;
            }

            if (entry.Mechanic.Effects is null ||
                entry.Mechanic.Effects.Count == 0)
            {
                error =
                    $"recipe entry #{i} mechanic has no effects.";
                return false;
            }

            var effects =
                new List<CroMechanicEffectSpec>(
                    entry.Mechanic.Effects.Count);

            var existingFunctions =
                new CroPatchAddress?[entry.Mechanic.Effects.Count];

            for (int effectIndex = 0;
                 effectIndex < entry.Mechanic.Effects.Count;
                 effectIndex++)
            {
                CroMechanicEffectSpec effect =
                    entry.Mechanic.Effects[effectIndex];

                if (effect is null)
                {
                    error =
                        $"recipe entry #{i} effect #{effectIndex} is null.";
                    return false;
                }

                byte[] code =
                    effect.Code is null
                        ? []
                        : (byte[])effect.Code.Clone();

                var snapshot =
                    new CroMechanicEffectSpec
                    {
                        Timing = effect.Timing,
                        Name = effect.Name ?? string.Empty,
                        Code = code,
                        ExistingFunction = effect.ExistingFunction,
                    };

                if (snapshot.ReusesExisting)
                {
                    if (!TryCaptureAddress(
                            originalMap,
                            snapshot.ExistingFunction,
                            1,
                            out CroPatchAddress functionAddress))
                    {
                        error =
                            $"recipe entry #{i} effect #{effectIndex} ExistingFunction " +
                            $"0x{snapshot.ExistingFunction:X6} is not inside a declared file-backed CRO segment.";
                        return false;
                    }

                    existingFunctions[effectIndex] =
                        functionAddress;
                }

                effects.Add(
                    snapshot);
            }

            var mechanic =
                new CroMechanicRequest
                {
                    Name =
                        entry.Mechanic.Name ?? string.Empty,
                    Effects =
                        effects,
                };

            result[i] =
                new CapturedRecipeEntry(
                    RecipeIndex: i,
                    Domain: entry.Domain,
                    Id: entry.Id,
                    Mechanic: mechanic,
                    ExistingFunctions: existingFunctions);
        }

        captured =
            result;

        return true;
    }

    private static bool TryCaptureDomain(
        CroRelocationMap originalMap,
        CroMechanicDomainSpec spec,
        out CapturedDomain captured,
        out string error)
    {
        captured =
            null;

        error =
            string.Empty;

        CroMasterTableLayout layout =
            spec.Layout;

        int tableBytes;

        try
        {
            tableBytes =
                checked(
                    (layout.EntryCount * layout.EntrySize) +
                    layout.TerminatorBytes);
        }
        catch (OverflowException)
        {
            error =
                "stock master-table byte length overflowed.";
            return false;
        }

        if (!TryCaptureAddress(
                originalMap,
                layout.TableStart,
                tableBytes,
                out CroPatchAddress tableAddress))
        {
            error =
                $"stock table 0x{layout.TableStart:X6}+0x{tableBytes:X} is not file-backed.";
            return false;
        }

        uint firstPointerSlot;

        try
        {
            firstPointerSlot =
                checked(
                    layout.TableStart +
                    (uint)layout.PointerFieldOffset);
        }
        catch (OverflowException)
        {
            error =
                "stock first pointer slot overflowed.";
            return false;
        }

        CroRelocationReference[] matches =
            originalMap.References
                .Where(r =>
                    r.WriteFileBacked &&
                    r.WriteAddress == firstPointerSlot)
                .ToArray();

        if (matches.Length != 1 ||
            !matches[0].TargetFileBacked)
        {
            error =
                $"stock first pointer slot 0x{firstPointerSlot:X6} does not have exactly one file-backed relocation target.";
            return false;
        }

        if (!TryCaptureAddress(
                originalMap,
                matches[0].TargetAddress,
                1,
                out CroPatchAddress placeholderHandler))
        {
            error =
                $"stock placeholder handler 0x{matches[0].TargetAddress:X6} is not file-backed.";
            return false;
        }

        IReadOnlyList<CroMasterTableBoundPatch> bounds =
            layout.AppendOneBoundPatches ??
            Array.Empty<CroMasterTableBoundPatch>();

        if (bounds.Count == 0)
        {
            error =
                "stock master-table layout has no audited bound patches.";
            return false;
        }

        var boundAddresses =
            new CroPatchAddress[bounds.Count];

        for (int i = 0; i < bounds.Count; i++)
        {
            if (!TryCaptureAddress(
                    originalMap,
                    bounds[i].Address,
                    4,
                    out boundAddresses[i]))
            {
                error =
                    $"stock bound patch 0x{bounds[i].Address:X6} is not fully file-backed.";
                return false;
            }
        }

        captured =
            new CapturedDomain(
                Spec: spec,
                TableAddress: tableAddress,
                PlaceholderHandler: placeholderHandler,
                BoundAddresses: boundAddresses);

        return true;
    }

    private static bool TryBuildCurrentLayout(
        CroRelocationMap map,
        CapturedDomain captured,
        out CroMasterTableLayout layout,
        out string error)
    {
        layout =
            null;

        error =
            string.Empty;

        CroMasterTableLayout stock =
            captured.Spec.Layout;

        if (!TryResolveAddress(
                map,
                captured.TableAddress,
                out uint tableStart))
        {
            error =
                "stock master-table address no longer resolves.";
            return false;
        }

        IReadOnlyList<CroMasterTableBoundPatch> stockBounds =
            stock.AppendOneBoundPatches ??
            Array.Empty<CroMasterTableBoundPatch>();

        if (stockBounds.Count !=
            captured.BoundAddresses.Count)
        {
            error =
                "captured master-table bound count changed unexpectedly.";
            return false;
        }

        var currentBounds =
            new CroMasterTableBoundPatch[stockBounds.Count];

        for (int i = 0; i < stockBounds.Count; i++)
        {
            if (!TryResolveAddress(
                    map,
                    captured.BoundAddresses[i],
                    out uint boundAddress))
            {
                error =
                    $"bound patch #{i} no longer resolves.";
                return false;
            }

            currentBounds[i] =
                new CroMasterTableBoundPatch(
                    Address: boundAddress,
                    StockInstruction: stockBounds[i].StockInstruction,
                    ExpandedInstruction: stockBounds[i].ExpandedInstruction,
                    Encoding: stockBounds[i].Encoding);
        }

        layout =
            new CroMasterTableLayout(
                Name: stock.Name,
                TableStart: tableStart,
                EntryCount: stock.EntryCount,
                EntrySize: stock.EntrySize,
                IdFieldOffset: stock.IdFieldOffset,
                PointerFieldOffset: stock.PointerFieldOffset,
                TerminatorBytes: stock.TerminatorBytes,
                InboundRelocationIndexes: stock.InboundRelocationIndexes,
                AppendOneBoundPatches: currentBounds);

        return true;
    }

    private static bool TryBuildCurrentMechanic(
        CroRelocationMap map,
        CapturedRecipeEntry captured,
        out CroMechanicRequest mechanic,
        out string error)
    {
        mechanic =
            null;

        error =
            string.Empty;

        var effects =
            new List<CroMechanicEffectSpec>(
                captured.Mechanic.Effects.Count);

        for (int i = 0;
             i < captured.Mechanic.Effects.Count;
             i++)
        {
            CroMechanicEffectSpec source =
                captured.Mechanic.Effects[i];

            uint existingFunction =
                0;

            CroPatchAddress? capturedFunction =
                captured.ExistingFunctions[i];

            if (capturedFunction.HasValue)
            {
                if (!TryResolveAddress(
                        map,
                        capturedFunction.Value,
                        out existingFunction))
                {
                    error =
                        $"effect #{i} ExistingFunction no longer resolves.";
                    return false;
                }
            }

            effects.Add(
                new CroMechanicEffectSpec
                {
                    Timing = source.Timing,
                    Name = source.Name ?? string.Empty,
                    Code =
                        source.Code is null
                            ? []
                            : (byte[])source.Code.Clone(),
                    ExistingFunction = existingFunction,
                });
        }

        mechanic =
            new CroMechanicRequest
            {
                Name =
                    captured.Mechanic.Name ?? string.Empty,
                Effects =
                    effects,
            };

        return true;
    }

    private static bool TryBuildFinalEntryReport(
        byte[] finalImage,
        CroRelocationMap finalMap,
        CroPatchSessionReport mechanicReport,
        CroRelocationEditReport attachment,
        PlannedRecipeEntry planned,
        out CroMechanicRecipeEntryReport report,
        out string error)
    {
        report =
            null;

        error =
            string.Empty;

        if (!TryResolveAddress(
                finalMap,
                planned.Expanded.EntryAddress,
                out uint entryStart) ||
            !TryResolveAddress(
                finalMap,
                planned.Expanded.PointerAddress,
                out uint pointerSlot) ||
            !TryResolveAddress(
                finalMap,
                planned.Expanded.PlaceholderHandler,
                out uint placeholderHandler) ||
            !TryResolveAddress(
                finalMap,
                planned.HandlerAddress,
                out uint handlerOffset) ||
            !TryResolveAddress(
                finalMap,
                planned.TimingAddress,
                out uint timingOffset))
        {
            error =
                "final entry/package addresses no longer resolve.";
            return false;
        }

        if ((ulong)entryStart +
            (uint)planned.Expanded.Layout.EntrySize >
            (ulong)finalImage.Length)
        {
            error =
                "final master-table entry lies outside the CRO.";
            return false;
        }

        uint idAddress;

        try
        {
            idAddress =
                checked(
                    entryStart +
                    (uint)planned.Expanded.Layout.IdFieldOffset);
        }
        catch (OverflowException)
        {
            error =
                "final entry id address overflowed.";
            return false;
        }

        uint finalId =
            BitConverter.ToUInt32(
                finalImage,
                checked((int)idAddress));

        if (finalId !=
            planned.Captured.Id)
        {
            error =
                $"final entry id is 0x{finalId:X8}; expected 0x{planned.Captured.Id:X8}.";
            return false;
        }

        int entryRelocationIndex =
            planned.Expanded.TableEntry.RelocationIndex;

        if (entryRelocationIndex < 0 ||
            (uint)entryRelocationIndex >= finalMap.PatchTableCount)
        {
            error =
                $"entry relocation #{entryRelocationIndex} is outside the final patch table.";
            return false;
        }

        CroRelocationReference domainReference =
            finalMap.References[
                entryRelocationIndex];

        if (!domainReference.WriteFileBacked ||
            !domainReference.TargetFileBacked ||
            domainReference.WriteAddress != pointerSlot ||
            domainReference.TargetAddress != handlerOffset)
        {
            error =
                "final master-table relocation does not point to the generated handler.";
            return false;
        }

        if (attachment.RelocationIndex !=
                entryRelocationIndex ||
            attachment.NewWriteAddress !=
                pointerSlot ||
            attachment.NewTargetAddress !=
                handlerOffset)
        {
            error =
                "attachment audit report does not match the final master-table relocation.";
            return false;
        }

        if (planned.RelocationStart < 0 ||
            planned.RelocationCount <= 0 ||
            planned.RelocationStart + planned.RelocationCount >
                mechanicReport.Relocations.Count)
        {
            error =
                "mechanic relocation slice is outside the session report.";
            return false;
        }

        var mechanicRelocations =
            mechanicReport.Relocations
                .Skip(planned.RelocationStart)
                .Take(planned.RelocationCount)
                .ToArray();

        CroRelocationWriteReport handlerRelocation =
            mechanicRelocations[0];

        if (handlerRelocation.RelocationIndex < 0 ||
            (uint)handlerRelocation.RelocationIndex >=
                finalMap.PatchTableCount)
        {
            error =
                $"handler relocation #{handlerRelocation.RelocationIndex} is outside the final patch table.";
            return false;
        }

        uint expectedHandlerWrite;

        try
        {
            expectedHandlerWrite =
                checked(
                    handlerOffset +
                    16u);
        }
        catch (OverflowException)
        {
            error =
                "handler timing-pointer address overflowed.";
            return false;
        }

        CroRelocationReference finalHandlerReference =
            finalMap.References[
                handlerRelocation.RelocationIndex];

        if (!finalHandlerReference.WriteFileBacked ||
            !finalHandlerReference.TargetFileBacked ||
            finalHandlerReference.WriteAddress != expectedHandlerWrite ||
            finalHandlerReference.TargetAddress != timingOffset)
        {
            error =
                "generated handler does not relocate to its timing table.";
            return false;
        }

        if (mechanicRelocations.Length !=
            planned.Plan.Effects.Count + 1)
        {
            error =
                $"mechanic relocation slice has {mechanicRelocations.Length} record(s); " +
                $"expected {planned.Plan.Effects.Count + 1}.";
            return false;
        }

        var finalEffects =
            new CroMechanicEffectPlacement[
                planned.Plan.Effects.Count];

        for (int i = 0;
             i < finalEffects.Length;
             i++)
        {
            CroRelocationWriteReport relocation =
                mechanicRelocations[i + 1];

            if (relocation.RelocationIndex < 0 ||
                (uint)relocation.RelocationIndex >=
                    finalMap.PatchTableCount)
            {
                error =
                    $"effect relocation #{relocation.RelocationIndex} is outside the final patch table.";
                return false;
            }

            uint expectedWrite;

            try
            {
                expectedWrite =
                    checked(
                        timingOffset +
                        (uint)(i * 8) +
                        4u);
            }
            catch (OverflowException)
            {
                error =
                    $"effect #{i} timing pointer address overflowed.";
                return false;
            }

            if (!TryResolveAddress(
                    finalMap,
                    planned.FunctionAddresses[i],
                    out uint expectedTarget))
            {
                error =
                    $"effect #{i} target no longer resolves.";
                return false;
            }

            CroRelocationReference finalReference =
                finalMap.References[
                    relocation.RelocationIndex];

            if (!finalReference.WriteFileBacked ||
                !finalReference.TargetFileBacked ||
                finalReference.WriteAddress != expectedWrite ||
                finalReference.TargetAddress != expectedTarget)
            {
                error =
                    $"generated timing row #{i} does not relocate to its expected effect function.";
                return false;
            }

            CroMechanicEffectPlacement sourcePlacement =
                planned.Plan.Effects[i];

            finalEffects[i] =
                new CroMechanicEffectPlacement(
                    Timing: sourcePlacement.Timing,
                    Name: sourcePlacement.Name,
                    FunctionOffset: expectedTarget,
                    ReusesExisting: sourcePlacement.ReusesExisting,
                    CodeLength: sourcePlacement.CodeLength);
        }

        report =
            new CroMechanicRecipeEntryReport(
                RecipeIndex:
                    planned.Captured.RecipeIndex,
                Domain:
                    planned.Captured.Domain,
                EntryId:
                    planned.Captured.Id,
                PlaceholderHandlerTarget:
                    placeholderHandler,
                EntryStart:
                    entryStart,
                PointerSlot:
                    pointerSlot,
                EntryRelocationIndex:
                    entryRelocationIndex,
                HandlerOffset:
                    handlerOffset,
                TimingTableOffset:
                    timingOffset,
                EffectCount:
                    finalEffects.Length,
                MechanicGrant:
                    planned.Plan.Grant,
                Effects:
                    finalEffects,
                MechanicRelocations:
                    mechanicRelocations,
                Attachment:
                    attachment);

        return true;
    }

    private static bool TryGetDomainSpec(
        CroMechanicRecipeDomain domain,
        out CroMechanicDomainSpec spec)
    {
        spec =
            domain switch
            {
                CroMechanicRecipeDomain.Item =>
                    CroMechanicDomains.ItemUsum,

                CroMechanicRecipeDomain.Ability =>
                    CroMechanicDomains.AbilityUsum,

                CroMechanicRecipeDomain.Move =>
                    CroMechanicDomains.MoveUsum,

                _ =>
                    null,
            };

        return
            spec is not null;
    }

    private static bool TryCaptureAddress(
        CroRelocationMap map,
        uint absolute,
        int requiredBytes,
        out CroPatchAddress address)
    {
        address =
            default;

        if (requiredBytes <= 0)
            return false;

        for (int i = 0;
             i < 3 &&
             i < map.Segments.Count;
             i++)
        {
            CroSegmentInfo segment =
                map.Segments[i];

            if (!segment.FileBacked)
                continue;

            ulong start =
                segment.Start;

            ulong end =
                start +
                segment.Size;

            ulong requestedEnd =
                (ulong)absolute +
                (uint)requiredBytes;

            if ((ulong)absolute < start ||
                requestedEnd > end)
            {
                continue;
            }

            address =
                new CroPatchAddress(
                    Segment: i,
                    Relative: absolute - segment.Start,
                    RequiredBytes: requiredBytes,
                    PlannedAbsolute: absolute);

            return true;
        }

        return false;
    }

    private static bool TryResolveAddress(
        CroRelocationMap map,
        CroPatchAddress planned,
        out uint absolute)
    {
        absolute =
            0;

        if (planned.RequiredBytes <= 0 ||
            planned.Segment < 0 ||
            planned.Segment >= 3 ||
            planned.Segment >= map.Segments.Count)
        {
            return false;
        }

        CroSegmentInfo segment =
            map.Segments[
                planned.Segment];

        if (!segment.FileBacked)
            return false;

        ulong start =
            segment.Start;

        ulong resolved =
            start +
            planned.Relative;

        ulong end =
            start +
            segment.Size;

        ulong requestedEnd =
            resolved +
            (uint)planned.RequiredBytes;

        if (resolved < start ||
            requestedEnd > end ||
            resolved > uint.MaxValue)
        {
            return false;
        }

        absolute =
            (uint)resolved;

        return true;
    }

    private sealed record CapturedDomain(
        CroMechanicDomainSpec Spec,
        CroPatchAddress TableAddress,
        CroPatchAddress PlaceholderHandler,
        IReadOnlyList<CroPatchAddress> BoundAddresses);

    private sealed record CapturedRecipeEntry(
        int RecipeIndex,
        CroMechanicRecipeDomain Domain,
        uint Id,
        CroMechanicRequest Mechanic,
        IReadOnlyList<CroPatchAddress?> ExistingFunctions);

    private sealed record ExpandedRecipeEntry(
        int RecipeIndex,
        CroMechanicRecipeDomain Domain,
        CroMasterTableLayout Layout,
        CroMasterTableAppendedEntryReport TableEntry,
        CroPatchAddress EntryAddress,
        CroPatchAddress PointerAddress,
        CroPatchAddress PlaceholderHandler);

    private sealed class PlannedRecipeEntry
    {
        public CapturedRecipeEntry Captured { get; }
        public ExpandedRecipeEntry Expanded { get; }
        public CroMechanicPlan Plan { get; }
        public CroMechanicRequest Mechanic { get; }
        public int RelocationStart { get; }
        public int RelocationCount { get; }

        public CroPatchAddress HandlerAddress { get; set; }
        public CroPatchAddress TimingAddress { get; set; }
        public IReadOnlyList<CroPatchAddress> FunctionAddresses { get; set; } =
            Array.Empty<CroPatchAddress>();

        public PlannedRecipeEntry(
            CapturedRecipeEntry Captured,
            ExpandedRecipeEntry Expanded,
            CroMechanicPlan Plan,
            CroMechanicRequest Mechanic,
            int RelocationStart,
            int RelocationCount)
        {
            this.Captured = Captured;
            this.Expanded = Expanded;
            this.Plan = Plan;
            this.Mechanic = Mechanic;
            this.RelocationStart = RelocationStart;
            this.RelocationCount = RelocationCount;
        }
    }
}
