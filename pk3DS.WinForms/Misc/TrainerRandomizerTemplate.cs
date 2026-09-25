using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace pk3DS.WinForms;

public enum TrainerMoveSource
{
    DontModify = 0,
    RandomizeAll = 1,
    LevelUpOnly = 2,
    Metronome = 3,
}

public sealed class TrainerRandomizerTemplate
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "Trainer randomizer template";
    public string Game { get; set; } = "ANY";
    public ProgressiveBSTTemplate ProgressiveBST { get; set; }
    public TrainerMoveSettingsTemplate MoveSettings { get; set; }
    public TrainerLevelCapsTemplate LevelCaps { get; set; }
    public TrainerMoveRulesTemplate TrainerMoveRules { get; set; }
}

public sealed class ProgressiveBSTTemplate
{
    public bool Enabled { get; set; } = true;
    public List<ProgressiveBSTTemplateRule> Ranges { get; set; } = [];
}

public sealed class ProgressiveBSTTemplateRule
{
    public int MinLevel { get; set; }
    public int MaxLevel { get; set; }
    public int MinBST { get; set; }
    public int MaxBST { get; set; }
    public bool FullRandom { get; set; }
}

public sealed class TrainerMoveSettingsTemplate
{
    public TrainerMoveSource Source { get; set; } = TrainerMoveSource.RandomizeAll;

    // Gen7 global Rules settings.
    // Nullable so older JSON templates do not silently overwrite the current UI.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? RandomDoubleBattles { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DoubleBattleChance { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? MaxTrainerAI { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? RandomHeldItems { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? BanBadItems { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ItemClause { get; set; }
    public bool BetterMovesets { get; set; }

    // Gen7 global Better Movesets category selectors.
    // Defaults preserve the behavior of older templates when BetterMovesets is enabled.
    public bool BetterMovesetsNormalTrainers { get; set; } = true;
    public bool BetterMovesetsImportantTrainers { get; set; } = true;
    public bool BetterMovesetsBosses { get; set; } = true;

    // Gen7 global Smart Items settings.
    public bool SmartItems { get; set; }
    public bool SmartItemsNormalTrainers { get; set; } = true;
    public bool SmartItemsImportantTrainers { get; set; } = true;
    public bool SmartItemsBosses { get; set; } = true;

    // 0 = Normal, 1 = Strong, 2 = Competitive.
    // Legacy single quality value. Kept so older trainer templates still load.
    public int SmartItemMode { get; set; } = 1;

    // -1 means: use the legacy SmartItemMode value.
    public int SmartItemModeNormalTrainers { get; set; } = -1;
    public int SmartItemModeImportantTrainers { get; set; } = -1;
    public int SmartItemModeBosses { get; set; } = -1;
    public bool ForceHighPower { get; set; }
    public int HighPowerLevel { get; set; } = 30;
    public bool NoFixedDamage { get; set; } = true;
    public bool EnsureDamagingMoves { get; set; } = true;
    public int DamagingMoveCount { get; set; } = 2;
    public bool EnsureSTABMoves { get; set; } = true;
    public int STABMoveCount { get; set; } = 1;
}

public sealed class TrainerLevelCapsTemplate
{
    public bool Enabled { get; set; } = true;
    public bool ApplyToPreviousTrainers { get; set; } = true;
    public int PreviousTrainerGap { get; set; } = 2;
    public bool ResetUnlistedTrainers { get; set; } = true;
    public List<TrainerLevelCapTemplateEntry> Trainers { get; set; } = [];
}

public sealed class TrainerLevelCapTemplateEntry
{
    public int TrainerID { get; set; }
    public bool Use { get; set; } = true;
    public int LevelCap { get; set; }
    public bool Mega { get; set; }
    public bool ZMove { get; set; }
}

public sealed class TrainerMoveRulesTemplate
{
    public bool ResetUnlistedTrainers { get; set; } = true;
    public List<TrainerMoveRuleTemplateEntry> Trainers { get; set; } = [];
}

public sealed class TrainerMoveRuleTemplateEntry
{
    public int TrainerID { get; set; }
    public bool Use { get; set; } = true;
    public int MinMovePower { get; set; }
    public bool StrongStat { get; set; }
    public int MixedTolerance { get; set; } = 15;
    public bool AllowStatusMoves { get; set; } = true;
    public bool BetterMovesets { get; set; } = true;
    public bool SmartItems { get; set; } = true;
    public int EVs { get; set; } = -1;
}

public sealed class TrainerTemplateApplyResult
{
    public int LevelCapsApplied { get; set; }
    public int MoveRulesApplied { get; set; }
    public List<int> UnknownLevelCapTrainerIDs { get; } = [];
    public List<int> UnknownMoveRuleTrainerIDs { get; } = [];
}

