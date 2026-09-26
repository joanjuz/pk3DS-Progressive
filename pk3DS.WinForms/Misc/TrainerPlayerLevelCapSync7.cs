using pk3DS.Core.Modding.Research;
using pk3DS.Core.Structures;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace pk3DS.WinForms;

internal sealed record TrainerPlayerLevelCapSyncChange(
    string Label,
    byte OldCap,
    byte NewCap,
    string Source);

internal sealed class TrainerPlayerLevelCapSyncResult
{
    public LevelCapTable Table { get; set; }
    public List<TrainerPlayerLevelCapSyncChange> Changes { get; } = [];
    public List<string> Notes { get; } = [];

    public string BuildPreview()
    {
        var lines =
            new List<string>
            {
                "The table contains only checkpoints validated with before/after USUM saves.",
                $"Checkpoints: {Table.Entries.Count}",
                $"Trainer-derived caps changed: {Changes.Count}",
                string.Empty,
            };

        foreach (LevelCapEntry entry in Table.Entries)
        {
            lines.Add(
                $"{entry.Label}: Lv{entry.Cap}");
        }

        if (Notes.Count > 0)
        {
            lines.Add(string.Empty);
            lines.AddRange(Notes);
        }

        return string.Join(
            Environment.NewLine,
            lines);
    }

    public string BuildStatusSummary()
    {
        string result =
            $"{Table.Entries.Count} validated checkpoint(s); " +
            $"{Changes.Count} trainer-derived cap change(s).";

        if (Notes.Count > 0)
            result += $" {Notes.Count} note(s).";

        return result;
    }
}

internal static class TrainerPlayerLevelCapSync7
{
    private sealed record TrainerCapMapping(
        string CheckpointLabel,
        int[] TrainerIDs);

    // Strict whitelist:
    // only Trainer IDs whose completion detector is represented by a supplied
    // before/after save pair are imported into Player Level Caps.
    //
    // Totem/trial-only checkpoints and Ultra Necrozma keep the canonical caps
    // from LevelCapTable.Default(). Extra Trainer Level Cap rows are ignored.
    private static readonly TrainerCapMapping[] TrainerMappings =
    [
        new(
            "Defeat Kahuna Hala",
            [23]),

        new(
            "Defeat Olivia - Mayla",
            [90]),

        new(
            "Defeat Guzma",
            [235]),

        new(
            "Defeat Nanu - Denio",
            [154]),

        new(
            "Defeat Lusamine",
            [131]),

        new(
            "Defeat Hapu - Hela",
            [497]),

        new(
            "Defeat Gladion - Mount Lanakila",
            [416]),

        new(
            "Elite Four - Kahili",
            [156]),

        new(
            "Elite Four - Molayne (Lario)",
            [489]),

        new(
            "Elite Four - Olivia (Mayla)",
            [153]),

        new(
            "Elite Four - Acerola (Zarala)",
            [149]),

        // The supplied champion save activates TR_FLAG_496 specifically.
        // Do not infer 494/495 until those variants are independently tested.
        new(
            "Defeat Champion Hau (Tilo)",
            [496]),
    ];

