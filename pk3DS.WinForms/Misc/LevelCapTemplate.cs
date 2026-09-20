using pk3DS.Core.Modding.Research;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace pk3DS.WinForms;

public sealed class LevelCapTemplate
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "Player Level Caps";
    public string Game { get; set; } = "USUM";
    public int PresetShift { get; set; }
    public int FinalCap { get; set; } = LevelCapTable.ResearchFinalCap;
    public List<LevelCapTemplateEntry> Entries { get; set; } = [];

    public LevelCapTable ToTable() => new()
    {
        Entries = Entries
            .Select(z => new LevelCapEntry(
                z.Label ?? string.Empty,
                z.FlagOffset,
                z.FlagBit,
                z.Cap))
            .ToList(),
    };

    public static LevelCapTemplate FromTable(
        LevelCapTable table,
        string name,
        string game,
        int presetShift = 0,
        int finalCap = LevelCapTable.ResearchFinalCap)
    {
        ArgumentNullException.ThrowIfNull(table);

        return new LevelCapTemplate
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Player Level Caps" : name.Trim(),
            Game = string.IsNullOrWhiteSpace(game) ? "USUM" : game.Trim(),
            PresetShift = presetShift,
            FinalCap = Math.Clamp(finalCap, 5, 100),
            Entries = table.Entries
                .Select(z => new LevelCapTemplateEntry
                {
                    Label = z.Label,
                    FlagOffset = z.FlagOffset,
                    FlagBit = z.FlagBit,
                    Cap = z.Cap,
                })
                .ToList(),
        };
    }
}

public sealed class LevelCapTemplateEntry
{
    public string Label { get; set; } = string.Empty;
    public byte FlagOffset { get; set; }
    public byte FlagBit { get; set; }
    public byte Cap { get; set; }
}

public static class LevelCapTemplateFile
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly Dictionary<string, LevelCapTemplate> CurrentTemplates =
        new(StringComparer.OrdinalIgnoreCase);

    public static string TemplateDirectory => CustomBalanceTemplates.GetTemplateDirectory();

    public static void SetCurrent(LevelCapTemplate template, string currentGame)
    {
        Validate(template, currentGame);
        CurrentTemplates[NormalizeGame(currentGame)] = Clone(template);
    }

    public static bool TryGetCurrent(string currentGame, out LevelCapTemplate template)
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

    public static LevelCapTemplate Load(string path, string currentGame)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Template path is empty.", nameof(path));

        string json = File.ReadAllText(path, Encoding.UTF8);
        var template = JsonSerializer.Deserialize<LevelCapTemplate>(json, JsonOptions)
            ?? throw new InvalidDataException("The Player Level Caps template file is empty or invalid.");

        Validate(template, currentGame);
        return template;
    }

    public static void Save(string path, LevelCapTemplate template, string currentGame)
    {
        Validate(template, currentGame);

        string folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(folder))
            Directory.CreateDirectory(folder);

        File.WriteAllText(path, JsonSerializer.Serialize(template, JsonOptions), Encoding.UTF8);
    }

    public static void SaveLastState(LevelCapTemplate template, string currentGame)
    {
        Save(GetLastStatePath(currentGame), template, currentGame);
        SetCurrent(template, currentGame);
    }

    public static bool TryLoadLastState(string currentGame, out LevelCapTemplate template)
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

    public static void Validate(LevelCapTemplate template, string currentGame)
    {
        if (template is null)
            throw new ArgumentNullException(nameof(template));

        if (template.Version != 1)
            throw new InvalidDataException($"Unsupported Player Level Caps template version {template.Version}. Expected version 1.");

        string wanted = NormalizeGame(template.Game);
        string actual = NormalizeGame(currentGame);
        if (wanted.Length != 0 && wanted != "ANY" && wanted != actual)
            throw new InvalidDataException($"This Player Level Caps template is for {template.Game}, but the current game is {currentGame}.");

        if (actual.Length != 0 && actual != "USUM")
            throw new InvalidDataException("Player Level Caps are currently supported only for Ultra Sun / Ultra Moon.");

        if (template.FinalCap is < 5 or > 100)
            throw new InvalidDataException("Player Level Caps final cap must be between 5 and 100.");

        var table = template.ToTable();
        var problems = table.Validate();
        if (problems.Count != 0)
            throw new InvalidDataException("Invalid Player Level Caps table: " + problems[0]);
    }

    private static string GetLastStatePath(string currentGame)
    {
        string folder = Path.Combine(TemplateDirectory, "_state");
        Directory.CreateDirectory(folder);
        string game = NormalizeGame(currentGame).ToLowerInvariant();
        return Path.Combine(folder, $"player_level_caps_{game}.json");
    }

    private static LevelCapTemplate Clone(LevelCapTemplate template)
    {
        string json = JsonSerializer.Serialize(template, JsonOptions);
        return JsonSerializer.Deserialize<LevelCapTemplate>(json, JsonOptions)
            ?? throw new InvalidDataException("Could not clone Player Level Caps template.");
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
