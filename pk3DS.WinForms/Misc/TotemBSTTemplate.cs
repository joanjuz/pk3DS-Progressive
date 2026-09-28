using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace pk3DS.WinForms;

public sealed class TotemBSTTemplate
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "USUM Totem BST";
    public string Game { get; set; } = "USUM";
    public bool Enabled { get; set; } = true;
    public List<TotemBSTTemplateEntry> Entries { get; set; } = [];
}

public sealed class TotemBSTTemplateEntry
{
    public int EntryID { get; set; }
    public bool Use { get; set; } = true;
    public int MinBST { get; set; }
    public int MaxBST { get; set; }
}

public static class TotemBSTTemplateFile
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static string TemplateDirectory =>
        CustomBalanceTemplates.GetTemplateDirectory();

    public static TotemBSTTemplate Load(
        string path,
        string currentGame)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException(
                "Template path is empty.",
                nameof(path));

        string json =
            File.ReadAllText(
                path,
                Encoding.UTF8);

        var template =
            JsonSerializer.Deserialize<TotemBSTTemplate>(
                json,
                JsonOptions)
            ?? throw new InvalidDataException(
                "The Totem BST template is empty or invalid.");

        Validate(template, currentGame);
        return template;
    }

    public static void Save(
        string path,
        TotemBSTTemplate template,
        string currentGame)
    {
        Validate(template, currentGame);

        string folder =
            Path.GetDirectoryName(path);

        if (!string.IsNullOrWhiteSpace(folder))
            Directory.CreateDirectory(folder);

        File.WriteAllText(
            path,
            JsonSerializer.Serialize(
                template,
                JsonOptions),
            Encoding.UTF8);
    }

    public static void Validate(
        TotemBSTTemplate template,
        string currentGame)
    {
        if (template is null)
            throw new ArgumentNullException(
                nameof(template));

        if (template.Version != 1)
        {
            throw new InvalidDataException(
                $"Unsupported Totem BST template version {template.Version}. Expected version 1.");
        }

        string wanted =
            NormalizeGame(template.Game);

        string actual =
            NormalizeGame(currentGame);

        if (wanted.Length != 0 &&
            wanted != "ANY" &&
            wanted != actual)
        {
            throw new InvalidDataException(
                $"This Totem BST template is for {template.Game}, but the current game is {currentGame}.");
        }

        var entries =
            template.Entries ?? [];

        if (entries.Any(
                entry =>
                    entry.EntryID < 0))
        {
            throw new InvalidDataException(
                "Totem Static Encounter IDs cannot be negative.");
        }

        if (entries.Any(
                entry =>
                    entry.MinBST < 1 ||
                    entry.MaxBST < 1 ||
                    entry.MinBST > 2000 ||
                    entry.MaxBST > 2000 ||
                    entry.MinBST > entry.MaxBST))
        {
            throw new InvalidDataException(
                "Totem BST values must be between 1 and 2000, and MinBST may not exceed MaxBST.");
        }

        if (entries
            .GroupBy(entry => entry.EntryID)
            .Any(group => group.Count() > 1))
        {
            throw new InvalidDataException(
                "Duplicate Static Encounter IDs were found in the Totem BST template.");
        }
    }

    private static string NormalizeGame(
        string game)
    {
        if (string.IsNullOrWhiteSpace(game))
            return string.Empty;

        return new string(
            game.Where(char.IsLetterOrDigit).ToArray())
            .ToUpperInvariant();
    }
}