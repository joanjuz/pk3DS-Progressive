using System;
using System.Drawing;
using System.Windows.Forms;

namespace pk3DS.WinForms;

public partial class ItemEditor7
{
    private Button B_PersistentBattleConsumables;

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        AddPersistentBattleConsumablesButton();
        ArrangeItemEditorActionButtons();
    }

    private void AddPersistentBattleConsumablesButton()
    {
        if (!Main.Config.USUM ||
            B_PersistentBattleConsumables is not null)
        {
            return;
        }

        int gap = 4;
        int width = Math.Max(70, (Grid.Width - (gap * 2)) / 3);

        B_PersistentBattleConsumables = new Button
        {
            Location = new Point(Grid.Right - width, B_Table.Top),
            Name = "B_PersistentBattleConsumables",
            Size = new Size(width, B_Table.Height),
            Text = "Persistent Items",
            UseVisualStyleBackColor = true,
        };

        B_PersistentBattleConsumables.Click +=
            B_PersistentBattleConsumables_Click;

        Controls.Add(B_PersistentBattleConsumables);
        B_PersistentBattleConsumables.BringToFront();

        RefreshPersistentBattleConsumablesButton();
    }

    private void ArrangeItemEditorActionButtons()
    {
        int gap = 4;
        int left = Grid.Left;
        int totalWidth = Grid.Width;
        int y = B_Table.Top;
        int height = B_Table.Height;

        if (B_PersistentBattleConsumables is null)
        {
            int leftWidth =
                Math.Max(
                    70,
                    (totalWidth - gap) / 2);

            int rightWidth =
                Math.Max(
                    70,
                    totalWidth - leftWidth - gap);

            B_FixEconomy.SetBounds(
                left,
                y,
                leftWidth,
                height);

            B_Table.SetBounds(
                left + leftWidth + gap,
                y,
                rightWidth,
                height);

            B_FixEconomy.BringToFront();
            B_Table.BringToFront();
            return;
        }

        int width =
            Math.Max(
                70,
                (totalWidth - (gap * 2)) / 3);

        int lastWidth =
            Math.Max(
                70,
                totalWidth - (width * 2) - (gap * 2));

        B_FixEconomy.SetBounds(
            left,
            y,
            width,
            height);

        B_Table.SetBounds(
            left + width + gap,
            y,
            width,
            height);

        B_PersistentBattleConsumables.SetBounds(
            left + (width * 2) + (gap * 2),
            y,
            lastWidth,
            height);

        B_FixEconomy.BringToFront();
        B_Table.BringToFront();
        B_PersistentBattleConsumables.BringToFront();
    }
    private void B_PersistentBattleConsumables_Click(
        object sender,
        EventArgs e)
    {
        if (WinFormsUtil.Prompt(
                MessageBoxButtons.YesNo,
                "Enable Persistent Battle Consumables?",
                "Single-use held items will still activate and disappear normally during battle, but the original held item will be restored after wild and NPC trainer battles.") != DialogResult.Yes)
        {
            return;
        }

        try
        {
            int changed =
                Gen7PersistentConsumablesPatcher.Apply();

            RandomizationSessionState.MarkAction(
                Gen7PersistentConsumablesPatcher.ActionId);

            RefreshPersistentBattleConsumablesButton();

            WinFormsUtil.Alert(
                changed == 0
                    ? "Persistent Battle Consumables were already enabled."
                    : "Persistent Battle Consumables enabled!",
                changed == 0
                    ? "No Battle.cro instructions needed to be changed."
                    : $"{changed} Battle.cro instruction(s) were updated. Consumable held items still work normally during battle and are restored afterward.");
        }
        catch (Exception ex)
        {
            WinFormsUtil.Error(
                "Could not enable Persistent Battle Consumables.",
                ex.Message);
        }
    }

    private void RefreshPersistentBattleConsumablesButton()
    {
        if (B_PersistentBattleConsumables is null)
            return;

        var state =
            Gen7PersistentConsumablesPatcher.GetState();

        switch (state)
        {
            case Gen7PersistentConsumablesPatcher.PatchState.Stock:
                B_PersistentBattleConsumables.Text =
                    "Persistent Items";
                B_PersistentBattleConsumables.Enabled = true;
                break;

            case Gen7PersistentConsumablesPatcher.PatchState.Partial:
                B_PersistentBattleConsumables.Text =
                    "Persistent: Partial";
                B_PersistentBattleConsumables.Enabled = true;
                break;

            case Gen7PersistentConsumablesPatcher.PatchState.Applied:
                B_PersistentBattleConsumables.Text =
                    "Persistent: ON";
                B_PersistentBattleConsumables.Enabled = false;

                RandomizationSessionState.MarkAction(
                    Gen7PersistentConsumablesPatcher.ActionId);
                break;

            case Gen7PersistentConsumablesPatcher.PatchState.MissingBattleCro:
                B_PersistentBattleConsumables.Text =
                    "Battle.cro missing";
                B_PersistentBattleConsumables.Enabled = false;
                break;

            default:
                B_PersistentBattleConsumables.Text =
                    "Unsupported CRO";
                B_PersistentBattleConsumables.Enabled = false;
                break;
        }
    }
}
