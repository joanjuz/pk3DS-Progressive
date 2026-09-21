using System;
using System.Collections.Generic;
using System.Linq;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// One timed function in a CRO mechanic package.
/// Exactly one of <see cref="Code"/> and <see cref="ExistingFunction"/> must be supplied.
/// </summary>
public sealed class CroMechanicEffectSpec
{
    public byte Timing { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Raw ARM bytes copied into newly allocated segment-0 space.
    /// Phase 1 does not rebase branches; callers must provide position-independent bytes or code
    /// assembled for the final placement.
    /// </summary>
    public byte[] Code { get; set; } = [];

    /// <summary>
    /// Absolute file-backed address of a routine already present in the CRO.
    /// The installer captures it segment-relative before allocations, so later segment movement
    /// cannot stale the target.
    /// </summary>
    public uint ExistingFunction { get; set; }

    public bool ReusesExisting => ExistingFunction != 0;
}

/// <summary>
/// A self-contained handler + timing-table request.
/// This phase deliberately does not attach the handler to a game's master mechanic table yet.
/// </summary>
public sealed class CroMechanicRequest
{
    public string Name { get; set; } = string.Empty;
    public List<CroMechanicEffectSpec> Effects { get; set; } = [];
}

/// <summary>
/// Final placement of one effect in a planned mechanic package.
/// </summary>
public sealed record CroMechanicEffectPlacement(
    byte Timing,
    string Name,
    uint FunctionOffset,
    bool ReusesExisting,
    int CodeLength);

/// <summary>
/// A planned mechanic package. Planning reserves one contiguous CRO grant and queues every loader
/// pointer before any bytes are written. Several plans may therefore be created in one
/// <see cref="CroPatchSession"/>, then written afterwards.
/// </summary>
public sealed class CroMechanicPlan
{
    private readonly CroPatchSession session;
    private readonly byte[] payload;

    private bool written;

    public string Name { get; }
    public CroCodeGrant Grant { get; }
    public uint HandlerOffset { get; }
    public uint TimingTableOffset { get; }
    public IReadOnlyList<CroMechanicEffectPlacement> Effects { get; }
    public IReadOnlyList<CroPatchPointerPlan> PointerPlans { get; }

    public int BytesUsed { get; }
    public bool Written => written;

    internal CroMechanicPlan(
        CroPatchSession session,
        string name,
        CroCodeGrant grant,
        uint handlerOffset,
        uint timingTableOffset,
        IReadOnlyList<CroMechanicEffectPlacement> effects,
        IReadOnlyList<CroPatchPointerPlan> pointerPlans,
        byte[] payload,
        int bytesUsed)
    {
        this.session = session;
        this.payload = payload;

        Name = name;
        Grant = grant;
        HandlerOffset = handlerOffset;
        TimingTableOffset = timingTableOffset;
        Effects = effects;
        PointerPlans = pointerPlans;
        BytesUsed = bytesUsed;
    }

    /// <summary>
    /// Writes this package into its reserved grant. Pointer plans must already have been queued by
    /// the installer, so writing can safely begin after every desired mechanic has been planned.
    /// Repeated calls are an idempotent no-op.
    /// </summary>
    public bool TryWrite(out string error)
    {
        error = string.Empty;

        if (written)
            return true;

        if (!session.TryWriteCode(
                Grant,
                payload,
                out error))
        {
            return false;
        }

        written = true;
        return true;
    }
}

/// <summary>
/// Plans the canonical CRO call-setup package used by timed battle mechanics:
/// <code>
/// handler:
///   MOV R1, #slotCount
///   STR R1, [R0]
///   LDR R0, [PC]
///   BX  LR
///   .word timingTable        // loader relocation
///
/// timingTable:
///   byte timing; byte pad[3]; .word function
///   ...
/// </code>
/// <para>
/// The complete package uses one contiguous grant, so a partially-sized mechanic cannot fragment
/// the code allocator. All relocation write slots are inside allocator-owned blank bytes.
/// </para>
/// </summary>
public static class CroMechanicInstaller
{
    private const int HandlerSize = 20;
    private const int TimingEntrySize = 8;

