using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

using pk3DS.Core;
using pk3DS.Core.Randomizers;
using pk3DS.Core.Structures;

namespace pk3DS.WinForms;

public partial class StaticEncounterEditor7
{
    private const string TotemBSTActionId = "static-encounters.totem-bst";

    private sealed class TotemBSTRule
    {
        public bool Enabled { get; set; } = true;
        public int EntryID { get; set; }
        public string Group { get; set; } = "Totem";
        public string OriginalTotem { get; set; } = string.Empty;
        public string CurrentPokemon { get; set; } = string.Empty;
        public int Level { get; set; }
        public int OriginalBST { get; set; }
        public int CurrentBST { get; set; }
        public int MinBST { get; set; }
        public int MaxBST { get; set; }

        public TotemBSTRule Clone() => new()
        {
            Enabled = Enabled,
            EntryID = EntryID,
            Group = Group,
            OriginalTotem = OriginalTotem,
            CurrentPokemon = CurrentPokemon,
            Level = Level,
            OriginalBST = OriginalBST,
            CurrentBST = CurrentBST,
            MinBST = MinBST,
            MaxBST = MaxBST,
        };
    }

    private Button B_TotemBST;
    private List<TotemBSTRule> TotemBSTRules = [];
    private bool TotemBSTEnabled;

    private void InitializeTotemBST()
    {
        if (!Main.Config.USUM || TotemBSTRules.Count != 0)
            return;

        foreach (int index in GetUSUMTotemBossIndices())
        {
            var encounter = Encounters[index];
            bool ultraNecrozma =
                IsUSUMUltraNecrozmaEncounterIndex(index);

            int bst =
                GetTotemBST(
                    encounter.Species,
                    encounter.Form);

            var range = ultraNecrozma
                ? (MinBST: 700, MaxBST: 800)
                : GetDefaultTotemBSTRange(index);

            TotemBSTRules.Add(new TotemBSTRule
            {
                Enabled = true,
                EntryID = index,
                Group = ultraNecrozma
                    ? "Ultra Necrozma"
                    : GetTotemGroup(index),
                OriginalTotem = GetTotemSpeciesName(encounter.Species),
                CurrentPokemon = GetTotemSpeciesName(encounter.Species),
                Level = encounter.Level,
                OriginalBST = bst,
                CurrentBST = bst,
                MinBST = range.MinBST,
                MaxBST = range.MaxBST,
            });
        }

        TotemBSTRules = TotemBSTRules
            .OrderBy(r => r.Level)
            .ThenBy(r => r.EntryID)
            .ToList();

        LoadTotemBSTActionFromSession();
        RefreshTotemBSTCurrentState();
    }

    private static (int MinBST, int MaxBST) GetDefaultTotemBSTRange(int entryID)
    {
        return entryID switch
        {
            4 or 9 => (380, 450),
            137 => (420, 500),
            249 => (400, 480),
            24 => (440, 520),
            146 => (420, 500),
            39 => (440, 520),
            45 => (540, 640),
            162 => (480, 560),
            229 or 231 => (520, 680),
            _ => (300, 600),
        };
    }

    private int GetTotemBST(int species, int form)
    {
        if ((uint)species >= (uint)Main.SpeciesStat.Length)
            return 0;

        return Main.Config.Personal
            .GetFormEntry(species, form)
            .BST;
    }

    private IEnumerable<int> GetTotemBSTAllowedForms(int species)
    {
        if ((uint)species >= (uint)Main.SpeciesStat.Length)
            yield break;

        int formCount =
            Math.Max(
                1,
                Main.SpeciesStat[species].FormeCount);

        if (formCount <= 1)
        {
            yield return 0;
            yield break;
        }

        // Mirror Randomizer.GetRandomForme special cases.
        if (species is 664 or 665 or 666)
        {
            yield return 30;
            yield break;
        }

        if (species == 774)
        {
            int count = Math.Min(7, formCount);
            for (int form = 0; form < count; form++)
                yield return form;

            yield break;
        }

        if (Legal.EvolveToAlolanForms.Contains(species))
        {
            yield return 0;
            if (formCount > 1)
                yield return 1;

            yield break;
        }

        if (Legal.Mega_ORAS.Contains((ushort)species) &&
            !CHK_AllowMega.Checked)
        {
            yield return 0;
            yield break;
        }

        for (int form = 0; form < formCount; form++)
            yield return form;
    }

