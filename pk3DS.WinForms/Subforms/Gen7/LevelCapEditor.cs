using pk3DS.Core.Modding.Research;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace pk3DS.WinForms;

/// <summary>
/// Advanced editor for the validated story conditions and caps used by
/// Player Level Caps.
/// </summary>
public sealed class LevelCapEditor : Form
{
    private readonly DataGridView Grid = new()
    {
        Dock = DockStyle.Fill,
        AllowUserToResizeRows = false,
        RowHeadersVisible = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        SelectionMode = DataGridViewSelectionMode.CellSelect,
    };

    private readonly TextBox Problems = new()
    {
        Multiline = true,
        Dock = DockStyle.Bottom,
        Height = 90,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Font = new Font(FontFamily.GenericMonospace, 8.25f),
    };

    public LevelCapTable Result { get; private set; }

    public LevelCapEditor(LevelCapTable start)
    {
        Text = "Player Level Cap Checkpoints";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(760, 470);
        Size = new Size(900, 580);

        var flagCol = new DataGridViewComboBoxColumn
        {
            HeaderText = "Where in game",
            Name = "Label",
            FillWeight = 190,
            DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing,
            FlatStyle = FlatStyle.Flat,
            AutoComplete = true,
        };

        foreach (StoryFlag condition in LevelCapTable.KnownFlags)
            flagCol.Items.Add(condition.Label);

        var kindCol = new DataGridViewComboBoxColumn
        {
            HeaderText = "Condition",
            Name = "Kind",
            FillWeight = 90,
            DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing,
            FlatStyle = FlatStyle.Flat,
        };

        kindCol.Items.AddRange(
            Enum.GetNames(typeof(LevelCapConditionKind))
                .Cast<object>()
                .ToArray());

        Grid.Columns.Add(flagCol);
        Grid.Columns.Add(kindCol);

        Grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Flag offset / Work index",
            Name = "Offset",
            FillWeight = 85,
        });

        Grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Mask / threshold",
            Name = "Bit",
            FillWeight = 85,
        });

        Grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Level cap",
            Name = "Cap",
            FillWeight = 60,
        });

        var initial =
            start ??
            LevelCapTable.Default();

        foreach (LevelCapEntry entry in initial.Entries)
        {
            if (!flagCol.Items.Contains(entry.Label))
                flagCol.Items.Add(entry.Label);
        }

        Grid.CellValueChanged += (_, ev) =>
        {
            if (ev.RowIndex < 0 ||
                Grid.Columns[ev.ColumnIndex].Name != "Label")
            {
                return;
            }

            string picked =
                Grid.Rows[ev.RowIndex]
                    .Cells["Label"]
                    .Value?
                    .ToString() ??
                string.Empty;

            StoryFlag condition =
                LevelCapTable.KnownFlags
                    .FirstOrDefault(z =>
                        z.Label == picked);

            if (condition is null)
                return;

            Grid.Rows[ev.RowIndex].Cells["Kind"].Value =
                condition.Kind.ToString();

            Grid.Rows[ev.RowIndex].Cells["Offset"].Value =
                $"0x{condition.Offset:X4}";

            Grid.Rows[ev.RowIndex].Cells["Bit"].Value =
                FormatConditionValue(
                    condition.Kind,
                    condition.Bit);

            RevalidateTable();
        };

        Grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (Grid.IsCurrentCellDirty)
                Grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };

        foreach (LevelCapEntry entry in initial.Entries)
        {
            Grid.Rows.Add(
                entry.Label,
                entry.Kind.ToString(),
                $"0x{entry.FlagOffset:X4}",
                FormatConditionValue(
                    entry.Kind,
                    entry.FlagBit),
                entry.Cap.ToString());
        }

        var use = new Button { Text = "Use this", Width = 100 };
        var cancel = new Button { Text = "Cancel", Width = 80 };
        var reset = new Button { Text = "Reset validated", Width = 120 };
        var add = new Button { Text = "Add row", Width = 90 };
        var delete = new Button { Text = "Delete row", Width = 100 };

        use.Click += (_, _) =>
        {
            LevelCapTable table =
                Read(
                    out string error);

            if (table is null)
            {
                Problems.Text = error;
                return;
            }

            List<string> problems =
                table.Validate();

            if (problems.Count != 0)
            {
                Problems.Text =
                    string.Join(
                        Environment.NewLine,
                        problems.Select(z =>
                            "- " + z));
                return;
            }

            Result = table;
            DialogResult = DialogResult.OK;
            Close();
        };

        cancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        reset.Click += (_, _) =>
        {
            Grid.Rows.Clear();

            foreach (LevelCapEntry entry in LevelCapTable.Default().Entries)
            {
                Grid.Rows.Add(
                    entry.Label,
                    entry.Kind.ToString(),
                    $"0x{entry.FlagOffset:X4}",
                    FormatConditionValue(
                        entry.Kind,
                        entry.FlagBit),
                    entry.Cap.ToString());
            }

            RevalidateTable();
        };

        add.Click += (_, _) =>
        {
            var used =
                Grid.Rows
                    .Cast<DataGridViewRow>()
                    .Where(z =>
                        !z.IsNewRow)
                    .Select(z =>
                        z.Cells["Label"]
                            .Value?
                            .ToString() ??
                        string.Empty)
                    .ToHashSet();

            StoryFlag next =
                LevelCapTable.KnownFlags
                    .FirstOrDefault(z =>
                        !used.Contains(z.Label)) ??
                LevelCapTable.KnownFlags[0];

            Grid.Rows.Add(
                next.Label,
                next.Kind.ToString(),
                $"0x{next.Offset:X4}",
                FormatConditionValue(
                    next.Kind,
                    next.Bit),
                "100");

            RevalidateTable();
        };

        delete.Click += (_, _) =>
        {
            if (Grid.CurrentRow is not null &&
                !Grid.CurrentRow.IsNewRow)
            {
                Grid.Rows.Remove(
                    Grid.CurrentRow);
            }

            RevalidateTable();
        };

        Grid.CellEndEdit += (_, _) =>
            RevalidateTable();

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 36,
            Padding = new Padding(4, 3, 0, 0),
        };

        buttons.Controls.AddRange(
        [
            use,
            cancel,
            reset,
            add,
            delete,
        ]);

        var help = new Label
        {
            Dock = DockStyle.Top,
            Height = 44,
            ForeColor = Color.DimGray,
            Padding = new Padding(6, 4, 0, 0),
            Text =
                "Validated conditions only. EventFlagSet uses byte offset + mask; " +
                "EventWorkAtLeast uses work index + minimum value.",
        };

        Controls.Add(Grid);
        Controls.Add(Problems);
        Controls.Add(buttons);
        Controls.Add(help);

        RevalidateTable();
    }

    private static string FormatConditionValue(
        LevelCapConditionKind kind,
        ushort value) =>
        kind == LevelCapConditionKind.EventFlagSet
            ? $"0x{value:X2}"
            : value.ToString();

    private static bool TryByte(
        string text,
        out byte value)
    {
        value = 0;

        string s =
            (text ?? string.Empty)
                .Trim();

        if (s.Length == 0)
            return false;

        bool hex =
            s.StartsWith(
                "0x",
                StringComparison.OrdinalIgnoreCase);

        if (hex)
            s = s[2..];

        try
        {
            int parsed =
                Convert.ToInt32(
                    s,
                    hex
                        ? 16
                        : 10);

            if (parsed is < 0 or > 255)
                return false;

            value =
                (byte)parsed;

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryUInt16(
        string text,
        out ushort value)
    {
        value = 0;

        string s =
            (text ?? string.Empty)
                .Trim();

        if (s.Length == 0)
            return false;

        bool hex =
            s.StartsWith(
                "0x",
                StringComparison.OrdinalIgnoreCase);

        if (hex)
            s = s[2..];

        try
        {
            int parsed =
                Convert.ToInt32(
                    s,
                    hex
                        ? 16
                        : 10);

            if (parsed is < 0 or > ushort.MaxValue)
                return false;

            value =
                (ushort)parsed;

            return true;
        }
        catch
        {
            return false;
        }
    }

    private LevelCapTable Read(
        out string error)
    {
        error = null;

        var entries =
            new List<LevelCapEntry>();

        foreach (DataGridViewRow row in Grid.Rows)
        {
            if (row.IsNewRow)
                continue;

            string label =
                row.Cells["Label"]
                    .Value?
                    .ToString()?
                    .Trim() ??
                string.Empty;

            if (label.Length == 0)
                continue;

            string kindText =
                row.Cells["Kind"]
                    .Value?
                    .ToString() ??
                string.Empty;

            if (!Enum.TryParse(
                    kindText,
                    ignoreCase: true,
                    out LevelCapConditionKind kind))
            {
                error =
                    $"'{label}': invalid condition kind '{kindText}'.";
                return null;
            }

            if (!TryUInt16(
                    row.Cells["Offset"]
                        .Value?
                        .ToString(),
                    out ushort offset))
            {
                error =
                    $"'{label}': offset/work index is not a 16-bit value.";
                return null;
            }

            if (!TryUInt16(
                    row.Cells["Bit"]
                        .Value?
                        .ToString(),
                    out ushort value))
            {
                error =
                    $"'{label}': mask/threshold is not a 16-bit value.";
                return null;
            }

            if (!TryByte(
                    row.Cells["Cap"]
                        .Value?
                        .ToString(),
                    out byte cap))
            {
                error =
                    $"'{label}': level cap is not a number between 1 and 100.";
                return null;
            }

            entries.Add(
                new LevelCapEntry(
                    label,
                    kind,
                    offset,
                    value,
                    cap));
        }

        if (entries.Count == 0)
        {
            error =
                "No checkpoints are defined.";
            return null;
        }

        return new LevelCapTable
        {
            Entries = entries,
        };
    }

    private void RevalidateTable()
    {
        LevelCapTable table =
            Read(
                out string error);

        if (table is null)
        {
            Problems.Text = error;
            return;
        }

        List<string> problems =
            table.Validate();

        Problems.Text =
            problems.Count == 0
                ? $"OK - {table.Entries.Count} checkpoint(s), " +
                  $"{table.ToBytes().Length} table bytes."
                : string.Join(
                    Environment.NewLine,
                    problems.Select(z =>
                        "- " + z));
    }
}
