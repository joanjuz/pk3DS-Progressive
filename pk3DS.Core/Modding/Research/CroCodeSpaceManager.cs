using System;
using System.Collections.Generic;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// Plans one or more relocation-safe CRO code allocations and grows segment 0 when needed.
/// <para>
/// The manager owns a private clone of the CRO. Callers cannot mutate that image while planning,
/// which means grants remain blank and can be re-reserved safely if a later request forces a
/// segment expansion and the underlying <see cref="CroCodeAllocator"/> has to be rebuilt.
/// </para>
/// <para>
/// Allocation order is:
/// existing expansion-created .text arenas, then the ordinary executable page padding, then one
/// calculated segment expansion large enough to guarantee a fresh contiguous run for the request.
/// </para>
/// </summary>
public sealed class CroCodeSpaceManager
{
    private const int PageSize = 0x1000;

    private byte[] image;
    private CroCodeAllocator allocator;

    private readonly List<CroCodeGrant> grants = [];
    private readonly List<CroFreeRange> expandedArenas = [];
    private readonly List<CroCodeExpansionReport> expansions = [];

    private bool writesStarted;

    public int OriginalLength { get; }
    public int CurrentLength => image.Length;
    public bool WasExpanded => expansions.Count != 0;
    public bool WritesStarted => writesStarted;

    public CroRelocationMap Map => allocator.Map;
    public IReadOnlyList<CroCodeGrant> Granted => grants;
    public IReadOnlyList<CroFreeRange> ExpandedArenas => expandedArenas;
    public IReadOnlyList<CroCodeExpansionReport> Expansions => expansions;

    private CroCodeSpaceManager(
        byte[] image,
        CroCodeAllocator allocator)
    {
        this.image = image;
        this.allocator = allocator;
        OriginalLength = image.Length;
    }

    /// <summary>
    /// Creates a planning manager over a private clone of <paramref name="cro"/>.
    /// The caller's bytes are never changed by this class.
    /// </summary>
    public static bool TryCreate(
        byte[] cro,
        out CroCodeSpaceManager manager,
        out string error)
    {
        manager = null;
        error = string.Empty;

        if (cro is null)
        {
            error = "CRO data is null.";
            return false;
        }

        byte[] working = (byte[])cro.Clone();

        if (!CroCodeAllocator.TryCreate(
                working,
                out var allocator,
                out error))
        {
            return false;
        }

        manager = new CroCodeSpaceManager(
            working,
            allocator);

        return true;
    }

    /// <summary>
    /// Writes one payload into a grant previously returned by this manager.
    /// All allocations must be completed before the first write, because a later segment expansion
    /// rebuilds the allocator from blank planned ranges.
    /// </summary>
    public bool TryWrite(
        CroCodeGrant grant,
        ReadOnlySpan<byte> payload,
        out string error)
    {
        error = string.Empty;

        if (!grant.Success || !grants.Contains(grant))
        {
            error = "the supplied grant was not issued by this manager";
            return false;
        }

        if (payload.Length > grant.Size)
        {
            error =
                $"payload has 0x{payload.Length:X} bytes but the grant holds only 0x{grant.Size:X}";
            return false;
        }

        ulong end = (ulong)grant.Offset + (uint)grant.Size;
        if (end > (ulong)image.Length)
        {
            error = "the grant lies outside the current CRO image";
            return false;
        }

        // Deterministic fill for unused aligned bytes.
        Array.Clear(
            image,
            checked((int)grant.Offset),
            grant.Size);

        payload.CopyTo(
            image.AsSpan(
                checked((int)grant.Offset),
                payload.Length));

        writesStarted = true;
        return true;
    }

