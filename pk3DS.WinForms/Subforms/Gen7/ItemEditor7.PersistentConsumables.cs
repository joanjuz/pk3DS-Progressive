using System;
using System.Drawing;
using System.Linq;
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

        B_PersistentBattleConsumables = new Button
        {
            Name = "B_PersistentBattleConsumables",
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
        if (B_PersistentBattleConsumables is null)
            return;

        var fixEconomy = Controls
            .Find("B_FixEconomy", false)
            .OfType<Button>()
            .FirstOrDefault();

        int margin = 6;
        int gap = 4;
        int y = B_Table.Top;
        int height = B_Table.Height;

        if (fixEconomy is null)
        {
            B_PersistentBattleConsumables.SetBounds(
                margin,
                y,
                Math.Max(90, B_Table.Width),
                height);
            return;
        }

        int usable =
            ClientSize.Width -
            (margin * 2) -
            (gap * 2);

        int width =
            Math.Max(70, usable / 3);

        int lastWidth =
            Math.Max(
                70,
                usable - (width * 2));

        B_PersistentBattleConsumables.SetBounds(
            margin,
            y,
            width,
            height);

        fixEconomy.SetBounds(
            margin + width + gap,
            y,
            width,
            height);

        B_Table.SetBounds(
            margin + (width * 2) + (gap * 2),
            y,
            lastWidth,
            height);
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
