using pk3DS.Core.Modding.Research;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace pk3DS.WinForms;

/// <summary>
/// Configures, templates and installs the USUM Player Level Caps patch.
/// </summary>
public sealed class LevelCapManager7 : Form
{
    private const string Game = "USUM";

    private readonly ComboBox Preset = new();
    private readonly NumericUpDown FinalCap = new();
    private readonly Label Summary = new();
    private readonly TextBox Sequence = new();
    private readonly Label PatchStatus = new();
    private readonly Button Install = new();

    private LevelCapTable Table;
    private int PresetShift;

    public LevelCapManager7()
    {
        Text = "Player Level Caps";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(690, 350);

        Table = LoadStartingTable(out PresetShift, out int finalCap);

        int y = 18;
        Controls.Add(LabelAt("Preset", 18, y + 4));
        Preset.SetBounds(130, y, 250, 24);
        Preset.DropDownStyle = ComboBoxStyle.DropDownList;
        Preset.Items.AddRange(LevelCapShifts.All.Select(z => (object)LevelCapShifts.Describe(z)).ToArray());
        int presetIndex = Array.IndexOf(LevelCapShifts.All, PresetShift);
        Preset.SelectedIndex = presetIndex >= 0 ? presetIndex : LevelCapShifts.StandardIndex;
        Controls.Add(Preset);

        Controls.Add(LabelAt("Preset final cap", 410, y + 4));
        FinalCap.SetBounds(525, y, 70, 24);
        FinalCap.Minimum = 5;
        FinalCap.Maximum = LevelCapTable.HardCeiling;
        FinalCap.Value = Math.Clamp(finalCap, 5, LevelCapTable.HardCeiling);
        Controls.Add(FinalCap);

        var applyPreset = new Button
        {
            Text = "Apply preset",
            Left = 600,
            Top = y,
            Width = 78,
            Height = 24,
        };
        applyPreset.Click += (_, _) => ApplyPreset();
        Controls.Add(applyPreset);

        y += 44;
        Summary.SetBounds(18, y, 650, 36);
        Summary.AutoEllipsis = true;
        Controls.Add(Summary);

        y += 42;
        Controls.Add(LabelAt("Cap sequence", 18, y + 4));
        Sequence.SetBounds(130, y, 538, 48);
        Sequence.Multiline = true;
        Sequence.ReadOnly = true;
        Sequence.ScrollBars = ScrollBars.Vertical;
        Controls.Add(Sequence);

        y += 64;
        var edit = new Button { Text = "Edit checkpoints...", Left = 18, Top = y, Width = 135, Height = 28 };
        var load = new Button { Text = "Load template...", Left = 163, Top = y, Width = 120, Height = 28 };
        var save = new Button { Text = "Save template...", Left = 293, Top = y, Width = 120, Height = 28 };
        var reset = new Button { Text = "Research defaults", Left = 423, Top = y, Width = 120, Height = 28 };
        Controls.AddRange([edit, load, save, reset]);

        edit.Click += (_, _) => EditCheckpoints();
        load.Click += (_, _) => LoadTemplate();
        save.Click += (_, _) => SaveTemplate();
        reset.Click += (_, _) =>
        {
            Table = LevelCapTable.Default();
            PresetShift = 0;
            Preset.SelectedIndex = LevelCapShifts.StandardIndex;
            FinalCap.Value = LevelCapTable.ResearchFinalCap;
            PersistCurrent();
            RefreshSummary();
        };

        y += 46;
        PatchStatus.SetBounds(18, y, 650, 42);
        PatchStatus.AutoEllipsis = true;
        Controls.Add(PatchStatus);

        y += 48;
        Install.Text = "Enable Player Level Caps";
        Install.SetBounds(18, y, 190, 30);
        Install.Click += (_, _) => InstallPatch();
        Controls.Add(Install);

        var close = new Button
        {
            Text = "Close",
            DialogResult = DialogResult.Cancel,
            Left = 578,
            Top = y,
            Width = 90,
            Height = 30,
        };
        Controls.Add(close);
        CancelButton = close;

        RefreshSummary();
        RefreshPatchState();
    }

    private static Label LabelAt(string text, int x, int y) => new()
    {
        Text = text,
        AutoSize = true,
        Left = x,
        Top = y,
    };

    private LevelCapTable LoadStartingTable(out int shift, out int finalCap)
    {
        shift = 0;
        finalCap = LevelCapTable.ResearchFinalCap;

        if (LevelCapTemplateFile.TryGetCurrent(Game, out var current))
        {
            shift = current.PresetShift;
            finalCap = current.FinalCap;
            return current.ToTable();
        }

        // A ROM may already contain the patch when pk3DS is opened. Recover its actual
        // table from Battle.cro before consulting the generic last-state file, so a
        // previously edited ROM cannot inherit stale settings from another project.
        if (Gen7LevelCapPatcher.TryReadInstalledTable(out var installed))
        {
            finalCap = installed.Entries.Count == 0
                ? LevelCapTable.HardCeiling
                : installed.Entries.Max(z => (int)z.Cap);
            return installed;
        }

        if (LevelCapTemplateFile.TryLoadLastState(Game, out current))
        {
            shift = current.PresetShift;
            finalCap = current.FinalCap;
            return current.ToTable();
        }

        return LevelCapTable.Default();
    }

    private void ApplyPreset()
    {
        int index = Math.Clamp(Preset.SelectedIndex, 0, LevelCapShifts.All.Length - 1);
        PresetShift = LevelCapShifts.All[index];
        Table = LevelCapPresets.Build(PresetShift, (byte)FinalCap.Value);
        PersistCurrent();
        RefreshSummary();
    }

    private void EditCheckpoints()
    {
        using var dialog = new LevelCapEditor(Table.Clone());
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result is null)
            return;

