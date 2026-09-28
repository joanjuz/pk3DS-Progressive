using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace pk3DS.WinForms;

public sealed class GlobalRandomizationTemplate
{
    public int Version { get; set; } = 1;
    public int ActionCoverageVersion { get; set; } = 8;
    public string Name { get; set; } = "Global ROM template";
    public string Game { get; set; } = "ANY";
    public int Generation { get; set; }
    public DateTime SavedUtc { get; set; } = DateTime.UtcNow;

    // Snapshot of the existing pk3DS randomizer settings. This preserves all
    // CheckBox / NumericUpDown / ComboBox values already handled by RandSettings.
    public List<string> RandSettings { get; set; } = [];

    // Trainer settings contain extra state that RandSettings cannot represent,
    // such as Progressive BST ranges, per-trainer caps and move rules.
    public TrainerRandomizerTemplate Trainer { get; set; }

    // Wild encounter settings contain Progressive BST rows and other state that
    // cannot be represented completely by RandSettings alone.
    public WildRandomizerTemplate Wild { get; set; }

    // Player Level Caps have a variable story-flag table that needs its own
    // dedicated template instead of being flattened into action parameters.
    public LevelCapTemplate LevelCaps { get; set; }

    // A recipe of actions actually used in the current ROM. Batch building can
    // consume this list later without changing the template format.
    public List<GlobalRandomizationAction> Actions { get; set; } = [];

    // Fingerprints of external balance files used by this setup. They are not
    // overwritten when loading; hashes are only used to warn if a dependency
    // was changed or removed after the global template was saved.
    public List<GlobalTemplateAsset> Assets { get; set; } = [];
}

public sealed class GlobalRandomizationAction
{
    public string Id { get; set; } = string.Empty;
    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class GlobalTemplateAsset
{
    public string Role { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
}

/// <summary>
/// Remembers which high-level randomization operations were used in the current
/// ROM session. This is intentionally separate from RandSettings: RandSettings
/// stores HOW an editor is configured, while this class stores WHAT operations
/// were actually requested. The future batch builder can replay both.
/// </summary>
public static class RandomizationSessionState
{
    private static readonly Dictionary<string, GlobalRandomizationAction> Actions =
        new(StringComparer.OrdinalIgnoreCase);

    public static void MarkAction(string id, params (string Key, string Value)[] parameters)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        var action = new GlobalRandomizationAction { Id = id.Trim() };
        foreach (var (key, value) in parameters)
        {
            if (!string.IsNullOrWhiteSpace(key))
                action.Parameters[key] = value ?? string.Empty;
        }

        Actions[action.Id] = action;
    }

    public static bool ContainsAction(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;

        return Actions.ContainsKey(id.Trim());
    }

    public static List<GlobalRandomizationAction> ExportActions()
        => Actions.Values
            .OrderBy(z => z.Id, StringComparer.OrdinalIgnoreCase)
            .Select(CloneAction)
            .ToList();

    public static void ImportActions(IEnumerable<GlobalRandomizationAction> actions)
    {
        Actions.Clear();
        foreach (var action in actions ?? [])
        {
            if (string.IsNullOrWhiteSpace(action?.Id))
                continue;

            Actions[action.Id] = CloneAction(action);
        }
    }

    public static void Clear() => Actions.Clear();

    public static void RemoveAction(string id)
    {
        if (!string.IsNullOrWhiteSpace(id))
            Actions.Remove(id.Trim());
    }

    private static GlobalRandomizationAction CloneAction(GlobalRandomizationAction action)
    {
        return new GlobalRandomizationAction
        {
            Id = action.Id,
            Parameters = new Dictionary<string, string>(
                action.Parameters ?? new Dictionary<string, string>(),
                StringComparer.OrdinalIgnoreCase),
        };
    }
}

public static class GlobalRandomizationTemplateFile
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static string TemplateDirectory => CustomBalanceTemplates.GetTemplateDirectory();

    public static GlobalRandomizationTemplate Capture(string name, string currentGame, int generation)
    {
        TrainerRandomizerTemplate trainer = null;
        if (TrainerRandomizerTemplateFile.TryGetCurrent(currentGame, out var currentTrainer))
            trainer = currentTrainer;

        WildRandomizerTemplate wild = null;
        LevelCapTemplate levelCaps = null;
        if (generation == 7)
        {
            if (WildRandomizerTemplateFile.TryGetCurrent(currentGame, out var currentWild))
                wild = currentWild;
            else if (WildRandomizerTemplateFile.TryLoadLastState(currentGame, out var savedWild))
                wild = savedWild;

            if (LevelCapTemplateFile.TryGetCurrent(currentGame, out var currentCaps))
                levelCaps = currentCaps;
            else if (LevelCapTemplateFile.TryLoadLastState(currentGame, out var savedCaps))
                levelCaps = savedCaps;
        }

        return new GlobalRandomizationTemplate
        {
            Name = string.IsNullOrWhiteSpace(name) ? $"{currentGame} global ROM template" : name.Trim(),
            Game = NormalizeGame(currentGame),
            Generation = generation,
            SavedUtc = DateTime.UtcNow,
            RandSettings = RandSettings.Save().ToList(),
            Trainer = trainer,
            Wild = wild,
            LevelCaps = levelCaps,
            Actions = CaptureActions(wild, generation),
            Assets = CaptureAssets(generation),
        };
    }

