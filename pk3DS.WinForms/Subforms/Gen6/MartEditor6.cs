using pk3DS.Core;
using System;
using System.IO;
using System.Windows.Forms;
using pk3DS.Core.Structures;

namespace pk3DS.WinForms;

public partial class MartEditor6 : Form
{
    public MartEditor6()
    {
        InitializeComponent();
        AddRareCandyButton();

        if (Main.ExeFSPath == null) { WinFormsUtil.Alert("No exeFS code to load."); Close(); }
        string[] files = Directory.GetFiles(Main.ExeFSPath);
        if (!File.Exists(files[0]) || !Path.GetFileNameWithoutExtension(files[0]).Contains("code")) { WinFormsUtil.Alert("No .code.bin detected."); Close(); }
        data = File.ReadAllBytes(files[0]);
        if (data.Length % 0x200 != 0) { WinFormsUtil.Alert(".code.bin not decompressed. Aborting."); Close(); }
        offset = GetDataOffset(data);
        codebin = files[0];
        itemlist[0] = "";
        SetupDGV();
        CB_Location.Items.AddRange(locations);
        CB_Location.SelectedIndex = 0;
        RandSettings.GetFormSettings(this, Controls);
    }
    private const int RareCandyItemID = 50;
    private const int RareCandyPrice = 10;

