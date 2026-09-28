using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace pk3DS.WinForms;

public static class TrainerLevelCapDialog
{
    public static bool Edit(
        IWin32Window owner,
        ref List<TrainerLevelCapRule> rules,
        ref bool applyCapsToPreviousTrainers,
        ref int previousTrainerGap,
        ref decimal regularTrainerCurvePower,
        ref bool guaranteeMegaInImportantBattles)
    {
        // guaranteeMegaInImportantBattles is kept in the signature for compatibility with older callers.
        // Mega is now configured per trainer with TrainerLevelCapRule.GuaranteeMega.
        using var form = new Form
        {
            Text = "Trainer Level Caps",
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(1100, 630),
            MinimizeBox = false,
            MaximizeBox = false,
            FormBorderStyle = FormBorderStyle.Sizable,
            MinimumSize = new Size(1040, 600),
        };

        var editableRules = new BindingList<TrainerLevelCapRule>(
            rules.Select(r => r.Clone()).ToList()
        );

        // If an older settings flow enabled global Mega, migrate it into every enabled rule once.
        if (guaranteeMegaInImportantBattles)
        {
            foreach (var rule in editableRules.Where(z => z.Enabled))
                rule.GuaranteeMega = true;
            guaranteeMegaInImportantBattles = false;
        }

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
            DataPropertyName = nameof(TrainerLevelCapRule.Enabled),
            HeaderText = "Use",
            Width = 45,
        });
        grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(TrainerLevelCapRule.NerfTeam),
            HeaderText = "Max 3",
            ToolTipText = "Nerf this trainer to at most 3 Pokémon while preserving its strongest-level ace.",
            Width = 55,
        });
        grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(TrainerLevelCapRule.GuaranteeMega),
            HeaderText = "Mega",
            ToolTipText = "Guarantee at least one Mega in this selected important battle.",
            Width = 55,
        });
        grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(TrainerLevelCapRule.GuaranteeZMove),
            HeaderText = "Z-Move",
            ToolTipText = "Gen 7 only: guarantee one compatible Z-Crystal/Z-Move in this selected important battle.",
            Width = 65,
        });grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(TrainerLevelCapRule.TrainerID),
            HeaderText = "ID",
            Width = 55,
            ReadOnly = true,
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(TrainerLevelCapRule.Group),
            HeaderText = "Group",
            Width = 95,
            ReadOnly = true,
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(TrainerLevelCapRule.Trainer),
            HeaderText = "Trainer",
            Width = 260,
            ReadOnly = true,
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(TrainerLevelCapRule.CurrentAceLevel),
            HeaderText = "Current Ace",
            Width = 85,
            ReadOnly = true,
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(TrainerLevelCapRule.LevelCap),
            HeaderText = "Cap (0 = Ace)",
            Width = 95,
        });

        grid.DataError += (_, e) => e.ThrowException = false;

        var options = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 42,
            Padding = new Padding(8, 6, 8, 4),
            FlowDirection = FlowDirection.LeftToRight,
        };

        var chkPrevious = new CheckBox
        {
            AutoSize = true,
            Checked = applyCapsToPreviousTrainers,
            Text = "Scale story trainers toward the next cap -",
        };
        var nudGap = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 20,
            Value = Math.Min(Math.Max(previousTrainerGap, 0), 20),
            Width = 45,
        };
        var gapLabel = new Label
        {
            AutoSize = true,
            Text = "levels",
            Padding = new Padding(0, 4, 0, 0),
        };

        options.Controls.Add(chkPrevious);
        options.Controls.Add(nudGap);
        options.Controls.Add(gapLabel);
        var filterLabel = new Label
        {
            AutoSize = true,
            Text = "Show:",
            Padding = new Padding(18, 4, 0, 0),
        };
        var chkImportant = new CheckBox
        {
            AutoSize = true,
            Checked = true,
            Text = "Important Trainers",
        };
        var chkBosses = new CheckBox
        {
            AutoSize = true,
            Checked = true,
            Text = "Bosses",
        };

        options.Controls.Add(filterLabel);
        options.Controls.Add(chkImportant);
        options.Controls.Add(chkBosses);

        void ApplyTrainerVisibilityFilter()
        {
            grid.CurrentCell = null;

            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.DataBoundItem is not TrainerLevelCapRule rule)
                    continue;

                bool isBoss = string.Equals(
                    rule.Group,
                    "Boss",
                    StringComparison.OrdinalIgnoreCase);

                row.Visible = isBoss
                    ? chkBosses.Checked
                    : chkImportant.Checked;
            }
        }

        chkImportant.CheckedChanged += (_, _) => ApplyTrainerVisibilityFilter();
        chkBosses.CheckedChanged += (_, _) => ApplyTrainerVisibilityFilter();
        grid.DataBindingComplete += (_, _) => ApplyTrainerVisibilityFilter();
        ApplyTrainerVisibilityFilter();

        var note = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 46,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 8, 0),
            Text = "Positive caps are exact fixed levels. Max 3 nerfs that selected trainer to at most three Pokémon while preserving its strongest-level ace. In USUM, LevelCap 0 (Ace) keeps the global trainer scaling for milestone/fallback values, but main-story Important/Boss trainers are placed on the WikiDex-audited story curve while preserving their team level spread. Unmapped/postgame entries fall back to the globally scaled ace. Regular trainers still use the same story curve when scaling is enabled. Move filtering is configured in Trainer Move Rules.",
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

        selectAll.Click += (_, _) => SetSelected(editableRules, true);
        selectNone.Click += (_, _) => SetSelected(editableRules, false);

        List<TrainerLevelCapRule> acceptedRules = null;
        bool acceptedPrevious = applyCapsToPreviousTrainers;
        int acceptedGap = previousTrainerGap;

        ok.Click += (_, _) =>
        {
            grid.EndEdit();
            var candidateRules = editableRules
                .Select(r => r.Clone())
                .OrderBy(r => r.CurrentAceLevel)
                .ThenBy(r => r.TrainerID)
                .ToList();

            if (!Validate(candidateRules))
                return;

            acceptedRules = candidateRules;
            acceptedPrevious = chkPrevious.Checked;
            acceptedGap = (int)nudGap.Value;
            form.DialogResult = DialogResult.OK;
            form.Close();
        };

        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(selectNone);
        buttons.Controls.Add(selectAll);

        form.Controls.Add(grid);
        form.Controls.Add(options);
        form.Controls.Add(note);
        form.Controls.Add(buttons);

        if (form.ShowDialog(owner) != DialogResult.OK || acceptedRules is null)
            return false;

        rules = acceptedRules;
        applyCapsToPreviousTrainers = acceptedPrevious;
        previousTrainerGap = acceptedGap;
        return true;
    }

    private static void SetSelected(BindingList<TrainerLevelCapRule> rules, bool value)
    {
        foreach (var rule in rules)
            rule.Enabled = value;
        rules.ResetBindings();
    }

    private static bool Validate(List<TrainerLevelCapRule> rules)
    {
        foreach (var rule in rules)
        {
            if (rule.LevelCap != 0 && (rule.LevelCap < 1 || rule.LevelCap > 100))
            {
                WinFormsUtil.Alert("Invalid level cap detected. Use 0 for the current ace level, or a value from 1 to 100.");
                return false;
            }
}

        if (rules.GroupBy(r => r.TrainerID).Any(g => g.Count() > 1))
        {
            WinFormsUtil.Alert("Duplicate trainer IDs were detected in the level cap list.");
            return false;
        }

        return true;
    }
}
