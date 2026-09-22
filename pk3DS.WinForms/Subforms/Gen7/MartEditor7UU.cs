using pk3DS.Core;
using pk3DS.Core.Modding.Research;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pk3DS.Core.Structures;

namespace pk3DS.WinForms;

public partial class MartEditor7UU : Form
{
    private readonly string CROPath = Path.Combine(Main.RomFSPath, "Shop.cro");

    public MartEditor7UU()
    {
        if (!File.Exists(CROPath))
        {
            WinFormsUtil.Error("CRO does not exist! Closing.", CROPath);
            Close();
        }
        InitializeComponent();
        AddRareCandyButton();
        AddEVItemsButton();
        AddFreeMegaStonesButton();
        AddExpandedMartButtons();
        LayoutCustomMartButtons();

        data = File.ReadAllBytes(CROPath);

        if (!Gen7ShopCroLayout.TryRead(
                data,
                out Gen7ShopCroLayoutSnapshot shopLayout,
                out string layoutError))
        {
            ofs_Item = 0;
            ofs_BPItem = 0;
            len_Items = [];
            len_BPItem = [];
            regularMarts = [];
            regularMartSlotTokens = [];

            WinFormsUtil.Error(
                "Could not resolve the active USUM Shop.cro layout. Closing.",
                layoutError);

            Close();
            return;
        }

        ofs_Item = checked((int)shopLayout.RegularMarts.DataStart);
        ofs_BPItem = checked((int)shopLayout.BPItems.DataStart);

        len_Items = shopLayout.RegularMarts.Inventories
            .Select(z => checked((byte)z.Length))
            .ToArray();

        regularMarts = shopLayout.RegularMarts.Inventories
            .Select(z => z.ToList())
            .ToArray();

        regularMartSlotTokens = regularMarts
            .Select(z => Enumerable.Repeat(0, z.Count).ToList())
            .ToArray();

        len_BPItem = shopLayout.BPItems.Counts.ToArray();

        itemlist[0] = "";
        SetupDGV();
        CB_Location.Items.AddRange(locations);
        CB_LocationBPItem.Items.AddRange(locationsBP);
        CB_Location.SelectedIndex =
            CB_LocationBPItem.SelectedIndex = 0;
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
            Text = "Add RCandies",
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

    private void AddFreeMegaStonesButton()
    {
        const int gap = 8;

        B_FreeMegaStones = new Button
        {
            Location = new System.Drawing.Point(B_AddEVItems.Left, B_AddEVItems.Bottom + gap),
            Name = "B_FreeMegaStones",
            Size = B_AddEVItems.Size,
            TabIndex = B_AddEVItems.TabIndex + 1,
            Text = "Free Mega Stones",
            UseVisualStyleBackColor = true,
        };

        B_FreeMegaStones.Click += B_FreeMegaStones_Click;
        Controls.Add(B_FreeMegaStones);
        B_FreeMegaStones.BringToFront();

        int requiredHeight = Math.Max(CHK_XItems.Bottom, B_FreeMegaStones.Bottom) + 12;
        if (requiredHeight > ClientSize.Height)
            ClientSize = new System.Drawing.Size(ClientSize.Width, requiredHeight);
    }

    private void AddExpandedMartButtons()
    {
        B_AddItem = new Button
        {
            Name = "B_AddItem",
            Text = "Add Item",
            UseVisualStyleBackColor = true,
        };

        B_DeleteItem = new Button
        {
            Name = "B_DeleteItem",
            Text = "Delete Item",
            UseVisualStyleBackColor = true,
        };

        B_AddItem.Click += B_AddItem_Click;
        B_DeleteItem.Click += B_DeleteItem_Click;
        tabControl1.SelectedIndexChanged += TabControl1_SelectedIndexChanged;

        Controls.Add(B_AddItem);
        Controls.Add(B_DeleteItem);
    }

    private void TabControl1_SelectedIndexChanged(object sender, EventArgs e) =>
        UpdateExpandedMartButtons();

    private void UpdateExpandedMartButtons()
    {
        bool regularMartTab =
            tabControl1.SelectedIndex == 0;

        B_AddItem.Enabled =
            regularMartTab;

        B_DeleteItem.Enabled =
            regularMartTab &&
            entryItem >= 0 &&
            entryItem < regularMarts.Length &&
            regularMarts[entryItem].Count > 1;
    }

    private bool TryAppendRegularMartItem(
        int martIndex,
        ushort itemId,
        out int token,
        out string error)
    {
        token = 0;
        error = string.Empty;

        if (martIndex < 0 ||
            martIndex >= regularMarts.Length)
        {
            error =
                $"Regular mart index {martIndex} is outside the loaded mart table.";
            return false;
        }

        List<ushort> mart =
            regularMarts[martIndex];

        if (mart.Count >=
            Gen7ExpandedMartTable.MaximumSlotsPerMart)
        {
            error =
                $"'{locations[martIndex]}' already has {mart.Count} slots. " +
                $"USUM supports at most {Gen7ExpandedMartTable.MaximumSlotsPerMart} slots per regular mart.";
            return false;
        }

        int insertIndex =
            mart.Count;

        token =
            nextExpandedMartToken++;

        mart.Add(
            itemId);

        regularMartSlotTokens[martIndex].Add(
            token);

        expandedMartOperations.Add(
            new PendingExpandedMartOperation(
                new ExpandedMartOperation(
                    ExpandedMartOperationKind.Add,
                    martIndex,
                    insertIndex,
                    itemId),
                token));

        return true;
    }

    private void B_AddItem_Click(object sender, EventArgs e)
    {
        if (tabControl1.SelectedIndex != 0 ||
            entryItem < 0 ||
            entryItem >= regularMarts.Length)
        {
            return;
        }

        SetListItem();

        List<ushort> mart =
            regularMarts[entryItem];

        int sourceRow =
            dgv.CurrentCell?.RowIndex ??
            (mart.Count - 1);

        sourceRow =
            Math.Clamp(
                sourceRow,
                0,
                mart.Count - 1);

        // Append a duplicate of the selected item, then let the user choose a different
        // item from the normal combo box if desired.
        ushort insertedItem =
            mart[sourceRow];

        if (!TryAppendRegularMartItem(
                entryItem,
                insertedItem,
                out _,
                out string appendError))
        {
            WinFormsUtil.Error(
                "This mart cannot be expanded any further.",
                appendError);
            return;
        }

        GetListItem();

        int newRow =
            dgv.Rows.Count - 1;

        if (newRow >= 0)
            dgv.CurrentCell = dgv.Rows[newRow].Cells[1];

        UpdateExpandedMartButtons();
    }

    private void B_DeleteItem_Click(object sender, EventArgs e)
    {
        if (tabControl1.SelectedIndex != 0 ||
            entryItem < 0 ||
            entryItem >= regularMarts.Length)
        {
            return;
        }

        SetListItem();

        List<ushort> mart =
            regularMarts[entryItem];

        if (mart.Count <= 1)
        {
            WinFormsUtil.Error(
                "A regular mart cannot be empty.",
                "At least one item slot must remain.");
            return;
        }

        int row =
            dgv.CurrentCell?.RowIndex ??
            -1;

        if (row < 0 ||
            row >= mart.Count)
        {
            WinFormsUtil.Alert(
                "Select an item row to delete.");
            return;
        }

        expandedMartOperations.Add(
            new PendingExpandedMartOperation(
                new ExpandedMartOperation(
                    ExpandedMartOperationKind.Delete,
                    entryItem,
                    row),
                token: 0));

        mart.RemoveAt(
            row);

        regularMartSlotTokens[entryItem].RemoveAt(
            row);

        GetListItem();

        int nextRow =
            Math.Min(
                row,
                dgv.Rows.Count - 1);

        if (nextRow >= 0)
            dgv.CurrentCell = dgv.Rows[nextRow].Cells[1];

        UpdateExpandedMartButtons();
    }
    private void LayoutCustomMartButtons()
    {
        const int gap = 8;
        const int margin = 12;
        const int controlsGap = 6;

        // Row 1: randomization / shop actions.
        B_AddRareCandies.Size = new System.Drawing.Size(108, B_Randomize.Height);
        B_AddEVItems.Size = new System.Drawing.Size(95, B_Randomize.Height);
        B_FreeMegaStones.Size = new System.Drawing.Size(118, B_Randomize.Height);

        int actionTop = tabControl1.Bottom + controlsGap;
        B_Randomize.Location = new System.Drawing.Point(margin, actionTop);
        B_AddRareCandies.Location = new System.Drawing.Point(B_Randomize.Right + gap, actionTop);
        B_AddEVItems.Location = new System.Drawing.Point(B_AddRareCandies.Right + gap, actionTop);
        B_FreeMegaStones.Location = new System.Drawing.Point(B_AddEVItems.Right + gap, actionTop);

        // Row 2: structural regular-mart actions, option, then Cancel/Save.
        B_AddItem.Size = new System.Drawing.Size(82, B_Randomize.Height);
        B_DeleteItem.Size = new System.Drawing.Size(90, B_Randomize.Height);

        int secondRowTop = B_Randomize.Bottom + gap;
        CHK_XItems.AutoSize = true;

        int leftControlsWidth =
            B_AddItem.Width +
            gap +
            B_DeleteItem.Width +
            gap +
            CHK_XItems.Width;

        int requiredWidth = Math.Max(
            B_FreeMegaStones.Right + margin,
            margin +
            leftControlsWidth +
            gap +
            B_Cancel.Width +
            gap +
            B_Save.Width +
            margin);

        int requiredHeight =
            secondRowTop +
            Math.Max(B_Save.Height, CHK_XItems.Height) +
            margin;

        ClientSize = new System.Drawing.Size(
            Math.Max(ClientSize.Width, requiredWidth),
            Math.Max(ClientSize.Height, requiredHeight));

        // Keep the tabs above the two action rows. They are anchored to Bottom,
        // so increasing ClientSize would otherwise stretch them over the buttons.
        tabControl1.Size = new System.Drawing.Size(
            ClientSize.Width - tabControl1.Left - margin,
            actionTop - tabControl1.Top - controlsGap);

        B_Randomize.Location = new System.Drawing.Point(margin, actionTop);
        B_AddRareCandies.Location = new System.Drawing.Point(B_Randomize.Right + gap, actionTop);
        B_AddEVItems.Location = new System.Drawing.Point(B_AddRareCandies.Right + gap, actionTop);
        B_FreeMegaStones.Location = new System.Drawing.Point(B_AddEVItems.Right + gap, actionTop);

        B_AddItem.Location = new System.Drawing.Point(margin, secondRowTop);
        B_DeleteItem.Location = new System.Drawing.Point(B_AddItem.Right + gap, secondRowTop);

        CHK_XItems.Location = new System.Drawing.Point(
            B_DeleteItem.Right + gap,
            secondRowTop + ((B_Save.Height - CHK_XItems.Height) / 2));

        B_Save.Location = new System.Drawing.Point(
            ClientSize.Width - margin - B_Save.Width,
            secondRowTop);

        B_Cancel.Location = new System.Drawing.Point(
            B_Save.Left - gap - B_Cancel.Width,
            secondRowTop);

        UpdateExpandedMartButtons();

        B_Randomize.BringToFront();
        B_AddRareCandies.BringToFront();
        B_AddEVItems.BringToFront();
        B_FreeMegaStones.BringToFront();
        B_AddItem.BringToFront();
        B_DeleteItem.BringToFront();
        CHK_XItems.BringToFront();
        B_Cancel.BringToFront();
        B_Save.BringToFront();
    }

    private bool HasTrackedRareCandySlot(int martIndex)
    {
        if (!rareCandySlotTokens.TryGetValue(
                martIndex,
                out int token))
        {
            return false;
        }

        return martIndex >= 0 &&
               martIndex < regularMartSlotTokens.Length &&
               regularMartSlotTokens[martIndex].Contains(
                   token);
    }

    private string GetRareCandyTemplateSlots()
    {
        var slots =
            new List<string>();

        foreach (var pair in
                 rareCandySlotTokens.OrderBy(z => z.Key))
        {
            int martIndex =
                pair.Key;

            if (martIndex < 0 ||
                martIndex >= regularMartSlotTokens.Length)
            {
                continue;
            }

            int slotIndex =
                regularMartSlotTokens[martIndex]
                    .IndexOf(
                        pair.Value);

            if (slotIndex < 0 ||
                slotIndex >= regularMarts[martIndex].Count)
            {
                continue;
            }

            slots.Add(
                $"{martIndex}:{slotIndex}");
        }

        return string.Join(
            "|",
            slots);
    }

    private void B_AddRareCandies_Click(object sender, EventArgs e)
    {
        if (DialogResult.Yes != WinFormsUtil.Prompt(
            MessageBoxButtons.YesNo,
            "Add Rare Candies to all progression marts?",
            "This will ADD one new Rare Candy slot to each progression mart that does not already sell Rare Candy, from No Trials through 7 Trials. " +
            "Rare Candy price will be set to 10 when you click Save."))
        {
            return;
        }

        if (entryItem > -1)
            SetListItem();

        int martCount =
            Math.Min(
                RegularMartCount,
                regularMarts.Length);

        ushort rareCandy =
            checked(
                (ushort)GetRareCandyItemID());

        int[] targets =
            Enumerable.Range(
                    0,
                    martCount)
                .Where(z =>
                    !HasTrackedRareCandySlot(
                        z) &&
                    !regularMarts[z].Contains(
                        rareCandy))
                .ToArray();

        if (targets.Length == 0)
        {
            WinFormsUtil.Alert(
                "Rare Candy is already available in all progression marts.",
                "No additional slots were added.");
            return;
        }

        foreach (int martIndex in targets)
        {
            if (regularMarts[martIndex].Count <
                Gen7ExpandedMartTable.MaximumSlotsPerMart)
            {
                continue;
            }

            WinFormsUtil.Error(
                "Rare Candies could not be added.",
                $"'{locations[martIndex]}' already has the maximum of " +
                $"{Gen7ExpandedMartTable.MaximumSlotsPerMart} slots.");
            return;
        }

        int added =
            0;

        foreach (int martIndex in targets)
        {
            if (!TryAppendRegularMartItem(
                    martIndex,
                    rareCandy,
                    out int token,
                    out string appendError))
            {
                WinFormsUtil.Error(
                    "Rare Candies could not be added.",
                    appendError);
                return;
            }

            rareCandySlotTokens[martIndex] =
                token;

            added++;
        }

        setRareCandyPriceOnSave = true;
        recordRareCandyExpandedActionOnSave = true;

        if (entryItem > -1)
            GetListItem();

        UpdateExpandedMartButtons();

        WinFormsUtil.Alert(
            "Rare Candies added!",
            $"{added} new mart slot(s) were added. Existing shop items were preserved. " +
            "Click Save to rebuild Shop.cro and set Rare Candy price to 10.");
    }

    private void ApplyRareCandiesFromTemplate(
        GlobalRandomizationAction action)
    {
        string mode =
            null;

        if (action?.Parameters is not null)
        {
            action.Parameters.TryGetValue(
                "mode",
                out mode);
        }

        if (string.IsNullOrWhiteSpace(
                mode))
        {
            ApplyLegacyRareCandyReplacement();
            return;
        }

        if (!string.Equals(
                mode,
                "expanded",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Unsupported marts.add-rare-candies mode '{mode}'.");
        }

        string serializedSlots =
            string.Empty;

        action.Parameters.TryGetValue(
            "slots",
            out serializedSlots);

        ushort rareCandy =
            checked(
                (ushort)GetRareCandyItemID());

        if (!string.IsNullOrWhiteSpace(
                serializedSlots))
        {
            var seen =
                new HashSet<string>(
                    StringComparer.Ordinal);

            foreach (string raw in
                     serializedSlots.Split(
                         '|',
                         StringSplitOptions.RemoveEmptyEntries))
            {
                string part =
                    raw.Trim();

                if (!seen.Add(
                        part))
                {
                    continue;
                }

                string[] fields =
                    part.Split(
                        ':');

                if (fields.Length != 2 ||
                    !int.TryParse(
                        fields[0],
                        out int martIndex) ||
                    !int.TryParse(
                        fields[1],
                        out int slotIndex))
                {
                    throw new InvalidDataException(
                        $"Invalid Rare Candy mart slot '{part}'. Expected mart:slot.");
                }

                if (martIndex < 0 ||
                    martIndex >= RegularMartCount ||
                    martIndex >= regularMarts.Length)
                {
                    throw new InvalidDataException(
                        $"Rare Candy mart index {martIndex} is outside the progression marts.");
                }

                if (slotIndex < 0 ||
                    slotIndex >= regularMarts[martIndex].Count)
                {
                    throw new InvalidDataException(
                        $"Rare Candy slot {martIndex}:{slotIndex} does not exist after Expanded Marts replay.");
                }

                regularMarts[martIndex][slotIndex] =
                    rareCandy;
            }
        }

        setRareCandyPriceOnSave = true;

        if (entryItem > -1)
            GetListItem();
    }

    private void ApplyLegacyRareCandyReplacement()
    {
        ushort rareCandy =
            checked(
                (ushort)GetRareCandyItemID());

        for (int i = 0;
             i < RegularMartCount &&
             i < regularMarts.Length;
             i++)
        {
            List<ushort> mart =
                regularMarts[i];

            if (mart.Count == 0)
                continue;

            mart[^1] =
                rareCandy;
        }

        setRareCandyPriceOnSave = true;

        if (entryItem > -1)
            GetListItem();
    }

    private sealed record TrackedEVItemSlot(
        int MartIndex,
        ushort ItemId,
        int SlotIndex,
        int Token);

    private bool TryResolveEVShopItems(
        out ushort[] wings,
        out ushort heartScale,
        out ushort rareCandy,
        out ushort megaRing,
        out string error)
    {
        wings = [];
        heartScale = 0;
        rareCandy = 0;
        megaRing = 0;
        error = string.Empty;

        string[][] wingAliases =
        [
            ["Health Wing", "Pluma Vigor"],
            ["Muscle Wing", "Pluma MÃºsculo", "Pluma Musculo"],
            ["Resist Wing", "Pluma Aguante"],
            ["Genius Wing", "Pluma Intelecto"],
            ["Clever Wing", "Pluma Mente"],
            ["Swift Wing", "Pluma Ãmpetu", "Pluma Impetu"],
        ];

        var resolvedWings =
            new List<ushort>();

        foreach (string[] aliases in wingAliases)
        {
            int item =
                FindItemID(
                    aliases);

            if (item <= 0)
            {
                error =
                    $"Could not resolve '{aliases[0]}' from this ROM's item table.";
                return false;
            }

            resolvedWings.Add(
                checked(
                    (ushort)item));
        }

        int heartScaleId =
            FindItemID(
                "Heart Scale",
                "Escama CorazÃ³n",
                "Escama Corazon");

        int rareCandyId =
            GetRareCandyItemID();

        int megaRingId =
            GetMegaRingItemID();

        if (heartScaleId <= 0)
        {
            error =
                "Could not resolve 'Heart Scale' from this ROM's item table.";
            return false;
        }

        if (rareCandyId <= 0)
        {
            error =
                "Could not resolve 'Rare Candy' from this ROM's item table.";
            return false;
        }

        if (megaRingId <= 0)
        {
            error =
                "Could not resolve 'Mega Ring' from this ROM's item table.";
            return false;
        }

        wings =
            resolvedWings.ToArray();

        heartScale =
            checked(
                (ushort)heartScaleId);

        rareCandy =
            checked(
                (ushort)rareCandyId);

        megaRing =
            checked(
                (ushort)megaRingId);

        return true;
    }

    private static ushort[] GetRequiredEVItemsForMart(
        int martIndex,
        IReadOnlyList<ushort> wings,
        ushort heartScale,
        ushort rareCandy,
        ushort megaRing)
    {
        var required =
            new List<ushort>(
                wings);

        required.Add(
            rareCandy);

        if (martIndex >=
            HeartScaleTrial)
        {
            required.Add(
                heartScale);
        }

        if (martIndex >=
            MegaRingTrial)
        {
            required.Add(
                megaRing);
        }

        return required
            .Distinct()
            .ToArray();
    }

    private string GetEVTemplateSlots()
    {
        var slots =
            new List<string>();

        foreach (TrackedEVItemSlot tracked in
                 evItemSlots
                     .OrderBy(z => z.MartIndex)
                     .ThenBy(z => z.SlotIndex)
                     .ThenBy(z => z.ItemId))
        {
            if (tracked.MartIndex < 0 ||
                tracked.MartIndex >= regularMarts.Length)
            {
                continue;
            }

            int slotIndex =
                tracked.Token > 0
                    ? regularMartSlotTokens[
                            tracked.MartIndex]
                        .IndexOf(
                            tracked.Token)
                    : tracked.SlotIndex;

            if (slotIndex < 0 ||
                slotIndex >=
                regularMarts[tracked.MartIndex].Count)
            {
                continue;
            }

            slots.Add(
                $"{tracked.MartIndex}:{slotIndex}:{tracked.ItemId}");
        }

        return string.Join(
            "|",
            slots);
    }

    private void B_AddEVItems_Click(object sender, EventArgs e)
    {
        if (DialogResult.Yes != WinFormsUtil.Prompt(
            MessageBoxButtons.YesNo,
            "Add EV/training items to progression marts?",
            "This will ADD the six EV Wings and Rare Candy without replacing existing shop items. " +
            "Heart Scale is added from 3 Trials onward, and Mega Ring from 5 Trials onward. " +
            "Items already sold by a mart will not be duplicated."))
        {
            return;
        }

        if (entryItem > -1)
            SetListItem();

        if (!TryResolveEVShopItems(
                out ushort[] wings,
                out ushort heartScale,
                out ushort rareCandy,
                out ushort megaRing,
                out string resolveError))
        {
            WinFormsUtil.Error(
                "Could not resolve the EV shop items from this ROM's item table.",
                resolveError);
            return;
        }

        int martCount =
            Math.Min(
                RegularMartCount,
                regularMarts.Length);

        for (int mart = 0;
             mart < martCount;
             mart++)
        {
            ushort[] required =
                GetRequiredEVItemsForMart(
                    mart,
                    wings,
                    heartScale,
                    rareCandy,
                    megaRing);

            int missing =
                required.Count(item =>
                    !regularMarts[mart].Contains(
                        item));

            if (regularMarts[mart].Count + missing <=
                Gen7ExpandedMartTable.MaximumSlotsPerMart)
            {
                continue;
            }

            WinFormsUtil.Error(
                "EV/training items could not be added.",
                $"'{locations[mart]}' needs {missing} new slot(s), but that would exceed the maximum of " +
                $"{Gen7ExpandedMartTable.MaximumSlotsPerMart} slots.");
            return;
        }

        evItemSlots.Clear();

        int added =
            0;

        int alreadyAvailable =
            0;

        for (int mart = 0;
             mart < martCount;
             mart++)
        {
            ushort[] required =
                GetRequiredEVItemsForMart(
                    mart,
                    wings,
                    heartScale,
                    rareCandy,
                    megaRing);

            foreach (ushort itemId in required)
            {
                int existingSlot =
                    regularMarts[mart]
                        .IndexOf(
                            itemId);

                if (existingSlot >= 0)
                {
                    evItemSlots.Add(
                        new TrackedEVItemSlot(
                            mart,
                            itemId,
                            existingSlot,
                            Token: 0));

                    alreadyAvailable++;
                    continue;
                }

                if (!TryAppendRegularMartItem(
                        mart,
                        itemId,
                        out int token,
                        out string appendError))
                {
                    WinFormsUtil.Error(
                        "EV/training items could not be added.",
                        appendError);
                    return;
                }

                evItemSlots.Add(
                    new TrackedEVItemSlot(
                        mart,
                        itemId,
                        regularMarts[mart].Count - 1,
                        token));

                added++;
            }
        }

        setEVItemsOnSave = true;
        recordEVExpandedActionOnSave = true;

        if (entryItem > -1)
            GetListItem();

        UpdateExpandedMartButtons();

        if (added == 0)
        {
            WinFormsUtil.Alert(
                "All EV/training items are already available.",
                $"{alreadyAvailable} required mart entries were found. No additional slots were added. " +
                "Click Save to preserve the configuration in the Global Template.");
            return;
        }

        WinFormsUtil.Alert(
            "EV/training items added!",
            $"{added} new mart slot(s) were added and {alreadyAvailable} required item(s) were already present. " +
            "Existing shop items were preserved. Click Save to rebuild Shop.cro.");
    }

    private void ApplyEVItemsFromTemplate(
        GlobalRandomizationAction action)
    {
        string mode =
            null;

        if (action?.Parameters is not null)
        {
            action.Parameters.TryGetValue(
                "mode",
                out mode);
        }

        if (string.IsNullOrWhiteSpace(
                mode) ||
            string.Equals(
                mode,
                "replace-healing-only",
                StringComparison.OrdinalIgnoreCase))
        {
            ApplyLegacyEVItems();
            return;
        }

        if (!string.Equals(
                mode,
                "expanded",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Unsupported marts.add-ev-items mode '{mode}'.");
        }

        if (!action.Parameters.TryGetValue(
                "slots",
                out string serializedSlots) ||
            string.IsNullOrWhiteSpace(
                serializedSlots))
        {
            throw new InvalidDataException(
                "Expanded marts.add-ev-items action is missing its slot map.");
        }

        var seen =
            new HashSet<string>(
                StringComparer.Ordinal);

        foreach (string raw in
                 serializedSlots.Split(
                     '|',
                     StringSplitOptions.RemoveEmptyEntries))
        {
            string part =
                raw.Trim();

            if (!seen.Add(
                    part))
            {
                continue;
            }

            string[] fields =
                part.Split(
                    ':');

            if (fields.Length != 3 ||
                !int.TryParse(
                    fields[0],
                    out int martIndex) ||
                !int.TryParse(
                    fields[1],
                    out int slotIndex) ||
                !int.TryParse(
                    fields[2],
                    out int itemId))
            {
                throw new InvalidDataException(
                    $"Invalid EV mart slot '{part}'. Expected mart:slot:itemId.");
            }

            if (martIndex < 0 ||
                martIndex >= RegularMartCount ||
                martIndex >= regularMarts.Length)
            {
                throw new InvalidDataException(
                    $"EV mart index {martIndex} is outside the progression marts.");
            }

            if (slotIndex < 0 ||
                slotIndex >= regularMarts[martIndex].Count)
            {
                throw new InvalidDataException(
                    $"EV item slot {martIndex}:{slotIndex} does not exist after Expanded Marts replay.");
            }

            if (itemId <= 0 ||
                itemId >= itemlist.Length)
            {
                throw new InvalidDataException(
                    $"EV item ID {itemId} is outside this ROM's item table.");
            }

            regularMarts[martIndex][slotIndex] =
                checked(
                    (ushort)itemId);
        }

        setEVItemsOnSave = true;

        if (entryItem > -1)
            GetListItem();
    }

    private void ApplyLegacyEVItems()
    {
        string[] requiredItems =
        [
            "Health Wing|Pluma Vigor",
            "Muscle Wing|Pluma MÃºsculo|Pluma Musculo",
            "Resist Wing|Pluma Aguante",
            "Genius Wing|Pluma Intelecto",
            "Clever Wing|Pluma Mente",
            "Swift Wing|Pluma Ãmpetu|Pluma Impetu",
            "Heart Scale|Escama CorazÃ³n|Escama Corazon",
            "Rare Candy|Caramelo Raro",
            "Mega Ring|Megaaro|Mega Aro|Mega-Aro",
        ];

        var missing =
            new List<string>();

        foreach (string group in requiredItems)
        {
            string[] aliases =
                group.Split(
                    '|');

            if (FindItemID(
                    aliases) <= 0)
            {
                missing.Add(
                    aliases[0]);
            }
        }

        if (missing.Count != 0)
        {
            throw new InvalidDataException(
                "Could not resolve the legacy EV shop items from this ROM's item table. Missing: " +
                string.Join(
                    ", ",
                    missing));
        }

        int martCount =
            Math.Min(
                RegularMartCount,
                regularMarts.Length);

        int[] megaRingSlots =
            new int[martCount];

        Array.Fill(
            megaRingSlots,
            -1);

        for (int mart = MegaRingTrial;
             mart < martCount;
             mart++)
        {
            List<ushort> inventory =
                regularMarts[mart];

            if (inventory.Count == 0)
                continue;

            megaRingSlots[mart] =
                FindMegaRingTargetSlot(
                    inventory);

            if (megaRingSlots[mart] >= 0)
                continue;

            throw new InvalidDataException(
                $"Could not reserve a healing-item slot for Mega Ring in '{locations[mart]}'. " +
                "Legacy EV Items requires a compatible healing slot.");
        }

        int changed =
            0;

        for (int mart = 0;
             mart < martCount;
             mart++)
        {
            List<ushort> inventory =
                regularMarts[mart];

            for (int slot = 0;
                 slot < inventory.Count;
                 slot++)
            {
                int current =
                    inventory[slot];

                int replacement =
                    slot == megaRingSlots[mart]
                        ? GetMegaRingItemID()
                        : GetEVItemReplacement(
                            current,
                            mart);

                if (replacement <= 0 ||
                    replacement == current)
                {
                    continue;
                }

                inventory[slot] =
                    checked(
                        (ushort)replacement);

                changed++;
            }
        }

        setEVItemsOnSave =
            changed > 0;

        if (entryItem > -1)
            GetListItem();
    }
    private void B_FreeMegaStones_Click(object sender, EventArgs e)
    {
        if (DialogResult.Yes != WinFormsUtil.Prompt(
            MessageBoxButtons.YesNo,
            "Set all Battle Tree Mega Stone BP prices to 0?",
            "Only the BP shop labeled 'Battle Tree [Mega Stones]' will be changed. The item list itself is not modified."))
        {
            return;
        }

        if (entryBPItem > -1)
            SetListBPItem();

        SetMegaStoneBPPrices(0);
        freeMegaStonesOnSave = true;

        if (entryBPItem == MegaStoneBPShopIndex)
            GetListBPItem();

        WinFormsUtil.Alert(
            "Mega Stones are now free!",
            "All prices in Battle Tree [Mega Stones] were set to 0 BP. Click Save to write Shop.cro.");
    }

    private void SetMegaStoneBPPrices(int price)
    {
        if (MegaStoneBPShopIndex < 0 || MegaStoneBPShopIndex >= len_BPItem.Length)
            return;

        int count = len_BPItem[MegaStoneBPShopIndex];
        int ofs = ofs_BPItem + (len_BPItem.Take(MegaStoneBPShopIndex).Sum(z => z) * 4);
        ushort value = (ushort)Math.Clamp(price, 0, ushort.MaxValue);

        for (int i = 0; i < count; i++)
            Array.Copy(BitConverter.GetBytes(value), 0, data, ofs + (4 * i) + 2, 2);
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

    private int FindMegaRingTargetSlot(IReadOnlyList<ushort> inventory)
    {
        int megaRing = GetMegaRingItemID();
        int[] preferredHealingItems =
        [
            megaRing,
            696, // Legacy value written by the first EV-items patch; repair it in-place.
            FindItemID("Hyper Potion", "HiperpociÃ³n", "Hiperpocion"),
            FindItemID("Full Heal", "Cura Total"),
            FindItemID("Max Potion", "PociÃ³n MÃ¡xima", "Pocion Maxima"),
            FindItemID("Full Restore", "Restaurar Todo", "Restaurar todo"),
        ];

        foreach (int candidate in preferredHealingItems)
        {
            if (candidate <= 0)
                continue;

            for (int slot = 0;
                 slot < inventory.Count;
                 slot++)
            {
                if (inventory[slot] == candidate)
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
    private readonly int ofs_Item;
    private readonly int ofs_BPItem;
    private readonly byte[] len_Items;
    private readonly byte[] len_BPItem;
    private readonly List<ushort>[] regularMarts;
    private readonly List<int>[] regularMartSlotTokens;
    private readonly List<PendingExpandedMartOperation> expandedMartOperations = [];
    private readonly Dictionary<int, int> rareCandySlotTokens = [];
    private readonly List<TrackedEVItemSlot> evItemSlots = [];
    private int nextExpandedMartToken = 1;

    private readonly string[] itemlist = Main.Config.GetText(TextName.ItemNames);
    //private readonly string[] movelist = Main.Config.GetText(TextName.MoveNames);
    private readonly byte[] data;

    private const int RareCandyItemID = 50;
    private const int RareCandyPrice = 10;
    private const int RegularMartCount = 8;

    // Resolve item IDs from the ROM's own item-name table. Hardcoded IDs from
    // other generations/versions can point to unrelated entries (for example Data Cards).
    private const int MegaRingPrice = 10;
    private const int HeartScaleTrial = 3;
    private const int MegaRingTrial = 5;
    private const int MegaStoneBPShopIndex = 5;

    private bool setRareCandyPriceOnSave;
    private bool recordRareCandyExpandedActionOnSave;
    private bool setEVItemsOnSave;
    private bool recordEVExpandedActionOnSave;
    private bool randomizeMartsOnSave;
    private bool randomizeBPMartsOnSave;
    private bool randomizeMartsSpecialOnly;
    private bool freeMegaStonesOnSave;

    private Button B_AddRareCandies;
    private Button B_AddEVItems;
    private Button B_FreeMegaStones;
    private Button B_AddItem;
    private Button B_DeleteItem;

    #region Tables
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
        "Route 3 [X Items]",
        "Konikoni City [X Items]",
        "Tapu Village [X Items]",
        "Mount Lanakila [X Items]",
    ];

    private readonly string[] locationsBP =
    [
        "Battle Royal Dome [Medicine]",
        "Battle Royal Dome [EV Training]",
        "Battle Royal Dome [Held Items]",
        "Battle Tree [Trade Evolution Items]",
        "Battle Tree [Held Items]",
        "Battle Tree [Mega Stones]",
        "Beaches [Medicine]",
    ];
    #endregion

    private void B_Save_Click(object sender, EventArgs e)
    {
        if (entryItem > -1) SetListItem();

        if (entryBPItem > -1) SetListBPItem();

        if (!TryBuildShopForSave(
                out byte[] shopToWrite,
                out string shopBuildError))
        {
            WinFormsUtil.Error(
                "Could not rebuild Shop.cro.",
                shopBuildError);
            return;
        }

        if (setRareCandyPriceOnSave || setEVItemsOnSave)
            SetItemPrice(GetRareCandyItemID(), RareCandyPrice);
        if (setEVItemsOnSave)
        {
            int megaRing = GetMegaRingItemID();
            if (megaRing > 0)
                SetItemPrice(megaRing, MegaRingPrice);
        }

        File.WriteAllBytes(CROPath, shopToWrite);

        RememberExpandedMartTemplateAction();

        if (recordRareCandyExpandedActionOnSave)
        {
            RandomizationSessionState.MarkAction(
                "marts.add-rare-candies",
                ("mode", "expanded"),
                ("slots", GetRareCandyTemplateSlots()),
                ("price", RareCandyPrice.ToString()));
        }
        if (recordEVExpandedActionOnSave)
        {
            RandomizationSessionState.MarkAction(
                "marts.add-ev-items",
                ("mode", "expanded"),
                ("slots", GetEVTemplateSlots()),
                ("heartScaleFromTrial", HeartScaleTrial.ToString()),
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

    private sealed class PendingExpandedMartOperation
    {
        internal ExpandedMartOperation Operation { get; set; }
        internal int Token { get; }

        internal PendingExpandedMartOperation(
            ExpandedMartOperation operation,
            int token)
        {
            Operation = operation;
            Token = token;
        }
    }

    private void ApplyExpandedMartsFromTemplate(
        GlobalRandomizationAction action)
    {
        ExpandedMartTemplateAction.Validate(
            action,
            Main.Config.USUM
                ? "USUM"
                : "SM");

        if (!ExpandedMartTemplateAction.TryParse(
                action,
                out List<ExpandedMartOperation> operations,
                out string parseError))
        {
            throw new InvalidOperationException(
                parseError);
        }

        if (!ExpandedMartTemplateAction.TryApply(
                regularMarts,
                operations,
                out string applyError))
        {
            throw new InvalidOperationException(
                applyError);
        }

        for (int mart = 0;
             mart < regularMartSlotTokens.Length;
             mart++)
        {
            regularMartSlotTokens[mart].Clear();
            regularMartSlotTokens[mart].AddRange(
                Enumerable.Repeat(
                    0,
                    regularMarts[mart].Count));
        }

        if (entryItem >= 0)
            GetListItem();
    }

    private void RememberExpandedMartTemplateAction()
    {
        if (expandedMartOperations.Count == 0)
            return;

        RefreshPendingExpandedMartItems();

        var combined =
            new List<ExpandedMartOperation>();

        GlobalRandomizationAction previous =
            RandomizationSessionState
                .ExportActions()
                .FirstOrDefault(z =>
                    string.Equals(
                        z.Id,
                        ExpandedMartTemplateAction.ActionId,
                        StringComparison.OrdinalIgnoreCase));

        if (previous is not null)
        {
            if (!ExpandedMartTemplateAction.TryParse(
                    previous,
                    out List<ExpandedMartOperation> previousOperations,
                    out string previousError))
            {
                throw new InvalidOperationException(
                    "Existing Expanded Marts template action is invalid: " +
                    previousError);
            }

            combined.AddRange(
                previousOperations);
        }

        combined.AddRange(
            expandedMartOperations.Select(z => z.Operation));

        GlobalRandomizationAction action =
            ExpandedMartTemplateAction.Create(
                combined);

        RandomizationSessionState.MarkAction(
            action.Id,
            action.Parameters
                .Select(z =>
                    (
                        Key: z.Key,
                        Value: z.Value))
                .ToArray());
    }

    private void RefreshPendingExpandedMartItems()
    {
        foreach (PendingExpandedMartOperation pending in
                 expandedMartOperations)
        {
            ExpandedMartOperation operation =
                pending.Operation;

            if (operation.Kind !=
                    ExpandedMartOperationKind.Add ||
                pending.Token <= 0 ||
                operation.MartIndex < 0 ||
                operation.MartIndex >= regularMarts.Length)
            {
                continue;
            }

            int currentSlot =
                regularMartSlotTokens[
                        operation.MartIndex]
                    .IndexOf(
                        pending.Token);

            // The inserted slot may have been deleted later in the same session.
            // In that case its original item value is irrelevant because the delete
            // operation will remove it during replay.
            if (currentSlot < 0 ||
                currentSlot >=
                regularMarts[operation.MartIndex].Count)
            {
                continue;
            }

            pending.Operation =
                operation with
                {
                    ItemId =
                        regularMarts[
                            operation.MartIndex][
                            currentSlot],
                };
        }
    }
    private bool TryBuildShopForSave(
        out byte[] updatedShop,
        out string error)
    {
        updatedShop = null;
        error = string.Empty;

        ushort[][] inventories =
            regularMarts
                .Select(z => z.ToArray())
                .ToArray();

        bool countsChanged =
            inventories.Length != len_Items.Length ||
            inventories
                .Select(z => z.Length)
                .Where((count, index) =>
                    index >= len_Items.Length ||
                    count != len_Items[index])
                .Any();

        if (countsChanged)
        {
            return Gen7ExpandedMartTable.TryRebuild(
                data,
                inventories,
                out updatedShop,
                out _,
                out error);
        }

        // Preserve the traditional fixed-size path when only item IDs changed.
        // This avoids expanding Shop.cro for ordinary randomization/QoL edits.
        updatedShop =
            (byte[])data.Clone();

        int offset =
            ofs_Item;

        for (int mart = 0;
             mart < inventories.Length;
             mart++)
        {
            ushort[] inventory =
                inventories[mart];

            for (int slot = 0;
                 slot < inventory.Length;
                 slot++)
            {
                Array.Copy(
                    BitConverter.GetBytes(
                        inventory[slot]),
                    0,
                    updatedShop,
                    offset,
                    2);

                offset +=
                    2;
            }
        }

        return true;
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        RandSettings.SetFormSettings(this, Controls);
        base.OnFormClosing(e);
    }

    private void B_Cancel_Click(object sender, EventArgs e) => Close();

    private void SetupDGV()
    {
        dgvItem.Items.AddRange(itemlist); // add only the Names
        dgvItemBP.Items.AddRange(itemlist); // add only the Names
    }

    private int entryItem = -1;
    private int entryBPItem = -1;

    private void ChangeIndexItem(object sender, EventArgs e)
    {
        if (entryItem > -1) SetListItem();
        entryItem = CB_Location.SelectedIndex;
        GetListItem();
    }

    private void ChangeIndexBPItem(object sender, EventArgs e)
    {
        if (entryBPItem > -1) SetListBPItem();
        entryBPItem = CB_LocationBPItem.SelectedIndex;
        GetListBPItem();
    }

    private void GetListItem()
    {
        dgv.Rows.Clear();

        if (entryItem < 0 ||
            entryItem >= regularMarts.Length)
        {
            UpdateExpandedMartButtons();
            return;
        }

        List<ushort> mart =
            regularMarts[entryItem];

        dgv.Rows.Add(
            mart.Count);

        for (int i = 0;
             i < mart.Count;
             i++)
        {
            dgv.Rows[i].Cells[0].Value =
                i.ToString();

            ushort item =
                mart[i];

            dgv.Rows[i].Cells[1].Value =
                item < itemlist.Length
                    ? itemlist[item]
                    : string.Empty;
        }

        UpdateExpandedMartButtons();
    }

    private void GetListBPItem()
    {
        dgvbp.Rows.Clear();
        int count = len_BPItem[entryBPItem];
        dgvbp.Rows.Add(count);
        var ofs = ofs_BPItem + (len_BPItem.Take(entryBPItem).Sum(z => z) * 4);
        for (int i = 0; i < count; i++)
        {
            dgvbp.Rows[i].Cells[0].Value = i.ToString();
            dgvbp.Rows[i].Cells[1].Value = itemlist[BitConverter.ToUInt16(data, ofs + (4 * i))];
            dgvbp.Rows[i].Cells[2].Value = BitConverter.ToUInt16(data, ofs + (4 * i) + 2).ToString();
        }
    }

    private void SetListItem()
    {
        if (entryItem < 0 ||
            entryItem >= regularMarts.Length)
        {
            return;
        }

        List<ushort> mart =
            regularMarts[entryItem];

        if (dgv.Rows.Count !=
            mart.Count)
        {
            throw new InvalidOperationException(
                $"Regular mart #{entryItem} has {mart.Count} slots but the editor shows {dgv.Rows.Count} rows.");
        }

        for (int i = 0;
             i < mart.Count;
             i++)
        {
            int item =
                Array.IndexOf(
                    itemlist,
                    dgv.Rows[i].Cells[1].Value);

            if (item < 0)
            {
                throw new InvalidOperationException(
                    $"Regular mart #{entryItem}, slot #{i} does not contain a valid item selection.");
            }

            mart[i] =
                (ushort)item;
        }
    }

    private void SetListBPItem()
    {
        int count = dgvbp.Rows.Count;
        var ofs = ofs_BPItem + (len_BPItem.Take(entryBPItem).Sum(z => z) * 4);
        for (int i = 0; i < count; i++)
        {
            int item = Array.IndexOf(itemlist, dgvbp.Rows[i].Cells[1].Value);
            Array.Copy(BitConverter.GetBytes((ushort)item), 0, data, ofs + (4 * i), 2);
            string p = dgvbp.Rows[i].Cells[2].Value.ToString();
            if (int.TryParse(p, out var price))
                Array.Copy(BitConverter.GetBytes((ushort)price), 0, data, ofs + (4 * i) + 2, 2);
        }
    }

    private void B_Randomize_Click(object sender, EventArgs e)
    {
        switch (tabControl1.SelectedIndex)
        {
            case 0:
                RandomizeItems();
                break;
            case 1:
                RandomizeBPItems();
                break;
        }
    }

    private void RandomizeItems()
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

    private void RandomizeBPItems()
    {
        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNoCancel, "Randomize BP inventories?"))
            return;

        int[] validItems = Randomizer.GetRandomItemList();

        int ctr = 0;
        Util.Shuffle(validItems);

        for (int i = 0; i < CB_LocationBPItem.Items.Count; i++)
        {
            CB_LocationBPItem.SelectedIndex = i;
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
}