using System;
using System.Collections.Generic;

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// Mapping from one external recipe document to the contiguous entry range that it contributes
/// to the aggregate binary recipe.
/// </summary>
public sealed record CroMechanicRecipeApplicationDocumentReport(
    int DocumentIndex,
    string Name,
    string RecipeId,
    CroRecipeVersion? Version,
    int EntryStartIndex,
    int EntryCount);

/// <summary>
/// End-to-end report for one coordinated recipe application.
/// </summary>
public sealed record CroMechanicRecipeApplicationReport(
    CroMechanicRecipeSetCompatibilityReport Compatibility,
    IReadOnlyList<CroMechanicRecipeApplicationDocumentReport> Documents,
    CroMechanicRecipeReport BinaryRecipe);

/// <summary>
/// Coordinates external recipe parsing / compatibility validation with one atomic binary
/// <see cref="CroMechanicRecipeEngine"/> transaction.
/// </summary>
public static class CroMechanicRecipeApplication
{
    /// <summary>
    /// Parses and applies one external JSON recipe document.
    /// </summary>
    public static bool TryApplyJson(
        byte[] cro,
        string json,
        out byte[] updated,
        out CroMechanicRecipeApplicationReport report,
        out string error) =>
        TryApplyJson(
            cro,
            [json],
            out updated,
            out report,
            out error);

    /// <summary>
    /// Parses all external JSON recipe documents before performing any binary work.
    /// </summary>
    public static bool TryApplyJson(
        byte[] cro,
        IReadOnlyList<string> jsonDocuments,
        out byte[] updated,
        out CroMechanicRecipeApplicationReport report,
        out string error)
    {
        updated =
            null;

        report =
            null;

        error =
            string.Empty;

        if (jsonDocuments is null)
        {
            error =
                "recipe JSON document set is null.";
            return false;
        }

        if (jsonDocuments.Count == 0)
        {
            error =
                "recipe JSON document set is empty.";
            return false;
        }

        var documents =
            new CroMechanicRecipeDocument[jsonDocuments.Count];

        for (int i = 0; i < jsonDocuments.Count; i++)
        {
            if (!CroMechanicRecipeFormat.TryParse(
                    jsonDocuments[i],
                    out CroMechanicRecipeDocument document,
                    out error))
            {
                error =
                    $"recipe JSON document #{i} is invalid: " +
                    error;
                return false;
            }

            documents[i] =
                document;
        }

        return TryApply(
            cro,
            documents,
            out updated,
            out report,
            out error);
    }

    /// <summary>
    /// Validates the complete document set, converts every document to the binary recipe model,
    /// combines all entries in document order, and invokes the binary engine exactly once.
    /// </summary>
    public static bool TryApply(
        byte[] cro,
        IReadOnlyList<CroMechanicRecipeDocument> documents,
        out byte[] updated,
        out CroMechanicRecipeApplicationReport report,
        out string error)
    {
        updated =
            null;

        report =
            null;

        error =
            string.Empty;

        if (cro is null)
        {
            error =
                "CRO data is null.";
            return false;
        }

        if (documents is null)
        {
            error =
                "recipe document set is null.";
            return false;
        }

        if (documents.Count == 0)
        {
            error =
                "recipe document set is empty.";
            return false;
        }

        byte[] inputSnapshot =
            (byte[])cro.Clone();

        if (!CroMechanicRecipeSetCompatibility.TryCheck(
                documents,
                out CroMechanicRecipeSetCompatibilityReport compatibilityReport,
                out error))
        {
            return false;
        }

        var combinedEntries =
            new List<CroMechanicRecipeEntry>();

        var documentReports =
            new CroMechanicRecipeApplicationDocumentReport[documents.Count];

        string combinedName =
            documents.Count == 1
                ? documents[0].Name ?? string.Empty
                : $"recipe-application-set:{documents.Count}";

        for (int i = 0; i < documents.Count; i++)
        {
            CroMechanicRecipeDocument document =
                documents[i];

            if (!CroMechanicRecipeFormat.TryBuildRecipe(
                    document,
                    out CroMechanicRecipe recipe,
                    out error))
            {
                error =
                    $"recipe document #{i} could not build binary recipe: " +
                    error;
                return false;
            }

            int entryStartIndex =
                combinedEntries.Count;

            foreach (CroMechanicRecipeEntry entry in recipe.Entries)
                combinedEntries.Add(entry);

            documentReports[i] =
                new CroMechanicRecipeApplicationDocumentReport(
                    DocumentIndex: i,
                    Name: document.Name ?? string.Empty,
                    RecipeId: document.RecipeId ?? string.Empty,
                    Version: document.Version,
                    EntryStartIndex: entryStartIndex,
                    EntryCount: recipe.Entries.Count);
        }

        var combinedRecipe =
            new CroMechanicRecipe
            {
                Name = combinedName,
                Entries = combinedEntries,
            };

        if (!CroMechanicRecipeEngine.TryInstall(
                cro,
                combinedRecipe,
                out byte[] generated,
                out CroMechanicRecipeReport binaryReport,
                out error))
        {
            error =
                "recipe application binary transaction failed: " +
                error;
            return false;
        }

        if (!cro.AsSpan().SequenceEqual(
                inputSnapshot))
        {
            error =
                "recipe application unexpectedly modified the input CRO buffer.";
            return false;
        }

        updated =
            generated;

        report =
            new CroMechanicRecipeApplicationReport(
                Compatibility: compatibilityReport,
                Documents: documentReports,
                BinaryRecipe: binaryReport);

        return true;
    }
}