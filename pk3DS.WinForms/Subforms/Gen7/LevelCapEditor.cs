using pk3DS.Core.Modding.Research;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace pk3DS.WinForms;

/// <summary>
/// Advanced editor for the story flags and caps used by Player Level Caps.
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
        MinimumSize = new Size(650, 470);
        Size = new Size(760, 580);

        var flagCol = new DataGridViewComboBoxColumn
        {
            HeaderText = "Where in game",
            Name = "Label",
            FillWeight = 190,
            DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing,
            FlatStyle = FlatStyle.Flat,
            AutoComplete = true,
        };

        foreach (var f in LevelCapTable.KnownFlags)
            flagCol.Items.Add(f.Label);

        Grid.Columns.Add(flagCol);
        Grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Flag offset",
            Name = "Offset",
            FillWeight = 70,
        });
        Grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Flag bit",
            Name = "Bit",
            FillWeight = 60,
        });
        Grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Level cap",
            Name = "Cap",
            FillWeight = 60,
        });

        var initial = start ?? LevelCapTable.Default();
        foreach (var e in initial.Entries)
        {
            if (!flagCol.Items.Contains(e.Label))
                flagCol.Items.Add(e.Label);
        }

        Grid.CellValueChanged += (_, ev) =>
        {
            if (ev.RowIndex < 0 || Grid.Columns[ev.ColumnIndex].Name != "Label")
                return;

            string picked = Grid.Rows[ev.RowIndex].Cells["Label"].Value?.ToString() ?? string.Empty;
            var flag = LevelCapTable.KnownFlags.FirstOrDefault(z => z.Label == picked);
            if (flag is null)
                return;

            Grid.Rows[ev.RowIndex].Cells["Offset"].Value = $"0x{flag.Offset:X2}";
            Grid.Rows[ev.RowIndex].Cells["Bit"].Value = $"0x{flag.Bit:X2}";
            RevalidateTable();
        };

        Grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (Grid.IsCurrentCellDirty)
                Grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };

        foreach (var e in initial.Entries)
            Grid.Rows.Add(e.Label, $"0x{e.FlagOffset:X2}", $"0x{e.FlagBit:X2}", e.Cap.ToString());

        var use = new Button { Text = "Use this", Width = 100 };
        var cancel = new Button { Text = "Cancel", Width = 80 };
        var reset = new Button { Text = "Reset default", Width = 110 };
        var add = new Button { Text = "Add row", Width = 90 };
        var delete = new Button { Text = "Delete row", Width = 100 };

        use.Click += (_, _) =>
        {
            var table = Read(out string error);
            if (table is null)
            {
                Problems.Text = error;
                return;
            }

            var problems = table.Validate();
            if (problems.Count != 0)
            {
                Problems.Text = string.Join(Environment.NewLine, problems.Select(z => "- " + z));
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
            foreach (var e in LevelCapTable.Default().Entries)
                Grid.Rows.Add(e.Label, $"0x{e.FlagOffset:X2}", $"0x{e.FlagBit:X2}", e.Cap.ToString());
            RevalidateTable();
        };

        add.Click += (_, _) =>
        {
            var used = Grid.Rows.Cast<DataGridViewRow>()
                .Where(z => !z.IsNewRow)
                .Select(z => z.Cells["Label"].Value?.ToString() ?? string.Empty)
                .ToHashSet();

            var next = LevelCapTable.KnownFlags.FirstOrDefault(z => !used.Contains(z.Label))
                       ?? LevelCapTable.KnownFlags[0];

            Grid.Rows.Add(next.Label, $"0x{next.Offset:X2}", $"0x{next.Bit:X2}", "100");
            RevalidateTable();
        };

        delete.Click += (_, _) =>
        {
            if (Grid.CurrentRow is not null && !Grid.CurrentRow.IsNewRow)
                Grid.Rows.Remove(Grid.CurrentRow);
            RevalidateTable();
        };

        Grid.CellEndEdit += (_, _) => RevalidateTable();

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 36,
            Padding = new Padding(4, 3, 0, 0),
        };
        buttons.Controls.AddRange([use, cancel, reset, add, delete]);

        var help = new Label
        {
            Dock = DockStyle.Top,
            Height = 40,
            ForeColor = Color.DimGray,
            Padding = new Padding(6, 4, 0, 0),
            Text = "Each row is one story flag and the cap that applies until the next checkpoint. " +
                   "Offsets and flag bits are hexadecimal.",
        };

        Controls.Add(Grid);
        Controls.Add(Problems);
        Controls.Add(buttons);
        Controls.Add(help);
        RevalidateTable();
    }

    private static bool TryByte(string text, out byte value)
    {
        value = 0;
        string s = (text ?? string.Empty).Trim();
        if (s.Length == 0)
            return false;

        bool hex = s.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        if (hex)
            s = s[2..];

        try
        {
            int v = Convert.ToInt32(s, hex ? 16 : 10);
            if (v is < 0 or > 255)
                return false;

            value = (byte)v;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private LevelCapTable Read(out string error)
    {
        error = null;
        var entries = new List<LevelCapEntry>();

        foreach (DataGridViewRow row in Grid.Rows)
        {
            if (row.IsNewRow)
                continue;

            string label = row.Cells["Label"].Value?.ToString()?.Trim() ?? string.Empty;
            if (label.Length == 0)
                continue;

            if (!TryByte(row.Cells["Offset"].Value?.ToString(), out byte offset))
            {
                error = $"'{label}': flag offset is not a byte (use hex like 0x11).";
                return null;
            }

            if (!TryByte(row.Cells["Bit"].Value?.ToString(), out byte bit))
            {
                error = $"'{label}': flag bit is not a byte (use hex like 0x01).";
                return null;
            }

            if (!TryByte(row.Cells["Cap"].Value?.ToString(), out byte cap))
            {
                error = $"'{label}': level cap is not a number between 1 and 100.";
                return null;
            }

            entries.Add(new LevelCapEntry(label, offset, bit, cap));
        }

        if (entries.Count == 0)
        {
            error = "No checkpoints are defined.";
            return null;
        }

        return new LevelCapTable { Entries = entries };
    }

    private void RevalidateTable()
    {
        var table = Read(out string error);
        if (table is null)
        {
            Problems.Text = error;
            return;
        }

        var problems = table.Validate();
        Problems.Text = problems.Count == 0
            ? $"OK - {table.Entries.Count} checkpoint(s), {table.ToBytes().Length} table bytes."
            : string.Join(Environment.NewLine, problems.Select(z => "- " + z));
    }
}