    private SpeciesRandomizer CreateTotemBSTSpeciesRandomizer()
    {
        return new SpeciesRandomizer(Main.Config)
        {
            G1 = CHK_G1.Checked,
            G2 = CHK_G2.Checked,
            G3 = CHK_G3.Checked,
            G4 = CHK_G4.Checked,
            G5 = CHK_G5.Checked,
            G6 = CHK_G6.Checked,
            G7 = CHK_G7.Checked,
            L = CHK_L.Checked,
            E = CHK_E.Checked,
            rBST = false,
        };
    }

    private HashSet<int> GetTotemFinalEvolutionPool()
    {
        var result = Legal.FinalEvolutions_7.ToHashSet();

        if (CHK_L.Checked)
        {
            foreach (int species in Legendary)
                result.Add(species);
        }

        if (CHK_E.Checked)
        {
            foreach (int species in Mythical)
                result.Add(species);
        }

        return result;
    }

    private void AddTotemBSTButton()
    {
        if (!Main.Config.USUM || TotemBSTRules.Count == 0 || B_TotemBST != null)
            return;

        B_TotemBST = new Button
        {
            Location = new Point(12, 58),
            Name = "B_TotemBST",
            Size = new Size(155, 28),
            Text = "Totem BST...",
            UseVisualStyleBackColor = true,
        };
        B_TotemBST.Click += B_TotemBST_Click;

        GB_Progressive.Controls.Add(B_TotemBST);
        B_TotemBST.BringToFront();
    }
    private void B_RandAll_TotemBSTPost(object sender, EventArgs e)
    {
        if (!TotemBSTEnabled)
            return;

        if (!ApplyTotemBST(updateMoves: true))
            return;

        RefreshTotemBSTCurrentState();
        GetEncounter();
    }

    private void B_TotemBST_Click(object sender, EventArgs e)
    {
        SetEncounter();
        RefreshTotemBSTCurrentState();

        var editableRules = new BindingList<TotemBSTRule>(
            TotemBSTRules.Select(r => r.Clone()).ToList()
        );

        using var form = new Form
        {
            Text = "USUM Totem BST",
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(1330, 660),
            MinimumSize = new Size(1180, 600),
            MinimizeBox = false,
            MaximizeBox = false,
            FormBorderStyle = FormBorderStyle.Sizable,
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
            DataPropertyName = nameof(TotemBSTRule.Enabled),
            HeaderText = "Use",
            Width = 45,
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(TotemBSTRule.EntryID),
            HeaderText = "ID",
            Width = 55,
            ReadOnly = true,
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(TotemBSTRule.Group),
            HeaderText = "Group",
            Width = 105,
            ReadOnly = true,
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(TotemBSTRule.OriginalTotem),
            HeaderText = "Original Totem",
            Width = 145,
            ReadOnly = true,
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(TotemBSTRule.CurrentPokemon),
            HeaderText = "Current Pokemon",
            Width = 160,
            ReadOnly = true,
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(TotemBSTRule.Level),
            HeaderText = "Lv.",
            Width = 55,
            ReadOnly = true,
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(TotemBSTRule.OriginalBST),
            HeaderText = "Original BST",
            Width = 90,
            ReadOnly = true,
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(TotemBSTRule.CurrentBST),
            HeaderText = "Current BST",
            Width = 90,
            ReadOnly = true,
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(TotemBSTRule.MinBST),
            HeaderText = "Min BST",
            Width = 85,
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(TotemBSTRule.MaxBST),
            HeaderText = "Max BST",
            Width = 85,
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
            Checked = TotemBSTEnabled,
            Text = "Enable Totem BST",
        };
        options.Controls.Add(chkEnable);

        var note = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 58,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 8, 0),
            Text =
                "Each USUM Totem boss plus the plot Ultra Necrozma battle is tracked by Static Encounter entry ID. " +
                "BST filtering is form-aware: the selected species AND form must fall inside the configured range and obey the current generation / Legendary / Event / Mega filters. " +
                "Force Totem or Force Fully Evolved additionally restricts the species pool to final evolutions.",
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
        var reset = new Button { Text = "Reset Defaults", Width = 120 };
        var selectAll = new Button { Text = "Select All", Width = 100 };
        var selectNone = new Button { Text = "Select None", Width = 105 };
        var loadTemplate = new Button { Text = "Load Template...", Width = 125 };
        var saveTemplate = new Button { Text = "Save Template...", Width = 125 };

        selectAll.Click += (_, _) => SetTotemBSTRulesSelected(editableRules, true);
        selectNone.Click += (_, _) => SetTotemBSTRulesSelected(editableRules, false);
        reset.Click += (_, _) => ResetTotemBSTDefaults(editableRules);

