using System.Collections.Generic;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// Audit report for one end-to-end custom Item mechanic installation.
/// </summary>
public sealed record CroItemMechanicInstallReport(
    uint ItemId,
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
/// Item-specific compatibility wrapper over the generic audited CRO mechanic installer.
/// </summary>
public static class CroItemMechanicInstaller
{
    public static bool TryInstall(
        byte[] cro,
        uint itemId,
        CroMechanicRequest mechanic,
        out byte[] updated,
        out CroItemMechanicInstallReport report,
        out string error)
    {
        updated = null;
        report = null;
        error = string.Empty;

        if (!CroMechanicDomainInstaller.TryInstall(
                cro,
                CroMechanicDomains.ItemUsum,
                itemId,
                mechanic,
                out updated,
                out CroMechanicDomainInstallReport generic,
                out error))
        {
            return false;
        }

        report =
            new CroItemMechanicInstallReport(
                ItemId: itemId,
                PlaceholderHandlerTarget: generic.PlaceholderHandlerTarget,
                HandlerOffset: generic.HandlerOffset,
                TimingTableOffset: generic.TimingTableOffset,
                EffectCount: generic.EffectCount,
                MechanicGrant: generic.MechanicGrant,
                Effects: generic.Effects,
                Table: generic.Table,
                MechanicSession: generic.MechanicSession,
                Attachment: generic.Attachment,
                FinalPatchCount: generic.FinalPatchCount,
                OriginalFileSize: generic.OriginalFileSize,
                FinalFileSize: generic.FinalFileSize);

        return true;
    }
}