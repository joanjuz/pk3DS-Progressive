using System;
using System.Collections.Generic;
using System.Linq;

namespace pk3DS.Core.Modding.Research;

public readonly record struct CroPatchAddress(
    int Segment,
    uint Relative,
    int RequiredBytes,
    uint PlannedAbsolute);

public sealed record CroPatchPointerPlan(
    CroPatchAddress Write,
    CroPatchAddress Target,
    string Purpose);

public sealed record CroPatchSessionReport(
    int OriginalLength,
    int FinalLength,
    IReadOnlyList<CroCodeGrant> CodeGrants,
    IReadOnlyList<CroCodeExpansionReport> CodeExpansions,
    IReadOnlyList<CroRelocationWriteReport> Relocations)
{
    public int TotalCodeBytesAdded =>
        CodeExpansions.Sum(z => z.BytesAdded);

    public int TotalRelocationHeadroomBytesAdded =>
        Relocations.Sum(z => z.Headroom.BytesInserted);
}

/// <summary>
/// Coordinates code allocation, payload writes and loader relocations as one detached CRO
/// transaction. Planning happens first; after the first payload write no new grants or pointer
/// plans are accepted. Pointer addresses are stored segment-relative so later structural moves
/// cannot stale them.
/// </summary>
public sealed class CroPatchSession
{
    private readonly CroCodeSpaceManager codeManager;
    private readonly List<CroPatchPointerPlan> pointerPlans = [];
    private readonly HashSet<CroCodeGrant> writtenGrants = [];

    private byte[] finalImage;
    private CroPatchSessionReport finalReport;

    public int OriginalLength => codeManager.OriginalLength;
    public int CurrentLength => codeManager.CurrentLength;
    public bool WritesStarted => codeManager.WritesStarted;
    public bool Finalized => finalImage is not null;

    public CroRelocationMap Map => codeManager.Map;
    public IReadOnlyList<CroCodeGrant> CodeGrants => codeManager.Granted;
    public IReadOnlyList<CroPatchPointerPlan> PointerPlans => pointerPlans;

    private CroPatchSession(CroCodeSpaceManager codeManager)
    {
        this.codeManager = codeManager;
    }

    public static bool TryCreate(
        byte[] cro,
        out CroPatchSession session,
        out string error)
    {
        session = null;
        error = string.Empty;

        if (!CroCodeSpaceManager.TryCreate(
                cro,
                out var manager,
                out error))
        {
            return false;
        }

        session = new CroPatchSession(manager);
        return true;
    }

    public bool TryAllocateCode(
        int size,
        string purpose,
        out CroCodeGrant grant,
        out string error)
    {
        grant = default;
        error = string.Empty;

        if (Finalized)
        {
            error = "the CRO patch session is already finalized";
            return false;
        }

        return codeManager.TryAllocate(
            size,
            purpose,
            out grant,
            out error);
    }

    public bool TryAllocateCode(
        int size,
        out CroCodeGrant grant,
        out string error) =>
        TryAllocateCode(size, string.Empty, out grant, out error);

    /// <summary>
    /// Allocates code guaranteed to be inside declared segment 0, so it may safely contain CRO
    /// relocation write slots and relocation targets.
    /// </summary>
    public bool TryAllocateRelocatableCode(
        int size,
        string purpose,
        out CroCodeGrant grant,
        out string error)
    {
        grant = default;
        error = string.Empty;

        if (Finalized)
        {
            error = "the CRO patch session is already finalized";
            return false;
        }

        return codeManager.TryAllocateRelocatable(
            size,
            purpose,
            out grant,
            out error);
    }

    public bool TryAllocateRelocatableCode(
        int size,
        out CroCodeGrant grant,
        out string error) =>
        TryAllocateRelocatableCode(
            size,
            string.Empty,
            out grant,
            out error);
    public bool TryQueuePointer(
        uint writeAddress,
        uint targetAddress,
        string purpose,
        out CroPatchPointerPlan plan,
        out string error)
    {
        plan = null;
        error = string.Empty;

        if (Finalized)
        {
            error = "the CRO patch session is already finalized";
            return false;
        }

        if (WritesStarted)
        {
            error =
                "cannot queue relocations after payload writes have started; plan every pointer first";
            return false;
        }

        if ((writeAddress & 3u) != 0)
        {
            error =
                $"relocation write slot 0x{writeAddress:X} is not 4-byte aligned";
            return false;
        }

        if (!TryCaptureAddress(
                Map,
                writeAddress,
                4,
                out CroPatchAddress write))
        {
            error =
                $"write slot 0x{writeAddress:X} is not fully inside a declared file-backed CRO segment";
            return false;
        }

        if (!TryCaptureAddress(
                Map,
                targetAddress,
                1,
                out CroPatchAddress target))
        {
            error =
                $"target 0x{targetAddress:X} is not inside a declared file-backed CRO segment";
            return false;
        }

        if (write.Segment == target.Segment &&
            target.Relative >= write.Relative &&
            target.Relative < write.Relative + 4u)
        {
            error = "target address lies inside the loader write slot";
            return false;
        }

        if (Map.CountReferencesInRange(writeAddress, 4) != 0)
        {
            error =
                $"relocation write slot 0x{writeAddress:X} already overlaps a CRO relocation reference";
            return false;
        }

        if (pointerPlans.Any(z =>
                z.Write.Segment == write.Segment &&
                z.Write.Relative == write.Relative))
        {
            error =
                $"relocation write slot 0x{writeAddress:X} is already queued in this session";
            return false;
        }

        plan = new CroPatchPointerPlan(
            write,
            target,
            purpose ?? string.Empty);

        pointerPlans.Add(plan);
        return true;
    }