    private bool setRareCandyPriceOnSave;
    private bool randomizeMartsOnSave;
    private bool randomizeMartsSpecialOnly;
    private Button B_AddRareCandies;
    private static int RegularMartCount => Main.Config.ORAS ? 10 : 9;
    private void AddRareCandyButton()
    {
        B_AddRareCandies = new Button
        {
            Name = "B_AddRareCandies",
            Size = new System.Drawing.Size(150, 23),
            TabIndex = 305,
            Text = "Add Rare Candies",
            UseVisualStyleBackColor = true,
        };

        B_AddRareCandies.Click += B_AddRareCandies_Click;
        Controls.Add(B_AddRareCandies);
        B_AddRareCandies.BringToFront();

        FixMartEditor6Layout();
    }
    private void FixMartEditor6Layout()
    {
        const int gap = 8;

        MaximumSize = new System.Drawing.Size(520, 520);
        MinimumSize = new System.Drawing.Size(470, 440);
        ClientSize = new System.Drawing.Size(470, Math.Max(ClientSize.Height, 405));

        CB_Location.Width = ClientSize.Width - CB_Location.Left - 12;

        dgv.Width = ClientSize.Width - 24;
        dgv.Height = ClientSize.Height - 105;

        int buttonY = dgv.Bottom + gap;

        B_Randomize.Location = new System.Drawing.Point(12, buttonY);

        B_AddRareCandies.Location = new System.Drawing.Point(
            B_Randomize.Right + gap,
            buttonY
        );

        B_Save.Location = new System.Drawing.Point(
            ClientSize.Width - B_Save.Width - 12,
            buttonY
        );

        B_Cancel.Location = new System.Drawing.Point(
            B_Save.Left - B_Cancel.Width - gap,
            buttonY
        );

        CHK_XItems.AutoSize = true;
        CHK_XItems.Location = new System.Drawing.Point(
            12,
            B_Randomize.Bottom + gap
        );
    }
    private void B_AddRareCandies_Click(object sender, EventArgs e)
    {
        if (DialogResult.Yes != WinFormsUtil.Prompt(
            MessageBoxButtons.YesNo,
            "Add Rare Candies to all regular marts?",
            "This will replace the last slot of each regular mart with Rare Candy. Special marts will not be changed. Rare Candy price will be set to 10 when you click Save."))
        {
            return;
        }

        if (entry > -1)
            SetList();

        int rareCandy = GetRareCandyItemID();

        for (int i = 0; i < RegularMartCount && i < entries.Length; i++)
        {
            GetDataOffset(i);

            int count = entries[i];

            if (count <= 0)
                continue;

            int lastSlot = count - 1;
            int writeOffset = dataoffset + (2 * lastSlot);

            if (writeOffset < 0 || writeOffset + 2 > data.Length)
                continue;

            Array.Copy(BitConverter.GetBytes((ushort)rareCandy), 0, data, writeOffset, 2);
        }

        setRareCandyPriceOnSave = true;

        if (entry > -1)
            GetList();

        WinFormsUtil.Alert(
            "Rare Candies added!",
            "Click Save to write code.bin and set Rare Candy price to 10.");
    }
    private int GetRareCandyItemID()
    {
        int item = Array.FindIndex(itemlist, z =>
            string.Equals(z, "Rare Candy", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(z, "Caramelo Raro", StringComparison.OrdinalIgnoreCase));

        return item > 0 ? item : RareCandyItemID;
    }
    private static void SetItemPrice(int itemID, int price)
    {
        var g = Main.Config.GetGARCData("item");
        byte[][] files = g.Files;

        if (itemID <= 0 || itemID >= files.Length)
        {
            WinFormsUtil.Alert("Could not set Rare Candy price.", $"Invalid item ID: {itemID}");
            return;
        }

        var item = new Item(files[itemID]);
        item.BuyPrice = price;

        files[itemID] = item.Write();

        g.Files = files;
        g.Save();
    }
    private static int GetDataOffset(byte[] data)
    {
        byte[] vanilla =
        [
            0x00, 0x72, 0x6F, 0x6D, 0x3A, 0x2F, 0x44, 0x6C, 0x6C, 0x53, 0x74, 0x61, 0x72, 0x74, 0x4D, 0x65,
            0x6E, 0x75, 0x2E, 0x63, 0x72, 0x6F, 0x00,
        ];
        int offset = Util.IndexOfBytes(data, vanilla, 0x400000, 0);
        if (offset >= 0)
            return offset + vanilla.Length;

        byte[] patched =
        [
            0x00, 0x72, 0x6F, 0x6D, 0x32, 0x3A, 0x2F, 0x44, 0x6C, 0x6C, 0x53, 0x74, 0x61, 0x72, 0x74, 0x4D,
            0x65, 0x6E, 0x75, 0x2E, 0x63, 0x72, 0x6F, 0x00, 0xFF,
        ];
        offset = Util.IndexOfBytes(data, patched, 0x400000, 0);

        if (offset >= 0)
            return offset + patched.Length;

        return -1;
    }

    private readonly string codebin;
    private readonly string[] itemlist = Main.Config.GetText(TextName.ItemNames);
    private readonly byte[] data;

    private readonly byte[] entries = Main.Config.ORAS
        ?
        [
            3, 10, 14, 17, 18, 19, 19, 19, 19, // General
            1,
            9, 6, 4, 3, 8,
            8, 3, 3, 4,
            3, 6, 8,
            7, 4,
        ]
        :
        [
            2, 11, 14, 17, 18, 19, 19, 19, 19, // General
            1, // Unused
            4, 10, 3, 9, 1, 1, // Misc
            3, 3, // Balls
            5, 5, // TMs
            6, // Vitamins
            7, // Balls
            5, // TMs
            5, // TMs
            8, // Battle
            3, // Balls
        ];

    private readonly int offset;
    private int dataoffset;

    private readonly string[] locations = Main.Config.ORAS
        ?
        [
            "No Gym Badges [After Pokédex]", "1 Gym Badge", "2 Gym Badges", "3 Gym Badges", "4 Gym Badges", "5 Gym Badges", "6 Gym Badges", "7 Gym Badges", "8 Gym Badges",
            "No Gym Badges [Before Pokédex]",
            "Slateport Market [Incenses]", "Slateport Market [Vitamins]", "Slateport Market [TMs]", "Rustboro City [Poké Balls]", "Slateport City [X Items]",
            "Mauville City [TMs]", "Verdanturf Town [Poké Balls]", "Fallarbor Town [Poké Balls]", "Lavaridge Town [Herbs]",
            "Lilycove Dept Store, 2F Left [Run Away Items]", "Lilycove Dept Store, 3F Left [Vitamins]", "Lilycove Dept Store, 3F Right [X Items]",
            "Lilycove Dept Store, 4F Left [Offensive TMs]", "Lilycove Dept Store, 4F Right [Defensive TMs]",
        ]
        :
        [
            "No Gym Badges", "1 Gym Badge", "2 Gym Badges", "3 Gym Badges", "4 Gym Badges", "5 Gym Badges", "6 Gym Badges", "7 Gym Badges", "8 Gym Badges",
            "Unused",
            "Lumiose City [Herboriste]", "Lumiose City [Poké Ball Boutique]", "Lumiose City [Stone Emporium]", "Coumarine City [Incenses]", "Aquacorde Town [Poké Ball]", "Aquacorde Town [Potion]",
            "Lumiose City North Boulevard [Poké Balls]", "Cyllage City [Poké Balls]",
            "Shalour City [TMs]", "Lumiose City South Boulevard [TMs]",
            "Laverre City [Vitamins]",
            "Snowbelle City [Poké Balls]",
            "Kiloude City [TMs]",
            "Anistar City [TMs]",
            "Santalune City [X Items]",
            "Coumarine City [Poké Balls]",
        ];

    private void GetDataOffset(int index)
    {
        dataoffset = offset; // reset
        for (int i = 0; i < index; i++)
            dataoffset += 2 * entries[i];
    }

    private void SetupDGV()
    {
        var dgvIndex = new DataGridViewTextBoxColumn();
        {
            dgvIndex.HeaderText = "Index";
            dgvIndex.DisplayIndex = 0;
            dgvIndex.Width = 45;
            dgvIndex.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }
        var dgvItem = new DataGridViewComboBoxColumn();
        {
            dgvItem.HeaderText = "Item";
            dgvItem.DisplayIndex = 1;
            dgvItem.Items.AddRange(itemlist); // add only the Names

            dgvItem.Width = 135;
            dgvItem.FlatStyle = FlatStyle.Flat;
        }
        dgv.Columns.Add(dgvIndex);
        dgv.Columns.Add(dgvItem);
    }

    private int entry = -1;

    private void ChangeIndex(object sender, EventArgs e)
    {
        if (entry > -1) SetList();
        entry = CB_Location.SelectedIndex;
        GetList();
    }

    private void GetList()
    {
        dgv.Rows.Clear();
        int count = entries[entry];
        dgv.Rows.Add(count);
        GetDataOffset(entry);
        for (int i = 0; i < count; i++)
        {
            dgv.Rows[i].Cells[0].Value = i.ToString();
            dgv.Rows[i].Cells[1].Value = itemlist[BitConverter.ToUInt16(data, dataoffset + (2 * i))];
        }
    }

    private void SetList()
    {
        int count = dgv.Rows.Count;
        for (int i = 0; i < count; i++)
            Array.Copy(BitConverter.GetBytes((ushort)Array.IndexOf(itemlist, dgv.Rows[i].Cells[1].Value)), 0, data, dataoffset + (2 * i), 2);
    }

    private void B_Save_Click(object sender, EventArgs e)
    {
        if (entry > -1)
            SetList();

        if (setRareCandyPriceOnSave)
            SetItemPrice(GetRareCandyItemID(), RareCandyPrice);

        File.WriteAllBytes(codebin, data);

        if (setRareCandyPriceOnSave)
            RandomizationSessionState.MarkAction("marts.add-rare-candies", ("price", RareCandyPrice.ToString()));
        if (randomizeMartsOnSave)
        {
            RandomizationSessionState.MarkAction(
                "marts.randomize",
                ("specialOnly", randomizeMartsSpecialOnly.ToString()),
                ("keepXItems", CHK_XItems.Checked.ToString()));
        }

        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        RandSettings.SetFormSettings(this, Controls);
        base.OnFormClosing(e);
    }

    private void B_Cancel_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void B_Randomize_Click(object sender, EventArgs e)
    {
        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNoCancel, "Randomize mart inventories?"))
            return;

        int[] validItems = Randomizer.GetRandomItemList();

        int ctr = 0;
        Util.Shuffle(validItems);

        bool specialOnly = DialogResult.Yes == WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Randomize only special marts?", "Will leave regular necessities intact.");
        int start = specialOnly ? 10 : 0;
        for (int i = start; i < CB_Location.Items.Count; i++)
        {
            CB_Location.SelectedIndex = i;
            for (int r = 0; r < dgv.Rows.Count; r++)
            {
                int currentItem = Array.IndexOf(itemlist, dgv.Rows[r].Cells[1].Value);
                if (CHK_XItems.Checked && MartEditor7.XItems.Contains(currentItem))
                    continue;
                if (MartEditor7.BannedItems.Contains(currentItem))
                    continue;
                dgv.Rows[r].Cells[1].Value = itemlist[validItems[ctr++]];
                if (ctr <= validItems.Length) continue;
                Util.Shuffle(validItems); ctr = 0;
            }
        }
        randomizeMartsOnSave = true;
        randomizeMartsSpecialOnly = specialOnly;
        WinFormsUtil.Alert("Randomized!");
    }
}