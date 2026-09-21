using pk3DS.Core.Modding.Research;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace pk3DS.WinForms;

public sealed partial class Main
{
    private Button B_CroRecipes;

    private void AddCroRecipeButtonIfNeeded()
    {
        if (Config?.USUM != true)
            return;

        B_CroRecipes ??= new Button
        {
            Name = "B_CroRecipes",
            Size = new Size(138, 28),
            Margin = new Padding(4, 4, 4, 6),
            Text = "CRO Recipes",
            UseVisualStyleBackColor = true,
        };

        B_CroRecipes.Click -= B_CroRecipes_Click;
        B_CroRecipes.Click += B_CroRecipes_Click;

        if (!FLP_CRO.Controls.Contains(B_CroRecipes))
            FLP_CRO.Controls.Add(B_CroRecipes);
    }

    private void B_CroRecipes_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;

        if (Config?.USUM != true)
        {
            WinFormsUtil.Alert(
                "CRO Recipes are currently available only for Ultra Sun / Ultra Moon.");
            return;
        }

        string battlePath = FindBattleCroForRecipes();

        if (string.IsNullOrWhiteSpace(battlePath) || !File.Exists(battlePath))
        {
            WinFormsUtil.Alert(
                "Battle.cro was not found.",
                "Load an extracted USUM RomFS before applying CRO recipes.");
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Title = "Select all CRO recipe JSON files to apply together",
            Filter = "CRO recipe JSON (*.json)|*.json|All files (*.*)|*.*",
            Multiselect = true,
            CheckFileExists = true,
            RestoreDirectory = true,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        string[] recipePaths = dialog.FileNames
            .OrderBy(z => z, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (recipePaths.Length == 0)
            return;

        try
        {
            string[] jsonDocuments = recipePaths.Select(File.ReadAllText).ToArray();
            byte[] original = File.ReadAllBytes(battlePath);

            if (!CroMechanicRecipeApplication.TryApplyJson(
                    original,
                    jsonDocuments,
                    out byte[] updated,
                    out CroMechanicRecipeApplicationReport report,
                    out string error))
            {
                WinFormsUtil.Error(
                    "CRO recipe validation/application failed.",
                    error,
                    "No files were changed.");
                return;
            }

            string summary = BuildCroRecipeApplicationSummary(report);

            if (DialogResult.Yes != WinFormsUtil.Prompt(
                    MessageBoxButtons.YesNo,
                    "Apply selected CRO recipes?",
                    summary,
                    "",
                    "All selected recipes will be written as one CRO transaction.",
                    "No file has been changed yet."))
            {
                return;
            }

            string backupPath = PatchBackupManager.BackupOnce(
                battlePath,
                "cro-recipes");

            WriteCroRecipeResultSafely(
                battlePath,
                original,
                updated);

            WinFormsUtil.Alert(
                "CRO recipes applied successfully!",
                summary,
                "",
                $"Backup: {backupPath}");
        }
        catch (Exception ex)
        {
            WinFormsUtil.Error(
                "CRO recipe application failed.",
                ex.Message);
        }
    }

    private static string BuildCroRecipeApplicationSummary(
        CroMechanicRecipeApplicationReport report)
    {
        var builder = new StringBuilder();

        builder.AppendLine($"Recipe engine: {report.Compatibility.EngineVersion}");
        builder.AppendLine($"Documents: {report.Compatibility.DocumentCount}");
        builder.AppendLine($"Dependencies: {report.Compatibility.DependencyCount}");
        builder.AppendLine($"Entries: {report.BinaryRecipe.EntryCount}");
        builder.AppendLine(
            $"Relocations: {report.BinaryRecipe.OriginalPatchCount} -> {report.BinaryRecipe.FinalPatchCount}");

        int sizeDelta =
            report.BinaryRecipe.FinalFileSize -
            report.BinaryRecipe.OriginalFileSize;

        builder.AppendLine(
            $"CRO size: {report.BinaryRecipe.OriginalFileSize} -> {report.BinaryRecipe.FinalFileSize} bytes ({sizeDelta:+#;-#;0})");

        builder.AppendLine();
        builder.AppendLine("Recipes:");

        foreach (CroMechanicRecipeApplicationDocumentReport document in report.Documents)
        {
            string identity = string.IsNullOrWhiteSpace(document.RecipeId)
                ? document.Name
                : document.RecipeId;

            string version = document.Version.HasValue
                ? $" v{document.Version.Value}"
                : string.Empty;

            builder.AppendLine(
                $"- {identity}{version}: {document.EntryCount} entr{(document.EntryCount == 1 ? "y" : "ies")}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string FindBattleCroForRecipes()
    {
        if (string.IsNullOrWhiteSpace(RomFSPath) || !Directory.Exists(RomFSPath))
            return string.Empty;

        string[] direct =
        [
            Path.Combine(RomFSPath, "Battle.cro"),
            Path.Combine(RomFSPath, "dll", "Battle.cro"),
        ];

        string exact = direct.FirstOrDefault(File.Exists);
        if (!string.IsNullOrWhiteSpace(exact))
            return exact;

        try
        {
            return Directory
                .EnumerateFiles(RomFSPath, "Battle.cro", SearchOption.AllDirectories)
                .FirstOrDefault() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void WriteCroRecipeResultSafely(
        string battlePath,
        byte[] original,
        byte[] updated)
    {
        if (updated is null || updated.Length == 0)
            throw new InvalidDataException("The generated CRO image is empty.");

        string temporaryPath = battlePath + ".pk3ds-cro-recipe.tmp";

        try
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);

            File.WriteAllBytes(temporaryPath, updated);

            byte[] staged = File.ReadAllBytes(temporaryPath);
            if (!staged.AsSpan().SequenceEqual(updated))
                throw new IOException("The staged CRO image did not verify byte-for-byte.");

            File.Move(temporaryPath, battlePath, true);

            byte[] written = File.ReadAllBytes(battlePath);
            if (!written.AsSpan().SequenceEqual(updated))
            {
                File.WriteAllBytes(battlePath, original);
                throw new IOException(
                    "Battle.cro did not verify after replacement; the pre-application image was restored.");
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
