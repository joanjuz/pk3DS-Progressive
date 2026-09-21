using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// Strict numeric version used by external recipe identity and compatibility metadata.
/// Version 1 intentionally supports MAJOR.MINOR.PATCH only.
/// </summary>
public readonly record struct CroRecipeVersion(
    uint Major,
    uint Minor,
    uint Patch) : IComparable<CroRecipeVersion>
{
    public int CompareTo(
        CroRecipeVersion other)
    {
        int major =
            Major.CompareTo(
                other.Major);

        if (major != 0)
            return major;

        int minor =
            Minor.CompareTo(
                other.Minor);

        if (minor != 0)
            return minor;

        return Patch.CompareTo(
            other.Patch);
    }

    public override string ToString() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{Major}.{Minor}.{Patch}");
}

/// <summary>
/// Normalized, versioned representation of one external CRO mechanic recipe document.
/// Parsing is strict and conversion to the binary recipe model is explicit.
/// </summary>
public sealed class CroMechanicRecipeDocument
{
    public int FormatVersion { get; set; } = CroMechanicRecipeFormat.CurrentFormatVersion;
    public string Name { get; set; } = string.Empty;
    public string RecipeId { get; set; } = string.Empty;
    public CroRecipeVersion? Version { get; set; }
    public CroRecipeVersion? MinimumEngineVersion { get; set; }
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TargetGame { get; set; } = string.Empty;
    public string TargetRegion { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public List<CroMechanicRecipeDocumentEntry> Entries { get; set; } = [];
}

/// <summary>
/// One external recipe entry after JSON values have been normalized.
/// </summary>
public sealed class CroMechanicRecipeDocumentEntry
{
    public CroMechanicRecipeDomain Domain { get; set; }
    public uint Id { get; set; }
    public CroMechanicRecipeDocumentMechanic Mechanic { get; set; } = new();
}

/// <summary>
/// External representation of one mechanic package.
/// </summary>
public sealed class CroMechanicRecipeDocumentMechanic
{
    public string Name { get; set; } = string.Empty;
    public List<CroMechanicRecipeDocumentEffect> Effects { get; set; } = [];
}

/// <summary>
/// External representation of one timed mechanic effect.
/// Exactly one of <see cref="ExistingFunction"/> and non-empty <see cref="Code"/> is allowed.
/// </summary>
public sealed class CroMechanicRecipeDocumentEffect
{
    public byte Timing { get; set; }
    public string Name { get; set; } = string.Empty;
    public uint? ExistingFunction { get; set; }
    public byte[] Code { get; set; } = [];
}

/// <summary>
/// Strict JSON parser / serializer for versioned CRO mechanic recipe documents.
/// The binary <see cref="CroMechanicRecipeEngine"/> remains independent from this format layer.
/// </summary>
public static class CroMechanicRecipeFormat
{
    public const int CurrentFormatVersion = 1;

    public static bool TryParse(
        string json,
        out CroMechanicRecipeDocument document,
        out string error)
    {
        document = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "recipe JSON is empty.";
            return false;
        }

        JsonDocument parsed;

        try
        {
            parsed = JsonDocument.Parse(
                json,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                });
        }
        catch (JsonException ex)
        {
            error = "recipe JSON is invalid: " + ex.Message;
            return false;
        }

        using (parsed)
        {
            JsonElement root = parsed.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "recipe JSON root must be an object.";
                return false;
            }

            if (!TryValidateObjectProperties(
                    root,
                    "$",
                    ["formatVersion", "name", "entries"],
                    [
                        "formatVersion",
                        "name",
                        "recipeId",
                        "version",
                        "minimumEngineVersion",
                        "author",
                        "description",
                        "targetGame",
                        "targetRegion",
                        "tags",
                        "entries",
                    ],
                    out error))
            {
                return false;
            }

            if (!TryReadInt32(
                    root.GetProperty("formatVersion"),
                    "$.formatVersion",
                    out int formatVersion,
                    out error))
            {
                return false;
            }

            if (formatVersion != CurrentFormatVersion)
            {
                error =
                    $"unsupported recipe formatVersion {formatVersion}; expected {CurrentFormatVersion}.";
                return false;
            }

            if (!TryReadString(
                    root.GetProperty("name"),
                    "$.name",
                    out string name,
                    out error))
            {
                return false;
            }

