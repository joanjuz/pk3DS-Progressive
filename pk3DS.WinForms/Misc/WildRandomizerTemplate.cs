using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace pk3DS.WinForms;

public sealed class WildRandomizerTemplate
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "Wild Encounter randomizer";
    public string Game { get; set; } = "ANY";
    public bool G1 { get; set; } = true;
    public bool G2 { get; set; } = true;
    public bool G3 { get; set; } = true;
    public bool G4 { get; set; } = true;
    public bool G5 { get; set; } = true;
    public bool G6 { get; set; } = true;
    public bool G7 { get; set; } = true;
    public bool Legendaries { get; set; }
    public bool Events { get; set; }
    public bool MegaForms { get; set; }
    public bool SimilarBST { get; set; }
    public int SlotRandomizationOption { get; set; }
    public bool ModifyLevel { get; set; }
    public decimal LevelAmplifier { get; set; } = 1m;
    public int AdvancedLevelFlat { get; set; }
    public int AdvancedLevelMultiplier { get; set; } = 100;
    public bool AdvancedKeepRange { get; set; } = true;
    public WildProgressiveBSTTemplate ProgressiveBST { get; set; } = new();
}

public sealed class WildProgressiveBSTTemplate
{
    public bool Enabled { get; set; }
    public List<WildProgressiveBSTTemplateRule> Ranges { get; set; } = [];
}

public sealed class WildProgressiveBSTTemplateRule
{
    public int MinLevel { get; set; }
    public int MaxLevel { get; set; }
    public int MinBST { get; set; }
    public int MaxBST { get; set; }
    public bool FullRandom { get; set; }
}

public static class WildRandomizerTemplateFile
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly Dictionary<string, WildRandomizerTemplate> CurrentTemplates = new(StringComparer.OrdinalIgnoreCase);
    public static string TemplateDirectory => CustomBalanceTemplates.GetTemplateDirectory();

    public static void SetCurrent(WildRandomizerTemplate template, string currentGame)
    {
        Validate(template, currentGame);
        CurrentTemplates[NormalizeGame(currentGame)] = Clone(template);
    }

    public static bool TryGetCurrent(string currentGame, out WildRandomizerTemplate template)
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

    public static WildRandomizerTemplate Load(string path, string currentGame)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Template path is empty.", nameof(path));
        string json = File.ReadAllText(path, Encoding.UTF8);
        var template = JsonSerializer.Deserialize<WildRandomizerTemplate>(json, JsonOptions)
            ?? throw new InvalidDataException("The Wild Encounter template file is empty or invalid.");
        Validate(template, currentGame);
        return template;
    }

    public static void Save(string path, WildRandomizerTemplate template, string currentGame)
    {
        Validate(template, currentGame);
        string folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(folder))
            Directory.CreateDirectory(folder);
        File.WriteAllText(path, JsonSerializer.Serialize(template, JsonOptions), Encoding.UTF8);
    }

    public static void SaveLastState(WildRandomizerTemplate template, string currentGame)
    {
        Save(GetLastStatePath(currentGame), template, currentGame);
        SetCurrent(template, currentGame);
    }

    public static bool TryLoadLastState(string currentGame, out WildRandomizerTemplate template)
    {
        string path = GetLastStatePath(currentGame);
        if (!File.Exists(path))
        {
            template = null;
            return false;
        }
        try
        {
            template = Load(path, currentGame);
            SetCurrent(template, currentGame);
            return true;
        }
        catch
        {
            template = null;
            return false;
        }
    }

    public static void Validate(WildRandomizerTemplate template, string currentGame)
    {
        if (template is null) throw new ArgumentNullException(nameof(template));
        if (template.Version != 1)
            throw new InvalidDataException($"Unsupported Wild Encounter template version {template.Version}. Expected version 1.");

        string wantedGame = NormalizeGame(template.Game);
        string actualGame = NormalizeGame(currentGame);
        if (wantedGame.Length != 0 && wantedGame != "ANY" && wantedGame != actualGame)
            throw new InvalidDataException($"This Wild Encounter template is for {template.Game}, but the current game is {currentGame}.");

        if (template.SlotRandomizationOption is < 0 or > 3)
            throw new InvalidDataException("Wild slot randomization option must be between 0 and 3.");
        if (template.LevelAmplifier < 0.10m || template.LevelAmplifier > 3.00m)
            throw new InvalidDataException("Wild level amplifier must be between 0.10 and 3.00.");
        if (template.AdvancedLevelFlat is < -100 or > 100)
            throw new InvalidDataException("Advanced Wild level flat adjustment must be between -100 and 100.");
        if (template.AdvancedLevelMultiplier is < 1 or > 500)
            throw new InvalidDataException("Advanced Wild level multiplier must be between 1 and 500.");

        ValidateProgressiveBST(template.ProgressiveBST);
    }

    private static void ValidateProgressiveBST(WildProgressiveBSTTemplate section)
    {
        if (section is null) return;
        var ranges = section.Ranges ?? [];
        if (section.Enabled && ranges.Count == 0)
            throw new InvalidDataException("Progressive Wild BST is enabled, but no ranges are defined.");
        if (ranges.Count == 0) return;

        var sorted = ranges.OrderBy(rule => rule.MinLevel).ToList();
        foreach (var rule in sorted)
        {
            if (rule.MinLevel < 1 || rule.MaxLevel > 100 || rule.MinLevel > rule.MaxLevel)
                throw new InvalidDataException("Progressive Wild levels must be between 1 and 100 and may not be reversed.");
            if (!rule.FullRandom && (rule.MinBST < 1 || rule.MaxBST > 999 || rule.MinBST > rule.MaxBST))
                throw new InvalidDataException("Progressive Wild BST values must be between 1 and 999 and may not be reversed.");
        }

        if (sorted[0].MinLevel != 1 || sorted[^1].MaxLevel != 100)
            throw new InvalidDataException("Progressive Wild ranges must cover levels 1 through 100.");
        for (int i = 1; i < sorted.Count; i++)
        {
            if (sorted[i].MinLevel != sorted[i - 1].MaxLevel + 1)
                throw new InvalidDataException("Progressive Wild ranges must cover levels 1 through 100 without gaps or overlaps.");
        }
    }

    private static string GetLastStatePath(string currentGame)
    {
        string folder = Path.Combine(TemplateDirectory, "_state");
        Directory.CreateDirectory(folder);
        string game = NormalizeGame(currentGame).ToLowerInvariant();
        return Path.Combine(folder, $"wild_randomizer_{game}.json");
    }

    private static WildRandomizerTemplate Clone(WildRandomizerTemplate template)
    {
        string json = JsonSerializer.Serialize(template, JsonOptions);
        return JsonSerializer.Deserialize<WildRandomizerTemplate>(json, JsonOptions)
            ?? throw new InvalidDataException("Could not clone Wild Encounter template.");
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
        if (string.IsNullOrWhiteSpace(game)) return string.Empty;
        return new string(game.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }
}