    private static List<GlobalRandomizationAction> CaptureActions(
        WildRandomizerTemplate wild,
        int generation)
    {
        var actions = RandomizationSessionState.ExportActions();

        // Progressive Wild BST is configuration state as well as an operation.
        // Save it as a replay action even when the user configured the Wild
        // editor and saved the Global template before pressing Randomize All.
        if (generation == 7 &&
            wild?.ProgressiveBST?.Enabled == true &&
            wild.ProgressiveBST.Ranges is { Count: > 0 })
        {
            actions.RemoveAll(action =>
                string.Equals(action?.Id, "wild-encounters.randomize", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(action?.Id, "wild-encounters.progressive", StringComparison.OrdinalIgnoreCase));

            string rules = string.Join(
                ";",
                wild.ProgressiveBST.Ranges
                    .OrderBy(rule => rule.MinLevel)
                    .Select(rule => $"{rule.MinLevel},{rule.MaxLevel},{rule.MinBST},{rule.MaxBST},{rule.FullRandom}"));

            actions.Add(new GlobalRandomizationAction
            {
                Id = "wild-encounters.progressive",
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["progression"] = "area-max-valid-table",
                    ["rules"] = rules,
                },
            });
        }

        return actions
            .OrderBy(action => action.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
    public static void Save(string path, GlobalRandomizationTemplate template, string currentGame)
    {
        Validate(template, currentGame);

        string folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(folder))
            Directory.CreateDirectory(folder);

        File.WriteAllText(path, JsonSerializer.Serialize(template, JsonOptions), Encoding.UTF8);
    }

    public static GlobalRandomizationTemplate Load(string path, string currentGame)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Template path is empty.", nameof(path));

        string json = File.ReadAllText(path, Encoding.UTF8);
        var template = JsonSerializer.Deserialize<GlobalRandomizationTemplate>(json, JsonOptions)
            ?? throw new InvalidDataException("The global template file is empty or invalid.");

        Validate(template, currentGame);
        return template;
    }

    public static List<string> Apply(GlobalRandomizationTemplate template, string currentGame)
    {
        Validate(template, currentGame);

        RandSettings.Load((template.RandSettings ?? []).ToArray());

        // Also update randsettings.txt immediately so the setup survives even if
        // pk3DS is closed before another randomizer editor is opened.
        try
        {
            File.WriteAllLines(RandSettings.FileName, RandSettings.Save(), Encoding.Unicode);
        }
        catch
        {
            // Loading into memory is still useful if the executable directory is read-only.
        }

        if (template.Trainer is not null)
            TrainerRandomizerTemplateFile.SetCurrent(template.Trainer, currentGame);
        else
            TrainerRandomizerTemplateFile.ClearCurrent(currentGame);

        if (template.Wild is not null)
        {
            WildRandomizerTemplateFile.SetCurrent(template.Wild, currentGame);
            try
            {
                WildRandomizerTemplateFile.SaveLastState(template.Wild, currentGame);
            }
            catch
            {
                // In-memory application remains valid if persistent state cannot be written.
            }
        }
        else
        {
            WildRandomizerTemplateFile.ClearCurrent(currentGame);
        }

        if (template.LevelCaps is not null)
        {
            LevelCapTemplateFile.SetCurrent(template.LevelCaps, currentGame);
            try
            {
                LevelCapTemplateFile.SaveLastState(template.LevelCaps, currentGame);
            }
            catch
            {
                // In-memory application remains valid if persistent state cannot be written.
            }
        }
        else
        {
            LevelCapTemplateFile.ClearCurrent(currentGame);
        }

        RandomizationSessionState.ImportActions(template.Actions ?? []);
        return ValidateAssets(template.Assets ?? [], template.Generation);
    }

