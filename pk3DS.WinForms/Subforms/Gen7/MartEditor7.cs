using pk3DS.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using pk3DS.Core.Structures;

namespace pk3DS.WinForms;

public partial class MartEditor7 : Form
{
    private readonly string CROPath = Path.Combine(Main.RomFSPath, "Shop.cro");

    public MartEditor7()
    {
        if (!File.Exists(CROPath))
        {
            WinFormsUtil.Error("CRO does not exist! Closing.", CROPath);
            Close();
        }
        InitializeComponent();
        AddRareCandyButton();
        AddEVItemsButton();

        data = File.ReadAllBytes(CROPath);
        offset = Util.IndexOfBytes(data, Signature, 0x5000, 0) + Signature.Length;
        offsetBP = Util.IndexOfBytes(data, BPSignature, 0x5000, 0) + BPSignature.Length;

        itemlist[0] = "";
        SetupDGV();
        CB_Location.Items.AddRange(locations);
        CB_LocationBP.Items.AddRange(locationsBP);
        CB_Location.SelectedIndex = 0;
        CB_LocationBP.SelectedIndex = 0;
        RandSettings.GetFormSettings(this, Controls);

    }
    private void AddRareCandyButton()
    {
        const int gap = 8;
        const int buttonWidth = 150;

        B_AddRareCandies = new Button
        {
            Location = new System.Drawing.Point(B_Randomize.Right + gap, B_Randomize.Top),
            Name = "B_AddRareCandies",
            Size = new System.Drawing.Size(buttonWidth, 23),
            TabIndex = 305,
            Text = "Add Rare Candies",
            UseVisualStyleBackColor = true,
        };

        B_AddRareCandies.Click += B_AddRareCandies_Click;
        Controls.Add(B_AddRareCandies);

        // Mover el checkbox a una segunda línea para evitar solapamientos.
        CHK_XItems.AutoSize = true;
        CHK_XItems.Location = new System.Drawing.Point(
            B_Randomize.Left,
            B_Randomize.Bottom + gap
        );

        // Si algún control queda muy abajo, aumentar un poco la ventana.
        int requiredHeight = CHK_XItems.Bottom + 12;

        if (requiredHeight > ClientSize.Height)
            ClientSize = new System.Drawing.Size(ClientSize.Width, requiredHeight);
    }
    private void AddEVItemsButton()
    {
        const int gap = 8;

        B_AddEVItems = new Button
        {
            Location = new System.Drawing.Point(B_AddRareCandies.Left, B_AddRareCandies.Bottom + gap),
            Name = "B_AddEVItems",
            Size = B_AddRareCandies.Size,
            TabIndex = B_AddRareCandies.TabIndex + 1,
            Text = "Add EV Items",
            UseVisualStyleBackColor = true,
        };

        B_AddEVItems.Click += B_AddEVItems_Click;
        Controls.Add(B_AddEVItems);
        B_AddEVItems.BringToFront();

        int requiredHeight = Math.Max(CHK_XItems.Bottom, B_AddEVItems.Bottom) + 12;
        if (requiredHeight > ClientSize.Height)
            ClientSize = new System.Drawing.Size(ClientSize.Width, requiredHeight);
    }

