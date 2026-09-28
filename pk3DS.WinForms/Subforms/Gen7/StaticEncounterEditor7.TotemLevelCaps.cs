using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace pk3DS.WinForms;

public partial class StaticEncounterEditor7
{
    private const string TotemLevelCapsActionId = "static-encounters.totem-level-caps";

    private sealed class TotemLevelCapRule
    {
        public bool Enabled { get; set; } = true;
        public int EntryID { get; set; }
        public string Group { get; set; } = "Totem";
        public string OriginalTotem { get; set; } = string.Empty;
        public string CurrentPokemon { get; set; } = string.Empty;
        public int OriginalLevel { get; set; }
        public int CurrentLevel { get; set; }
        public int LevelCap { get; set; }

        public TotemLevelCapRule Clone() => new()
        {
            Enabled = Enabled,
            EntryID = EntryID,
            Group = Group,
            OriginalTotem = OriginalTotem,
            CurrentPokemon = CurrentPokemon,
            OriginalLevel = OriginalLevel,
            CurrentLevel = CurrentLevel,
            LevelCap = LevelCap,
        };
    }

    private Button B_TotemLevelCaps;
    private List<TotemLevelCapRule> TotemLevelCapRules = [];
    private bool TotemLevelCapsEnabled;

    private void InitializeTotemLevelCaps()
    {
        if (!Main.Config.USUM || TotemLevelCapRules.Count != 0)
            return;

        int[] indices = GetUSUMTotemBossIndices();
        foreach (int index in indices)
        {
            var encounter = Encounters[index];
            TotemLevelCapRules.Add(new TotemLevelCapRule
            {
                Enabled = true,
                EntryID = index,
                Group = IsUSUMUltraNecrozmaEncounterIndex(index) ? "Ultra Necrozma" : GetTotemGroup(index),
                OriginalTotem = GetTotemSpeciesName(encounter.Species),
                CurrentPokemon = GetTotemSpeciesName(encounter.Species),
                OriginalLevel = encounter.Level,
                CurrentLevel = encounter.Level,
                LevelCap = encounter.Level,
            });
        }

        TotemLevelCapRules = TotemLevelCapRules
            .OrderBy(r => r.OriginalLevel)
            .ThenBy(r => r.EntryID)
            .ToList();

        LoadTotemLevelCapsActionFromSession();
    }

    private static readonly int[] USUMTotemBossEntryIDs =
    [
        4,   // Gumshoos Lv.12 - Ultra Sun first trial
        9,   // Alolan Raticate Lv.12 - Ultra Moon first trial
        137, // Araquanid Lv.20
        249, // Alolan Marowak Lv.22
        24,  // Lurantis Lv.24
        146, // Togedemaru Lv.33
        39,  // Mimikyu Lv.35
        45,  // Kommo-o Lv.49
        162, // Ribombee Lv.55
        229, // Gumshoos Lv.60 - Ultra Sun postgame
        231, // Alolan Raticate Lv.60 - Ultra Moon postgame
    ];

    private const int USUMUltraNecrozmaEntryID = 160;

    private int[] GetUSUMTotemBossIndices()
    {
        // Static Encounter IDs are the stable identity used by Totem Level Caps
        // and Totem BST. Ultra Necrozma is always entry 160 in USUM.
        var indices = USUMTotemBossEntryIDs
            .Where(i => (uint)i < (uint)Encounters.Length)
            .ToList();

        if ((uint)USUMUltraNecrozmaEntryID < (uint)Encounters.Length &&
            !indices.Contains(USUMUltraNecrozmaEntryID))
        {
            indices.Add(USUMUltraNecrozmaEntryID);
        }

        return indices.ToArray();
    }

    private bool IsUSUMUltraNecrozmaEncounterIndex(int index)
    {
        return index == USUMUltraNecrozmaEntryID &&
               (uint)index < (uint)Encounters.Length;
    }

    private static string GetTotemGroup(int entryID)
    {
        return entryID switch
        {
            4 => "Trial Totem (US)",
            9 => "Trial Totem (UM)",
            229 => "Postgame Totem (US)",
            231 => "Postgame Totem (UM)",
            _ => "Trial Totem",
        };
    }

    private string GetTotemSpeciesName(int species)
    {
        return (uint)species < (uint)specieslist.Length
            ? specieslist[species]
            : $"Species {species}";
    }