    /// <summary>
    /// Builds a detached final CRO image and refreshes all four integrity hashes after any payload
    /// writes. The manager keeps its private working image so the returned array may be modified
    /// freely by the caller.
    /// </summary>
    public bool TryBuildImage(
        out byte[] built,
        out string error)
    {
        built = (byte[])image.Clone();

        if (!CroSegmentExpander.TryUpdateHashes(
                built,
                out error))
        {
            built = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Convenience form for callers that prefer an exception if final integrity hashing fails.
    /// </summary>
    public byte[] BuildImage()
    {
        if (!TryBuildImage(
                out byte[] built,
                out string error))
        {
            throw new InvalidOperationException(
                "Could not finalize CRO image: " + error);
        }

        return built;
    }

    /// <summary>
    /// Allocates <paramref name="size"/> bytes. Existing known code space is consumed first.
    /// If none fits, segment 0 is expanded once by enough whole pages to guarantee a fresh run,
    /// the relocation-aware allocator is rebuilt, previous grants are restored, and allocation
    /// is retried.
    /// </summary>
    public bool TryAllocate(
        int size,
        string purpose,
        out CroCodeGrant grant,
        out string error)
    {
        grant = default;
        error = string.Empty;

        if (writesStarted)
        {
            error =
                "cannot allocate more code space after payload writes have started; obtain every grant first";
            return false;
        }

        if (size <= 0)
        {
            error = "requested size must be positive";
            return false;
        }

        if (!TryAlign4(size, out int need, out error))
            return false;

        if (TryAllocateKnownSpace(
                need,
                purpose,
                out grant))
        {
            grants.Add(grant);
            return true;
        }

        if (!TryCalculateExpansion(
                need,
                out int bytesToAdd,
                out error))
        {
            return false;
        }

        if (!CroSegmentExpander.TryExpandCodeSegment(
                image,
                bytesToAdd,
                out byte[] expanded,
                out CroCodeExpansionReport report,
                out error))
        {
            return false;
        }

        if (!TryRebuildAllocator(
                expanded,
                out var rebuilt,
                out error))
        {
            return false;
        }

        image = expanded;
        allocator = rebuilt;
        expansions.Add(report);
        expandedArenas.Add(report.ExpandedCodeRange);

        if (!TryAllocateKnownSpace(
                need,
                purpose,
                out grant))
        {
            error =
                $"CRO grew by 0x{bytesToAdd:X}, but no relocation-safe run of 0x{need:X} bytes was available afterwards";
            return false;
        }

        grants.Add(grant);
        return true;
    }

    /// <summary>
    /// Convenience overload when the caller does not need a purpose label.
    /// </summary>
    public bool TryAllocate(
        int size,
        out CroCodeGrant grant,
        out string error) =>
        TryAllocate(
            size,
            string.Empty,
            out grant,
            out error);

    private bool TryAllocateKnownSpace(
        int need,
        string purpose,
        out CroCodeGrant grant)
    {
        // Prefer already-created .text arenas. This packs expanded code together and preserves
        // the small executable tail padding for legacy callers that specifically depend on it.
        for (int i = 0; i < expandedArenas.Count; i++)
        {
            grant = allocator.AllocateInRange(
                expandedArenas[i],
                need,
                $"expanded-code#{i + 1}",
                purpose ?? string.Empty);

            if (grant.Success)
                return true;
        }

        grant = allocator.Allocate(
            need,
            purpose ?? string.Empty);

        return grant.Success;
    }

    private bool TryCalculateExpansion(
        int need,
        out int bytesToAdd,
        out string error)
    {
        bytesToAdd = 0;
        error = string.Empty;

        uint codeEnd = allocator.Map.CodeEnd;
        uint nextSegmentStart = uint.MaxValue;

        foreach (var segment in allocator.Map.Segments)
        {
            if (!segment.FileBacked ||
                segment.Start <= codeEnd)
            {
                continue;
            }

            if (segment.Start < nextSegmentStart)
                nextSegmentStart = segment.Start;
        }

        if (nextSegmentStart == uint.MaxValue)
        {
            error = "no file-backed segment follows .text";
            return false;
        }

        if (nextSegmentStart < codeEnd)
        {
            error = "the file-backed segment layout overlaps .text";
            return false;
        }

        // Expander inserts bytes at the next segment's start. The old gap between CodeEnd and
        // that insertion point remains part of the newly enlarged .text, but it may contain
        // existing patches or relocation targets. Add that gap to the requested size so the
        // physically inserted zero bytes alone guarantee one contiguous run of at least `need`.
        ulong gap = (ulong)nextSegmentStart - codeEnd;
        ulong required = gap + (uint)need;

        ulong rounded =
            (required + (PageSize - 1u)) &
            ~(ulong)(PageSize - 1u);

        if (rounded == 0 ||
            rounded > int.MaxValue)
        {
            error =
                $"required CRO expansion 0x{rounded:X} is too large";
            return false;
        }

        bytesToAdd = (int)rounded;
        return true;
    }

    private bool TryRebuildAllocator(
        byte[] expanded,
        out CroCodeAllocator rebuilt,
        out string error)
    {
        rebuilt = null;
        error = string.Empty;

        if (!CroCodeAllocator.TryCreate(
                expanded,
                out rebuilt,
                out error))
        {
            return false;
        }

        // Existing grants were made against this manager's private, still-blank working image.
        // Physical expansion happens after them, so their file offsets do not change. Re-register
        // them in the fresh allocator to prevent a later allocation from reusing planned bytes.
        foreach (var prior in grants)
        {
            if (!rebuilt.TryReserve(
                    prior.Offset,
                    prior.Size,
                    prior.Purpose,
                    out _,
                    out string reserveError))
            {
                error =
                    $"could not restore prior grant 0x{prior.Offset:X6}+0x{prior.Size:X} after CRO expansion: {reserveError}";
                return false;
            }
        }

        return true;
    }

    private static bool TryAlign4(
        int size,
        out int aligned,
        out string error)
    {
        aligned = 0;
        error = string.Empty;

        if (size <= 0)
        {
            error = "requested size must be positive";
            return false;
        }

        if (size > int.MaxValue - 3)
        {
            error = "requested size is too large to align";
            return false;
        }

        aligned = (size + 3) & ~3;
        return true;
    }
}
