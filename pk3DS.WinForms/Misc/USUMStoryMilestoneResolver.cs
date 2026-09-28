using System;
using System.Collections.Generic;
using System.Linq;

using pk3DS.Core.Structures;

namespace pk3DS.WinForms;

internal static class USUMStoryMilestoneResolver
{
    internal const string TotemLevelCapsActionId = "static-encounters.totem-level-caps";

    internal static GlobalRandomizationAction GetSessionTotemLevelCapsAction()
    {
        return RandomizationSessionState.ExportActions()
            .FirstOrDefault(action =>
                string.Equals(
                    action?.Id,
                    TotemLevelCapsActionId,
                    StringComparison.OrdinalIgnoreCase));
    }

    internal static IReadOnlyDictionary<int, int> ReadCurrentStaticEncounterLevels()
    {
        var result = new Dictionary<int, int>();

        if (Main.Config?.USUM != true)
            return result;

        var garc = Main.Config.GetGARCData("encounterstatic");
        if (garc?.Files is null || garc.Files.Length <= 1 || garc.Files[1] is null)
            return result;

        byte[] data = garc.Files[1];
        int count = data.Length / EncounterStatic7.SIZE;

        for (int entryID = 0; entryID < count; entryID++)
        {
            int offset = entryID * EncounterStatic7.SIZE;
            var entry = new byte[EncounterStatic7.SIZE];
            Array.Copy(data, offset, entry, 0, entry.Length);

            var encounter = new EncounterStatic7(entry);
            result[entryID] = Math.Clamp(encounter.Level, 1, 100);
        }

        return result;
    }

    internal static bool HasEnabledStaticEntry(
        GlobalRandomizationAction action,
        IEnumerable<int> entryIDs)
    {
        if (action?.Parameters is null ||
            !action.Parameters.TryGetValue("caps", out string raw) ||
            string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var wanted = entryIDs?.ToHashSet() ?? new HashSet<int>();
        if (wanted.Count == 0)
            return false;

        foreach (string token in raw.Split(
                     ';',
                     StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries))
        {
            string[] parts = token.Split(':');
            if (parts.Length != 3 ||
                !int.TryParse(parts[0], out int entryID) ||
                !wanted.Contains(entryID))
            {
                continue;
            }

            bool enabled =
                parts[1] == "1" ||
                bool.TryParse(parts[1], out bool parsedEnabled) &&
                parsedEnabled;

            if (enabled)
                return true;
        }

        return false;
    }

    internal static int? ResolveEnabledStaticCap(
        GlobalRandomizationAction action,
        IEnumerable<int> entryIDs,
        IReadOnlyDictionary<int, int> currentLevels)
    {
        if (action?.Parameters is null ||
            !action.Parameters.TryGetValue("caps", out string raw) ||
            string.IsNullOrWhiteSpace(raw) ||
            currentLevels is null)
        {
            return null;
        }

        var wanted = entryIDs?.ToHashSet() ?? new HashSet<int>();
        if (wanted.Count == 0)
            return null;

        var effectiveCaps = new List<int>();

        foreach (string token in raw.Split(
                     ';',
                     StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries))
        {
            string[] parts = token.Split(':');
            if (parts.Length != 3 ||
                !int.TryParse(parts[0], out int entryID) ||
                !wanted.Contains(entryID))
            {
                continue;
            }

            bool enabled =
                parts[1] == "1" ||
                bool.TryParse(parts[1], out bool parsedEnabled) &&
                parsedEnabled;

            if (!enabled || !currentLevels.TryGetValue(entryID, out int level))
                continue;

            // The Static Editor has already resolved LevelCap=0 ("Current") and
            // any positive cap into encounter.Level before Trainers run. Reading
            // the effective encounter level keeps interactive and Batch behavior
            // identical and avoids duplicating Totem cap numeric semantics here.
            effectiveCaps.Add(Math.Clamp(level, 1, 100));
        }

        // pk3DS identifies the loaded pair as USUM, so the first trial uses the
        // 4/9 variant set. If enabled variants differ, the lower effective cap is
        // the conservative ceiling and prevents Regular trainers overtaking either.
        return effectiveCaps.Count == 0
            ? null
            : effectiveCaps.Min();
    }
}