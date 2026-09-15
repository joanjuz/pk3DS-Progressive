using System;
using System.Drawing;
using System.Windows.Forms;

namespace pk3DS.WinForms;

public partial class StaticEncounterEditor7
{
    private const int ArcanineGiftBaselineSpecies = 59;
    private const int SquirtleGiftBaselineSpecies = 7;
    private Button B_ArcanineGiftBaseline;

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        AddArcanineGiftBaselineButton();
        InitializeTotemLevelCaps();
        AddTotemLevelCapsButton();
        B_RandAll.Click += B_RandAll_TotemLevelCapsPost;
    }

    private void AddArcanineGiftBaselineButton()
    {
        if (B_ArcanineGiftBaseline != null)
            return;

        B_ArcanineGiftBaseline = new Button
        {
            Location = new Point(B_RandAll.Left, B_RandAll.Bottom + 8),
            Name = "B_ArcanineGiftBaseline",
            Size = B_RandAll.Size,
            Text = "Set Gift BST Baseline",
            UseVisualStyleBackColor = true,
        };
        B_ArcanineGiftBaseline.Click += B_ArcanineGiftBaseline_Click;

        Tab_Randomizer.Controls.Add(B_ArcanineGiftBaseline);
        B_ArcanineGiftBaseline.BringToFront();
    }

    private void B_ArcanineGiftBaseline_Click(object sender, EventArgs e)
    {
        if (WinFormsUtil.Prompt(
            MessageBoxButtons.YesNo,
            "Set Gift Pokemon BST baselines?",
            "Gift entries 04 through 13 will be changed to Squirtle (BST 314).",
            "All other non-starter Gift entries will be changed to Arcanine (BST 555).",
            "Levels, items, abilities and the rest of each Gift entry are preserved.") != DialogResult.Yes)
        {
            return;
        }

        SetGift();

        // Randomize All intentionally skips the first 3 Gift entries because
        // those are the starter slots. Set only the Gifts that B_RandAll
        // actually randomizes, so starters are not accidentally left as Arcanine.
        int arcanineCount = 0;
        int squirtleCount = 0;
        for (int i = 3; i < Gifts.Length; i++)
        {
            var gift = Gifts[i];
            bool useSquirtle = i >= 4 && i <= 13;
            gift.Species = useSquirtle ? SquirtleGiftBaselineSpecies : ArcanineGiftBaselineSpecies;
            gift.Form = 0;
            if (useSquirtle)
                squirtleCount++;
            else
                arcanineCount++;
        }

        GetListBoxEntries();
        if (LB_Gift.Items.Count > 0)
            LB_Gift.SelectedIndex = Math.Min(Math.Max(gEntry, 0), LB_Gift.Items.Count - 1);
        GetGift();

        WinFormsUtil.Alert(
            "Gift Pokemon baselines updated!",
            $"{squirtleCount} Gift entries (04-13) now use Squirtle (BST 314).",
            $"{arcanineCount} other non-starter Gift entries use Arcanine (BST 555).",
            "Now enable Randomize by BST and press Randomize All.");
    }
}
