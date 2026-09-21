using System;
using System.Collections.Generic;
using System.Linq;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// Audit report for one end-to-end custom Ability mechanic installation.
/// </summary>
public sealed record CroAbilityMechanicInstallReport(
    uint AbilityId,
    uint PlaceholderHandlerTarget,
    uint HandlerOffset,
    uint TimingTableOffset,
    int EffectCount,
    CroCodeGrant MechanicGrant,
    IReadOnlyList<CroMechanicEffectPlacement> Effects,
    CroMasterTableAppendReport Table,
    CroPatchSessionReport MechanicSession,
    CroRelocationEditReport Attachment,
    uint FinalPatchCount,
    int OriginalFileSize,
    int FinalFileSize);

/// <summary>
/// Installs one custom Ability mechanic into the audited USUM Ability master table.
/// </summary>
public static class CroAbilityMechanicInstaller
{
    public static bool TryInstall(
        byte[] cro,
        uint abilityId,
        CroMechanicRequest mechanic,
        out byte[] updated,
        out CroAbilityMechanicInstallReport report,
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

        if (mechanic is null)
        {
            error = "Ability mechanic request is null.";
            return false;
        }

        byte[] inputSnapshot =
            (byte[])cro.Clone();

        CroMasterTableLayout layout =
            CroMasterTableLayouts.AbilityUsumStock;

        if (!TryGetPlaceholderHandler(
                cro,
                layout,
                out uint placeholderHandler,
                out error))
        {
            return false;
        }

        string mechanicName =
            string.IsNullOrWhiteSpace(mechanic.Name)
                ? $"ability-0x{abilityId:X4}"
                : mechanic.Name.Trim();

        var append =
            new CroMasterTableAppendRequest(
                Id: abilityId,
                HandlerTarget: placeholderHandler,
                Purpose: $"ability-mechanic:{mechanicName}:master-entry");

        if (!CroMasterTableExpander.TryAppendEntry(
                cro,
                layout,
                append,
                out byte[] expanded,
                out CroMasterTableAppendReport tableReport,
                out error))
        {
            error =
                "could not append Ability master-table entry: " +
                error;
            return false;
        }

        if (!CroPatchSession.TryCreate(
                expanded,
                out var session,
                out error))
        {
            error =
                "could not create Ability mechanic patch session: " +
                error;
            return false;
        }

        if (!CroMechanicInstaller.TryPlan(
                session,
                mechanic,
                out CroMechanicPlan plan,
                out error))
        {
            error =
                "could not plan Ability mechanic package: " +
                error;
            return false;
        }

        if (!plan.TryWrite(out error))
        {
            error =
                "could not write Ability mechanic package: " +
                error;
            return false;
        }

        if (!session.TryBuildImage(
                out byte[] mechanicImage,
                out CroPatchSessionReport mechanicReport,
                out error))
        {
            error =
                "could not finalize Ability mechanic package: " +
                error;
            return false;
        }

        if (mechanicReport.Relocations.Count !=
            mechanic.Effects.Count + 1)
        {
            error =
                $"Ability mechanic generated {mechanicReport.Relocations.Count} relocation(s); " +
                $"expected {mechanic.Effects.Count + 1}.";
            return false;
        }

        if (!CroRelocationEditor.TryRewritePointer(
                mechanicImage,
                tableReport.NewRelocationIndex,
                tableReport.NewPointerSlot,
                plan.HandlerOffset,
                out byte[] attached,
                out CroRelocationEditReport attachment,
                out error))
        {
            error =
                "could not attach generated handler to Ability master-table entry: " +
                error;
            return false;
        }

        if (!CroRelocationMap.TryCreate(
                attached,
                out var finalMap,
                out error))
        {
            error =
                "final Ability mechanic CRO is invalid: " +
                error;
            return false;
        }

        uint expectedPatchCount;

        try
        {
            expectedPatchCount =
                checked(
                    tableReport.FinalPatchCount +
                    (uint)mechanicReport.Relocations.Count);
        }
        catch (OverflowException)
        {
            error =
                "final Ability mechanic relocation count overflowed.";
            return false;
        }

        if (finalMap.PatchTableCount != expectedPatchCount)
        {
            error =
                $"final Ability mechanic patch count is {finalMap.PatchTableCount}; " +
                $"expected {expectedPatchCount}.";
            return false;
        }

        if (tableReport.NewRelocationIndex < 0 ||
            (uint)tableReport.NewRelocationIndex >= finalMap.PatchTableCount)
        {
            error =
                $"Ability entry relocation #{tableReport.NewRelocationIndex} is outside the final patch table.";
            return false;
        }

        var abilityReference =
            finalMap.References[
                tableReport.NewRelocationIndex];

        if (!abilityReference.WriteFileBacked ||
            !abilityReference.TargetFileBacked ||
            abilityReference.WriteAddress != tableReport.NewPointerSlot ||
            abilityReference.TargetAddress != plan.HandlerOffset)
        {
            error =
                "final Ability entry relocation does not point to the generated handler.";
            return false;
        }

        if ((ulong)tableReport.NewEntryStart + 4u >
            (ulong)attached.Length)
        {
            error =
                "final Ability entry lies outside the CRO.";
            return false;
        }

        uint finalAbilityId =
            BitConverter.ToUInt32(
                attached,
                checked((int)tableReport.NewEntryStart));

        if (finalAbilityId != abilityId)
        {
            error =
                $"final Ability entry id is 0x{finalAbilityId:X8}; expected 0x{abilityId:X8}.";
            return false;
        }

        if (!TryValidateMechanicGraph(
                finalMap,
                plan,
                mechanicReport,
                out error))
        {
            return false;
        }

        if (!cro.AsSpan().SequenceEqual(inputSnapshot))
        {
            error =
                "Ability mechanic installation mutated the caller's input CRO.";
            return false;
        }

        report =
            new CroAbilityMechanicInstallReport(
                AbilityId: abilityId,
                PlaceholderHandlerTarget: placeholderHandler,
                HandlerOffset: plan.HandlerOffset,
                TimingTableOffset: plan.TimingTableOffset,
                EffectCount: plan.Effects.Count,
                MechanicGrant: plan.Grant,
                Effects: plan.Effects.ToArray(),
                Table: tableReport,
                MechanicSession: mechanicReport,
                Attachment: attachment,
                FinalPatchCount: finalMap.PatchTableCount,
                OriginalFileSize: cro.Length,
                FinalFileSize: attached.Length);

        updated =
            attached;

        return true;
    }