            if (!TryReadOptionalString(
                    root,
                    "recipeId",
                    "$.recipeId",
                    out string recipeId,
                    out error) ||
                !TryReadOptionalRecipeVersion(
                    root,
                    "version",
                    "$.version",
                    out CroRecipeVersion? version,
                    out error) ||
                !TryReadOptionalRecipeVersion(
                    root,
                    "minimumEngineVersion",
                    "$.minimumEngineVersion",
                    out CroRecipeVersion? minimumEngineVersion,
                    out error) ||
                !TryReadOptionalString(
                    root,
                    "author",
                    "$.author",
                    out string author,
                    out error) ||
                !TryReadOptionalString(
                    root,
                    "description",
                    "$.description",
                    out string description,
                    out error) ||
                !TryReadOptionalString(
                    root,
                    "targetGame",
                    "$.targetGame",
                    out string targetGame,
                    out error) ||
                !TryReadOptionalString(
                    root,
                    "targetRegion",
                    "$.targetRegion",
                    out string targetRegion,
                    out error) ||
                !TryReadOptionalStringArray(
                    root,
                    "tags",
                    "$.tags",
                    out List<string> tags,
                    out error))
            {
                return false;
            }

            if (root.TryGetProperty(
                    "recipeId",
                    out _) &&
                string.IsNullOrEmpty(
                    recipeId))
            {
                error =
                    "$.recipeId must not be empty.";
                return false;
            }

            JsonElement entriesElement =
                root.GetProperty("entries");

            if (entriesElement.ValueKind != JsonValueKind.Array)
            {
                error = "$.entries must be an array.";
                return false;
            }

            if (entriesElement.GetArrayLength() == 0)
            {
                error = "$.entries must contain at least one entry.";
                return false;
            }

            var entries =
                new List<CroMechanicRecipeDocumentEntry>(
                    entriesElement.GetArrayLength());

            int entryIndex = 0;

            foreach (JsonElement entryElement in entriesElement.EnumerateArray())
            {
                string path =
                    $"$.entries[{entryIndex}]";

                if (!TryParseEntry(
                        entryElement,
                        path,
                        out CroMechanicRecipeDocumentEntry entry,
                        out error))
                {
                    return false;
                }

                entries.Add(entry);
                entryIndex++;
            }

            var candidate =
                new CroMechanicRecipeDocument
                {
                    FormatVersion = formatVersion,
                    Name = name,
                    RecipeId = recipeId,
                    Version = version,
                    MinimumEngineVersion = minimumEngineVersion,
                    Author = author,
                    Description = description,
                    TargetGame = targetGame,
                    TargetRegion = targetRegion,
                    Tags = tags,
                    Entries = entries,
                };

            if (!TryValidateDocument(
                    candidate,
                    out error))
            {
                return false;
            }