    public static void Validate(GlobalRandomizationTemplate template, string currentGame)
    {
        if (template is null)
            throw new ArgumentNullException(nameof(template));

        if (template.Version != 1)
            throw new InvalidDataException($"Unsupported global template version {template.Version}. Expected version 1.");

        string wantedGame = NormalizeGame(template.Game);
        string actualGame = NormalizeGame(currentGame);
        if (wantedGame.Length != 0 && wantedGame != "ANY" && wantedGame != actualGame)
            throw new InvalidDataException($"This global template is for {template.Game}, but the current game is {currentGame}.");

        if (template.Trainer is not null)
            TrainerRandomizerTemplateFile.Validate(template.Trainer, actualGame);

        if (template.Wild is not null)
            WildRandomizerTemplateFile.Validate(template.Wild, actualGame);

        if (template.LevelCaps is not null)
            LevelCapTemplateFile.Validate(template.LevelCaps, actualGame);

        GlobalRandomizationAction expandedMarts =
            (template.Actions ?? [])
                .FirstOrDefault(z =>
                    z is not null &&
                    string.Equals(
                        z.Id,
                        ExpandedMartTemplateAction.ActionId,
                        StringComparison.OrdinalIgnoreCase));

        if (expandedMarts is not null)
            ExpandedMartTemplateAction.Validate(expandedMarts, actualGame);

        GlobalRandomizationAction economy =
            (template.Actions ?? [])
                .FirstOrDefault(z =>
                    z is not null &&
                    string.Equals(
                        z.Id,
                        EconomyFixer.ActionId,
                        StringComparison.OrdinalIgnoreCase));

        if (economy is not null)
            EconomyFixer.ValidateAction(economy);
    }

    public static void ResetSession()
    {
        RandomizationSessionState.Clear();
        TrainerRandomizerTemplateFile.ClearCurrent();
        WildRandomizerTemplateFile.ClearCurrent();
        LevelCapTemplateFile.ClearCurrent();
    }

    private static List<GlobalTemplateAsset> CaptureAssets(int generation)
    {
        var result = new List<GlobalTemplateAsset>();
        CaptureAsset(result, "move-balance", CustomBalanceTemplates.GetMoveTemplatePath(generation));
        CaptureAsset(result, "evolution-balance", CustomBalanceTemplates.GetEvolutionTemplatePath(generation));
        CaptureAsset(result, "pokemon-stats", CustomBalanceTemplates.GetPokemonStatsTemplatePath(generation));
        CaptureAsset(result, "trainer-held-items", TrainerHeldItemTemplate.DefaultPath);
        CaptureAsset(result, "trainer-better-movesets", TrainerBetterMovesetTemplate.DefaultPath);
        CaptureAsset(result, "field-items", FieldItemRandomizerTemplate.DefaultPath);
        return result;
    }

    private static void CaptureAsset(List<GlobalTemplateAsset> result, string role, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        result.Add(new GlobalTemplateAsset
        {
            Role = role,
            FileName = Path.GetFileName(path),
            Sha256 = GetSha256(path),
        });
    }

    private static List<string> ValidateAssets(IEnumerable<GlobalTemplateAsset> assets, int generation)
    {
        var warnings = new List<string>();
        foreach (var asset in assets)
        {
            string path = ResolveAssetPath(asset.Role, generation);
            if (string.IsNullOrWhiteSpace(path))
                continue;

            if (!File.Exists(path))
            {
                warnings.Add($"Missing template dependency: {asset.Role} ({asset.FileName}).");
                continue;
            }

            string currentHash = GetSha256(path);
            if (!string.Equals(currentHash, asset.Sha256, StringComparison.OrdinalIgnoreCase))
                warnings.Add($"Template dependency changed since this global template was saved: {asset.Role} ({asset.FileName}).");
        }

        return warnings;
    }

    private static string ResolveAssetPath(string role, int generation)
    {
        return role switch
        {
            "move-balance" => CustomBalanceTemplates.GetMoveTemplatePath(generation),
            "evolution-balance" => CustomBalanceTemplates.GetEvolutionTemplatePath(generation),
            "pokemon-stats" => CustomBalanceTemplates.GetPokemonStatsTemplatePath(generation),
            "trainer-held-items" => TrainerHeldItemTemplate.DefaultPath,
            "trainer-better-movesets" => TrainerBetterMovesetTemplate.DefaultPath,
            "field-items" => FieldItemRandomizerTemplate.DefaultPath,
            _ => string.Empty,
        };
    }

    private static string GetSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static string NormalizeGame(string game)
    {
        if (string.IsNullOrWhiteSpace(game))
            return string.Empty;

        string normalized = new string(game.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return normalized switch
        {
            "SUNMOON" => "SM",
            "ULTRASUNULTRAMOON" => "USUM",
            "OMEGARUBYALPHASAPPHIRE" => "ORAS",
            _ => normalized,
        };
    }
}