        loadTemplate.Click += (_, _) =>
        {
            try
            {
                System.IO.Directory.CreateDirectory(
                    TotemBSTTemplateFile.TemplateDirectory);

                using var dialog = new OpenFileDialog
                {
                    Title = "Load Totem BST template",
                    Filter = "Totem BST template (*.json)|*.json|All files (*.*)|*.*",
                    InitialDirectory =
                        TotemBSTTemplateFile.TemplateDirectory,
                    CheckFileExists = true,
                };

                if (dialog.ShowDialog(form) != DialogResult.OK)
                    return;

                var template =
                    TotemBSTTemplateFile.Load(
                        dialog.FileName,
                        "USUM");

                var byEntry =
                    editableRules.ToDictionary(
                        rule => rule.EntryID);

                var unknown =
                    new List<int>();

                foreach (var entry in template.Entries ?? [])
                {
                    if (!byEntry.TryGetValue(
                            entry.EntryID,
                            out var rule))
                    {
                        unknown.Add(entry.EntryID);
                        continue;
                    }

                    rule.Enabled = entry.Use;
                    rule.MinBST = entry.MinBST;
                    rule.MaxBST = entry.MaxBST;
                }

                chkEnable.Checked =
                    template.Enabled;

                editableRules.ResetBindings();

                if (unknown.Count != 0)
                {
                    WinFormsUtil.Alert(
                        "Totem BST template loaded with warnings.",
                        "Static Encounter IDs not present in this USUM table: " +
                        string.Join(", ", unknown));
                }
            }
            catch (Exception ex)
            {
                WinFormsUtil.Alert(
                    "Could not load Totem BST template.",
                    ex.Message);
            }
        };

        saveTemplate.Click += (_, _) =>
        {
            try
            {
                grid.EndEdit();

                var candidate =
                    editableRules
                        .Select(rule => rule.Clone())
                        .OrderBy(rule => rule.Level)
                        .ThenBy(rule => rule.EntryID)
                        .ToList();

                if (!ValidateTotemBSTRules(candidate))
                    return;

                System.IO.Directory.CreateDirectory(
                    TotemBSTTemplateFile.TemplateDirectory);

                using var dialog = new SaveFileDialog
                {
                    Title = "Save Totem BST template",
                    Filter = "Totem BST template (*.json)|*.json",
                    InitialDirectory =
                        TotemBSTTemplateFile.TemplateDirectory,
                    FileName = "totem_bst_usum.json",
                    AddExtension = true,
                    DefaultExt = "json",
                };

                if (dialog.ShowDialog(form) != DialogResult.OK)
                    return;

                var template =
                    new TotemBSTTemplate
                    {
                        Name = "USUM Totem BST",
                        Game = "USUM",
                        Enabled = chkEnable.Checked,
                        Entries = candidate
                            .Select(rule =>
                                new TotemBSTTemplateEntry
                                {
                                    EntryID = rule.EntryID,
                                    Use = rule.Enabled,
                                    MinBST = rule.MinBST,
                                    MaxBST = rule.MaxBST,
                                })
                            .ToList(),
                    };

                TotemBSTTemplateFile.Save(
                    dialog.FileName,
                    template,
                    "USUM");

                WinFormsUtil.Alert(
                    "Totem BST template saved successfully.");
            }
            catch (Exception ex)
            {
                WinFormsUtil.Alert(
                    "Could not save Totem BST template.",
                    ex.Message);
            }
        };

        List<TotemBSTRule> acceptedRules = null;
        bool acceptedEnabled = TotemBSTEnabled;

        ok.Click += (_, _) =>
        {
            grid.EndEdit();

            var candidateRules = editableRules
                .Select(r => r.Clone())
                .OrderBy(r => r.Level)
                .ThenBy(r => r.EntryID)
                .ToList();

            if (!ValidateTotemBSTRules(candidateRules))
                return;

            if (chkEnable.Checked &&
                !ValidateTotemBSTPools(candidateRules, out string poolError))
            {
                WinFormsUtil.Alert(
                    "Totem BST cannot be applied with the current randomizer filters.",
                    poolError);
                return;
            }

            acceptedRules = candidateRules;
            acceptedEnabled = chkEnable.Checked;
            form.DialogResult = DialogResult.OK;
            form.Close();
        };

        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(saveTemplate);
        buttons.Controls.Add(loadTemplate);
        buttons.Controls.Add(reset);
        buttons.Controls.Add(selectNone);
        buttons.Controls.Add(selectAll);

        form.Controls.Add(grid);
        form.Controls.Add(options);
        form.Controls.Add(note);
        form.Controls.Add(buttons);