    private void B_AddRareCandies_Click(object sender, EventArgs e)
    {
        if (DialogResult.Yes != WinFormsUtil.Prompt(
            MessageBoxButtons.YesNo,
            "Add Rare Candies to all regular marts?",
            "This will replace the last slot of each regular mart with Rare Candy. Special marts and BP shops will not be changed. Rare Candy price will be set to 10 when you click Save."))
        {
            return;
        }

        if (entry > -1)
            SetList();

        int rareCandy = GetRareCandyItemID();

        for (int i = 0; i < RegularMartCount; i++)
        {
            GetDataOffset(i);

            int lastSlot = entries[i] - 1;
            int writeOffset = dataoffset + (2 * lastSlot);

            Array.Copy(BitConverter.GetBytes((ushort)rareCandy), 0, data, writeOffset, 2);
        }

        setRareCandyPriceOnSave = true;

        if (entry > -1)
            GetList();

        WinFormsUtil.Alert(
            "Rare Candies added!",
            "Click Save to write Shop.cro and set Rare Candy price to 10.");
    }
    private void B_AddEVItems_Click(object sender, EventArgs e)
    {
        if (DialogResult.Yes != WinFormsUtil.Prompt(
            MessageBoxButtons.YesNo,
            "Add EV/training items to regular marts?",
            "This keeps each shop at its original size and replaces ONLY healing items. " +
            "The six EV Wings are available immediately, Heart Scale replaces Super Potion, " +
            "Rare Candy replaces Revive, and Mega Ring is guaranteed from the 5 Trials mart onward."))
        {
            return;
        }

        if (entry > -1)
            SetList();

        string[] requiredItems =
        [
            "Health Wing|Pluma Vigor",
            "Muscle Wing|Pluma Músculo|Pluma Musculo",
            "Resist Wing|Pluma Aguante",
            "Genius Wing|Pluma Intelecto",
            "Clever Wing|Pluma Mente",
            "Swift Wing|Pluma Ímpetu|Pluma Impetu",
            "Heart Scale|Escama Corazón|Escama Corazon",
            "Rare Candy|Caramelo Raro",
            "Mega Ring|Megaaro|Mega Aro|Mega-Aro",
        ];

        var missing = new List<string>();
        foreach (string group in requiredItems)
        {
            string[] aliases = group.Split('|');
            if (FindItemID(aliases) <= 0)
                missing.Add(aliases[0]);
        }

        if (missing.Count != 0)
        {
            WinFormsUtil.Error(
                "Could not resolve the EV shop items from this ROM's item table.",
                "Missing: " + string.Join(", ", missing));
            return;
        }

        int[] megaRingSlots = new int[RegularMartCount];
        Array.Fill(megaRingSlots, -1);
        for (int mart = MegaRingTrial; mart < RegularMartCount; mart++)
        {
            GetDataOffset(mart);
            megaRingSlots[mart] = FindMegaRingTargetSlot(dataoffset, entries[mart]);
            if (megaRingSlots[mart] >= 0)
                continue;

            WinFormsUtil.Error(
                $"Could not reserve a healing-item slot for Mega Ring in '{locations[mart]}'.",
                "Mega Ring must be available starting at 5 Trials. No compatible healing slot was found.");
            return;
        }

        int changed = 0;
        for (int mart = 0; mart < RegularMartCount; mart++)
        {
            GetDataOffset(mart);
            for (int slot = 0; slot < entries[mart]; slot++)
            {
                int writeOffset = dataoffset + (2 * slot);
                int current = BitConverter.ToUInt16(data, writeOffset);
                int replacement = slot == megaRingSlots[mart]
                    ? GetMegaRingItemID()
                    : GetEVItemReplacement(current, mart);
                if (replacement <= 0 || replacement == current)
                    continue;

                Array.Copy(BitConverter.GetBytes((ushort)replacement), 0, data, writeOffset, 2);
                changed++;
            }
        }

        setEVItemsOnSave = changed > 0;
        if (entry > -1)
            GetList();

        WinFormsUtil.Alert(
            "EV/training items added!",
            $"{changed} healing-item slots were replaced. Click Save to write Shop.cro.");
    }

    private int GetEVItemReplacement(int itemID, int trialCount)
    {
        int potion = FindItemID("Potion", "Poción", "Pocion");
        int antidote = FindItemID("Antidote", "Antídoto", "Antidoto");
        int paralyzeHeal = FindItemID("Paralyze Heal", "Antiparalizador");
        int awakening = FindItemID("Awakening", "Despertar");
        int burnHeal = FindItemID("Burn Heal", "Antiquemar");
        int iceHeal = FindItemID("Ice Heal", "Antihielo");
        int superPotion = FindItemID("Super Potion", "Superpoción", "Superpocion");
        int revive = FindItemID("Revive", "Revivir");

        // Repair shops written by the first Add EV Items patch. Those numeric IDs
        // belonged to another item table and show up in Gen 7 as Data Cards/other items.
        if (itemID == 517)
            return FindItemID("Health Wing", "Pluma Vigor");
        if (itemID == 518)
            return FindItemID("Muscle Wing", "Pluma Músculo", "Pluma Musculo");
        if (itemID == 519)
            return FindItemID("Resist Wing", "Pluma Aguante");
        if (itemID == 520)
            return FindItemID("Genius Wing", "Pluma Intelecto");
        if (itemID == 521)
            return FindItemID("Clever Wing", "Pluma Mente");
        if (itemID == 522)
            return FindItemID("Swift Wing", "Pluma Ímpetu", "Pluma Impetu");
        if (itemID == 111)
        {
            if (trialCount >= 3)
                return FindItemID("Heart Scale", "Escama Corazón", "Escama Corazon");

            return superPotion;
        }

        if (itemID == potion)
            return FindItemID("Health Wing", "Pluma Vigor");
        if (itemID == antidote)
            return FindItemID("Muscle Wing", "Pluma Músculo", "Pluma Musculo");
        if (itemID == paralyzeHeal)
            return FindItemID("Resist Wing", "Pluma Aguante");
        if (itemID == awakening)
            return FindItemID("Genius Wing", "Pluma Intelecto");
        if (itemID == burnHeal)
            return FindItemID("Clever Wing", "Pluma Mente");
        if (itemID == iceHeal)
            return FindItemID("Swift Wing", "Pluma Ímpetu", "Pluma Impetu");
        if (itemID == superPotion && trialCount >= 3)
            return FindItemID("Heart Scale", "Escama Corazón", "Escama Corazon");
        if (itemID == revive)
            return GetRareCandyItemID();

        return 0;
    }

