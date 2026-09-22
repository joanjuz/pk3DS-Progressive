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
            int bst = GetTotemBST(encounter.Species);
            var range = GetDefaultTotemBSTRange(index);

            TotemBSTRules.Add(new TotemBSTRule
            {
                Enabled = true,
                EntryID = index,
                Group = GetTotemGroup(index),
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

    private int GetTotemBST(int species)
    {
        return (uint)species < (uint)Main.SpeciesStat.Length
            ? Main.SpeciesStat[species].BST
            : 0;
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
                "Each USUM Totem boss is tracked by Static Encounter entry ID. " +
                "The selected species must fall inside its configured BST range and obey the current generation / Legendary / Event filters. " +
                "Force Totem or Force Fully Evolved additionally restricts the base-species pool to final evolutions. Forms are chosen afterward, so an enabled alternate/Mega form can exceed the displayed base-species BST range.",
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

        selectAll.Click += (_, _) => SetTotemBSTRulesSelected(editableRules, true);
        selectNone.Click += (_, _) => SetTotemBSTRulesSelected(editableRules, false);
        reset.Click += (_, _) => ResetTotemBSTDefaults(editableRules);

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
            throw new InvalidOperationException(
                "The Totem BST template contains invalid ranges.");

        if (!ValidateTotemBSTPools(TotemBSTRules, out string poolError))
            throw new InvalidOperationException(poolError);

        if (!ApplyTotemBST(updateMoves: true))
            throw new InvalidOperationException(
                "Totem BST could not be applied.");

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

    private static void ResetTotemBSTDefaults(
        BindingList<TotemBSTRule> rules)
    {
        foreach (var rule in rules)
        {
            var range =
                GetDefaultTotemBSTRange(rule.EntryID);

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

        var specrand = CreateTotemBSTSpeciesRandomizer();

        foreach (var rule in rules.Where(r => r.Enabled))
        {
            if ((uint)rule.EntryID >= (uint)Encounters.Length)
                continue;

            int[] pool =
                GetTotemBSTPool(
                    specrand,
                    rule,
                    Encounters[rule.EntryID]);

            if (pool.Length != 0)
                continue;

            error =
                $"Entry {rule.EntryID} ({rule.OriginalTotem}) has no allowed species between BST {rule.MinBST} and {rule.MaxBST} with the current filters.";

            return false;
        }

        return true;
    }

    private int[] GetTotemBSTPool(
        SpeciesRandomizer specrand,
        TotemBSTRule rule,
        EncounterStatic7 encounter)
    {
        int[] pool =
            specrand.GetSpeciesPoolByBST(
                rule.MinBST,
                rule.MaxBST);

        bool forceFinal =
            CHK_ForceTotem.Checked ||
            (CHK_ForceFullyEvolved.Checked &&
             encounter.Level >= NUD_ForceFullyEvolved.Value);

        if (forceFinal)
        {
            var finalSet = GetTotemFinalEvolutionPool();

            pool = pool
                .Where(finalSet.Contains)
                .ToArray();
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
            new List<(TotemBSTRule Rule, int[] Pool)>();

        foreach (var rule in TotemBSTRules.Where(r => r.Enabled))
        {
            int index = rule.EntryID;

            if ((uint)index >= (uint)Encounters.Length)
                continue;

            int[] pool =
                GetTotemBSTPool(
                    specrand,
                    rule,
                    Encounters[index]);

            if (pool.Length == 0)
            {
                WinFormsUtil.Alert(
                    "Totem BST was not applied.",
                    $"Entry {rule.EntryID} ({rule.OriginalTotem}) has no allowed species between BST {rule.MinBST} and {rule.MaxBST} with the current filters.");

                return false;
            }

            prepared.Add((rule, pool));
        }

        foreach (var item in prepared)
        {
            int index = item.Rule.EntryID;
            var encounter = Encounters[index];

            int[] alternatives =
                item.Pool
                    .Where(species =>
                        species != encounter.Species)
                    .ToArray();

            int[] choices =
                alternatives.Length != 0
                    ? alternatives
                    : item.Pool;

            encounter.Species =
                choices[Util.Rand.Next(choices.Length)];

            encounter.Form =
                Randomizer.GetRandomForme(
                    encounter.Species,
                    CHK_AllowMega.Checked,
                    true,
                    Main.SpeciesStat);

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

            if ((uint)index < (uint)LB_Encounter.Items.Count)
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
                GetTotemBST(encounter.Species);
        }
    }
}