public static class TrainerRandomizerTemplateFile
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly Dictionary<string, TrainerRandomizerTemplate> CurrentTemplates =
        new(StringComparer.OrdinalIgnoreCase);

    public static string TemplateDirectory => CustomBalanceTemplates.GetTemplateDirectory();

    public static void SetCurrent(TrainerRandomizerTemplate template, string currentGame)
    {
        Validate(template, currentGame);
        CurrentTemplates[NormalizeGame(currentGame)] = Clone(template);
    }

    public static bool TryGetCurrent(string currentGame, out TrainerRandomizerTemplate template)
    {
        if (CurrentTemplates.TryGetValue(NormalizeGame(currentGame), out var current))
        {
            template = Clone(current);
            return true;
        }

        template = null;
        return false;
    }

    public static void ClearCurrent(string currentGame = null)
    {
        if (string.IsNullOrWhiteSpace(currentGame))
        {
            CurrentTemplates.Clear();
            return;
        }

        CurrentTemplates.Remove(NormalizeGame(currentGame));
    }

    public static TrainerRandomizerTemplate Load(string path, string currentGame)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Template path is empty.", nameof(path));

        string json = File.ReadAllText(path);
        var template = JsonSerializer.Deserialize<TrainerRandomizerTemplate>(json, JsonOptions)
            ?? throw new InvalidDataException("The template file is empty or invalid.");

        Validate(template, currentGame);
        return template;
    }

    public static void Save(string path, TrainerRandomizerTemplate template, string currentGame)
    {
        Validate(template, currentGame);

        string folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(folder))
            Directory.CreateDirectory(folder);

        File.WriteAllText(path, JsonSerializer.Serialize(template, JsonOptions));
    }

    public static TrainerTemplateApplyResult ApplyTrainerRules(
        TrainerRandomizerTemplate template,
        IList<TrainerLevelCapRule> levelCapRules,
        IList<TrainerMoveRule> moveRules)
    {
        var result = new TrainerTemplateApplyResult();

        if (template.LevelCaps is not null)
        {
            var section = template.LevelCaps;
            if (section.ResetUnlistedTrainers)
            {
                foreach (var rule in levelCapRules)
                    rule.Enabled = false;
            }

            var byID = levelCapRules.ToDictionary(r => r.TrainerID);
            foreach (var entry in section.Trainers ?? [])
            {
                if (!byID.TryGetValue(entry.TrainerID, out var rule))
                {
                    result.UnknownLevelCapTrainerIDs.Add(entry.TrainerID);
                    continue;
                }

                rule.Enabled = entry.Use;
                rule.LevelCap = entry.LevelCap;
                rule.GuaranteeMega = entry.Mega;
                rule.GuaranteeZMove = entry.ZMove;
                result.LevelCapsApplied++;
            }
        }

        if (template.TrainerMoveRules is not null)
        {
            var section = template.TrainerMoveRules;
            if (section.ResetUnlistedTrainers)
            {
                foreach (var rule in moveRules)
                    rule.Enabled = false;
            }

            var byID = moveRules.ToDictionary(r => r.TrainerID);
            foreach (var entry in section.Trainers ?? [])
            {
                if (!byID.TryGetValue(entry.TrainerID, out var rule))
                {
                    result.UnknownMoveRuleTrainerIDs.Add(entry.TrainerID);
                    continue;
                }

                rule.Enabled = entry.Use;
                rule.MinMovePower = entry.MinMovePower;
                rule.UseStrongestAttackStat = entry.StrongStat;
                rule.MixedTolerance = entry.MixedTolerance;
                rule.AllowStatusMoves = entry.AllowStatusMoves;
                rule.BetterMovesets = entry.BetterMovesets;
                rule.SmartItems = entry.SmartItems;
                rule.OverrideEVs = entry.EVs;
                result.MoveRulesApplied++;
            }
        }

        return result;
    }

    public static void Validate(TrainerRandomizerTemplate template, string currentGame)
    {
        if (template.Version != 1)
            throw new InvalidDataException($"Unsupported trainer template version {template.Version}. Expected version 1.");

        string wantedGame = NormalizeGame(template.Game);
        string actualGame = NormalizeGame(currentGame);
        if (wantedGame.Length != 0 && wantedGame != "ANY" && wantedGame != actualGame)
            throw new InvalidDataException($"This template is for {template.Game}, but the current game is {currentGame}.");

        if (template.ProgressiveBST is not null)
        {
            var ranges = template.ProgressiveBST.Ranges ?? [];
            if (template.ProgressiveBST.Enabled && ranges.Count == 0)
                throw new InvalidDataException("Progressive BST is enabled, but the template has no BST ranges.");

            foreach (var range in ranges)
            {
                if (range.MinLevel < 1 || range.MaxLevel > 100 || range.MinLevel > range.MaxLevel)
                    throw new InvalidDataException("Invalid Progressive BST level range. Levels must be between 1 and 100 and may not be reversed.");

                if (!range.FullRandom && (range.MinBST < 1 || range.MaxBST > 999 || range.MinBST > range.MaxBST))
                    throw new InvalidDataException("Invalid Progressive BST value range. BST must be between 1 and 999 and may not be reversed.");
            }

            var sorted = ranges.OrderBy(r => r.MinLevel).ToList();
            for (int i = 1; i < sorted.Count; i++)
            {
                if (sorted[i].MinLevel <= sorted[i - 1].MaxLevel)
                    throw new InvalidDataException("Progressive BST ranges overlap.");
            }
        }

        if (template.MoveSettings is not null)
        {
            var moves = template.MoveSettings;
            if (!Enum.IsDefined(moves.Source))
                throw new InvalidDataException("Invalid move source in template.");

            if (moves.DoubleBattleChance is < 0 or > 100)
                throw new InvalidDataException("DoubleBattleChance must be between 0 and 100.");
            if (moves.HighPowerLevel < 1 || moves.HighPowerLevel > 100)
                throw new InvalidDataException("HighPowerLevel must be between 1 and 100.");
            if (moves.DamagingMoveCount < 0 || moves.DamagingMoveCount > 4)
                throw new InvalidDataException("DamagingMoveCount must be between 0 and 4.");
            if (moves.STABMoveCount < 0 || moves.STABMoveCount > 4)
                throw new InvalidDataException("STABMoveCount must be between 0 and 4.");

            if (moves.SmartItemMode < 0 || moves.SmartItemMode > 2)
                throw new InvalidDataException("SmartItemMode must be 0 (Normal), 1 (Strong), or 2 (Competitive).");

            if (moves.SmartItemModeNormalTrainers < -1 || moves.SmartItemModeNormalTrainers > 2)
                throw new InvalidDataException("SmartItemModeNormalTrainers must be -1 (legacy), 0 (Normal), 1 (Strong), or 2 (Competitive).");
            if (moves.SmartItemModeImportantTrainers < -1 || moves.SmartItemModeImportantTrainers > 2)
                throw new InvalidDataException("SmartItemModeImportantTrainers must be -1 (legacy), 0 (Normal), 1 (Strong), or 2 (Competitive).");
            if (moves.SmartItemModeBosses < -1 || moves.SmartItemModeBosses > 2)
                throw new InvalidDataException("SmartItemModeBosses must be -1 (legacy), 0 (Normal), 1 (Strong), or 2 (Competitive).");
        }

        if (template.LevelCaps is not null)
        {
            var caps = template.LevelCaps;
            if (caps.PreviousTrainerGap < 0 || caps.PreviousTrainerGap > 20)
                throw new InvalidDataException("PreviousTrainerGap must be between 0 and 20.");

            var entries = caps.Trainers ?? [];
            if (entries.Any(e => e.TrainerID <= 0))
                throw new InvalidDataException("Level Cap trainer IDs must be greater than 0.");
            if (entries.Any(e => e.LevelCap < 0 || e.LevelCap > 100))
                throw new InvalidDataException("Level caps must be 0 (use current ace) or between 1 and 100.");
            if (entries.GroupBy(e => e.TrainerID).Any(g => g.Count() > 1))
                throw new InvalidDataException("Duplicate trainer IDs found in Level Caps.");
        }

        if (template.TrainerMoveRules is not null)
        {
            var entries = template.TrainerMoveRules.Trainers ?? [];
            if (entries.Any(e => e.TrainerID <= 0))
                throw new InvalidDataException("Move Rule trainer IDs must be greater than 0.");
            if (entries.Any(e => e.MinMovePower < 0 || e.MinMovePower > 250))
                throw new InvalidDataException("MinMovePower must be between 0 and 250.");
            if (entries.Any(e => e.MixedTolerance < 0 || e.MixedTolerance > 255))
                throw new InvalidDataException("MixedTolerance must be between 0 and 255.");
            if (entries.Any(e => e.EVs < -1 || e.EVs > 252))
                throw new InvalidDataException("EVs must be -1 (off) or between 0 and 252.");
            if (entries.GroupBy(e => e.TrainerID).Any(g => g.Count() > 1))
                throw new InvalidDataException("Duplicate trainer IDs found in Trainer Move Rules.");
        }
    }

    private static TrainerRandomizerTemplate Clone(TrainerRandomizerTemplate template)
    {
        string json = JsonSerializer.Serialize(template, JsonOptions);
        return JsonSerializer.Deserialize<TrainerRandomizerTemplate>(json, JsonOptions)
            ?? throw new InvalidDataException("Could not clone trainer template.");
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static string NormalizeGame(string game)
    {
        if (string.IsNullOrWhiteSpace(game))
            return string.Empty;

        return new string(game.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }
}