    private int FindMegaRingTargetSlot(int offset, int count)
    {
        int megaRing = GetMegaRingItemID();
        int[] preferredHealingItems =
        [
            megaRing,
            696, // Legacy value written by the first EV-items patch; repair it in-place.
            FindItemID("Hyper Potion", "Hiperpoción", "Hiperpocion"),
            FindItemID("Full Heal", "Cura Total"),
            FindItemID("Max Potion", "Poción Máxima", "Pocion Maxima"),
            FindItemID("Full Restore", "Restaurar Todo", "Restaurar todo"),
        ];

        foreach (int candidate in preferredHealingItems)
        {
            if (candidate <= 0)
                continue;

            for (int slot = 0; slot < count; slot++)
            {
                int current = BitConverter.ToUInt16(data, offset + (2 * slot));
                if (current == candidate)
                    return slot;
            }
        }

        return -1;
    }

    private int FindItemID(params string[] names)
    {
        foreach (string name in names)
        {
            int item = Array.FindIndex(itemlist, z =>
                string.Equals(z, name, StringComparison.OrdinalIgnoreCase));
            if (item > 0)
                return item;
        }

        return -1;
    }

    private int GetMegaRingItemID() =>
        FindItemID("Mega Ring", "Megaaro", "Mega Aro", "Mega-Aro");

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
            WinFormsUtil.Alert("Could not set item price.", $"Invalid item ID: {itemID}");
            return;
        }

        var item = new Item(files[itemID]);
        item.BuyPrice = price;

        files[itemID] = item.Write();

        g.Files = files;
        g.Save();
    }
    private readonly string[] itemlist = Main.Config.GetText(TextName.ItemNames);
    private readonly byte[] data;
    private const int RareCandyItemID = 50;
    private const int RareCandyPrice = 10;
    private const int RegularMartCount = 8;

    // Resolve item IDs from the ROM's own item-name table. Hardcoded IDs from
    // other generations/versions can point to unrelated entries (for example Data Cards).
    private const int MegaRingPrice = 10;
    private const int MegaRingTrial = 5;

    private bool setRareCandyPriceOnSave;
    private bool setEVItemsOnSave;
    private bool randomizeMartsOnSave;
    private bool randomizeBPMartsOnSave;
    private bool randomizeMartsSpecialOnly;
    private Button B_AddRareCandies;
    private Button B_AddEVItems;

    #region Tables
    private readonly byte[] Signature = // Leadup to the Shop Data, the shop arrays are the 3rd data array in the rodata section.
    [
        0x2D, 0x00, 0x00, 0x00, 0x3B, 0x00, 0x00, 0x00, 0x2F, 0x00, 0x00, 0x00, 0x3D, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00,
        0x10, 0x00, 0x00, 0x00, 0x0E, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00,
    ];

    private readonly byte[] BPSignature = // 2 arrays after the regular shops, the BP shops start. Skip over the second one to get BP offset.
    [
        0x09, 0x0B, 0x0D, 0x0F, 0x11, 0x13, 0x14, 0x15, 0x09, 0x04, 0x08, 0x0C, 0x05, 0x04, 0x0B, 0x03,
        0x0A, 0x06, 0x0A, 0x06, 0x04, 0x05, 0x07, 0x01,
    ];

    private readonly byte[] entries =
    [
        9, 11, 13, 15, 17, 19, 20, 21, // Regular Mart
        9, // KoniKoni Incense
        4, // KoniKoni Herb
        8, // Hau'oli Battle
        12, // Route 2
        5, // Heahea
        4, // Royal Avenue
        11, // Route 8
        3, // Paniola
        10, // Malie TMs
        6, // Mount Hokulani
        10, // Seafolk TM
        6, // KoniKoni TM
        4, // KoniKoni Jewelry
        5, // Thrifty 1
        7, // Thrifty 2
        1, // Thrifty 3 (Souvenir)
    ];

    private readonly string[] locations =
    [
        "No Trials", "1 Trial", "2 Trials", "3 Trials", "4 Trials", "5 Trials", "6 Trials", "7 Trials",
        "Konikoni City [Incenses]",
        "Konikoni City [Herbs]",
        "Hau'oli City [X Items]",
        "Route 2 [Misc]",
        "Heahea City [TMs]",
        "Royal Avenue [TMs]",
        "Route 8 [Misc]",
        "Paniola Town [Poké Balls]",
        "Malie City [TMs]",
        "Mount Hokulani [Vitamins]",
        "Seafolk Village [TMs]",
        "Konikoni City [TMs]",
        "Konikoni City [Stones]",
        "Thrifty Megamart, Left [Poké Balls]",
        "Thrifty Megamart, Middle [Misc]",
        "Thrifty Megamart, Right [Strange Souvenir]",
    ];

    private readonly int[] entriesBP =
    [
        8, // Royal 1 (Abil Capsule)
        7, // Royal 2
        18, // Royal 3
        12, // Tree 1
        21, // Tree 2
        16, // Tree 3
    ];

    private readonly string[] locationsBP =
    [
        "Battle Royal Dome [Medicine]",
        "Battle Royal Dome [EV Training]",
        "Battle Royal Dome [Held Items]",
        "Battle Tree [Trade Evolution Items]",
        "Battle Tree [Held Items]",
        "Battle Tree [Mega Stones]",
    ];
    #endregion

    private void B_Save_Click(object sender, EventArgs e)
    {
        if (entry > -1) SetList();

        if (entryBP > -1) SetListBP();

        if (setRareCandyPriceOnSave || setEVItemsOnSave)
            SetItemPrice(GetRareCandyItemID(), RareCandyPrice);
        if (setEVItemsOnSave)
        {
            int megaRing = GetMegaRingItemID();
            if (megaRing > 0)
                SetItemPrice(megaRing, MegaRingPrice);
        }

        File.WriteAllBytes(CROPath, data);

        if (setRareCandyPriceOnSave)
            RandomizationSessionState.MarkAction("marts.add-rare-candies", ("price", RareCandyPrice.ToString()));
        if (setEVItemsOnSave)
        {
            RandomizationSessionState.MarkAction(
                "marts.add-ev-items",
                ("mode", "replace-healing-only"),
                ("megaRingFromTrial", MegaRingTrial.ToString()),
                ("megaRingPrice", MegaRingPrice.ToString()),
                ("rareCandyPrice", RareCandyPrice.ToString()));
        }
        if (randomizeMartsOnSave)
        {
            RandomizationSessionState.MarkAction(
                "marts.randomize",
                ("specialOnly", randomizeMartsSpecialOnly.ToString()),
                ("keepXItems", CHK_XItems.Checked.ToString()));
        }
        if (randomizeBPMartsOnSave)
            RandomizationSessionState.MarkAction("marts.randomize-bp");

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

    private readonly int offset;
    private int dataoffset;

    private void GetDataOffset(int index)
    {
        dataoffset = offset; // reset
        for (int i = 0; i < index; i++)
            dataoffset += 2 * entries[i];
    }

    private void SetupDGV()
    {
        dgvItem.Items.AddRange(itemlist); // add only the Names
        dgvItemBP.Items.AddRange(itemlist); // add only the Names
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

    /// <summary>
    /// Just TMs & HMs; don't want these to be changed; if changed, they are not available elsewhere ingame.
    /// </summary>
    internal static readonly HashSet<int> BannedItems =
    [
        328, 329, 330, 331, 332, 333, 334, 335, 336, 337, 338, 339, 340, 341, 342, 343, 344, 345, 346, 347, 348,
        349, 350, 351, 352, 353, 354, 355, 356, 357, 358, 359, 360, 361, 362, 363, 364, 365, 366, 367, 368, 369,
        370, 371, 372, 373, 374, 375, 376, 377, 378, 379, 380, 381, 382, 383, 384, 385, 386, 387, 388, 389, 390,
        391, 392, 393, 394, 395, 396, 397, 398, 399, 400, 401, 402, 403, 404, 405, 406, 407, 408, 409, 410, 411,
        412, 413, 414, 415, 416, 417, 418, 419, 420, 421, 422, 423, 424, 425, 426, 427, 618, 619, 620, 690, 691,
        692, 693, 694, 701, 737,
    ];

    /// <summary>
    /// All X Items usable in Generations 6 and 7. Speedrunners utilize these Items a lot, so make sure they are still available.
    /// </summary>
    internal static readonly HashSet<int> XItems = [055, 056, 057, 058, 059, 060, 061, 062];

    private void B_Randomize_Click(object sender, EventArgs e)
    {
        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNoCancel, "Randomize mart inventories?"))
            return;

        int[] validItems = Randomizer.GetRandomItemList();

        int ctr = 0;
        Util.Shuffle(validItems);

        bool specialOnly = DialogResult.Yes == WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Randomize only special marts?", "Will leave regular necessities intact.");
        int start = specialOnly ? 8 : 0;
        for (int i = start; i < CB_Location.Items.Count; i++)
        {
            CB_Location.SelectedIndex = i;
            for (int r = 0; r < dgv.Rows.Count; r++)
            {
                int currentItem = Array.IndexOf(itemlist, dgv.Rows[r].Cells[1].Value);
                if (CHK_XItems.Checked && XItems.Contains(currentItem))
                    continue;
                if (BannedItems.Contains(currentItem))
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

    private void GetDataOffsetBP(int index)
    {
        dataoffsetBP = offsetBP; // reset
        for (int i = 0; i < index; i++)
            dataoffsetBP += 4 * entriesBP[i];
    }

    private readonly int offsetBP;
    private int dataoffsetBP;
    private int entryBP = -1;

    private void ChangeIndexBP(object sender, EventArgs e)
    {
        if (entryBP > -1) SetListBP();
        entryBP = CB_LocationBP.SelectedIndex;
        GetListBP();
    }

    private void GetListBP()
    {
        dgvbp.Rows.Clear();
        int count = entriesBP[entryBP];
        dgvbp.Rows.Add(count);
        GetDataOffsetBP(entryBP);
        for (int i = 0; i < count; i++)
        {
            dgvbp.Rows[i].Cells[0].Value = i.ToString();
            dgvbp.Rows[i].Cells[1].Value = itemlist[BitConverter.ToUInt16(data, dataoffsetBP + (4 * i))];
            dgvbp.Rows[i].Cells[2].Value = BitConverter.ToUInt16(data, dataoffsetBP + (4 * i) + 2).ToString();
        }
    }

    private void SetListBP()
    {
        int count = dgvbp.Rows.Count;
        for (int i = 0; i < count; i++)
        {
            int item = Array.IndexOf(itemlist, dgvbp.Rows[i].Cells[1].Value);
            Array.Copy(BitConverter.GetBytes((ushort)item), 0, data, dataoffsetBP + (4 * i), 2);
            string p = dgvbp.Rows[i].Cells[2].Value.ToString();
            if (int.TryParse(p, out var price))
                Array.Copy(BitConverter.GetBytes((ushort)price), 0, data, dataoffsetBP + (4 * i) + 2, 2);
        }
    }

    private void B_RandomizeBP_Click(object sender, EventArgs e)
    {
        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNoCancel, "Randomize BP inventories?"))
            return;

        int[] validItems = Randomizer.GetRandomItemList();

        int ctr = 0;
        Util.Shuffle(validItems);

        for (int i = 0; i < CB_LocationBP.Items.Count; i++)
        {
            CB_LocationBP.SelectedIndex = i;
            for (int r = 0; r < dgvbp.Rows.Count; r++)
            {
                dgvbp.Rows[r].Cells[1].Value = itemlist[validItems[ctr++]];
                if (ctr <= validItems.Length) continue;
                Util.Shuffle(validItems); ctr = 0;
            }
        }
        randomizeBPMartsOnSave = true;
        WinFormsUtil.Alert("Randomized!");
    }
}