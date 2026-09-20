using System;
using System.Drawing;
using System.Windows.Forms;

namespace pk3DS.WinForms;

public sealed partial class Main
{
    private Button B_PlayerLevelCaps;

    private void AddPlayerLevelCapsButtonIfNeeded()
    {
        if (Config?.USUM != true)
            return;

        B_PlayerLevelCaps ??= new Button
        {
            Name = "B_PlayerLevelCaps",
            Size = new Size(138, 28),
            Margin = new Padding(4, 4, 4, 6),
            Text = "Player Level Caps",
            UseVisualStyleBackColor = true,
        };

        B_PlayerLevelCaps.Click -= B_PlayerLevelCaps_Click;
        B_PlayerLevelCaps.Click += B_PlayerLevelCaps_Click;

        if (!FLP_CRO.Controls.Contains(B_PlayerLevelCaps))
            FLP_CRO.Controls.Add(B_PlayerLevelCaps);
    }

    private void B_PlayerLevelCaps_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;

        if (Config?.USUM != true)
        {
            WinFormsUtil.Alert("Player Level Caps are currently available only for Ultra Sun / Ultra Moon.");
            return;
        }

        using var form = new LevelCapManager7();
        form.ShowDialog(this);
    }
}
