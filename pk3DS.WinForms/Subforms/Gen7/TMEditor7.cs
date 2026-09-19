using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Linq;
using pk3DS.Core;

namespace pk3DS.WinForms;

public partial class TMEditor7 : Form
{
    public TMEditor7()
    {
        InitializeComponent();
        if (Main.ExeFSPath == null) { WinFormsUtil.Alert("No exeFS code to load."); Close(); }
        string[] files = Directory.GetFiles(Main.ExeFSPath);
        if (!File.Exists(files[0]) || !Path.GetFileNameWithoutExtension(files[0]).Contains("code")) { WinFormsUtil.Alert("No .code.bin detected."); Close(); }
        data = File.ReadAllBytes(files[0]);
        if (data.Length % 0x200 != 0) { WinFormsUtil.Alert(".code.bin not decompressed. Aborting."); Close(); }
        offset = Util.IndexOfBytes(data, Signature, 0x400000, 0) + Signature.Length;
        if (Main.Config.USUM)
            offset += 0x22;
        codebin = files[0];
        movelist[0] = "";
        SetupDGV();
        GetList();
        AddTMSanityCheckBox();
    }

    private static readonly byte[] Signature = [0x03, 0x40, 0x03, 0x41, 0x03, 0x42, 0x03, 0x43, 0x03]; // tail end of item::ITEM_CheckBeads
    private readonly string codebin;
    private readonly string[] movelist = Main.Config.GetText(TextName.MoveNames);
    private readonly int offset = 0x0059795A; // Default
    private readonly byte[] data;
    private int dataoffset;

    private void GetDataOffset()
    {
        dataoffset = offset; // reset
    }

    private void SetupDGV()
    {
        dgvTM.Columns.Clear();
        var dgvIndex = new DataGridViewTextBoxColumn();
        {
            dgvIndex.HeaderText = "Index";
            dgvIndex.DisplayIndex = 0;
            dgvIndex.Width = 45;
            dgvIndex.ReadOnly = true;
            dgvIndex.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvIndex.SortMode = DataGridViewColumnSortMode.NotSortable;
        }
        var dgvMove = new DataGridViewComboBoxColumn();
        {
            dgvMove.HeaderText = "Move";
            dgvMove.DisplayIndex = 1;
            dgvMove.Items.AddRange(movelist); // add only the Names

            dgvMove.Width = 133;
            dgvMove.FlatStyle = FlatStyle.Flat;
            dgvIndex.SortMode = DataGridViewColumnSortMode.NotSortable;
        }
        dgvTM.Columns.Add(dgvIndex);
        dgvTM.Columns.Add(dgvMove);
    }

    private List<ushort> tms = [];

    private void GetList()
    {
        tms = [];
        dgvTM.Rows.Clear();

        GetDataOffset();
        for (int i = 0; i < 100; i++) // TMs stored sequentially
            tms.Add(BitConverter.ToUInt16(data, dataoffset + (2 * i)));

        ushort[] tmlist = [.. tms];
        for (int i = 0; i < tmlist.Length; i++)
        { dgvTM.Rows.Add(); dgvTM.Rows[i].Cells[0].Value = (i + 1).ToString(); dgvTM.Rows[i].Cells[1].Value = movelist[tmlist[i]]; }
    }

    private CheckBox CHK_TMSanity;

    private void AddTMSanityCheckBox()
    {
        CHK_TMSanity = new CheckBox
        {
            Name = "CHK_TMSanity",
            AutoSize = true,
            Location = new System.Drawing.Point(145, 5),
            Text = "TM Sanity",
            UseVisualStyleBackColor = true,
        };
        CHK_TMSanity.CheckedChanged += CHK_TMSanity_CheckedChanged;
        Controls.Add(CHK_TMSanity);
        CHK_TMSanity.BringToFront();
    }

    private void CHK_TMSanity_CheckedChanged(object sender, EventArgs e)
    {
        if (CHK_TMSanity.Checked)
            RandomizationSessionState.MarkAction("tms.sanity");
        else
            RandomizationSessionState.RemoveAction("tms.sanity");
    }

    private ushort[] GetCurrentTMList()
    {
        var result = new ushort[Math.Min(100, dgvTM.Rows.Count)];

        for (int i = 0; i < result.Length; i++)
        {
            int move = Array.IndexOf(movelist, dgvTM.Rows[i].Cells[1].Value);
            result[i] = move > 0 ? (ushort)move : (ushort)0;
        }

        return result;
    }

