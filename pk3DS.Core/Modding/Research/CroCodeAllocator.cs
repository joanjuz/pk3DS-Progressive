using System;
using System.Collections.Generic;
using System.Linq;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// One allocation handed out by <see cref="CroCodeAllocator"/>.
/// Size is 4-byte aligned and may therefore be slightly larger than requested.
/// </summary>
public readonly record struct CroCodeGrant(
    uint Offset,
    int Size,
    string Source,
    string Purpose)
{
    public bool Success => Size > 0;

    public static CroCodeGrant Fail(string reason, string purpose = "") =>
        new(0, 0, reason, purpose ?? string.Empty);
}

/// <summary>
/// Reserves relocation-safe executable space inside a CRO.
/// <para>
/// The default arena is the file-backed padding between the end of segment 0 (.text)
/// and the end of that executable page, clipped before the next file-backed segment.
/// This is the same class of space already validated in-game by Player Level Caps.
/// </para>
/// <para>
/// Allocations are tracked for the lifetime of the allocator, so multiple patches planned
/// before any bytes are written cannot receive the same address. Arbitrary caller-supplied
/// arenas are also supported for the future CRO Expander, which will be able to hand the
/// allocator the newly-created code range after growing segment 0.
/// </para>
/// </summary>
public sealed class CroCodeAllocator
{
    private readonly byte[] cro;
    private readonly CroRelocationMap map;
    private readonly List<CroCodeGrant> granted = [];

    public CroRelocationMap Map => map;
    public uint CodeStart => map.CodeStart;
    public uint CodeEnd => map.CodeEnd;
    public uint ExecutablePaddingStart => map.CodeEnd;
    public uint ExecutablePaddingEnd { get; }
    public IReadOnlyList<CroCodeGrant> Granted => granted;

    private CroCodeAllocator(
        byte[] cro,
        CroRelocationMap map,
        uint executablePaddingEnd)
    {
        this.cro = cro;
        this.map = map;
        ExecutablePaddingEnd = executablePaddingEnd;
    }

    public static bool TryCreate(
        byte[] cro,
        out CroCodeAllocator allocator,
        out string error)
    {
        allocator = null;
        error = string.Empty;

        if (!CroRelocationMap.TryCreate(cro, out var map, out error))
            return false;

        uint codeEnd = map.CodeEnd;
        if (codeEnd >= cro.Length)
        {
            error = "CRO .text ends at or past the end of the file.";
            return false;
        }

        ulong rounded = ((ulong)codeEnd + 0xFFFUL) & ~0xFFFUL;
        uint paddingEnd = (uint)Math.Min(rounded, (ulong)cro.Length);

        foreach (var segment in map.Segments)
        {
            if (!segment.FileBacked ||
                segment.Start <= codeEnd ||
                segment.Start >= paddingEnd)
            {
                continue;
            }

            paddingEnd = segment.Start;
        }

        if (paddingEnd < codeEnd)
        {
            error = "CRO executable padding window is invalid.";
            return false;
        }

        allocator = new CroCodeAllocator(cro, map, paddingEnd);
        return true;
    }

    /// <summary>
    /// Allocates from the known executable page padding immediately after .text.
    /// </summary>
    public CroCodeGrant Allocate(int size, string purpose = "") =>
        AllocateInRange(
            new CroFreeRange(
                ExecutablePaddingStart,
                checked((int)(ExecutablePaddingEnd - ExecutablePaddingStart))),
            size,
            "executable-page-padding",
            purpose);

    /// <summary>
    /// Allocates from a caller-supplied arena. This is the entry point the future CRO Expander
    /// can use after creating new code space.
    /// </summary>
    public CroCodeGrant AllocateInRange(
        CroFreeRange arena,
        int size,
        string source = "caller-range",
        string purpose = "")
    {
        if (size <= 0)
            return CroCodeGrant.Fail("requested size must be positive", purpose);

        int need = Align4(size);
        if (arena.Length <= 0)
            return CroCodeGrant.Fail($"{source}: arena is empty", purpose);

        ulong arenaEnd64 = (ulong)arena.Offset + (uint)arena.Length;
        if (arenaEnd64 > (ulong)cro.Length)
            return CroCodeGrant.Fail($"{source}: arena extends past the end of the CRO", purpose);

        uint arenaEnd = (uint)arenaEnd64;
        uint cursor = Align4(arena.Offset);

        while ((ulong)cursor + (uint)need <= arenaEnd)
        {
            uint? candidate = map.FindFirstFreeRun(cursor, arenaEnd, need);
            if (candidate is not { } at)
                break;

            var collision = granted.FirstOrDefault(
                grant => RangesOverlap(at, need, grant.Offset, grant.Size));

            if (!collision.Success)
            {
                var grant = new CroCodeGrant(
                    at,
                    need,
                    source,
                    purpose ?? string.Empty);

                granted.Add(grant);
                return grant;
            }

            ulong next = (ulong)collision.Offset + (uint)collision.Size;
            if (next > uint.MaxValue)
                break;

            cursor = Align4((uint)next);
        }

        return CroCodeGrant.Fail(
            $"{source}: no relocation-safe blank run of {need} bytes",
            purpose);
    }

    /// <summary>
    /// Marks a range as already promised by this planning session.
    /// Useful when a caller knows about an existing planned block whose bytes have not been
    /// written yet. The range must itself be relocation-safe and blank.
    /// </summary>
    public bool TryReserve(
        uint offset,
        int size,
        string purpose,
        out CroCodeGrant reservation,
        out string error)
    {
        reservation = default;
        error = string.Empty;

        if (size <= 0)
        {
            error = "reserved size must be positive";
            return false;
        }

        int need = Align4(size);
        if (!map.IsRangeFree(offset, need))
        {
            error = $"0x{offset:X6}+0x{need:X} is not relocation-safe blank space";
            return false;
        }

        if (granted.Any(grant =>
                RangesOverlap(offset, need, grant.Offset, grant.Size)))
        {
            error = $"0x{offset:X6}+0x{need:X} overlaps an existing allocator grant";
            return false;
        }

        reservation = new CroCodeGrant(
            offset,
            need,
            "reserved",
            purpose ?? string.Empty);

        granted.Add(reservation);
        return true;
    }

    public int CountReferences(uint offset, int size) =>
        map.CountReferencesInRange(offset, size);

    private static bool RangesOverlap(
        uint leftOffset,
        int leftSize,
        uint rightOffset,
        int rightSize)
    {
        ulong leftEnd = (ulong)leftOffset + (uint)leftSize;
        ulong rightEnd = (ulong)rightOffset + (uint)rightSize;

        return (ulong)leftOffset < rightEnd &&
               (ulong)rightOffset < leftEnd;
    }

    private static int Align4(int value)
    {
        if (value > int.MaxValue - 3)
            throw new ArgumentOutOfRangeException(nameof(value));

        return (value + 3) & ~3;
    }

    private static uint Align4(uint value)
    {
        if (value > uint.MaxValue - 3)
            throw new ArgumentOutOfRangeException(nameof(value));

        return (value + 3u) & ~3u;
    }
}
