using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace pk3DS.WinForms;

internal sealed class EconomyPriceDialog : Form
{
    private readonly byte[][] files;
    private readonly string[] itemNames;
    private readonly DataGridView grid = new();
    private readonly TextBox search = new();
    private readonly Label summary = new();

    internal SortedDictionary<int, int> Prices { get; private set; } = [];

    internal EconomyPriceDialog(
        byte[][] files,
        string[] itemNames,
        IReadOnlyDictionary<int, int> initialPrices)
    {
        this.files =
            files ??
            throw new ArgumentNullException(
                nameof(files));

        this.itemNames =
            itemNames ??
            throw new ArgumentNullException(
                nameof(itemNames));

        Text =
            "Economy Price Table";

        StartPosition =
            FormStartPosition.CenterParent;

        MinimumSize =
            new Size(
                860,
                520);

        ClientSize =
            new Size(
                1040,
                650);

        BuildUi();
        LoadRows(
            initialPrices);
    }

    private void BuildUi()
    {
        var info =
            new Label
            {
                AutoSize = false,
                Left = 12,
                Top = 10,
                Width = 755,
                Height = 38,
                Text =
                    "Every item in this table is applied. Remove an item to exclude it. " +
                    "Gen 7 stores one price field, so Sell Price is always half of Buy Price. " +
                    "Target prices must be multiples of 10.",
            };

        Controls.Add(
            info);

        var searchLabel =
            new Label
            {
                AutoSize = true,
                Text = "Search:",
                Left = 780,
                Top = 17,
            };

        Controls.Add(
            searchLabel);

        search.SetBounds(
            835,
            12,
            190,
            24);

        search.TextChanged +=
            (_, _) =>
                ApplyFilter();

        Controls.Add(
            search);

        grid.SetBounds(
            12,
            55,
            ClientSize.Width - 24,
            ClientSize.Height - 120);

        grid.Anchor =
            AnchorStyles.Top |
            AnchorStyles.Bottom |
            AnchorStyles.Left |
            AnchorStyles.Right;

        grid.AllowUserToAddRows =
            false;

        grid.AllowUserToDeleteRows =
            false;

        grid.AllowUserToResizeRows =
            false;

        grid.AutoGenerateColumns =
            false;

        grid.MultiSelect =
            false;

        grid.SelectionMode =
            DataGridViewSelectionMode.FullRowSelect;

        grid.RowHeadersVisible =
            false;

        grid.CellValidating +=
            Grid_CellValidating;

        grid.CellEndEdit +=
            Grid_CellEndEdit;

        grid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                Name = "ID",
                HeaderText = "ID",
                ReadOnly = true,
                Width = 58,
                ValueType = typeof(int),
            });

        grid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                Name = "Item",
                HeaderText = "Item",
                ReadOnly = true,
                AutoSizeMode =
                    DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 190,
            });

        grid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                Name = "CurrentBuy",
                HeaderText = "Current Buy",
                ReadOnly = true,
                Width = 105,
                ValueType = typeof(int),
            });

        grid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                Name = "TargetBuy",
                HeaderText = "Target Buy",
                ReadOnly = false,
                Width = 105,
                ValueType = typeof(int),
            });

        grid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                Name = "TargetSell",
                HeaderText = "Target Sell",
                ReadOnly = true,
                Width = 105,
                ValueType = typeof(int),
            });

        grid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                Name = "Source",
                HeaderText = "Source",
                ReadOnly = true,
                Width = 155,
            });

        Controls.Add(
            grid);

        int bottom =
            ClientSize.Height - 52;

        var add =
            MakeButton(
                "Add Item",
                12,
                bottom,
                92);

        add.Anchor =
            AnchorStyles.Left |
            AnchorStyles.Bottom;

        add.Click +=
            (_, _) =>
                AddItem();

        Controls.Add(
            add);

        var remove =
            MakeButton(
                "Remove Item",
                112,
                bottom,
                105);

        remove.Anchor =
            AnchorStyles.Left |
            AnchorStyles.Bottom;

        remove.Click +=
            (_, _) =>
                RemoveItem();

        Controls.Add(
            remove);

        var reset =
            MakeButton(
                "Reset Defaults",
                225,
                bottom,
                118);

        reset.Anchor =
            AnchorStyles.Left |
            AnchorStyles.Bottom;

        reset.Click +=
            (_, _) =>
                ResetDefaults();

        Controls.Add(
            reset);

        summary.AutoSize =
            true;

        summary.Left =
            360;

        summary.Top =
            bottom + 7;

        summary.Anchor =
            AnchorStyles.Left |
            AnchorStyles.Bottom;

        Controls.Add(
            summary);

        var cancel =
            MakeButton(
                "Cancel",
                ClientSize.Width - 205,
                bottom,
                88);

        cancel.Anchor =
            AnchorStyles.Right |
            AnchorStyles.Bottom;

        cancel.DialogResult =
            DialogResult.Cancel;

        Controls.Add(
            cancel);

        var apply =
            MakeButton(
                "Apply",
                ClientSize.Width - 105,
                bottom,
                88);

        apply.Anchor =
            AnchorStyles.Right |
            AnchorStyles.Bottom;

        apply.Click +=
            (_, _) =>
                ApplyAndClose();

        Controls.Add(
            apply);

        CancelButton =
            cancel;

        AcceptButton =
            apply;
    }

    private static Button MakeButton(
        string text,
        int left,
        int top,
        int width) =>
        new()
        {
            Text = text,
            Left = left,
            Top = top,
            Width = width,
            Height = 28,
            UseVisualStyleBackColor = true,
        };

    private void LoadRows(
        IReadOnlyDictionary<int, int> prices)
    {
        grid.Rows.Clear();

        foreach ((int itemID, int targetPrice) in
                 (prices ??
                  new SortedDictionary<int, int>())
                    .OrderBy(z => z.Key))
        {
            if (!IsUsableItem(
                    itemID))
            {
                continue;
            }

            AddGridRow(
                itemID,
                targetPrice,
                EconomyFixer.GetSourceLabel(
                    itemID,
                    targetPrice,
                    files.Length));
        }

        UpdateSummary();
        ApplyFilter();
    }

    private void AddGridRow(
        int itemID,
        int targetPrice,
        string source)
    {
        int currentPrice =
            EconomyFixer.GetCurrentBuyPrice(
                files,
                itemID);

        string name =
            itemID < itemNames.Length &&
            !string.IsNullOrWhiteSpace(
                itemNames[itemID])
                ? itemNames[itemID]
                : $"Item {itemID}";

        int row =
            grid.Rows.Add(
                itemID,
                name,
                currentPrice,
                targetPrice,
                EconomyFixer.GetSellPriceFromBuy(
                    targetPrice),
                source);

        grid.Rows[row].Tag =
            itemID;
    }

    private bool IsUsableItem(
        int itemID) =>
        itemID > 0 &&
        itemID < files.Length &&
        files[itemID] is { Length: > 0 };

    private HashSet<int> GetListedItemIds() =>
        grid.Rows
            .Cast<DataGridViewRow>()
            .Where(z =>
                z.Tag is int)
            .Select(z =>
                (int)z.Tag)
            .ToHashSet();

    private void AddItem()
    {
        HashSet<int> listed =
            GetListedItemIds();

        ItemChoice[] choices =
            Enumerable
                .Range(
                    1,
                    files.Length - 1)
                .Where(id =>
                    IsUsableItem(id) &&
                    !listed.Contains(id))
                .Select(id =>
                    new ItemChoice(
                        id,
                        id < itemNames.Length &&
                        !string.IsNullOrWhiteSpace(
                            itemNames[id])
                            ? itemNames[id]
                            : $"Item {id}"))
                .ToArray();

        if (choices.Length == 0)
        {
            WinFormsUtil.Alert(
                "Every available item is already in the economy table.");
            return;
        }

        using var picker =
            new Form
            {
                Text = "Add Economy Item",
                StartPosition =
                    FormStartPosition.CenterParent,
                FormBorderStyle =
                    FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ClientSize =
                    new Size(
                        430,
                        105),
            };

        var label =
            new Label
            {
                Text = "Item:",
                AutoSize = true,
                Left = 12,
                Top = 18,
            };

        var combo =
            new ComboBox
            {
                Left = 55,
                Top = 13,
                Width = 360,
                DropDownStyle =
                    ComboBoxStyle.DropDownList,
            };

        combo.Items.AddRange(
            choices
                .Cast<object>()
                .ToArray());

        combo.SelectedIndex =
            0;

        var cancel =
            new Button
            {
                Text = "Cancel",
                DialogResult =
                    DialogResult.Cancel,
                Left = 230,
                Top = 58,
                Width = 85,
            };

        var add =
            new Button
            {
                Text = "Add",
                DialogResult =
                    DialogResult.OK,
                Left = 330,
                Top = 58,
                Width = 85,
            };

        picker.Controls.AddRange(
            [label, combo, cancel, add]);

        picker.CancelButton =
            cancel;

        picker.AcceptButton =
            add;

        if (picker.ShowDialog(this) !=
                DialogResult.OK ||
            combo.SelectedItem is not
                ItemChoice selected)
        {
            return;
        }

        int currentPrice =
            EconomyFixer.GetCurrentBuyPrice(
                files,
                selected.Id);

        AddGridRow(
            selected.Id,
            currentPrice,
            "Custom");

        grid.Sort(
            grid.Columns["ID"],
            System.ComponentModel.ListSortDirection.Ascending);

        SelectItem(
            selected.Id);

        UpdateSummary();
        ApplyFilter();
    }

    private void RemoveItem()
    {
        if (grid.CurrentRow is null)
        {
            WinFormsUtil.Alert(
                "Select an item row to remove.");
            return;
        }

        grid.Rows.Remove(
            grid.CurrentRow);

        UpdateSummary();
    }

    private void ResetDefaults()
    {
        if (DialogResult.Yes !=
            WinFormsUtil.Prompt(
                MessageBoxButtons.YesNo,
                "Reset economy price table?",
                "This will replace the current table with the default UPR-ZX Gen 7 + pk3DS-Progressive preset."))
        {
            return;
        }

        LoadRows(
            EconomyFixer.GetDefaultPrices(
                files.Length));
    }

    private void ApplyAndClose()
    {
        if (!TryCollectPrices(
                out SortedDictionary<int, int> prices,
                out string error))
        {
            WinFormsUtil.Error(
                "Could not apply Fix Economy.",
                error);
            return;
        }

        Prices =
            prices;

        DialogResult =
            DialogResult.OK;

        Close();
    }

    private bool TryCollectPrices(
        out SortedDictionary<int, int> prices,
        out string error)
    {
        prices =
            new SortedDictionary<int, int>();

        error =
            string.Empty;

        foreach (DataGridViewRow row in
                 grid.Rows)
        {
            if (row.Tag is not int itemID)
                continue;

            if (!TryReadTargetPrice(
                    row,
                    out int targetPrice,
                    out error))
            {
                return false;
            }

            if (!prices.TryAdd(
                    itemID,
                    targetPrice))
            {
                error =
                    $"Duplicate item ID {itemID}.";
                return false;
            }
        }

        return true;
    }

    private static bool TryReadTargetPrice(
        DataGridViewRow row,
        out int price,
        out string error)
    {
        price =
            0;

        error =
            string.Empty;

        object raw =
            row.Cells["TargetBuy"].Value;

        if (!int.TryParse(
                Convert.ToString(raw),
                out price))
        {
            error =
                $"Target Buy for item {row.Cells["ID"].Value} is not a valid integer.";
            return false;
        }

        if (price < 0 ||
            price > 655350)
        {
            error =
                $"Target Buy for item {row.Cells["ID"].Value} must be between 0 and 655350.";
            return false;
        }

        if (price % 10 != 0)
        {
            error =
                $"Target Buy for item {row.Cells["ID"].Value} must be a multiple of 10.";
            return false;
        }

        return true;
    }

    private void Grid_CellValidating(
        object sender,
        DataGridViewCellValidatingEventArgs e)
    {
        if (grid.Columns[e.ColumnIndex].Name !=
            "TargetBuy")
        {
            return;
        }

        if (!int.TryParse(
                Convert.ToString(
                    e.FormattedValue),
                out int price) ||
            price < 0 ||
            price > 655350 ||
            price % 10 != 0)
        {
            e.Cancel =
                true;

            grid.Rows[e.RowIndex].ErrorText =
                "Target Buy must be an integer from 0 to 655350 and a multiple of 10.";

            return;
        }

        grid.Rows[e.RowIndex].ErrorText =
            string.Empty;
    }

    private void Grid_CellEndEdit(
        object sender,
        DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 ||
            grid.Columns[e.ColumnIndex].Name !=
                "TargetBuy")
        {
            return;
        }

        DataGridViewRow row =
            grid.Rows[e.RowIndex];

        row.ErrorText =
            string.Empty;

        if (TryReadTargetPrice(
                row,
                out int price,
                out _))
        {
            row.Cells["TargetSell"].Value =
                EconomyFixer.GetSellPriceFromBuy(
                    price);

            if (row.Tag is int itemID)
            {
                row.Cells["Source"].Value =
                    EconomyFixer.GetSourceLabel(
                        itemID,
                        price,
                        files.Length);
            }
        }
    }

    private void ApplyFilter()
    {
        string needle =
            search.Text.Trim();

        foreach (DataGridViewRow row in
                 grid.Rows)
        {
            if (needle.Length == 0)
            {
                row.Visible =
                    true;

                continue;
            }

            string haystack =
                string.Join(
                    " ",
                    row.Cells["ID"].Value,
                    row.Cells["Item"].Value,
                    row.Cells["Source"].Value);

            row.Visible =
                haystack.Contains(
                    needle,
                    StringComparison.OrdinalIgnoreCase);
        }
    }

    private void SelectItem(
        int itemID)
    {
        foreach (DataGridViewRow row in
                 grid.Rows)
        {
            if (row.Tag is int id &&
                id == itemID)
            {
                row.Selected =
                    true;

                grid.CurrentCell =
                    row.Cells["TargetBuy"];

                return;
            }
        }
    }

    private void UpdateSummary() =>
        summary.Text =
            $"{grid.Rows.Count} item(s) in table";

    private sealed record ItemChoice(
        int Id,
        string Name)
    {
        public override string ToString() =>
            $"{Id:000} - {Name}";
    }
}