    public bool TryQueuePointer(
        uint writeAddress,
        uint targetAddress,
        out CroPatchPointerPlan plan,
        out string error) =>
        TryQueuePointer(
            writeAddress,
            targetAddress,
            string.Empty,
            out plan,
            out error);

    public bool TryWriteCode(
        CroCodeGrant grant,
        ReadOnlySpan<byte> payload,
        out string error)
    {
        error = string.Empty;

        if (Finalized)
        {
            error = "the CRO patch session is already finalized";
            return false;
        }

        if (!codeManager.TryWrite(grant, payload, out error))
            return false;

        writtenGrants.Add(grant);
        return true;
    }

    public bool TryBuildImage(
        out byte[] built,
        out CroPatchSessionReport report,
        out string error)
    {
        built = null;
        report = null;
        error = string.Empty;

        if (Finalized)
        {
            built = (byte[])finalImage.Clone();
            report = finalReport;
            return true;
        }

        foreach (var grant in codeManager.Granted)
        {
            if (writtenGrants.Contains(grant))
                continue;

            error =
                $"code grant 0x{grant.Offset:X6}+0x{grant.Size:X} ({grant.Purpose}) was allocated but never written";
            return false;
        }

        if (!codeManager.TryBuildImage(
                out byte[] current,
                out error))
        {
            return false;
        }

        var relocationReports =
            new List<CroRelocationWriteReport>(pointerPlans.Count);

        foreach (var pointer in pointerPlans)
        {
            if (!CroRelocationMap.TryCreate(
                    current,
                    out var currentMap,
                    out error))
            {
                return false;
            }

            if (!TryResolveAddress(
                    currentMap,
                    pointer.Write,
                    out uint writeAddress))
            {
                error =
                    $"queued write slot {Describe(pointer.Write)} no longer resolves inside its segment";
                return false;
            }

            if (!TryResolveAddress(
                    currentMap,
                    pointer.Target,
                    out uint targetAddress))
            {
                error =
                    $"queued target {Describe(pointer.Target)} no longer resolves inside its segment";
                return false;
            }

            if (!CroRelocationWriter.TryAddPointer(
                    current,
                    writeAddress,
                    targetAddress,
                    out byte[] next,
                    out CroRelocationWriteReport relocation,
                    out error))
            {
                string prefix =
                    string.IsNullOrWhiteSpace(pointer.Purpose)
                        ? "queued relocation"
                        : $"queued relocation '{pointer.Purpose}'";

                error = prefix + " failed: " + error;
                return false;
            }

            current = next;
            relocationReports.Add(relocation);
        }

        if (!CroSegmentExpander.TryUpdateHashes(current, out error))
            return false;

        if (!CroRelocationMap.TryCreate(current, out _, out error))
            return false;

        finalImage = (byte[])current.Clone();

        finalReport = new CroPatchSessionReport(
            OriginalLength: OriginalLength,
            FinalLength: current.Length,
            CodeGrants: codeManager.Granted.ToArray(),
            CodeExpansions: codeManager.Expansions.ToArray(),
            Relocations: relocationReports.ToArray());

        built = (byte[])finalImage.Clone();
        report = finalReport;
        return true;
    }

    public byte[] BuildImage()
    {
        if (!TryBuildImage(
                out byte[] built,
                out _,
                out string error))
        {
            throw new InvalidOperationException(
                "Could not finalize CRO patch session: " + error);
        }

        return built;
    }

    private static bool TryCaptureAddress(
        CroRelocationMap map,
        uint absolute,
        int requiredBytes,
        out CroPatchAddress address)
    {
        address = default;

        if (requiredBytes <= 0)
            return false;

        for (int i = 0; i < 3 && i < map.Segments.Count; i++)
        {
            var segment = map.Segments[i];
            if (!segment.FileBacked)
                continue;

            ulong start = segment.Start;
            ulong end = start + segment.Size;
            ulong requestedEnd = (ulong)absolute + (uint)requiredBytes;

            if ((ulong)absolute < start || requestedEnd > end)
                continue;

            address = new CroPatchAddress(
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
        absolute = 0;

        if (planned.RequiredBytes <= 0 ||
            planned.Segment < 0 ||
            planned.Segment >= 3 ||
            planned.Segment >= map.Segments.Count)
        {
            return false;
        }

        var segment = map.Segments[planned.Segment];
        if (!segment.FileBacked)
            return false;

        ulong relativeEnd =
            (ulong)planned.Relative + (uint)planned.RequiredBytes;

        if (relativeEnd > segment.Size)
            return false;

        ulong resolved =
            (ulong)segment.Start + planned.Relative;

        if (resolved > uint.MaxValue ||
            resolved + (uint)planned.RequiredBytes > (ulong)int.MaxValue + 1u)
        {
            return false;
        }

        absolute = (uint)resolved;
        return true;
    }

    private static string Describe(
        CroPatchAddress address) =>
        $"seg{address.Segment}+0x{address.Relative:X} (planned 0x{address.PlannedAbsolute:X6})";
}
