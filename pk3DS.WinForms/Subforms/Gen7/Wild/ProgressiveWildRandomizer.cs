using System;
using System.Collections.Generic;
using System.Linq;

using pk3DS.Core;
using pk3DS.Core.Randomizers;

namespace pk3DS.WinForms;

public class ProgressiveWildRandomizer
{
    public SpeciesRandomizer RandSpec { private get; set; }
    public FormRandomizer RandForm { private get; set; }

    public IReadOnlyList<ProgressiveBSTRule> Rules { private get; set; }

    public int TableRandomizationOption { private get; set; }
    public decimal LevelAmplifier { private get; set; }
    public bool ModifyLevel { private get; set; }
    public bool USUM { private get; set; }
    public bool AllCanCallAllies { private get; set; }

    private readonly Dictionary<PoolKey, SpeciesTierPool> Pools = [];

    public void Execute(
        IEnumerable<Area7> areas,
        LazyGARCFile encdata)
    {
        GetTableRandSettings(
            (RandOption)TableRandomizationOption,
            out int slotStart,
            out int slotStop,
            out bool copy);

        Area7[] areaArray =
            areas as Area7[] ?? areas.ToArray();

        foreach (var area in areaArray)
        {
            int progressionLevel =
                GetAreaProgressionLevel(area);

            ProgressiveBSTRule rule =
                GetRule(progressionLevel);

            SpeciesTierPool pool =
                GetPool(rule);

            foreach (var table in area.Tables)
            {
                RandomizeTable7(
                    table,
                    slotStart,
                    slotStop,
                    pool);

                if (copy)
                    table.CopySlotsToSOS();

                if (AllCanCallAllies)
                    FillEmptySOSSlots(table, pool);

                // The area's progression tier is calculated before any
                // optional level modification. Encounter level scaling
                // therefore remains independent from species progression.
                if (ModifyLevel)
                {
                    table.MinLevel =
                        Randomizer.GetModifiedLevel(
                            table.MinLevel,
                            LevelAmplifier);

                    table.MaxLevel =
                        Randomizer.GetModifiedLevel(
                            table.MaxLevel,
                            LevelAmplifier);
                }

                table.Write();
            }

            encdata[area.FileNumber] =
                Area7.GetDayNightTableBinary(
                    area.Tables);
        }
    }

    private int GetAreaProgressionLevel(
        Area7 area)
    {
        int progressionLevel = 0;

        for (int i = 0; i < area.Tables.Count; i++)
        {
            if (!IsProgressionTableUsable(
                    area,
                    i))
            {
                continue;
            }

            var table = area.Tables[i];

            int level = Math.Max(
                table.MinLevel,
                table.MaxLevel);

            progressionLevel =
                Math.Max(
                    progressionLevel,
                    level);
        }

        if (progressionLevel > 0)
            return Math.Clamp(
                progressionLevel,
                1,
                100);

        // Safe fallback for encounter areas that have no usable
        // progression table after filtering.
        progressionLevel = area.Tables
            .Select(table =>
                Math.Max(
                    table.MinLevel,
                    table.MaxLevel))
            .DefaultIfEmpty(1)
            .Max();

        return Math.Clamp(
            progressionLevel <= 0 ? 1 : progressionLevel,
            1,
            100);
    }

    private bool IsProgressionTableUsable(
        Area7 area,
        int tableIndex)
    {
        var table = area.Tables[tableIndex];

        if (table.MinLevel <= 0 &&
            table.MaxLevel <= 0)
        {
            return false;
        }

        var regular = table.Encounter7s[0];

        if (regular.Length == 0 ||
            regular.All(slot => slot.Species == 0))
        {
            return false;
        }

        // Known placeholder tables used internally by Gen 7.
        if (regular.All(slot => slot.Species == 731))
            return false;

        if (area.Zones is null ||
            area.Zones.Length == 0)
        {
            return true;
        }

        var ignored = USUM
            ? Gen7SlotDumper.InaccessibleUnused_USUM
            : Gen7SlotDumper.InaccessibleUnused_SM;

        int tableNumber =
            (tableIndex >> 1) + 1;

        // Area7 can be shared by more than one ZoneData7.
        // A table counts as valid if at least one attached zone
        // can actually use it.
        foreach (var zone in area.Zones)
        {
            if (!ignored.TryGetValue(
                    zone.Index,
                    out int[] skipped))
            {
                return true;
            }

            if (!skipped.Contains(tableNumber))
                return true;
        }

        return false;
    }

