using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using pk3DS.Core;

namespace pk3DS.WinForms;

public partial class MartEditor7UU
{
    private const string BanAbilityCapsuleActionId = "marts.ban-ability-capsule";
    private const int AbilityCapsuleFallbackItemID = 645;

    private bool banAbilityCapsuleEnabled;
    private Button B_BanAbilityCapsule;

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        banAbilityCapsuleEnabled = RandomizationSessionState.ExportActions()
            .Any(a => string.Equals(a.Id, BanAbilityCapsuleActionId, StringComparison.OrdinalIgnoreCase));

        AddBanAbilityCapsuleButton();

        // The designer's normal randomize handler was registered first, so this
        // handler runs afterwards and removes any Ability Capsule that was rolled.
        B_Randomize.Click += B_Randomize_BanAbilityCapsulePost;
    }

    private void AddBanAbilityCapsuleButton()
    {
        if (B_BanAbilityCapsule != null)
            return;

        const int gap = 8;
        const int margin = 12;

        Control anchor = Controls.Find("B_FreeMegaStones", true).FirstOrDefault()
            ?? Controls.Find("B_AddEVItems", true).FirstOrDefault()
            ?? Controls.Find("B_AddRareCandies", true).FirstOrDefault()
            ?? B_Randomize;

        B_BanAbilityCapsule = new Button
        {
            Name = "B_BanAbilityCapsule",
            Text = "Ban Cap.H",
            Size = new System.Drawing.Size(90, B_Randomize.Height),
            Location = new System.Drawing.Point(anchor.Right + gap, B_Randomize.Top),
            UseVisualStyleBackColor = true,
        };
        B_BanAbilityCapsule.Click += B_BanAbilityCapsule_Click;
        Controls.Add(B_BanAbilityCapsule);

        int requiredWidth = B_BanAbilityCapsule.Right + margin;
        if (requiredWidth > ClientSize.Width)
            ClientSize = new System.Drawing.Size(requiredWidth, ClientSize.Height);

        // Keep the same two-row layout used by the other custom Mart buttons.
        tabControl1.Width = ClientSize.Width - tabControl1.Left - margin;
        B_Save.Left = ClientSize.Width - margin - B_Save.Width;
        B_Cancel.Left = B_Save.Left - gap - B_Cancel.Width;

        B_BanAbilityCapsule.BringToFront();
    }

    private void B_BanAbilityCapsule_Click(object sender, EventArgs e)
    {
        banAbilityCapsuleEnabled = true;
        int removed = RemoveAbilityCapsulesFromMarts();

        RandomizationSessionState.MarkAction(
            BanAbilityCapsuleActionId,
            ("item", "Ability Capsule"),
            ("mode", "post-randomize-filter"));

        WinFormsUtil.Alert(
            "Ability Capsule banned from marts!",
            removed == 0
                ? "No Ability Capsules were present. Future Mart randomizations in this editor will also remove them."
                : $"Removed {removed} Ability Capsule slot(s). Future Mart randomizations in this editor will also remove them.");
    }

    private void B_Randomize_BanAbilityCapsulePost(object sender, EventArgs e)
    {
        if (!banAbilityCapsuleEnabled)
            return;

        RemoveAbilityCapsulesFromMarts();
    }

    private int RemoveAbilityCapsulesFromMarts()
    {
        int abilityCapsule = GetAbilityCapsuleItemID();
        if (abilityCapsule <= 0)
        {
            WinFormsUtil.Error(
                "Could not find Ability Capsule in this ROM's item table.",
                "Expected Ability Capsule / Cápsula Habilidad.");
            return 0;
        }

        if (entryItem > -1)
            SetListItem();
        if (entryBPItem > -1)
            SetListBPItem();

        int[] replacements = Randomizer.GetRandomItemList()
            .Where(id => id > 0 && id < itemlist.Length)
            .Where(id => id != abilityCapsule)
            .Where(id => !BannedItems.Contains(id))
            .ToArray();

        if (replacements.Length == 0)
            throw new InvalidOperationException("No valid replacement items were available after banning Ability Capsule.");

        int removed = 0;

        for (int mart = 0;
             mart < regularMarts.Length;
             mart++)
        {
            List<ushort> inventory =
                regularMarts[mart];

            for (int slot = 0;
                 slot < inventory.Count;
                 slot++)
            {
                if (inventory[slot] != abilityCapsule)
                    continue;

                int replacement =
                    replacements[
                        Util.Rand.Next(
                            replacements.Length)];

                inventory[slot] =
                    (ushort)replacement;

                removed++;
            }
        }

        for (int mart = 0; mart < len_BPItem.Length; mart++)
        {
            int ofs = ofs_BPItem + (len_BPItem.Take(mart).Sum(z => z) * 4);
            for (int slot = 0; slot < len_BPItem[mart]; slot++)
            {
                int writeOffset = ofs + (slot * 4);
                int current = BitConverter.ToUInt16(data, writeOffset);
                if (current != abilityCapsule)
                    continue;

                int replacement = replacements[Util.Rand.Next(replacements.Length)];
                Array.Copy(BitConverter.GetBytes((ushort)replacement), 0, data, writeOffset, 2);
                // Keep the existing BP price unchanged.
                removed++;
            }
        }

        if (entryItem > -1)
            GetListItem();
        if (entryBPItem > -1)
            GetListBPItem();

        return removed;
    }

    private int GetAbilityCapsuleItemID()
    {
        int id = Array.FindIndex(itemlist, name =>
            string.Equals(name, "Ability Capsule", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "Cápsula Habilidad", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "Capsula Habilidad", StringComparison.OrdinalIgnoreCase));

        if (id > 0)
            return id;

        return AbilityCapsuleFallbackItemID < itemlist.Length
            ? AbilityCapsuleFallbackItemID
            : -1;
    }
}