        if (form.ShowDialog(this) != DialogResult.OK || acceptedRules is null)
            return;

        TotemBSTRules = acceptedRules;
        TotemBSTEnabled = acceptedEnabled;
        SaveTotemBSTActionToSession();

        if (TotemBSTEnabled && !ApplyTotemBST(updateMoves: true))
            return;

        RefreshTotemBSTCurrentState();
        GetEncounter();
    }

    private void SaveTotemBSTActionToSession()
    {
        if (!TotemBSTEnabled)
        {
            RandomizationSessionState.RemoveAction(TotemBSTActionId);
            return;
        }

        RandomizationSessionState.MarkAction(
            TotemBSTActionId,
            ("mode", "static-entry-id"),
            ("ranges", SerializeTotemBST()));
    }

    private string SerializeTotemBST()
    {
        return string.Join(
            ";",
            TotemBSTRules
                .OrderBy(r => r.EntryID)
                .Select(r =>
                    $"{r.EntryID}:{(r.Enabled ? 1 : 0)}:{r.MinBST}:{r.MaxBST}"));
    }

    private void LoadTotemBSTActionFromSession()
    {
        var action = RandomizationSessionState.ExportActions()
            .FirstOrDefault(a =>
                string.Equals(
                    a.Id,
                    TotemBSTActionId,
                    StringComparison.OrdinalIgnoreCase));

        if (action is not null)
            ImportTotemBSTAction(action);
    }

    private void ImportTotemBSTAction(GlobalRandomizationAction action)
    {
        if (action?.Parameters is null ||
            !action.Parameters.TryGetValue("ranges", out string raw) ||
            string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        var byEntry = TotemBSTRules.ToDictionary(r => r.EntryID);

        foreach (string token in raw.Split(
                     ';',
                     StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries))
        {
            string[] parts = token.Split(':');

            if (parts.Length != 4 ||
                !int.TryParse(parts[0], out int entryID) ||
                !byEntry.TryGetValue(entryID, out var rule))
            {
                continue;
            }

            rule.Enabled =
                parts[1] == "1" ||
                bool.TryParse(parts[1], out bool enabled) && enabled;

            if (int.TryParse(parts[2], out int minBST))
                rule.MinBST = minBST;

            if (int.TryParse(parts[3], out int maxBST))
                rule.MaxBST = maxBST;
        }

        TotemBSTEnabled = true;
    }

    // Called by Batch ROM Builder through reflection.
    private void ApplyTotemBSTFromTemplate(GlobalRandomizationAction action)
    {
        InitializeTotemBST();
        ImportTotemBSTAction(action);

        if (!ValidateTotemBSTRules(TotemBSTRules))
        {
            throw new InvalidOperationException(
                "The Totem BST template contains invalid ranges.");
        }

        if (!ValidateTotemBSTPools(TotemBSTRules, out string poolError))
            throw new InvalidOperationException(poolError);

        if (!ApplyTotemBST(updateMoves: true))
        {
            throw new InvalidOperationException(
                "Totem BST could not be applied.");
        }

        RefreshTotemBSTCurrentState();
    }

    private static void SetTotemBSTRulesSelected(
        BindingList<TotemBSTRule> rules,
        bool value)
    {
        foreach (var rule in rules)
            rule.Enabled = value;

        rules.ResetBindings();
    }

    private void ResetTotemBSTDefaults(
        BindingList<TotemBSTRule> rules)
    {
        foreach (var rule in rules)
        {
            var range =
                IsUSUMUltraNecrozmaEncounterIndex(rule.EntryID)
                    ? (MinBST: 700, MaxBST: 800)
                    : GetDefaultTotemBSTRange(rule.EntryID);

            rule.Enabled = true;
            rule.MinBST = range.MinBST;
            rule.MaxBST = range.MaxBST;
        }

        rules.ResetBindings();
    }

    private static bool ValidateTotemBSTRules(
        IEnumerable<TotemBSTRule> rules)
    {
        var list = rules?.ToList() ?? [];

        if (list.GroupBy(r => r.EntryID).Any(g => g.Count() > 1))
        {
            WinFormsUtil.Alert(
                "Duplicate Static Encounter IDs were detected in the Totem BST list.");
            return false;
        }

        foreach (var rule in list)
        {
            if (rule.MinBST < 1 ||
                rule.MaxBST < 1 ||
                rule.MinBST > 2000 ||
                rule.MaxBST > 2000 ||
                rule.MinBST > rule.MaxBST)
            {
                WinFormsUtil.Alert(
                    "Invalid Totem BST range.",
                    $"Entry {rule.EntryID}: use BST values from 1 to 2000 and ensure Min BST is not greater than Max BST.");
                return false;
            }
        }

        return true;
    }

    private bool ValidateTotemBSTPools(
        IEnumerable<TotemBSTRule> rules,
        out string error)
    {
        error = string.Empty;

        var specrand =
            CreateTotemBSTSpeciesRandomizer();

        foreach (var rule in rules.Where(r => r.Enabled))
        {
            if ((uint)rule.EntryID >= (uint)Encounters.Length)
                continue;

            var pool =
                GetTotemBSTPool(
                    specrand,
                    rule,
                    Encounters[rule.EntryID]);

            if (pool.Count != 0)
                continue;

            error =
                $"Entry {rule.EntryID} ({rule.OriginalTotem}) has no allowed species/form between BST {rule.MinBST} and {rule.MaxBST} with the current filters.";

            return false;
        }

        return true;
    }

    private List<(int Species, int Form)> GetTotemBSTPool(
        SpeciesRandomizer specrand,
        TotemBSTRule rule,
        EncounterStatic7 encounter)
    {
        bool forceFinal =
            CHK_ForceTotem.Checked ||
            (CHK_ForceFullyEvolved.Checked &&
             encounter.Level >= NUD_ForceFullyEvolved.Value);

        HashSet<int> finalSet =
            forceFinal
                ? GetTotemFinalEvolutionPool()
                : null;

        var pool =
            new List<(int Species, int Form)>();

        foreach (int species in specrand.GetAllowedSpeciesPool())
        {
            if (forceFinal &&
                !finalSet.Contains(species))
            {
                continue;
            }

            foreach (int form in
                     GetTotemBSTAllowedForms(species))
            {
                int bst =
                    GetTotemBST(
                        species,
                        form);

                if (bst < rule.MinBST ||
                    bst > rule.MaxBST)
                {
                    continue;
                }

                pool.Add((species, form));
            }
        }

        return pool;
    }

    private bool ApplyTotemBST(bool updateMoves)
    {
        if (!TotemBSTEnabled)
            return true;

        var specrand =
            CreateTotemBSTSpeciesRandomizer();

        var prepared =
            new List<(
                TotemBSTRule Rule,
                List<(int Species, int Form)> Pool)>();

        foreach (var rule in TotemBSTRules.Where(r => r.Enabled))
        {
            int index = rule.EntryID;

            if ((uint)index >= (uint)Encounters.Length)
                continue;

            var pool =
                GetTotemBSTPool(
                    specrand,
                    rule,
                    Encounters[index]);

            if (pool.Count == 0)
            {
                WinFormsUtil.Alert(
                    "Totem BST was not applied.",
                    $"Entry {rule.EntryID} ({rule.OriginalTotem}) has no allowed species/form between BST {rule.MinBST} and {rule.MaxBST} with the current filters.");

                return false;
            }

            prepared.Add((rule, pool));
        }

        foreach (var item in prepared)
        {
            int index = item.Rule.EntryID;
            var encounter = Encounters[index];

            var alternatives =
                item.Pool
                    .Where(candidate =>
                        candidate.Species != encounter.Species ||
                        candidate.Form != encounter.Form)
                    .ToArray();

            var choices =
                alternatives.Length != 0
                    ? alternatives
                    : item.Pool.ToArray();

            var selected =
                choices[
                    Util.Rand.Next(
                        choices.Length)];

            encounter.Species =
                selected.Species;

            encounter.Form =
                selected.Form;

            if (updateMoves)
            {
                encounter.RelearnMoves =
                    CHK_Metronome.Checked
                        ? [118, 0, 0, 0]
                        : learn.GetCurrentMoves(
                            encounter.Species,
                            encounter.Form,
                            encounter.Level,
                            4);
            }

            if ((uint)index <
                (uint)LB_Encounter.Items.Count)
            {
                LB_Encounter.Items[index] =
                    GetEntryText(encounter, index);
            }
        }

        return true;
    }

    private void RefreshTotemBSTCurrentState()
    {
        foreach (var rule in TotemBSTRules)
        {
            if ((uint)rule.EntryID >= (uint)Encounters.Length)
                continue;

            var encounter =
                Encounters[rule.EntryID];

            rule.CurrentPokemon =
                GetTotemSpeciesName(encounter.Species);

            rule.Level =
                encounter.Level;

            rule.CurrentBST =
                GetTotemBST(
                    encounter.Species,
                    encounter.Form);
        }
    }
}
