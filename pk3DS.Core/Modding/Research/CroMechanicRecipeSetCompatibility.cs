using System;
using System.Collections.Generic;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// Result of validating a set of external CRO mechanic recipe documents.
/// </summary>
public sealed record CroMechanicRecipeSetCompatibilityReport(
    CroRecipeVersion EngineVersion,
    int DocumentCount,
    int IdentifiedRecipeCount,
    int DependencyCount);

/// <summary>
/// Validates dependencies and conflicts across a set of recipe documents.
/// This layer does not modify CRO data and does not impose installation order.
/// </summary>
public static class CroMechanicRecipeSetCompatibility
{
    public static bool TryCheck(
        IReadOnlyList<CroMechanicRecipeDocument> documents,
        out CroMechanicRecipeSetCompatibilityReport report,
        out string error) =>
        TryCheck(
            documents,
            CroMechanicRecipeCompatibility.CurrentEngineVersion,
            out report,
            out error);

    public static bool TryCheck(
        IReadOnlyList<CroMechanicRecipeDocument> documents,
        CroRecipeVersion engineVersion,
        out CroMechanicRecipeSetCompatibilityReport report,
        out string error)
    {
        report =
            null;

        error =
            string.Empty;

        if (documents is null)
        {
            error =
                "recipe document set is null.";
            return false;
        }

        var identified =
            new Dictionary<string, CroMechanicRecipeDocument>(
                StringComparer.Ordinal);

        for (int i = 0; i < documents.Count; i++)
        {
            CroMechanicRecipeDocument document =
                documents[i];

            if (document is null)
            {
                error =
                    $"recipe document set entry #{i} is null.";
                return false;
            }

            if (!CroMechanicRecipeCompatibility.TryCheck(
                    document,
                    engineVersion,
                    out _,
                    out error))
            {
                return false;
            }

            if (string.IsNullOrEmpty(
                    document.RecipeId))
            {
                continue;
            }

            if (!identified.TryAdd(
                    document.RecipeId,
                    document))
            {
                error =
                    $"recipe document set contains duplicate recipeId '{document.RecipeId}'.";
                return false;
            }
        }

        int dependencyCount =
            0;

        for (int i = 0; i < documents.Count; i++)
        {
            CroMechanicRecipeDocument document =
                documents[i];

            string identity =
                string.IsNullOrEmpty(
                    document.RecipeId)
                    ? document.Name ?? string.Empty
                    : document.RecipeId;

            foreach (CroMechanicRecipeDependency dependency in document.Dependencies)
            {
                dependencyCount++;

                if (!identified.TryGetValue(
                        dependency.RecipeId,
                        out CroMechanicRecipeDocument provider))
                {
                    error =
                        $"recipe '{identity}' requires dependency '{dependency.RecipeId}', but it is not present.";
                    return false;
                }

                if (dependency.MinimumVersion.HasValue &&
                    (!provider.Version.HasValue ||
                     provider.Version.Value.CompareTo(
                         dependency.MinimumVersion.Value) < 0))
                {
                    string available =
                        provider.Version.HasValue
                            ? provider.Version.Value.ToString()
                            : "unversioned";

                    error =
                        $"recipe '{identity}' requires dependency '{dependency.RecipeId}' version {dependency.MinimumVersion.Value} or newer; available version is {available}.";
                    return false;
                }
            }

            foreach (string conflict in document.Conflicts)
            {
                if (identified.ContainsKey(
                        conflict))
                {
                    error =
                        $"recipe '{identity}' conflicts with recipe '{conflict}'.";
                    return false;
                }
            }
        }

        report =
            new CroMechanicRecipeSetCompatibilityReport(
                EngineVersion: engineVersion,
                DocumentCount: documents.Count,
                IdentifiedRecipeCount: identified.Count,
                DependencyCount: dependencyCount);

        return true;
    }
}