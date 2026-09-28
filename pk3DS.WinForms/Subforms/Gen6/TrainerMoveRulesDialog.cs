using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace pk3DS.WinForms;

public static class TrainerMoveRulesDialog
{
    public static bool Edit(IWin32Window owner, ref List<TrainerMoveRule> rules, bool showPerTrainerBetterSmart = true)
    {
        using var form = new Form
        {
            Text = "Trainer Move Rules",
            StartPosition = FormStartPosition.CenterParent,
            Size = showPerTrainerBetterSmart ? new Size(1540, 680) : new Size(1120, 650),
            MinimizeBox = false,
            MaximizeBox = false,
            FormBorderStyle = FormBorderStyle.Sizable,
            MinimumSize = showPerTrainerBetterSmart ? new Size(1480, 650) : new Size(1040, 600),
        };

        var editableRules = new BindingList<TrainerMoveRule>(
            rules.Select(r => r.Clone()).ToList()
        );

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
            DataPropertyName = nameof(TrainerMoveRule.Enabled),
            HeaderText = "Use",
            ToolTipText = "Enable move rules for this important trainer.",
            Width = 45,
        });

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(TrainerMoveRule.MinMovePower),
            HeaderText = "Min Power (0=Off)",
            ToolTipText = "Minimum power for damaging moves. 0 disables minimum power filtering.",
            Width = 115,
        });

        grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(TrainerMoveRule.UseStrongestAttackStat),
            HeaderText = "Strong Stat",
            ToolTipText = "Choose Physical or Special moves according to the Pokémon's stronger attacking stat.",
            Width = 90,
        });

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(TrainerMoveRule.MixedTolerance),
            HeaderText = "Tolerance",
            ToolTipText = "If Attack and Sp. Attack differ by this value or less, allow both Physical and Special moves.",
            Width = 80,
        });

        grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(TrainerMoveRule.AllowStatusMoves),
            HeaderText = "Allow Status",
            ToolTipText = "Allow status moves. Enabled by default.",
            Width = 95,
        });

        grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(TrainerMoveRule.BetterMovesets),
            HeaderText = "Better Movesets",
            ToolTipText = "Force Better Movesets for this trainer. Works when Use is checked, even if the global Better Movesets checkbox is off.",
            Width = 115,
        });

        grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(TrainerMoveRule.SmartItems),
            HeaderText = "Smart Items",
            ToolTipText = "Give this trainer competitive held items based on its final moveset. Only works when Use is checked and Random Held Items is enabled.",
            Width = 90,
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(TrainerMoveRule.OverrideEVs),
            HeaderText = "EVs (-1=Off)",
            ToolTipText = "Apply this EV value to the two strongest base stats of every final Pokémon; the other four are set to 0. -1 disables EV override.",
            Width = 95,
        });

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(TrainerMoveRule.TrainerID),
            HeaderText = "ID",
            Width = 55,
            ReadOnly = true,
        });

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(TrainerMoveRule.Group),
            HeaderText = "Group",
            Width = 95,
            ReadOnly = true,
        });

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(TrainerMoveRule.Trainer),
            HeaderText = "Trainer",
            Width = 280,
            ReadOnly = true,
        });

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(TrainerMoveRule.CurrentAceLevel),
            HeaderText = "Current Ace",
            Width = 85,
            ReadOnly = true,
        });

        grid.DataError += (_, e) => e.ThrowException = false;

        if (!showPerTrainerBetterSmart)
        {
            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (column.DataPropertyName == nameof(TrainerMoveRule.BetterMovesets) ||
                    column.DataPropertyName == nameof(TrainerMoveRule.SmartItems))
                {
                    column.Visible = false;
                }
            }
        }

        var filterPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 38,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(8, 6, 8, 4),
        };

        var filterLabel = new Label
        {
            AutoSize = true,
            Text = "Show:",
            Padding = new Padding(0, 4, 0, 0),
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

        filterPanel.Controls.Add(filterLabel);
        filterPanel.Controls.Add(chkImportant);
        filterPanel.Controls.Add(chkBosses);

        void ApplyTrainerVisibilityFilter()
        {
            grid.CurrentCell = null;

            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.DataBoundItem is not TrainerMoveRule rule)
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


        var evPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 38,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(6),
        };

        var evLabel = new Label
        {
            AutoSize = true,
            Text = "EV value for Use checked:",
            Padding = new Padding(0, 6, 0, 0),
        };

        var evValue = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 252,
            Value = 252,
            Width = 60,
        };

        var applyEVs = new Button { Text = "Apply EVs", Width = 90 };
        var clearEVs = new Button { Text = "Clear EVs", Width = 80 };
        applyEVs.Click += (_, _) =>
        {
            grid.EndEdit();

            int changed = 0;
            foreach (var rule in editableRules.Where(r => r.Enabled))
            {
                rule.OverrideEVs = (int)evValue.Value;
                changed++;
            }

            if (changed == 0)
            {
                WinFormsUtil.Alert("There are no trainers with Use checked.");
                return;
            }

            editableRules.ResetBindings();
            WinFormsUtil.Alert($"EV override applied to {changed} Use-checked trainers.");
        };
        clearEVs.Click += (_, _) =>
        {
            grid.EndEdit();

            int changed = 0;
            foreach (var rule in editableRules.Where(r => r.Enabled))
            {
                rule.OverrideEVs = -1;
                changed++;
            }

            if (changed == 0)
            {
                WinFormsUtil.Alert("There are no trainers with Use checked.");
                return;
            }

            editableRules.ResetBindings();
            WinFormsUtil.Alert($"EV override cleared for {changed} Use-checked trainers.");
        };
        evPanel.Controls.Add(evLabel);
        evPanel.Controls.Add(evValue);
        evPanel.Controls.Add(applyEVs);
        evPanel.Controls.Add(clearEVs);
        // Long explanatory text was removed from the dialog; the documentation now covers these options.

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 68,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(6),
        };

        var ok = new Button { Text = "OK", Width = 85 };
        var cancel = new Button { Text = "Cancel", Width = 85, DialogResult = DialogResult.Cancel };
        var selectAll = new Button { Text = "Select All", Width = 96 };
        var selectNone = new Button { Text = "Select None", Width = 104 };
        var allowStatusAll = new Button { Text = "Allow Status All", Width = 122 };
        var allowStatusNone = new Button { Text = "Allow Status None", Width = 135 };
        var betterMovesetsAll = new Button { Text = "Better Movesets All", Width = 145 };
        var betterMovesetsNone = new Button { Text = "Better Movesets None", Width = 158 };
        var smartItemsAll = new Button { Text = "Smart Items All", Width = 124 };
        var smartItemsNone = new Button { Text = "Smart Items None", Width = 138 };

        if (!showPerTrainerBetterSmart)
        {
            betterMovesetsAll.Visible = false;
            betterMovesetsNone.Visible = false;
            smartItemsAll.Visible = false;
            smartItemsNone.Visible = false;
        }

        foreach (var button in new[] { ok, cancel, selectAll, selectNone, allowStatusAll, allowStatusNone, betterMovesetsAll, betterMovesetsNone, smartItemsAll, smartItemsNone })
            StyleButton(button);

        selectAll.Click += (_, _) => SetSelected(editableRules, true);
        selectNone.Click += (_, _) => SetSelected(editableRules, false);
        allowStatusAll.Click += (_, _) =>
        {
            foreach (var rule in editableRules.Where(r => r.Enabled))
                rule.AllowStatusMoves = true;
            editableRules.ResetBindings();
        };
        betterMovesetsAll.Click += (_, _) => SetBetterMovesets(editableRules, true);
        betterMovesetsNone.Click += (_, _) => SetBetterMovesets(editableRules, false);
        smartItemsAll.Click += (_, _) => SetSmartItems(editableRules, true);
        smartItemsNone.Click += (_, _) => SetSmartItems(editableRules, false);
        allowStatusNone.Click += (_, _) =>
        {
            foreach (var rule in editableRules.Where(r => r.Enabled))
                rule.AllowStatusMoves = false;
            editableRules.ResetBindings();
        };

        List<TrainerMoveRule>? acceptedRules = null;

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
            form.DialogResult = DialogResult.OK;
            form.Close();
        };

        buttons.Controls.Add(selectAll);
        buttons.Controls.Add(selectNone);
        buttons.Controls.Add(allowStatusAll);
        buttons.Controls.Add(allowStatusNone);
        buttons.Controls.Add(betterMovesetsAll);
        buttons.Controls.Add(betterMovesetsNone);
        buttons.Controls.Add(smartItemsAll);
        buttons.Controls.Add(smartItemsNone);
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);

        form.Controls.Add(grid);
        form.Controls.Add(filterPanel);
        form.Controls.Add(evPanel);
        form.Controls.Add(buttons);

        if (form.ShowDialog(owner) != DialogResult.OK || acceptedRules is null)
            return false;

        rules = acceptedRules;
        return true;
    }

    private static void SetSelected(BindingList<TrainerMoveRule> rules, bool value)
    {
        foreach (var rule in rules)
            rule.Enabled = value;
        rules.ResetBindings();
    }

    private static void SetBetterMovesets(BindingList<TrainerMoveRule> rules, bool value)
    {
        foreach (var rule in rules.Where(r => r.Enabled))
            rule.BetterMovesets = value;
        rules.ResetBindings();
    }

    private static void SetSmartItems(BindingList<TrainerMoveRule> rules, bool value)
    {
        foreach (var rule in rules.Where(r => r.Enabled))
            rule.SmartItems = value;
        rules.ResetBindings();
    }

    private static void StyleButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = Color.White;
        button.ForeColor = Color.FromArgb(32, 38, 46);
        button.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(241, 245, 249);
        button.Height = Math.Max(button.Height, 30);
    }

    private static bool Validate(List<TrainerMoveRule> rules)
    {
        foreach (var rule in rules)
        {
            if (rule.MinMovePower < 0 || rule.MinMovePower > 250)
            {
                WinFormsUtil.Alert("Invalid minimum move power detected. Use 0 to disable it, or a value from 1 to 250.");
                return false;
            }


            if (rule.OverrideEVs < -1 || rule.OverrideEVs > 252)
            {
                WinFormsUtil.Alert("Invalid EV override detected. Use -1 to disable it, or a value from 0 to 252.");
                return false;
            }
            if (rule.MixedTolerance < 0 || rule.MixedTolerance > 255)
            {
                WinFormsUtil.Alert("Invalid tolerance detected. Use a value from 0 to 255.");
                return false;
            }
        }

        if (rules.GroupBy(r => r.TrainerID).Any(g => g.Count() > 1))
        {
            WinFormsUtil.Alert("Duplicate trainer IDs were detected in the move rules list.");
            return false;
        }

        return true;
    }
}