        Table = dialog.Result;
        PresetShift = 0;
        FinalCap.Value = Math.Clamp(Table.Entries.Max(z => (int)z.Cap), 5, 100);
        Preset.SelectedIndex = LevelCapShifts.StandardIndex;
        PersistCurrent();
        RefreshSummary();
    }

    private void LoadTemplate()
    {
        Directory.CreateDirectory(LevelCapTemplateFile.TemplateDirectory);
        using var dialog = new OpenFileDialog
        {
            Title = "Load Player Level Caps Template",
            Filter = "Player Level Caps template (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = LevelCapTemplateFile.TemplateDirectory,
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            var template = LevelCapTemplateFile.Load(dialog.FileName, Game);
            Table = template.ToTable();
            PresetShift = template.PresetShift;
            FinalCap.Value = Math.Clamp(template.FinalCap, 5, 100);

            int index = Array.IndexOf(LevelCapShifts.All, PresetShift);
            Preset.SelectedIndex = index >= 0 ? index : LevelCapShifts.StandardIndex;

            LevelCapTemplateFile.SaveLastState(template, Game);
            RefreshSummary();
        }
        catch (Exception ex)
        {
            WinFormsUtil.Error("Could not load Player Level Caps template.", ex.Message);
        }
    }

    private void SaveTemplate()
    {
        Directory.CreateDirectory(LevelCapTemplateFile.TemplateDirectory);
        using var dialog = new SaveFileDialog
        {
            Title = "Save Player Level Caps Template",
            Filter = "Player Level Caps template (*.json)|*.json",
            InitialDirectory = LevelCapTemplateFile.TemplateDirectory,
            FileName = "player_level_caps_usum.json",
            AddExtension = true,
            DefaultExt = "json",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            string name = Path.GetFileNameWithoutExtension(dialog.FileName);
            var template = CaptureTemplate(name);
            LevelCapTemplateFile.Save(dialog.FileName, template, Game);
            LevelCapTemplateFile.SaveLastState(template, Game);
            WinFormsUtil.Alert("Player Level Caps template saved!", Path.GetFileName(dialog.FileName));
        }
        catch (Exception ex)
        {
            WinFormsUtil.Error("Could not save Player Level Caps template.", ex.Message);
        }
    }

    private void InstallPatch()
    {
        var problems = Table.Validate();
        if (problems.Count != 0)
        {
            WinFormsUtil.Error("Player Level Caps table is invalid.", problems[0]);
            return;
        }

        bool updating = Gen7LevelCapPatcher.GetState() == Gen7LevelCapPatcher.PatchState.Applied;
        if (WinFormsUtil.Prompt(
                MessageBoxButtons.YesNo,
                updating ? "Update Player Level Caps?" : "Enable Player Level Caps?",
                $"This installs {Table.Entries.Count} story checkpoints.",
                "Battle EXP and Rare Candy will both respect the active cap.",
                "Battle.cro and code.bin are validated and backed up before they are changed.") != DialogResult.Yes)
        {
            return;
        }

        try
        {
            int changed = Gen7LevelCapPatcher.Apply(Table, out string report);
            var template = CaptureTemplate("Player Level Caps");
            LevelCapTemplateFile.SaveLastState(template, Game);
            RandomizationSessionState.MarkAction(Gen7LevelCapPatcher.ActionId);

            RefreshPatchState();

            WinFormsUtil.Alert(
                changed == 0 ? "Player Level Caps were already enabled." : "Player Level Caps enabled!",
                report);
        }
        catch (Exception ex)
        {
            WinFormsUtil.Error("Could not enable Player Level Caps.", ex.Message);
        }
    }

    private LevelCapTemplate CaptureTemplate(string name) =>
        LevelCapTemplate.FromTable(
            Table,
            name,
            Game,
            PresetShift,
            (int)FinalCap.Value);

    private void PersistCurrent()
    {
        try
        {
            LevelCapTemplateFile.SaveLastState(CaptureTemplate("Player Level Caps"), Game);
        }
        catch
        {
            // The editor can still be used when the application directory is read-only.
        }
    }

    private void RefreshSummary()
    {
        Summary.Text = LevelCapPresets.Summarise(Table);
        Sequence.Text = LevelCapPresets.CapSequence(Table);
    }

    private void RefreshPatchState()
    {
        var state = Gen7LevelCapPatcher.GetState();
        switch (state)
        {
            case Gen7LevelCapPatcher.PatchState.Stock:
                PatchStatus.Text = "Status: ready. Battle.cro and decompressed code.bin match the supported USUM hooks.";
                Install.Enabled = true;
                Install.Text = "Enable Player Level Caps";
                break;

            case Gen7LevelCapPatcher.PatchState.Partial:
                PatchStatus.Text = "Status: partial install detected. The missing half can be completed safely.";
                Install.Enabled = true;
                Install.Text = "Complete Level Caps";
                break;

            case Gen7LevelCapPatcher.PatchState.Applied:
                PatchStatus.Text = "Status: Player Level Caps are enabled. You can edit the table and update both binaries safely.";
                Install.Enabled = true;
                Install.Text = "Update Player Level Caps";
                RandomizationSessionState.MarkAction(Gen7LevelCapPatcher.ActionId);
                PersistCurrent();
                break;

            case Gen7LevelCapPatcher.PatchState.MissingFiles:
                PatchStatus.Text = "Status: Battle.cro and/or decompressed code.bin is missing. Load both RomFS and ExeFS.";
                Install.Enabled = false;
                Install.Text = "Files missing";
                break;

            default:
                PatchStatus.Text = "Status: unsupported or already-modified hook. No bytes will be written.";
                Install.Enabled = false;
                Install.Text = "Unsupported binaries";
                break;
        }
    }
}