            document = candidate;
            return true;
        }
    }

    public static bool TryBuildRecipe(
        CroMechanicRecipeDocument document,
        out CroMechanicRecipe recipe,
        out string error)
    {
        recipe = null;
        error = string.Empty;

        if (!TryValidateDocument(
                document,
                out error))
        {
            return false;
        }

        var entries =
            new List<CroMechanicRecipeEntry>(
                document.Entries.Count);

        foreach (CroMechanicRecipeDocumentEntry sourceEntry in document.Entries)
        {
            var effects =
                new List<CroMechanicEffectSpec>(
                    sourceEntry.Mechanic.Effects.Count);

            foreach (CroMechanicRecipeDocumentEffect sourceEffect in sourceEntry.Mechanic.Effects)
            {
                effects.Add(
                    new CroMechanicEffectSpec
                    {
                        Timing = sourceEffect.Timing,
                        Name = sourceEffect.Name ?? string.Empty,
                        ExistingFunction = sourceEffect.ExistingFunction ?? 0u,
                        Code =
                            sourceEffect.Code is null
                                ? []
                                : (byte[])sourceEffect.Code.Clone(),
                    });
            }

            entries.Add(
                new CroMechanicRecipeEntry
                {
                    Domain = sourceEntry.Domain,
                    Id = sourceEntry.Id,
                    Mechanic =
                        new CroMechanicRequest
                        {
                            Name = sourceEntry.Mechanic.Name ?? string.Empty,
                            Effects = effects,
                        },
                });
        }

        recipe =
            new CroMechanicRecipe
            {
                Name = document.Name ?? string.Empty,
                Entries = entries,
            };

        return true;
    }

    public static bool TryParseRecipe(
        string json,
        out CroMechanicRecipeDocument document,
        out CroMechanicRecipe recipe,
        out string error)
    {
        document = null;
        recipe = null;
        error = string.Empty;

        if (!TryParse(
                json,
                out document,
                out error))
        {
            return false;
        }

        if (!TryBuildRecipe(
                document,
                out recipe,
                out error))
        {
            document = null;
            return false;
        }

        return true;
    }

    public static bool TryCreateDocument(
        CroMechanicRecipe recipe,
        out CroMechanicRecipeDocument document,
        out string error)
    {
        document = null;
        error = string.Empty;

        if (recipe is null)
        {
            error = "mechanic recipe is null.";
            return false;
        }

        if (recipe.Entries is null ||
            recipe.Entries.Count == 0)
        {
            error = "mechanic recipe has no entries.";
            return false;
        }

        var entries =
            new List<CroMechanicRecipeDocumentEntry>(
                recipe.Entries.Count);

        for (int i = 0; i < recipe.Entries.Count; i++)
        {
            CroMechanicRecipeEntry sourceEntry =
                recipe.Entries[i];

            if (sourceEntry is null)
            {
                error =
                    $"recipe entry #{i} is null.";
                return false;
            }

            if (sourceEntry.Mechanic is null)
            {
                error =
                    $"recipe entry #{i} mechanic is null.";
                return false;
            }

            if (sourceEntry.Mechanic.Effects is null)
            {
                error =
                    $"recipe entry #{i} mechanic effects are null.";
                return false;
            }

            var effects =
                new List<CroMechanicRecipeDocumentEffect>(
                    sourceEntry.Mechanic.Effects.Count);

            for (int effectIndex = 0;
                 effectIndex < sourceEntry.Mechanic.Effects.Count;
                 effectIndex++)
            {
                CroMechanicEffectSpec sourceEffect =
                    sourceEntry.Mechanic.Effects[effectIndex];

                if (sourceEffect is null)
                {
                    error =
                        $"recipe entry #{i} effect #{effectIndex} is null.";
                    return false;
                }

                effects.Add(
                    new CroMechanicRecipeDocumentEffect
                    {
                        Timing = sourceEffect.Timing,
                        Name = sourceEffect.Name ?? string.Empty,
                        ExistingFunction =
                            sourceEffect.ExistingFunction == 0
                                ? null
                                : sourceEffect.ExistingFunction,
                        Code =
                            sourceEffect.Code is null
                                ? []
                                : (byte[])sourceEffect.Code.Clone(),
                    });
            }

            entries.Add(
                new CroMechanicRecipeDocumentEntry
                {
                    Domain = sourceEntry.Domain,
                    Id = sourceEntry.Id,
                    Mechanic =
                        new CroMechanicRecipeDocumentMechanic
                        {
                            Name = sourceEntry.Mechanic.Name ?? string.Empty,
                            Effects = effects,
                        },
                });
        }

        var candidate =
            new CroMechanicRecipeDocument
            {
                FormatVersion = CurrentFormatVersion,
                Name = recipe.Name ?? string.Empty,
                Entries = entries,
            };

        if (!TryValidateDocument(
                candidate,
                out error))
        {
            return false;
        }

        document = candidate;
        return true;
    }

    public static bool TrySerialize(
        CroMechanicRecipeDocument document,
        out string json,
        out string error)
    {
        json = string.Empty;
        error = string.Empty;

        if (!TryValidateDocument(
                document,
                out error))
        {
            return false;
        }

        using var stream =
            new MemoryStream();

        using (var writer =
               new Utf8JsonWriter(
                   stream,
                   new JsonWriterOptions
                   {
                       Indented = true,
                   }))
        {
            writer.WriteStartObject();

            writer.WriteNumber(
                "formatVersion",
                document.FormatVersion);

            writer.WriteString(
                "name",
                document.Name ?? string.Empty);

            if (!string.IsNullOrEmpty(
                    document.RecipeId))
            {
                writer.WriteString(
                    "recipeId",
                    document.RecipeId);
            }

            if (document.Version.HasValue)
            {
                writer.WriteString(
                    "version",
                    document.Version.Value.ToString());
            }

            if (document.MinimumEngineVersion.HasValue)
            {
                writer.WriteString(
                    "minimumEngineVersion",
                    document.MinimumEngineVersion.Value.ToString());
            }

            if (!string.IsNullOrEmpty(
                    document.Author))
            {
                writer.WriteString(
                    "author",
                    document.Author);
            }

            if (!string.IsNullOrEmpty(
                    document.Description))
            {
                writer.WriteString(
                    "description",
                    document.Description);
            }

            if (!string.IsNullOrEmpty(
                    document.TargetGame))
            {
                writer.WriteString(
                    "targetGame",
                    document.TargetGame);
            }

            if (!string.IsNullOrEmpty(
                    document.TargetRegion))
            {
                writer.WriteString(
                    "targetRegion",
                    document.TargetRegion);
            }

            if (document.Tags is { Count: > 0 })
            {
                writer.WriteStartArray(
                    "tags");

                foreach (string tag in document.Tags)
                    writer.WriteStringValue(tag);

                writer.WriteEndArray();
            }

            writer.WriteStartArray(
                "entries");

            foreach (CroMechanicRecipeDocumentEntry entry in document.Entries)
            {
                writer.WriteStartObject();

                writer.WriteString(
                    "domain",
                    entry.Domain.ToString());

                writer.WriteNumber(
                    "id",
                    entry.Id);

                writer.WriteStartObject(
                    "mechanic");

                writer.WriteString(
                    "name",
                    entry.Mechanic.Name ?? string.Empty);

                writer.WriteStartArray(
                    "effects");

                foreach (CroMechanicRecipeDocumentEffect effect in entry.Mechanic.Effects)
                {
                    writer.WriteStartObject();

                    writer.WriteNumber(
                        "timing",
                        effect.Timing);

                    writer.WriteString(
                        "name",
                        effect.Name ?? string.Empty);

                    if (effect.ExistingFunction.HasValue)
                    {
                        writer.WriteString(
                            "existingFunction",
                            $"0x{effect.ExistingFunction.Value:X8}");
                    }
                    else
                    {
                        writer.WriteString(
                            "code",
                            FormatCode(effect.Code));
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.Flush();
        }

        json =
            Encoding.UTF8.GetString(
                stream.ToArray());

        return true;
    }

    public static bool TrySerializeRecipe(
        CroMechanicRecipe recipe,
        out string json,
        out string error)
    {
        json = string.Empty;
        error = string.Empty;

        if (!TryCreateDocument(
                recipe,
                out CroMechanicRecipeDocument document,
                out error))
        {
            return false;
        }

        return TrySerialize(
            document,
            out json,
            out error);
    }

    private static bool TryParseEntry(
        JsonElement element,
        string path,
        out CroMechanicRecipeDocumentEntry entry,
        out string error)
    {
        entry = null;
        error = string.Empty;

        if (element.ValueKind != JsonValueKind.Object)
        {
            error = $"{path} must be an object.";
            return false;
        }

        if (!TryValidateObjectProperties(
                element,
                path,
                ["domain", "id", "mechanic"],
                ["domain", "id", "mechanic"],
                out error))
        {
            return false;
        }

        if (!TryReadString(
                element.GetProperty("domain"),
                path + ".domain",
                out string domainText,
                out error))
        {
            return false;
        }

        if (!Enum.TryParse(
                domainText,
                ignoreCase: true,
                out CroMechanicRecipeDomain domain) ||
            !IsSupportedDomain(domain))
        {
            error =
                $"{path}.domain '{domainText}' is unsupported; expected Item, Ability or Move.";
            return false;
        }

        if (!TryReadUInt32(
                element.GetProperty("id"),
                path + ".id",
                allowHexString: true,
                out uint id,
                out error))
        {
            return false;
        }

        if (!TryParseMechanic(
                element.GetProperty("mechanic"),
                path + ".mechanic",
                out CroMechanicRecipeDocumentMechanic mechanic,
                out error))
        {
            return false;
        }

        entry =
            new CroMechanicRecipeDocumentEntry
            {
                Domain = domain,
                Id = id,
                Mechanic = mechanic,
            };

        return true;
    }

    private static bool TryParseMechanic(
        JsonElement element,
        string path,
        out CroMechanicRecipeDocumentMechanic mechanic,
        out string error)
    {
        mechanic = null;
        error = string.Empty;

        if (element.ValueKind != JsonValueKind.Object)
        {
            error = $"{path} must be an object.";
            return false;
        }

        if (!TryValidateObjectProperties(
                element,
                path,
                ["effects"],
                ["name", "effects"],
                out error))
        {
            return false;
        }

        string name =
            string.Empty;

        if (element.TryGetProperty(
                "name",
                out JsonElement nameElement) &&
            !TryReadString(
                nameElement,
                path + ".name",
                out name,
                out error))
        {
            return false;
        }

        JsonElement effectsElement =
            element.GetProperty("effects");

        if (effectsElement.ValueKind != JsonValueKind.Array)
        {
            error = $"{path}.effects must be an array.";
            return false;
        }

        if (effectsElement.GetArrayLength() == 0)
        {
            error = $"{path}.effects must contain at least one effect.";
            return false;
        }

        var effects =
            new List<CroMechanicRecipeDocumentEffect>(
                effectsElement.GetArrayLength());

        int effectIndex = 0;

        foreach (JsonElement effectElement in effectsElement.EnumerateArray())
        {
            if (!TryParseEffect(
                    effectElement,
                    $"{path}.effects[{effectIndex}]",
                    out CroMechanicRecipeDocumentEffect effect,
                    out error))
            {
                return false;
            }

            effects.Add(effect);
            effectIndex++;
        }

        mechanic =
            new CroMechanicRecipeDocumentMechanic
            {
                Name = name,
                Effects = effects,
            };

        return true;
    }

    private static bool TryParseEffect(
        JsonElement element,
        string path,
        out CroMechanicRecipeDocumentEffect effect,
        out string error)
    {
        effect = null;
        error = string.Empty;

        if (element.ValueKind != JsonValueKind.Object)
        {
            error = $"{path} must be an object.";
            return false;
        }

        if (!TryValidateObjectProperties(
                element,
                path,
                ["timing"],
                ["timing", "name", "existingFunction", "code"],
                out error))
        {
            return false;
        }

        if (!TryReadByte(
                element.GetProperty("timing"),
                path + ".timing",
                out byte timing,
                out error))
        {
            return false;
        }

        string name =
            string.Empty;

        if (element.TryGetProperty(
                "name",
                out JsonElement nameElement) &&
            !TryReadString(
                nameElement,
                path + ".name",
                out name,
                out error))
        {
            return false;
        }

        bool hasExisting =
            element.TryGetProperty(
                "existingFunction",
                out JsonElement existingElement);

        bool hasCode =
            element.TryGetProperty(
                "code",
                out JsonElement codeElement);

        if (hasExisting == hasCode)
        {
            error =
                $"{path} must provide exactly one of existingFunction or code.";
            return false;
        }

        uint? existingFunction =
            null;

        byte[] code =
            [];

        if (hasExisting)
        {
            if (!TryReadUInt32(
                    existingElement,
                    path + ".existingFunction",
                    allowHexString: true,
                    out uint address,
                    out error))
            {
                return false;
            }

            if (address == 0)
            {
                error =
                    $"{path}.existingFunction must be non-zero.";
                return false;
            }

            existingFunction =
                address;
        }
        else
        {
            if (!TryReadString(
                    codeElement,
                    path + ".code",
                    out string codeText,
                    out error) ||
                !TryParseCode(
                    codeText,
                    path + ".code",
                    out code,
                    out error))
            {
                return false;
            }

            if (code.Length == 0)
            {
                error =
                    $"{path}.code must contain at least one byte.";
                return false;
            }
        }

        effect =
            new CroMechanicRecipeDocumentEffect
            {
                Timing = timing,
                Name = name,
                ExistingFunction = existingFunction,
                Code = code,
            };

        return true;
    }

    private static bool TryValidateDocument(
        CroMechanicRecipeDocument document,
        out string error)
    {
        error = string.Empty;

        if (document is null)
        {
            error = "recipe document is null.";
            return false;
        }

        if (document.FormatVersion != CurrentFormatVersion)
        {
            error =
                $"unsupported recipe formatVersion {document.FormatVersion}; expected {CurrentFormatVersion}.";
            return false;
        }

        bool hasRecipeId =
            !string.IsNullOrEmpty(
                document.RecipeId);

        bool hasVersion =
            document.Version.HasValue;

        if (hasRecipeId != hasVersion)
        {
            error =
                "recipe document recipeId and version must either both be present or both be omitted.";
            return false;
        }

        if (hasRecipeId &&
            !TryValidateRecipeId(
                document.RecipeId,
                out error))
        {
            error =
                "recipe document recipeId " +
                error;
            return false;
        }

        if (document.Tags is null)
        {
            error = "recipe document tags are null.";
            return false;
        }

        var seenTags =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < document.Tags.Count; i++)
        {
            string tag =
                document.Tags[i];

            if (string.IsNullOrWhiteSpace(tag))
            {
                error =
                    $"recipe document tag #{i} is empty.";
                return false;
            }

            if (!seenTags.Add(tag))
            {
                error =
                    $"recipe document contains duplicate tag '{tag}'.";
                return false;
            }
        }

        if (document.Entries is null ||
            document.Entries.Count == 0)
        {
            error = "recipe document has no entries.";
            return false;
        }

        var seenIds =
            new HashSet<(CroMechanicRecipeDomain Domain, uint Id)>();

        for (int i = 0; i < document.Entries.Count; i++)
        {
            CroMechanicRecipeDocumentEntry entry =
                document.Entries[i];

            if (entry is null)
            {
                error =
                    $"recipe document entry #{i} is null.";
                return false;
            }

            if (!IsSupportedDomain(entry.Domain))
            {
                error =
                    $"recipe document entry #{i} has unsupported domain value {(int)entry.Domain}.";
                return false;
            }

            if (!seenIds.Add(
                    (entry.Domain, entry.Id)))
            {
                error =
                    $"recipe document contains duplicate {entry.Domain} id 0x{entry.Id:X8}.";
                return false;
            }

            if (entry.Mechanic is null)
            {
                error =
                    $"recipe document entry #{i} mechanic is null.";
                return false;
            }

            if (entry.Mechanic.Effects is null ||
                entry.Mechanic.Effects.Count == 0)
            {
                error =
                    $"recipe document entry #{i} mechanic has no effects.";
                return false;
            }

            if (entry.Mechanic.Effects.Count > byte.MaxValue)
            {
                error =
                    $"recipe document entry #{i} mechanic has {entry.Mechanic.Effects.Count} effects; maximum is {byte.MaxValue}.";
                return false;
            }

            var timings =
                new HashSet<byte>();

            for (int effectIndex = 0;
                 effectIndex < entry.Mechanic.Effects.Count;
                 effectIndex++)
            {
                CroMechanicRecipeDocumentEffect effect =
                    entry.Mechanic.Effects[effectIndex];

                if (effect is null)
                {
                    error =
                        $"recipe document entry #{i} effect #{effectIndex} is null.";
                    return false;
                }

                if (!timings.Add(effect.Timing))
                {
                    error =
                        $"recipe document entry #{i} timing 0x{effect.Timing:X2} appears more than once.";
                    return false;
                }

                bool hasExisting =
                    effect.ExistingFunction.HasValue;

                bool hasCode =
                    effect.Code is { Length: > 0 };

                if (hasExisting == hasCode)
                {
                    error =
                        $"recipe document entry #{i} effect #{effectIndex} must provide exactly one of ExistingFunction or Code.";
                    return false;
                }

                if (hasExisting &&
                    effect.ExistingFunction.Value == 0)
                {
                    error =
                        $"recipe document entry #{i} effect #{effectIndex} ExistingFunction must be non-zero.";
                    return false;
                }
            }
        }

        return true;
    }

    private static bool TryValidateObjectProperties(
        JsonElement element,
        string path,
        IReadOnlyCollection<string> required,
        IReadOnlyCollection<string> allowed,
        out string error)
    {
        error = string.Empty;

        var seen =
            new HashSet<string>(
                StringComparer.Ordinal);

        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!seen.Add(property.Name))
            {
                error =
                    $"{path} contains duplicate property '{property.Name}'.";
                return false;
            }

            if (!allowed.Contains(property.Name))
            {
                error =
                    $"{path} contains unknown property '{property.Name}'.";
                return false;
            }
        }

        foreach (string name in required)
        {
            if (!seen.Contains(name))
            {
                error =
                    $"{path} is missing required property '{name}'.";
                return false;
            }
        }

        return true;
    }

    private static bool TryReadString(
        JsonElement element,
        string path,
        out string value,
        out string error)
    {
        value = string.Empty;
        error = string.Empty;

        if (element.ValueKind != JsonValueKind.String)
        {
            error = $"{path} must be a string.";
            return false;
        }

        value =
            element.GetString() ??
            string.Empty;

        return true;
    }

    private static bool TryReadOptionalString(
        JsonElement parent,
        string propertyName,
        string path,
        out string value,
        out string error)
    {
        value = string.Empty;
        error = string.Empty;

        if (!parent.TryGetProperty(
                propertyName,
                out JsonElement element))
        {
            return true;
        }

        return TryReadString(
            element,
            path,
            out value,
            out error);
    }

    private static bool TryReadOptionalRecipeVersion(
        JsonElement parent,
        string propertyName,
        string path,
        out CroRecipeVersion? value,
        out string error)
    {
        value =
            null;

        error =
            string.Empty;

        if (!parent.TryGetProperty(
                propertyName,
                out JsonElement element))
        {
            return true;
        }

        if (!TryReadString(
                element,
                path,
                out string text,
                out error))
        {
            return false;
        }

        if (!TryParseRecipeVersion(
                text,
                out CroRecipeVersion parsed))
        {
            error =
                $"{path} must use MAJOR.MINOR.PATCH with unsigned decimal components, for example '1.2.3'.";
            return false;
        }

        value =
            parsed;

        return true;
    }

    private static bool TryParseRecipeVersion(
        string text,
        out CroRecipeVersion version)
    {
        version =
            default;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        string[] parts =
            text.Split(
                '.');

        if (parts.Length != 3)
            return false;

        var values =
            new uint[3];

        for (int i = 0; i < parts.Length; i++)
        {
            string part =
                parts[i];

            if (part.Length == 0 ||
                (part.Length > 1 &&
                 part[0] == '0') ||
                !uint.TryParse(
                    part,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out values[i]))
            {
                return false;
            }
        }

        version =
            new CroRecipeVersion(
                Major: values[0],
                Minor: values[1],
                Patch: values[2]);

        return true;
    }

    private static bool TryValidateRecipeId(
        string recipeId,
        out string error)
    {
        error =
            string.Empty;

        if (string.IsNullOrEmpty(recipeId))
        {
            error =
                "must not be empty.";
            return false;
        }

        if (recipeId.Length > 128)
        {
            error =
                "must not exceed 128 characters.";
            return false;
        }

        static bool IsAsciiLowerLetterOrDigit(
            char c) =>
            (c >= 'a' && c <= 'z') ||
            (c >= '0' && c <= '9');

        if (!IsAsciiLowerLetterOrDigit(
                recipeId[0]) ||
            !IsAsciiLowerLetterOrDigit(
                recipeId[^1]))
        {
            error =
                "must start and end with a lowercase ASCII letter or digit.";
            return false;
        }

        for (int i = 0; i < recipeId.Length; i++)
        {
            char c =
                recipeId[i];

            if (IsAsciiLowerLetterOrDigit(c) ||
                c == '.' ||
                c == '-' ||
                c == '_')
            {
                continue;
            }

            error =
                "may contain only lowercase ASCII letters, digits, '.', '-' and '_'.";
            return false;
        }

        return true;
    }

    private static bool TryReadOptionalStringArray(
        JsonElement parent,
        string propertyName,
        string path,
        out List<string> values,
        out string error)
    {
        values = [];
        error = string.Empty;

        if (!parent.TryGetProperty(
                propertyName,
                out JsonElement element))
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            error =
                $"{path} must be an array.";
            return false;
        }

        var result =
            new List<string>(
                element.GetArrayLength());

        var seen =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        int index =
            0;

        foreach (JsonElement item in element.EnumerateArray())
        {
            if (!TryReadString(
                    item,
                    $"{path}[{index}]",
                    out string value,
                    out error))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                error =
                    $"{path}[{index}] must not be empty.";
                return false;
            }

            if (!seen.Add(value))
            {
                error =
                    $"{path} contains duplicate tag '{value}'.";
                return false;
            }

            result.Add(value);
            index++;
        }

        values =
            result;

        return true;
    }

    private static bool TryReadInt32(
        JsonElement element,
        string path,
        out int value,
        out string error)
    {
        value = 0;
        error = string.Empty;

        if (element.ValueKind != JsonValueKind.Number ||
            !element.TryGetInt32(out value))
        {
            error = $"{path} must be a 32-bit integer.";
            return false;
        }

        return true;
    }

    private static bool TryReadByte(
        JsonElement element,
        string path,
        out byte value,
        out string error)
    {
        value = 0;
        error = string.Empty;

        if (element.ValueKind == JsonValueKind.Number)
        {
            if (element.TryGetInt32(out int number) &&
                number >= byte.MinValue &&
                number <= byte.MaxValue)
            {
                value = (byte)number;
                return true;
            }

            error =
                $"{path} must be an integer from 0 to 255.";
            return false;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            string text =
                element.GetString() ??
                string.Empty;

            if (TryParseUInt32Text(
                    text,
                    out uint parsed) &&
                parsed <= byte.MaxValue)
            {
                value = (byte)parsed;
                return true;
            }
        }

        error =
            $"{path} must be an integer from 0 to 255 or a hexadecimal string such as '0x59'.";
        return false;
    }

    private static bool TryReadUInt32(
        JsonElement element,
        string path,
        bool allowHexString,
        out uint value,
        out string error)
    {
        value = 0;
        error = string.Empty;

        if (element.ValueKind == JsonValueKind.Number &&
            element.TryGetUInt32(out value))
        {
            return true;
        }

        if (allowHexString &&
            element.ValueKind == JsonValueKind.String)
        {
            string text =
                element.GetString() ??
                string.Empty;

            if (TryParseUInt32Text(
                    text,
                    out value))
            {
                return true;
            }
        }

        error =
            $"{path} must be an unsigned 32-bit integer or a numeric string such as '0x000C23E0'.";
        return false;
    }

    private static bool TryParseUInt32Text(
        string text,
        out uint value)
    {
        value = 0;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        string trimmed =
            text.Trim();

        if (trimmed.StartsWith(
                "0x",
                StringComparison.OrdinalIgnoreCase))
        {
            string hex =
                trimmed[2..];

            return hex.Length > 0 &&
                   uint.TryParse(
                       hex,
                       NumberStyles.AllowHexSpecifier,
                       CultureInfo.InvariantCulture,
                       out value);
        }

        return uint.TryParse(
            trimmed,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static bool TryParseCode(
        string text,
        string path,
        out byte[] code,
        out string error)
    {
        code = [];
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
            return true;

        string trimmed =
            text.Trim();

        string[] tokens =
            trimmed.Split(
                (char[])null,
                StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length == 1)
        {
            string compact =
                tokens[0];

            if (compact.StartsWith(
                    "0x",
                    StringComparison.OrdinalIgnoreCase))
            {
                compact =
                    compact[2..];
            }

            if (compact.Length > 2)
            {
                if ((compact.Length & 1) != 0)
                {
                    error =
                        $"{path} compact hexadecimal byte string must contain an even number of digits.";
                    return false;
                }

                var compactBytes =
                    new byte[compact.Length / 2];

                for (int i = 0; i < compactBytes.Length; i++)
                {
                    if (!byte.TryParse(
                            compact.AsSpan(i * 2, 2),
                            NumberStyles.AllowHexSpecifier,
                            CultureInfo.InvariantCulture,
                            out compactBytes[i]))
                    {
                        error =
                            $"{path} contains invalid hexadecimal bytes.";
                        return false;
                    }
                }

                code = compactBytes;
                return true;
            }
        }

        var bytes =
            new byte[tokens.Length];

        for (int i = 0; i < tokens.Length; i++)
        {
            string token =
                tokens[i];

            if (token.StartsWith(
                    "0x",
                    StringComparison.OrdinalIgnoreCase))
            {
                token =
                    token[2..];
            }

            if (token.Length == 0 ||
                token.Length > 2 ||
                !byte.TryParse(
                    token,
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture,
                    out bytes[i]))
            {
                error =
                    $"{path} token #{i} ('{tokens[i]}') is not a valid hexadecimal byte.";
                return false;
            }
        }

        code = bytes;
        return true;
    }

    private static string FormatCode(
        byte[] code)
    {
        if (code is null ||
            code.Length == 0)
        {
            return string.Empty;
        }

        var builder =
            new StringBuilder(
                (code.Length * 3) - 1);

        for (int i = 0; i < code.Length; i++)
        {
            if (i != 0)
                builder.Append(' ');

            builder.Append(
                code[i].ToString(
                    "X2",
                    CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static bool IsSupportedDomain(
        CroMechanicRecipeDomain domain) =>
        domain is
            CroMechanicRecipeDomain.Item or
            CroMechanicRecipeDomain.Ability or
            CroMechanicRecipeDomain.Move;
}
