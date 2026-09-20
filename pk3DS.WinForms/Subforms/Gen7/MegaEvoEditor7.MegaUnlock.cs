using System;
using System.Drawing;
using System.Windows.Forms;

namespace pk3DS.WinForms;

public partial class MegaEvoEditor7
{
    private Button B_UnlockMegaFromStart;

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        AddMegaEventFlagUnlockButton();
    }

    private void AddMegaEventFlagUnlockButton()
    {
        if (!Main.Config.USUM || B_UnlockMegaFromStart is not null)
            return;

        // The stock Mega Evolution editor is fixed-size and ends immediately
        // below the two Mega Evolution groups. Grow it just enough for this
        // deterministic gameplay patch without changing the existing designer.
        MaximumSize = Size.Empty;
        MinimumSize = Size.Empty;
        ClientSize = new Size(ClientSize.Width, ClientSize.Height + 40);

        B_UnlockMegaFromStart = new Button
        {
            Name = "B_UnlockMegaFromStart",
            Location = new Point(12, GB_MEvo1.Bottom + 8),
            Size = new Size(258, 27),
            Text = "Enable Mega Evolution from Start",
            UseVisualStyleBackColor = true,
        };
        B_UnlockMegaFromStart.Click += B_UnlockMegaFromStart_Click;

        Controls.Add(B_UnlockMegaFromStart);
        B_UnlockMegaFromStart.BringToFront();

        // Keep the editor fixed-size, matching its original behavior.
        MinimumSize = Size;
        MaximumSize = Size;

        RefreshMegaEventFlagUnlockButton();
    }

    private void B_UnlockMegaFromStart_Click(object sender, EventArgs e)
    {
        if (WinFormsUtil.Prompt(
                MessageBoxButtons.YesNo,
                "Enable Mega Evolution from the start of the game?",
                "This removes the USUM story/event-flag requirement for Mega Evolution by applying a validated Battle.cro patch.") != DialogResult.Yes)
        {
            return;
        }

        try
        {
            int changed = Gen7MegaEventFlagPatcher.Apply();

            RandomizationSessionState.MarkAction(
                Gen7MegaEventFlagPatcher.ActionId);

            RefreshMegaEventFlagUnlockButton();

            WinFormsUtil.Alert(
                changed == 0
                    ? "Mega Evolution from Start was already enabled."
                    : "Mega Evolution from Start enabled!",
                "Mega Evolution no longer requires the normal USUM story/event unlock flag.");
        }
        catch (Exception ex)
        {
            WinFormsUtil.Error(
                "Could not enable Mega Evolution from Start.",
                ex.Message);
        }
    }

    private void RefreshMegaEventFlagUnlockButton()
    {
        if (B_UnlockMegaFromStart is null)
            return;

        var state = Gen7MegaEventFlagPatcher.GetState();

        switch (state)
        {
            case Gen7MegaEventFlagPatcher.PatchState.Stock:
                B_UnlockMegaFromStart.Text =
                    "Enable Mega Evolution from Start";
                B_UnlockMegaFromStart.Enabled = true;
                break;

            case Gen7MegaEventFlagPatcher.PatchState.Applied:
                B_UnlockMegaFromStart.Text =
                    "Mega Evolution from Start: Enabled";
                B_UnlockMegaFromStart.Enabled = false;

                // If a ROM already contains the patch when this editor is
                // opened, treat it as part of the current setup so a newly
                // saved Global Template can reproduce it.
                RandomizationSessionState.MarkAction(
                    Gen7MegaEventFlagPatcher.ActionId);
                break;

            case Gen7MegaEventFlagPatcher.PatchState.MissingBattleCro:
                B_UnlockMegaFromStart.Text =
                    "Battle.cro not found";
                B_UnlockMegaFromStart.Enabled = false;
                break;

            default:
                B_UnlockMegaFromStart.Text =
                    "Mega unlock: unsupported Battle.cro";
                B_UnlockMegaFromStart.Enabled = false;
                break;
        }
    }
}
