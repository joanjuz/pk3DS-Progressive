using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace pk3DS.WinForms;

public sealed class TotemLevelCapTemplate
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "USUM Totem Level Caps";
    public string Game { get; set; } = "USUM";
    public bool Enabled { get; set; } = true;
    public List<TotemLevelCapTemplateEntry> Entries { get; set; } = [];
}

public sealed class TotemLevelCapTemplateEntry
{
    public int EntryID { get; set; }
    public bool Use { get; set; } = true;
    public int LevelCap { get; set; }
}

public static class TotemLevelCapTemplateFile
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static string TemplateDirectory => CustomBalanceTemplates.GetTemplateDirectory();

    public static TotemLevelCapTemplate Load(string path, string currentGame)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Template path is empty.", nameof(path));

        string json = File.ReadAllText(path, Encoding.UTF8);
        var template = JsonSerializer.Deserialize<TotemLevelCapTemplate>(json, JsonOptions)
            ?? throw new InvalidDataException("The Totem Level Caps template is empty or invalid.");
        Validate(template, currentGame);
        return template;
    }

    public static void Save(string path, TotemLevelCapTemplate template, string currentGame)
    {
        Validate(template, currentGame);
        string folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(folder))
            Directory.CreateDirectory(folder);
        File.WriteAllText(path, JsonSerializer.Serialize(template, JsonOptions), Encoding.UTF8);
    }

    public static void Validate(TotemLevelCapTemplate template, string currentGame)
    {
        if (template is null) throw new ArgumentNullException(nameof(template));
        if (template.Version != 1)
            throw new InvalidDataException($"Unsupported Totem Level Caps template version {template.Version}. Expected version 1.");

        string wanted = NormalizeGame(template.Game);
        string actual = NormalizeGame(currentGame);
        if (wanted.Length != 0 && wanted != "ANY" && wanted != actual)
            throw new InvalidDataException($"This Totem Level Caps template is for {template.Game}, but the current game is {currentGame}.");

        var entries = template.Entries ?? [];
        if (entries.Any(e => e.EntryID < 0))
            throw new InvalidDataException("Totem Static Encounter IDs cannot be negative.");
        if (entries.Any(e => e.LevelCap < 0 || e.LevelCap > 100))
            throw new InvalidDataException("Totem Level Caps must be 0 (Current) or between 1 and 100.");
        if (entries.GroupBy(e => e.EntryID).Any(g => g.Count() > 1))
            throw new InvalidDataException("Duplicate Static Encounter IDs were found in the Totem Level Caps template.");
    }

    private static string NormalizeGame(string game)
    {
        if (string.IsNullOrWhiteSpace(game)) return string.Empty;
        return new string(game.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }
}