    /// <summary>
    /// Plans one mechanic package without writing any payload bytes.
    /// All calls to this method for a session should happen before the first
    /// <see cref="CroMechanicPlan.TryWrite"/>.
    /// </summary>
    public static bool TryPlan(
        CroPatchSession session,
        CroMechanicRequest request,
        out CroMechanicPlan plan,
        out string error)
    {
        plan = null;
        error = string.Empty;

        if (session is null)
        {
            error = "CRO patch session is null";
            return false;
        }

        if (request is null)
        {
            error = "mechanic request is null";
            return false;
        }

        if (session.Finalized)
        {
            error = "the CRO patch session is already finalized";
            return false;
        }

        if (session.WritesStarted)
        {
            error =
                "cannot plan mechanics after payload writes have started; plan every mechanic first";
            return false;
        }

        if (request.Effects is null ||
            request.Effects.Count == 0)
        {
            error = "mechanic has no timed effects";
            return false;
        }

        // Phase 1 emits MOV R1,#imm with an unrotated ARM immediate.
        if (request.Effects.Count > byte.MaxValue)
        {
            error =
                $"mechanic has {request.Effects.Count} effects; phase 1 supports at most 255";
            return false;
        }

        var timings = new HashSet<byte>();
        var capturedExisting =
            new CroPatchAddress?[request.Effects.Count];

        for (int i = 0; i < request.Effects.Count; i++)
        {
            var effect = request.Effects[i];

            if (effect is null)
            {
                error = $"effect #{i} is null";
                return false;
            }

            if (!timings.Add(effect.Timing))
            {
                error =
                    $"timing 0x{effect.Timing:X2} appears more than once in the same mechanic";
                return false;
            }

            bool hasCode =
                effect.Code is { Length: > 0 };

            if (hasCode == effect.ReusesExisting)
            {
                error =
                    $"effect '{EffectName(effect, i)}' must provide exactly one of Code or ExistingFunction";
                return false;
            }

            if (!effect.ReusesExisting)
                continue;

            if (!TryCaptureAddress(
                    session.Map,
                    effect.ExistingFunction,
                    requiredBytes: 1,
                    out CroPatchAddress captured))
            {
                error =
                    $"effect '{EffectName(effect, i)}' existing function 0x{effect.ExistingFunction:X6} " +
                    "is not inside a declared file-backed CRO segment";
                return false;
            }

            capturedExisting[i] = captured;
        }

        if (!TryComputeLayout(
                request,
                out int totalBytes,
                out int timingRelative,
                out int[] functionRelatives,
                out error))
        {
            return false;
        }

        string purpose =
            string.IsNullOrWhiteSpace(request.Name)
                ? "mechanic-package"
                : "mechanic:" + request.Name.Trim();

        if (!session.TryAllocateCode(
                totalBytes,
                purpose,
                out CroCodeGrant grant,
                out error))
        {
            return false;
        }

        uint handlerOffset =
            grant.Offset;

        uint timingTableOffset =
            checked(grant.Offset + (uint)timingRelative);

        var placements =
            new List<CroMechanicEffectPlacement>(
                request.Effects.Count);

        var pointerPlans =
            new List<CroPatchPointerPlan>(
                request.Effects.Count + 1);

        var functionTargets =
            new uint[request.Effects.Count];

        for (int i = 0; i < request.Effects.Count; i++)
        {
            var effect = request.Effects[i];

            uint target;

            if (effect.ReusesExisting)
            {
                if (!TryResolveAddress(
                        session.Map,
                        capturedExisting[i]!.Value,
                        out target))
                {
                    error =
                        $"effect '{EffectName(effect, i)}' existing function no longer resolves after allocation";
                    return false;
                }
            }
            else
            {
                target =
                    checked(
                        grant.Offset +
                        (uint)functionRelatives[i]);
            }

            functionTargets[i] =
                target;

            placements.Add(
                new CroMechanicEffectPlacement(
                    Timing: effect.Timing,
                    Name: EffectName(effect, i),
                    FunctionOffset: target,
                    ReusesExisting: effect.ReusesExisting,
                    CodeLength:
                        effect.ReusesExisting
                            ? 0
                            : effect.Code.Length));
        }

        // Queue every pointer before any payload is written.
        if (!session.TryQueuePointer(
                handlerOffset + 16u,
                timingTableOffset,
                purpose + ":timing-table",
                out CroPatchPointerPlan handlerPointer,
                out error))
        {
            error =
                "could not queue handler timing-table pointer: " +
                error;
            return false;
        }

        pointerPlans.Add(handlerPointer);

        for (int i = 0; i < request.Effects.Count; i++)
        {
            uint write =
                checked(
                    timingTableOffset +
                    (uint)(i * TimingEntrySize) +
                    4u);

            if (!session.TryQueuePointer(
                    write,
                    functionTargets[i],
                    purpose + $":timing-0x{request.Effects[i].Timing:X2}",
                    out CroPatchPointerPlan effectPointer,
                    out error))
            {
                error =
                    $"could not queue pointer for timing 0x{request.Effects[i].Timing:X2}: " +
                    error;
                return false;
            }

            pointerPlans.Add(effectPointer);
        }

        byte[] payload =
            new byte[grant.Size];

        WriteHandler(
            payload,
            request.Effects.Count);

        for (int i = 0; i < request.Effects.Count; i++)
        {
            int entry =
                timingRelative +
                (i * TimingEntrySize);

            payload[entry] =
                request.Effects[i].Timing;

            // entry + 4 stays zero; the CRO loader owns that word.
        }

        for (int i = 0; i < request.Effects.Count; i++)
        {
            var effect =
                request.Effects[i];

            if (effect.ReusesExisting)
                continue;

            effect.Code.CopyTo(
                payload,
                functionRelatives[i]);
        }

        plan =
            new CroMechanicPlan(
                session: session,
                name: request.Name ?? string.Empty,
                grant: grant,
                handlerOffset: handlerOffset,
                timingTableOffset: timingTableOffset,
                effects: placements.ToArray(),
                pointerPlans: pointerPlans.ToArray(),
                payload: payload,
                bytesUsed: totalBytes);

        return true;
    }