    private void ApplyTMSanity()
    {
        ushort[] currentTMs = GetCurrentTMList();
        int entryCount = Math.Min(Main.SpeciesStat.Length, Main.Config.Learnsets.Length);
        int compatibilityAdded = 0;
        int entriesChanged = 0;

        for (int i = 1; i < entryCount; i++)
        {
            var personal = Main.SpeciesStat[i];
            var learnset = Main.Config.Learnsets[i];
            var levelUpMoves = learnset.Moves.ToHashSet();
            int tmCount = Math.Min(currentTMs.Length, personal.TMHM.Length);
            bool changed = false;

            for (int tm = 0; tm < tmCount; tm++)
            {
                int move = currentTMs[tm];

                if (move <= 0 || personal.TMHM[tm] || !levelUpMoves.Contains(move))
                    continue;

                personal.TMHM[tm] = true;
                compatibilityAdded++;
                changed = true;
            }

            if (changed)
                entriesChanged++;
        }

        if (compatibilityAdded > 0)
        {
            byte[][] personalFiles = Main.Config.GARCPersonal.Files;
            byte[][] serialized = Main.SpeciesStat.Select(z => z.Write()).ToArray();

            if (personalFiles.Length < serialized.Length + 1)
                throw new InvalidOperationException("Personal GARC does not contain the expected master table.");

            serialized.CopyTo(personalFiles, 0);

            for (int i = 0; i < serialized.Length; i++)
                serialized[i].CopyTo(personalFiles[^1], i * serialized[i].Length);

            Main.Config.GARCPersonal.Files = personalFiles;
            Main.Config.GARCPersonal.Save();
            Main.Config.InitializePersonal();
        }

        RandomizationSessionState.MarkAction("tms.sanity");

        string detail = compatibilityAdded == 0
            ? "TM Sanity found no missing level-up TM compatibility."
            : $"Added {compatibilityAdded} TM compatibility flag(s) across {entriesChanged} Pokemon/form entr{(entriesChanged == 1 ? "y" : "ies")}.";

        if (BatchRuntime.IsActive)
            BatchRuntime.Log(detail);
        else
            WinFormsUtil.Alert("TM Sanity complete!", detail);
    }
    private void SetList()
    {
        // Gather TM/HM list.
        tms = [];
        for (int i = 0; i < dgvTM.Rows.Count; i++)
            tms.Add((ushort)Array.IndexOf(movelist, dgvTM.Rows[i].Cells[1].Value));

        ushort[] tmlist = [.. tms];

        // Set TM/HM list in
        for (int i = 0; i < 100; i++)
            Array.Copy(BitConverter.GetBytes(tmlist[i]), 0, data, offset + (2 * i), 2);

        // Set Move Text Descriptions back into Item Text File
        string[] itemDescriptions = Main.Config.GetText(TextName.ItemFlavor);
        string[] moveDescriptions = Main.Config.GetText(TextName.MoveFlavor);
        for (int i = 1 - 1; i <= 92 - 1; i++) // TM01 - TM92
            itemDescriptions[328 + i] = moveDescriptions[tmlist[i]];
        for (int i = 93 - 1; i <= 95 - 1; i++) // TM92 - TM95
            itemDescriptions[618 + i - 92] = moveDescriptions[tmlist[i]];
        for (int i = 96 - 1; i <= 100 - 1; i++) // TM96 - TM100
            itemDescriptions[690 + i - 95] = moveDescriptions[tmlist[i]];
        Main.Config.SetText(TextName.ItemFlavor, itemDescriptions);
    }

    private void Form_Closing(object sender, FormClosingEventArgs e)
    {
        SetList();

        if (CHK_TMSanity?.Checked == true)
            ApplyTMSanity();

        File.WriteAllBytes(codebin, data);
    }

    private void B_RandomTM_Click(object sender, EventArgs e)
    {
        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Randomize TMs?", "Move compatibility will be the same as the base TMs.") != DialogResult.Yes) return;

        int[] randomMoves = Enumerable.Range(1, movelist.Length - 1).Select(i => i).ToArray();
        Util.Shuffle(randomMoves);

        int[] banned = [.. Legal.Z_Moves, .. new[] { 165, 621, 166, 226 }];
        int ctr = 0;

        for (int i = 0; i < dgvTM.Rows.Count; i++)
        {
            int val = Array.IndexOf(movelist, dgvTM.Rows[i].Cells[1].Value);
            if (banned.Contains(val)) continue;
            while (banned.Contains(randomMoves[ctr])) ctr++;

            dgvTM.Rows[i].Cells[1].Value = movelist[randomMoves[ctr++]];
        }
        RandomizationSessionState.MarkAction("tms.randomize");
        WinFormsUtil.Alert("Randomized!");
    }

    internal static ushort[] GetTMHMList()
    {
        if (Main.ExeFSPath == null)
            return [];
        string[] files = Directory.GetFiles(Main.ExeFSPath);
        if (!File.Exists(files[0]) || !Path.GetFileNameWithoutExtension(files[0]).Contains("code"))
            return [];
        byte[] data = File.ReadAllBytes(files[0]);
        int dataoffset = Util.IndexOfBytes(data, Signature, 0x400000, 0) + Signature.Length;
        if (data.Length % 0x200 != 0)
            return [];

        if (Main.Config.USUM)
            dataoffset += 0x22;
        List<ushort> tms = [];

        for (int i = 0; i < 100; i++) // TMs stored sequentially
            tms.Add(BitConverter.ToUInt16(data, dataoffset + (2 * i)));
        return [.. tms];
    }
}