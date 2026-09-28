using pk3DS.Core.Structures;
using System;
using System.Collections.Generic;
using System.Linq;

namespace pk3DS.Core.Randomizers;

// https://twitter.com/Drayano60/status/807297858244411397
// ORAS: 10682 moves learned on levelup/birth.
// 5593 are STAB. 52.3% are STAB.
// Steelix learns the most @ 25 (so many level 1)!
// Move relearner ingame glitch fixed (52 tested), but keep below 75
public class LearnsetRandomizer : IRandomizer
{
    private readonly MoveRandomizer moverand;
    private readonly GameConfig Config;
    private readonly Learnset[] Learnsets;

    public LearnsetRandomizer(GameConfig config, Learnset[] sets)
    {
        Config = config;
        moverand = new MoveRandomizer(config);
        Learnsets = sets;
        STABPercent = 52.3m;
    }

    public bool Expand = true;
    public int ExpandTo = 25;
    public bool Spread = true;
    public int SpreadTo = 75;
    public bool STABFirst = false; // retained for compatibility; no special first move is forced
    public bool Learn4Level1 = false;
    public bool OrderByPower = false; // learnset order remains randomized

    public bool STAB { set => moverand.rSTAB = value; }
    public IList<int> BannedMoves { set => moverand.BannedMoves = value; }
    public decimal STABPercent { set => moverand.rSTABPercent = value; }

    public void Execute()
    {
        for (var i = 0; i < Learnsets.Length; i++)
            Randomize(Learnsets[i], i);
    }

    private void Randomize(Learnset set, int index)
    {
        int[] moves = GetRandomMoves(set.Count, index);
        int[] levels = GetRandomLevels(set, moves.Length);

        if (Learn4Level1)
        {
            for (int i = 0; i < Math.Min(4, levels.Length); ++i)
                levels[i] = 1;
        }

        set.Moves = moves;
        set.Levels = levels;
    }

    private int[] GetRandomLevels(Learnset set, int count)
    {
        int[] levels = new int[count];
        if (count == 0)
            return levels;
        if (Spread)
        {
            levels[0] = 1;
            decimal increment = SpreadTo / (decimal)count;
            for (int i = 1; i < count; i++)
                levels[i] = (int)(i * increment);
            return levels;
        }
        if (levels.Length == count && levels.Length == set.Levels.Length)
            return set.Levels; // don't modify

        var exist = set.Levels;
        int lastlevel = Math.Min(1, exist.LastOrDefault());
        exist.CopyTo(levels, 0);
        for (int i = exist.Length; i < levels.Length; i++)
            levels[i] = Math.Max(100, lastlevel + (exist.Length - i + 1));

        return levels;
    }

    private int[] GetRandomMoves(int count, int index)
    {
        count = Expand ? ExpandTo : count;
        if (count == 0)
            return [];

        // Randomize the complete learnset in one pool. STAB only affects the requested
        // percentage; there is no longer a special/forced level-1 move.
        var moves = moverand.GetRandomLearnset(index, count);

        // Keep the resulting learnset in random order instead of sorting damaging
        // moves from lower to higher power.
        Util.Shuffle(moves);
        return moves;
    }

    public int[] GetHighPoweredMoves(int species, int form, int count = 4)
    {
        int index = Config.Personal.GetFormIndex(species, form);
        var moves = Learnsets[index].Moves.OrderByDescending(move => Config.Moves[move].Power).Distinct().Take(count).ToArray();
        Array.Resize(ref moves, count);
        return moves;
    }

    public int[] GetCurrentMoves(int species, int form, int level, int count = 4)
    {
        int i = Config.Personal.GetFormIndex(species, form);
        var moves = Learnsets[i].GetEncounterMoves(level);
        Array.Resize(ref moves, count);
        return moves;
    }

    /// <summary>
    /// Returns every distinct level-up move the requested species/form can
    /// naturally know at or before the supplied level.
    /// </summary>
    public int[] GetMovesUpToLevel(int species, int form, int level)
    {
        int i = Config.Personal.GetFormIndex(species, form);
        if ((uint)i >= (uint)Learnsets.Length)
            return [];

        int maxLevel = Math.Clamp(level, 1, 100);
        return Learnsets[i]
            .GetMoves(maxLevel)
            .Where(move => move > 0)
            .Distinct()
            .ToArray();
    }
}