    private static bool TryComputeLayout(
        CroMechanicRequest request,
        out int totalBytes,
        out int timingRelative,
        out int[] functionRelatives,
        out string error)
    {
        totalBytes = 0;
        timingRelative = 0;
        functionRelatives =
            new int[request.Effects.Count];

        error = string.Empty;

        try
        {
            int cursor =
                HandlerSize;

            timingRelative =
                Align4(cursor);

            cursor =
                checked(
                    timingRelative +
                    (request.Effects.Count * TimingEntrySize));

            for (int i = 0; i < request.Effects.Count; i++)
            {
                var effect =
                    request.Effects[i];

                if (effect.ReusesExisting)
                {
                    functionRelatives[i] = -1;
                    continue;
                }

                cursor =
                    Align4(cursor);

                functionRelatives[i] =
                    cursor;

                cursor =
                    checked(
                        cursor +
                        effect.Code.Length);
            }

            totalBytes =
                Align4(cursor);

            return true;
        }
        catch (OverflowException)
        {
            error =
                "mechanic package size overflowed";
            return false;
        }
    }

    private static void WriteHandler(
        byte[] payload,
        int slotCount)
    {
        // slotCount <= 255 was validated above, so ARM's plain imm8 form is sufficient.
        uint mov =
            0xE3A01000u |
            (uint)slotCount;

        WriteU32(
            payload,
            0,
            mov);

        WriteU32(
            payload,
            4,
            0xE5801000u); // STR R1, [R0]

        WriteU32(
            payload,
            8,
            0xE59F0000u); // LDR R0, [PC] -> literal at +0x10

        WriteU32(
            payload,
            12,
            0xE12FFF1Eu); // BX LR

        WriteU32(
            payload,
            16,
            0u); // timing-table pointer, loader-owned
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
                    Relative:
                        absolute -
                        segment.Start,
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

        var segment =
            map.Segments[planned.Segment];

        if (!segment.FileBacked)
            return false;

        ulong relativeEnd =
            (ulong)planned.Relative +
            (uint)planned.RequiredBytes;

        if (relativeEnd > segment.Size)
            return false;

        ulong resolved =
            (ulong)segment.Start +
            planned.Relative;

        if (resolved > uint.MaxValue ||
            resolved + (uint)planned.RequiredBytes > (ulong)int.MaxValue + 1u)
        {
            return false;
        }

        absolute =
            (uint)resolved;

        return true;
    }

    private static int Align4(
        int value) =>
        checked(
            (value + 3) &
            ~3);

    private static string EffectName(
        CroMechanicEffectSpec effect,
        int index) =>
        string.IsNullOrWhiteSpace(effect.Name)
            ? $"effect#{index}"
            : effect.Name.Trim();

    private static void WriteU32(
        byte[] data,
        int offset,
        uint value) =>
        BitConverter.GetBytes(value)
            .CopyTo(data, offset);
}