    private static bool TryGetPlaceholderHandler(
        byte[] cro,
        CroMasterTableLayout layout,
        out uint target,
        out string error)
    {
        target = 0;
        error = string.Empty;

        if (!CroRelocationMap.TryCreate(
                cro,
                out var map,
                out error))
        {
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
                "Ability master-table first pointer slot overflowed.";
            return false;
        }

        var matches =
            map.References
                .Where(r =>
                    r.WriteFileBacked &&
                    r.WriteAddress == firstPointerSlot)
                .ToArray();

        if (matches.Length != 1)
        {
            error =
                $"audited Ability master-table first pointer slot 0x{firstPointerSlot:X6} " +
                $"has {matches.Length} relocation writer(s); expected exactly 1.";
            return false;
        }

        if (!matches[0].TargetFileBacked)
        {
            error =
                $"audited Ability master-table relocation #{matches[0].Index} " +
                "does not target file-backed code.";
            return false;
        }

        target =
            matches[0].TargetAddress;

        return true;
    }

    private static bool TryValidateMechanicGraph(
        CroRelocationMap map,
        CroMechanicPlan plan,
        CroPatchSessionReport mechanicReport,
        out string error)
    {
        error = string.Empty;

        if (mechanicReport.Relocations.Count == 0)
        {
            error =
                "generated Ability mechanic has no relocation records.";
            return false;
        }

        var handlerPointer =
            mechanicReport.Relocations[0];

        if (handlerPointer.RelocationIndex < 0 ||
            (uint)handlerPointer.RelocationIndex >= map.PatchTableCount)
        {
            error =
                $"generated handler relocation #{handlerPointer.RelocationIndex} is outside the final patch table.";
            return false;
        }

        var finalHandlerPointer =
            map.References[
                handlerPointer.RelocationIndex];

        uint expectedHandlerWrite =
            checked(
                plan.HandlerOffset +
                16u);

        if (!finalHandlerPointer.WriteFileBacked ||
            !finalHandlerPointer.TargetFileBacked ||
            finalHandlerPointer.WriteAddress != expectedHandlerWrite ||
            finalHandlerPointer.TargetAddress != plan.TimingTableOffset)
        {
            error =
                "generated handler does not relocate to its timing table as expected.";
            return false;
        }

        for (int i = 0; i < plan.Effects.Count; i++)
        {
            var relocation =
                mechanicReport.Relocations[i + 1];

            if (relocation.RelocationIndex < 0 ||
                (uint)relocation.RelocationIndex >= map.PatchTableCount)
            {
                error =
                    $"generated effect relocation #{relocation.RelocationIndex} is outside the final patch table.";
                return false;
            }

            var finalReference =
                map.References[
                    relocation.RelocationIndex];

            uint expectedWrite =
                checked(
                    plan.TimingTableOffset +
                    (uint)(i * 8) +
                    4u);

            uint expectedTarget =
                plan.Effects[i].FunctionOffset;

            if (!finalReference.WriteFileBacked ||
                !finalReference.TargetFileBacked ||
                finalReference.WriteAddress != expectedWrite ||
                finalReference.TargetAddress != expectedTarget)
            {
                error =
                    $"generated timing row #{i} does not relocate to its expected effect function.";
                return false;
            }
        }

        return true;
    }
}
