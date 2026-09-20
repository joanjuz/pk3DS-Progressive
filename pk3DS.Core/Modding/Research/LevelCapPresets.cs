using System;
using System.Collections.Generic;
using System.Linq;

namespace pk3DS.Core.Modding.Research;

public static class LevelCapShifts
{
    public static readonly int[] All = [-10, -5, -3, 0, 3, 5, 10];
    public static readonly int StandardIndex = Array.IndexOf(All, 0);

    public static string Describe(int shift) => shift switch
    {
        0 => "Standard (research values)",
        > 0 => $"Relaxed (+{shift} levels)",
        _ => $"Strict ({shift} levels)",
    };
}

public static class LevelCapPresets
{
    public static LevelCapTable Build(int shift, byte finalCap)
    {
        finalCap = (byte)Math.Clamp((int)finalCap, 5, (int)LevelCapTable.HardCeiling);

        var basis = LevelCapTable.Default();
        int researchFinal = basis.Entries[^1].Cap;
        double scale = finalCap / (double)researchFinal;

        var entries = new List<LevelCapEntry>(basis.Entries.Count);
        foreach (var e in basis.Entries)
        {
            int cap = (int)Math.Round(e.Cap * scale) + shift;
            entries.Add(e with { Cap = (byte)Math.Clamp(cap, 2, finalCap) });
        }

        for (int i = 1; i < entries.Count; i++)
        {
            if (entries[i].Cap < entries[i - 1].Cap)
                entries[i] = entries[i] with { Cap = entries[i - 1].Cap };
        }

        // Keep every story checkpoint even when rounding creates equal adjacent caps.
        // Equal caps are useful: they delay the next increase until the later flag is set.
        return new LevelCapTable { Entries = entries };
    }

    public static string Summarise(LevelCapTable table)
    {
        if (table?.Entries is not { Count: > 0 })
            return "no checkpoints";

        var first = table.Entries[0];
        var last = table.Entries[^1];
        return $"{table.Entries.Count} checkpoints - Lv{first.Cap} at \"{first.Label}\" rising to Lv{last.Cap} at \"{last.Label}\"";
    }

    public static string CapSequence(LevelCapTable table) =>
        table?.Entries is not { Count: > 0 }
            ? string.Empty
            : string.Join(", ", table.Entries.Select(e => e.Cap));
}