    internal static TrainerPlayerLevelCapSyncResult Build(
        TrainerLevelCapsTemplate trainerCaps)
    {
        ArgumentNullException.ThrowIfNull(
            trainerCaps);

        LevelCapTable table =
            LevelCapTable.Default();

        var result =
            new TrainerPlayerLevelCapSyncResult
            {
                Table = table,
            };

        HashSet<int> verifiedTrainerIDs =
            TrainerMappings
                .SelectMany(z =>
                    z.TrainerIDs)
                .ToHashSet();

        int ignoredEnabledEntries =
            (trainerCaps.Trainers ?? [])
                .Count(z =>
                    z.Use &&
                    !verifiedTrainerIDs.Contains(
                        z.TrainerID));

        if (ignoredEnabledEntries > 0)
        {
            result.Notes.Add(
                $"{ignoredEnabledEntries} enabled Trainer Level Cap entr" +
                $"{(ignoredEnabledEntries == 1 ? "y was" : "ies were")} ignored " +
                "because its/their Player Level Cap checkpoint has not been save-validated.");
        }

        Dictionary<int, TrainerLevelCapTemplateEntry> configured =
            (trainerCaps.Trainers ?? [])
                .Where(z =>
                    z.Use &&
                    verifiedTrainerIDs.Contains(
                        z.TrainerID))
                .GroupBy(z =>
                    z.TrainerID)
                .ToDictionary(
                    z => z.Key,
                    z => z.First());

        Dictionary<int, int> currentAceLevels =
            ReadCurrentTrainerAceLevels(
                configured.Values
                    .Where(z =>
                        z.LevelCap == 0 &&
                        z.CurrentAceLevel == 0)
                    .Select(z =>
                        z.TrainerID));

        foreach (TrainerCapMapping mapping in TrainerMappings)
        {
            TrainerLevelCapTemplateEntry[] sources =
                mapping.TrainerIDs
                    .Where(configured.ContainsKey)
                    .Select(id =>
                        configured[id])
                    .ToArray();

            if (sources.Length == 0)
            {
                result.Notes.Add(
                    $"{mapping.CheckpointLabel}: no matching verified Trainer Cap entry; " +
                    "kept canonical cap.");
                continue;
            }

            int[] caps =
                sources
                    .Select(z =>
                        GetEffectiveTrainerCap(
                            z,
                            currentAceLevels))
                    .ToArray();

            int target =
                caps.Max();

            if (caps.Distinct().Count() > 1)
            {
                result.Notes.Add(
                    $"{mapping.CheckpointLabel}: verified trainer variants differ; " +
                    $"using highest configured cap Lv{target}.");
            }

            int index =
                table.Entries.FindIndex(z =>
                    string.Equals(
                        z.Label,
                        mapping.CheckpointLabel,
                        StringComparison.Ordinal));

            if (index < 0)
            {
                throw new InvalidDataException(
                    $"Validated Player Level Caps checkpoint " +
                    $"'{mapping.CheckpointLabel}' was not found.");
            }

            LevelCapEntry old =
                table.Entries[index];

            byte newCap =
                checked((byte)target);

            if (old.Cap != newCap)
            {
                table.Entries[index] =
                    old with
                    {
                        Cap = newCap,
                    };

                result.Changes.Add(
                    new TrainerPlayerLevelCapSyncChange(
                        old.Label,
                        old.Cap,
                        newCap,
                        string.Join(
                            ", ",
                            sources.Select(z =>
                                $"TR {z.TrainerID}"))));
            }
        }

        // The Player Level Cap sequence must never decrease. This matters for
        // bosses such as Guzma/Lusamine/Gladion whose configured cap can be
        // below a previous validated milestone.
        byte running =
            table.Entries[0].Cap;

        for (int i = 1; i < table.Entries.Count; i++)
        {
            LevelCapEntry entry =
                table.Entries[i];

            if (entry.Cap >= running)
            {
                running =
                    entry.Cap;
                continue;
            }

            table.Entries[i] =
                entry with
                {
                    Cap = running,
                };
        }

        List<string> problems =
            table.Validate();

        if (problems.Count != 0)
        {
            throw new InvalidDataException(
                "The rebuilt save-validated Player Level Caps table is invalid: " +
                problems[0]);
        }

        return result;
    }

    private static int GetEffectiveTrainerCap(
        TrainerLevelCapTemplateEntry entry,
        IReadOnlyDictionary<int, int> currentAceLevels)
    {
        int cap =
            entry.LevelCap;

        if (cap == 0)
        {
            cap =
                entry.CurrentAceLevel;

            if (cap == 0 &&
                currentAceLevels.TryGetValue(
                    entry.TrainerID,
                    out int current))
            {
                cap =
                    current;
            }
        }

        if (cap is < 1 or > LevelCapTable.HardCeiling)
        {
            throw new InvalidDataException(
                $"Trainer ID {entry.TrainerID} resolves to invalid cap {cap}. " +
                "LevelCap 0 requires a readable current ace level.");
        }

        return cap;
    }

    private static Dictionary<int, int> ReadCurrentTrainerAceLevels(
        IEnumerable<int> trainerIDs)
    {
        int[] ids =
            trainerIDs
                .Distinct()
                .ToArray();

        var result =
            new Dictionary<int, int>();

        if (ids.Length == 0)
            return result;

        var trdata =
            Main.Config.GetGARCData(
                "trdata");

        var trpoke =
            Main.Config.GetGARCData(
                "trpoke");

        byte[][] data =
            trdata.Files;

        byte[][] teams =
            trpoke.Files;

        foreach (int trainerID in ids)
        {
            if ((uint)trainerID >= (uint)data.Length ||
                (uint)trainerID >= (uint)teams.Length)
            {
                continue;
            }

            var trainer =
                new TrainerData7(
                    data[trainerID],
                    teams[trainerID]);

            if (trainer.Pokemon.Count == 0)
                continue;

            result[trainerID] =
                trainer.Pokemon.Max(z =>
                    z.Level);
        }

        return result;
    }
}