    private void AddTotemLevelCapsButton()
    {
        if (!Main.Config.USUM || TotemLevelCapRules.Count == 0 || B_TotemLevelCaps != null)
            return;

        B_TotemLevelCaps = new Button
        {
            Location = new Point(177, 24),
            Name = "B_TotemLevelCaps",
            Size = new Size(155, 28),
            Text = "Totem Level Caps...",
            UseVisualStyleBackColor = true,
        };
        B_TotemLevelCaps.Click += B_TotemLevelCaps_Click;

        GB_Progressive.Controls.Add(B_TotemLevelCaps);
        B_TotemLevelCaps.BringToFront();
    }
    private void B_RandAll_TotemLevelCapsPost(object sender, EventArgs e)
    {
        if (!TotemLevelCapsEnabled)
            return;

        ApplyTotemLevelCaps(updateMoves: true);
        RefreshTotemRuleCurrentState();
        GetEncounter();
    }

    private void B_TotemLevelCaps_Click(object sender, EventArgs e)
    {
        SetEncounter();
        RefreshTotemRuleCurrentState();

        var editableRules = new BindingList<TotemLevelCapRule>(
            TotemLevelCapRules.Select(r => r.Clone()).ToList()
        );

        using var form = new Form
        {
            Text = "USUM Totem Level Caps",
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(1100, 630),
            MinimizeBox = false,
            MaximizeBox = false,
            FormBorderStyle = FormBorderStyle.Sizable,
            MinimumSize = new Size(1040, 600),
        };

        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            DataSource = editableRules,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
        };

        grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(TotemLevelCapRule.Enabled),
            HeaderText = "Use",
            Width = 45,
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(TotemLevelCapRule.EntryID),
            HeaderText = "ID",
            Width = 60,
            ReadOnly = true,
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(TotemLevelCapRule.Group),
            HeaderText = "Group",
            Width = 90,
            ReadOnly = true,
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(TotemLevelCapRule.OriginalTotem),
            HeaderText = "Original Totem",
            Width = 220,
            ReadOnly = true,
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(TotemLevelCapRule.CurrentPokemon),
            HeaderText = "Current Pokemon",
            Width = 220,
            ReadOnly = true,
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(TotemLevelCapRule.OriginalLevel),
            HeaderText = "Original Lv.",
            Width = 90,
            ReadOnly = true,
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(TotemLevelCapRule.CurrentLevel),
            HeaderText = "Current Lv.",
            Width = 90,
            ReadOnly = true,
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(TotemLevelCapRule.LevelCap),
            HeaderText = "Cap (0 = Current)",
            Width = 115,
        });
        grid.DataError += (_, e) => e.ThrowException = false;

        var options = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 42,
            Padding = new Padding(8, 6, 8, 4),
            FlowDirection = FlowDirection.LeftToRight,
        };

        var chkEnable = new CheckBox
        {
            AutoSize = true,
            Checked = TotemLevelCapsEnabled,
            Text = "Enable Totem Level Caps",
        };
        options.Controls.Add(chkEnable);

        var note = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 46,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 8, 0),
            Text = "Totem bosses are tracked by their known USUM Static Encounter entry IDs. Cap 0 keeps the current level. Enabled caps are applied after Randomize All and override the global level multiplier.",
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(6),
        };

        var ok = new Button { Text = "OK", Width = 90 };
        var cancel = new Button { Text = "Cancel", Width = 90, DialogResult = DialogResult.Cancel };
        var selectAll = new Button { Text = "Select All", Width = 105 };
        var selectNone = new Button { Text = "Select None", Width = 115 };
        var loadTemplate = new Button { Text = "Load Template...", Width = 125 };
        var saveTemplate = new Button { Text = "Save Template...", Width = 125 };

        selectAll.Click += (_, _) => SetTotemRulesSelected(editableRules, true);
        selectNone.Click += (_, _) => SetTotemRulesSelected(editableRules, false);

        loadTemplate.Click += (_, _) =>
        {
            try
            {
                System.IO.Directory.CreateDirectory(TotemLevelCapTemplateFile.TemplateDirectory);
                using var dialog = new OpenFileDialog
                {
                    Title = "Load Totem Level Caps template",
                    Filter = "Totem Level Caps template (*.json)|*.json|All files (*.*)|*.*",
                    InitialDirectory = TotemLevelCapTemplateFile.TemplateDirectory,
                    CheckFileExists = true,
                };
                if (dialog.ShowDialog(form) != DialogResult.OK) return;

                var template = TotemLevelCapTemplateFile.Load(dialog.FileName, "USUM");
                var byEntry = editableRules.ToDictionary(rule => rule.EntryID);
                var unknown = new List<int>();
                foreach (var entry in template.Entries ?? [])
                {
                    if (!byEntry.TryGetValue(entry.EntryID, out var rule))
                    {
                        unknown.Add(entry.EntryID);
                        continue;
                    }
                    rule.Enabled = entry.Use;
                    rule.LevelCap = entry.LevelCap;
                }

                chkEnable.Checked = template.Enabled;
                editableRules.ResetBindings();
                if (unknown.Count != 0)
                    WinFormsUtil.Alert("Totem Level Caps template loaded with warnings.", "Static Encounter IDs not present in this USUM table: " + string.Join(", ", unknown));
            }
            catch (Exception ex)
            {
                WinFormsUtil.Alert("Could not load Totem Level Caps template.", ex.Message);
            }
        };

        saveTemplate.Click += (_, _) =>
        {
            try
            {
                grid.EndEdit();
                var candidate = editableRules
                    .Select(rule => rule.Clone())
                    .OrderBy(rule => rule.OriginalLevel)
                    .ThenBy(rule => rule.EntryID)
                    .ToList();
                if (!ValidateTotemLevelCaps(candidate)) return;

                System.IO.Directory.CreateDirectory(TotemLevelCapTemplateFile.TemplateDirectory);
                using var dialog = new SaveFileDialog
                {
                    Title = "Save Totem Level Caps template",
                    Filter = "Totem Level Caps template (*.json)|*.json",
                    InitialDirectory = TotemLevelCapTemplateFile.TemplateDirectory,
                    FileName = "totem_level_caps_usum.json",
                    AddExtension = true,
                    DefaultExt = "json",
                };
                if (dialog.ShowDialog(form) != DialogResult.OK) return;

                var template = new TotemLevelCapTemplate
                {
                    Name = "USUM Totem Level Caps",
                    Game = "USUM",
                    Enabled = chkEnable.Checked,
                    Entries = candidate.Select(rule => new TotemLevelCapTemplateEntry
                    {
                        EntryID = rule.EntryID,
                        Use = rule.Enabled,
                        LevelCap = rule.LevelCap,
                    }).ToList(),
                };
                TotemLevelCapTemplateFile.Save(dialog.FileName, template, "USUM");
                WinFormsUtil.Alert("Totem Level Caps template saved successfully.");
            }
            catch (Exception ex)
            {
                WinFormsUtil.Alert("Could not save Totem Level Caps template.", ex.Message);
            }
        };

        List<TotemLevelCapRule> acceptedRules = null;
        bool acceptedEnabled = TotemLevelCapsEnabled;

        ok.Click += (_, _) =>
        {
            grid.EndEdit();
            var candidateRules = editableRules
                .Select(r => r.Clone())
                .OrderBy(r => r.OriginalLevel)
                .ThenBy(r => r.EntryID)
                .ToList();

            if (!ValidateTotemLevelCaps(candidateRules))
                return;

            acceptedRules = candidateRules;
            acceptedEnabled = chkEnable.Checked;
            form.DialogResult = DialogResult.OK;
            form.Close();
        };

        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(saveTemplate);
        buttons.Controls.Add(loadTemplate);
        buttons.Controls.Add(selectNone);
        buttons.Controls.Add(selectAll);

        form.Controls.Add(grid);
        form.Controls.Add(options);
        form.Controls.Add(note);
        form.Controls.Add(buttons);

        if (form.ShowDialog(this) != DialogResult.OK || acceptedRules is null)
            return;

        TotemLevelCapRules = acceptedRules;
        TotemLevelCapsEnabled = acceptedEnabled;
        SaveTotemLevelCapsActionToSession();

        ApplyTotemLevelCaps(updateMoves: true);
        RefreshTotemRuleCurrentState();
        GetEncounter();
    }

    private void SaveTotemLevelCapsActionToSession()
    {
        if (!TotemLevelCapsEnabled)
        {
            RandomizationSessionState.RemoveAction(TotemLevelCapsActionId);
            return;
        }

        RandomizationSessionState.MarkAction(
            TotemLevelCapsActionId,
            ("mode", "static-entry-id"),
            ("caps", SerializeTotemLevelCaps()));
    }

    private string SerializeTotemLevelCaps()
    {
        return string.Join(
            ";",
            TotemLevelCapRules
                .OrderBy(r => r.EntryID)
                .Select(r => $"{r.EntryID}:{(r.Enabled ? 1 : 0)}:{r.LevelCap}"));
    }

    private void LoadTotemLevelCapsActionFromSession()
    {
        var action = RandomizationSessionState.ExportActions()
            .FirstOrDefault(a => string.Equals(a.Id, TotemLevelCapsActionId, StringComparison.OrdinalIgnoreCase));

        if (action is not null)
            ImportTotemLevelCapsAction(action);
    }

    private void ImportTotemLevelCapsAction(GlobalRandomizationAction action)
    {
        if (action?.Parameters is null || !action.Parameters.TryGetValue("caps", out string raw) || string.IsNullOrWhiteSpace(raw))
            return;

        var byEntry = TotemLevelCapRules.ToDictionary(r => r.EntryID);
        foreach (string token in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] parts = token.Split(':');
            if (parts.Length != 3 || !int.TryParse(parts[0], out int entryID) || !byEntry.TryGetValue(entryID, out var rule))
                continue;

            rule.Enabled = parts[1] == "1" || bool.TryParse(parts[1], out bool enabled) && enabled;
            if (int.TryParse(parts[2], out int cap) && cap >= 0 && cap <= 100)
                rule.LevelCap = cap;
        }

        TotemLevelCapsEnabled = true;
    }

    // Called by Batch ROM Builder through reflection. The form is never shown in
    // batch mode, so initialize the rules explicitly before importing the action.
    private void ApplyTotemLevelCapsFromTemplate(GlobalRandomizationAction action)
    {
        InitializeTotemLevelCaps();
        ImportTotemLevelCapsAction(action);
        ApplyTotemLevelCaps(updateMoves: true);
        RefreshTotemRuleCurrentState();
    }

    private static void SetTotemRulesSelected(BindingList<TotemLevelCapRule> rules, bool value)
    {
        foreach (var rule in rules)
            rule.Enabled = value;
        rules.ResetBindings();
    }

    private static bool ValidateTotemLevelCaps(List<TotemLevelCapRule> rules)
    {
        foreach (var rule in rules)
        {
            if (rule.LevelCap != 0 && (rule.LevelCap < 1 || rule.LevelCap > 100))
            {
                WinFormsUtil.Alert("Invalid Totem level cap detected. Use 0 for the current level, or a value from 1 to 100.");
                return false;
            }
        }

        if (rules.GroupBy(r => r.EntryID).Any(g => g.Count() > 1))
        {
            WinFormsUtil.Alert("Duplicate Static Encounter IDs were detected in the Totem level cap list.");
            return false;
        }

        return true;
    }

    private void RefreshTotemRuleCurrentState()
    {
        foreach (var rule in TotemLevelCapRules)
        {
            if ((uint)rule.EntryID >= (uint)Encounters.Length)
                continue;

            var encounter = Encounters[rule.EntryID];
            rule.CurrentPokemon = GetTotemSpeciesName(encounter.Species);
            rule.CurrentLevel = encounter.Level;
        }
    }

    private void ApplyTotemLevelCaps(bool updateMoves)
    {
        if (!TotemLevelCapsEnabled)
            return;

        foreach (var rule in TotemLevelCapRules.Where(r => r.Enabled))
        {
            int index = rule.EntryID;
            if ((uint)index >= (uint)Encounters.Length)
                continue;

            var encounter = Encounters[index];
            int cap = rule.LevelCap == 0 ? encounter.Level : rule.LevelCap;
            encounter.Level = Math.Clamp(cap, 1, 100);

            if (updateMoves)
            {
                encounter.RelearnMoves = CHK_Metronome.Checked
                    ? [118, 0, 0, 0]
                    : learn.GetCurrentMoves(encounter.Species, encounter.Form, encounter.Level, 4);
            }
        }
    }
}
