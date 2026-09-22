using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace pk3DS.WinForms;

internal enum ExpandedMartOperationKind
{
    Add,
    Delete,
}

internal sealed record ExpandedMartOperation(
    ExpandedMartOperationKind Kind,
    int MartIndex,
    int SlotIndex,
    ushort ItemId = 0);

internal static class ExpandedMartTemplateAction
{
    internal const string ActionId = "marts.expanded-layout";
    internal const int FormatVersion = 1;

    private const int MartCount = 28;
    private const int MaxOperations = 4096;
    private const int MaxSlotsPerMart = 0x7F;

    internal static GlobalRandomizationAction Create(
        IEnumerable<ExpandedMartOperation> operations)
    {
        var list = (operations ?? [])
            .ToList();

        if (list.Count == 0)
            throw new ArgumentException(
                "Expanded Marts action requires at least one operation.",
                nameof(operations));

        return new GlobalRandomizationAction
        {
            Id = ActionId,
            Parameters = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["version"] = FormatVersion.ToString(
                    CultureInfo.InvariantCulture),
                ["operations"] = Serialize(
                    list),
            },
        };
    }

    internal static bool TryParse(
        GlobalRandomizationAction action,
        out List<ExpandedMartOperation> operations,
        out string error)
    {
        operations = [];
        error = string.Empty;

        if (action is null ||
            !string.Equals(
                action.Id,
                ActionId,
                StringComparison.OrdinalIgnoreCase))
        {
            error =
                $"Expected action '{ActionId}'.";
            return false;
        }

        if (action.Parameters is null ||
            !action.Parameters.TryGetValue(
                "version",
                out string rawVersion) ||
            !int.TryParse(
                rawVersion,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int version) ||
            version != FormatVersion)
        {
            error =
                $"Unsupported Expanded Marts action version. Expected {FormatVersion}.";
            return false;
        }

        if (!action.Parameters.TryGetValue(
                "operations",
                out string rawOperations) ||
            string.IsNullOrWhiteSpace(
                rawOperations))
        {
            error =
                "Expanded Marts action has no operations.";
            return false;
        }

        string[] tokens =
            rawOperations.Split(
                '|',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

        if (tokens.Length == 0 ||
            tokens.Length > MaxOperations)
        {
            error =
                $"Expanded Marts action must contain between 1 and {MaxOperations} operations.";
            return false;
        }

        var parsed =
            new List<ExpandedMartOperation>(
                tokens.Length);

        for (int i = 0;
             i < tokens.Length;
             i++)
        {
            string[] parts =
                tokens[i].Split(
                    ',',
                    StringSplitOptions.TrimEntries);

            if (parts.Length is not (3 or 4))
            {
                error =
                    $"Expanded Marts operation #{i} has an invalid field count.";
                return false;
            }

            if (!int.TryParse(
                    parts[1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int mart) ||
                mart < 0 ||
                mart >= MartCount)
            {
                error =
                    $"Expanded Marts operation #{i} has invalid mart index '{parts[1]}'.";
                return false;
            }

            if (!int.TryParse(
                    parts[2],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int slot) ||
                slot < 0 ||
                slot > MaxSlotsPerMart)
            {
                error =
                    $"Expanded Marts operation #{i} has invalid slot index '{parts[2]}'.";
                return false;
            }

            if (string.Equals(
                    parts[0],
                    "A",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (parts.Length != 4 ||
                    !ushort.TryParse(
                        parts[3],
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out ushort item))
                {
                    error =
                        $"Expanded Marts add operation #{i} has invalid item ID.";
                    return false;
                }

                parsed.Add(
                    new ExpandedMartOperation(
                        ExpandedMartOperationKind.Add,
                        mart,
                        slot,
                        item));

                continue;
            }

            if (string.Equals(
                    parts[0],
                    "D",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (parts.Length != 3)
                {
                    error =
                        $"Expanded Marts delete operation #{i} must have exactly 3 fields.";
                    return false;
                }

                parsed.Add(
                    new ExpandedMartOperation(
                        ExpandedMartOperationKind.Delete,
                        mart,
                        slot));

                continue;
            }

            error =
                $"Expanded Marts operation #{i} has unknown kind '{parts[0]}'.";
            return false;
        }

        operations =
            parsed;

        return true;
    }

    internal static bool TryApply(
        IReadOnlyList<List<ushort>> marts,
        IReadOnlyList<ExpandedMartOperation> operations,
        out string error)
    {
        error = string.Empty;

        if (marts is null ||
            marts.Count != MartCount)
        {
            error =
                $"Expanded Marts requires exactly {MartCount} regular marts.";
            return false;
        }

        if (operations is null ||
            operations.Count == 0)
        {
            error =
                "Expanded Marts has no operations to apply.";
            return false;
        }

        var staged =
            marts
                .Select(z =>
                    z is null
                        ? null
                        : new List<ushort>(z))
                .ToArray();

        if (staged.Any(z => z is null))
        {
            error =
                "Expanded Marts contains a null mart inventory.";
            return false;
        }

        for (int i = 0;
             i < operations.Count;
             i++)
        {
            ExpandedMartOperation operation =
                operations[i];

            if (operation.MartIndex < 0 ||
                operation.MartIndex >= staged.Length)
            {
                error =
                    $"Expanded Marts operation #{i} targets invalid mart {operation.MartIndex}.";
                return false;
            }

            List<ushort> mart =
                staged[operation.MartIndex];

            if (operation.Kind ==
                ExpandedMartOperationKind.Add)
            {
                if (mart.Count >= MaxSlotsPerMart)
                {
                    error =
                        $"Expanded Marts operation #{i} would exceed {MaxSlotsPerMart} slots in mart {operation.MartIndex}.";
                    return false;
                }

                if (operation.SlotIndex < 0 ||
                    operation.SlotIndex > mart.Count)
                {
                    error =
                        $"Expanded Marts add operation #{i} targets slot {operation.SlotIndex}, but mart {operation.MartIndex} currently has {mart.Count} slots.";
                    return false;
                }

                mart.Insert(
                    operation.SlotIndex,
                    operation.ItemId);

                continue;
            }

            if (operation.Kind ==
                ExpandedMartOperationKind.Delete)
            {
                if (mart.Count <= 1)
                {
                    error =
                        $"Expanded Marts operation #{i} would leave mart {operation.MartIndex} empty.";
                    return false;
                }

                if (operation.SlotIndex < 0 ||
                    operation.SlotIndex >= mart.Count)
                {
                    error =
                        $"Expanded Marts delete operation #{i} targets slot {operation.SlotIndex}, but mart {operation.MartIndex} currently has {mart.Count} slots.";
                    return false;
                }

                mart.RemoveAt(
                    operation.SlotIndex);

                continue;
            }

            error =
                $"Expanded Marts operation #{i} has unsupported kind {operation.Kind}.";
            return false;
        }

        for (int mart = 0;
             mart < staged.Length;
             mart++)
        {
            marts[mart].Clear();
            marts[mart].AddRange(
                staged[mart]);
        }

        return true;
    }

    internal static void Validate(
        GlobalRandomizationAction action,
        string currentGame)
    {
        string normalizedGame =
            NormalizeGame(
                currentGame);

        if (normalizedGame != "USUM")
        {
            throw new InvalidOperationException(
                "Expanded Marts templates are currently supported only for Ultra Sun / Ultra Moon.");
        }

        if (!TryParse(
                action,
                out _,
                out string error))
        {
            throw new InvalidOperationException(
                error);
        }
    }

    private static string Serialize(
        IReadOnlyList<ExpandedMartOperation> operations)
    {
        return string.Join(
            "|",
            operations.Select(
                operation =>
                    operation.Kind ==
                    ExpandedMartOperationKind.Add
                        ? string.Join(
                            ",",
                            "A",
                            operation.MartIndex.ToString(
                                CultureInfo.InvariantCulture),
                            operation.SlotIndex.ToString(
                                CultureInfo.InvariantCulture),
                            operation.ItemId.ToString(
                                CultureInfo.InvariantCulture))
                        : string.Join(
                            ",",
                            "D",
                            operation.MartIndex.ToString(
                                CultureInfo.InvariantCulture),
                            operation.SlotIndex.ToString(
                                CultureInfo.InvariantCulture))));
    }

    private static string NormalizeGame(
        string game)
    {
        if (string.IsNullOrWhiteSpace(
                game))
        {
            return string.Empty;
        }

        string normalized =
            new string(
                game
                    .Where(
                        char.IsLetterOrDigit)
                    .ToArray())
                .ToUpperInvariant();

        return normalized switch
        {
            "ULTRASUNULTRAMOON" => "USUM",
            _ => normalized,
        };
    }
}