    private ProgressiveBSTRule GetRule(
        int progressionLevel)
    {
        var rule = Rules
            .FirstOrDefault(candidate =>
                progressionLevel >= candidate.MinLevel &&
                progressionLevel <= candidate.MaxLevel);

        if (rule is null)
        {
            throw new InvalidOperationException(
                $"No Progressive Wild BST rule covers " +
                $"area level {progressionLevel}.");
        }

        return rule;
    }

    private SpeciesTierPool GetPool(
        ProgressiveBSTRule rule)
    {
        var key = new PoolKey(
            rule.MinBST,
            rule.MaxBST,
            rule.FullRandom);

        if (Pools.TryGetValue(
                key,
                out var existing))
        {
            return existing;
        }

        int[] species = rule.FullRandom
            ? RandSpec.GetAllowedSpeciesPool()
            : RandSpec.GetSpeciesPoolByBST(
                rule.MinBST,
                rule.MaxBST);

        if (species.Length == 0)
        {
            throw new InvalidOperationException(
                rule.FullRandom
                    ? "No species are available with the selected generation filters."
                    : $"No species are available for BST " +
                      $"{rule.MinBST}-{rule.MaxBST}. " +
                      "Adjust the Progressive Wild ranges or generation filters.");
        }

        var pool = new SpeciesTierPool(species);
        Pools.Add(key, pool);
        return pool;
    }

    private void RandomizeTable7(
        EncounterTable table,
        int slotStart,
        int slotStop,
        SpeciesTierPool pool)
    {
        int end = slotStop < 0
            ? table.Encounter7s.Length
            : slotStop;

        for (int s = slotStart; s < end; s++)
        {
            var encounterSet =
                table.Encounter7s[s];

            foreach (var enc in encounterSet
                         .Where(encounter =>
                             encounter.Species != 0))
            {
                int oldSpecies =
                    (int)enc.Species;

                int newSpecies =
                    pool.Next(oldSpecies);

                enc.Species =
                    (uint)newSpecies;

                enc.Forme =
                    (uint)RandForm.GetRandomForme(
                        newSpecies);
            }
        }
    }

    private void FillEmptySOSSlots(
        EncounterTable table,
        SpeciesTierPool pool)
    {
        var regular = table.Encounter7s[0];

        // Encounter7s[8] is AdditionalSOS/weather. Do not create weather
        // allies where the original table intentionally has no entry.
        for (int s = 1; s < table.Encounter7s.Length - 1; s++)
        {
            var sos = table.Encounter7s[s];
            for (int i = 0; i < sos.Length && i < regular.Length; i++)
            {
                if (sos[i].Species != 0 || regular[i].Species == 0)
                    continue;

                int species = pool.Next((int)regular[i].Species);
                sos[i].Species = (uint)species;
                sos[i].Forme = (uint)RandForm.GetRandomForme(species);
            }
        }
    }

    private static void GetTableRandSettings(
        RandOption option,
        out int slotStart,
        out int slotStop,
        out bool copy)
    {
        copy = false;

        switch (option)
        {
            default:
                slotStart = 0;
                slotStop = -1;
                break;

            case RandOption.Regular_Only:
                slotStart = 0;
                slotStop = 1;
                break;

            case RandOption.SOS_Only:
                slotStart = 1;
                slotStop = -1;
                break;

            case RandOption.Regular_CopySOS:
                slotStart = 0;
                slotStop = 1;
                copy = true;
                break;
        }
    }

    private sealed class SpeciesTierPool(
        int[] species)
    {
        private readonly GenericRandomizer Randomizer =
            new(species);

        public int Next(int oldSpecies)
        {
            if (species.Length == 1)
                return Randomizer.Next();

            for (int i = 0; i < species.Length; i++)
            {
                int candidate =
                    Randomizer.Next();

                if (candidate != oldSpecies)
                    return candidate;
            }

            return Randomizer.Next();
        }
    }

    private readonly record struct PoolKey(
        int MinBST,
        int MaxBST,
        bool FullRandom);

    private enum RandOption
    {
        All = 0,
        Regular_Only = 1,
        SOS_Only = 2,
        Regular_CopySOS = 3,
    }
}
