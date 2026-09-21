using System.Collections.Generic;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// Audit report for one end-to-end custom Move mechanic installation.
/// </summary>
public sealed record CroMoveMechanicInstallReport(
    uint MoveId,
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
/// Move-specific compatibility wrapper over the generic audited CRO mechanic installer.
/// </summary>
public static class CroMoveMechanicInstaller
{
    public static bool TryInstall(
        byte[] cro,
        uint moveId,
        CroMechanicRequest mechanic,
        out byte[] updated,
        out CroMoveMechanicInstallReport report,
        out string error)
    {
        updated = null;
        report = null;
        error = string.Empty;

        if (!CroMechanicDomainInstaller.TryInstall(
                cro,
                CroMechanicDomains.MoveUsum,
                moveId,
                mechanic,
                out updated,
                out CroMechanicDomainInstallReport generic,
                out error))
        {
            return false;
        }

        report =
            new CroMoveMechanicInstallReport(
                MoveId: moveId,
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