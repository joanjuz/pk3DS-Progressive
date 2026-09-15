using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Linq;
using pk3DS.Core;

namespace pk3DS.WinForms;

public partial class TMHMEditor6 : Form
{
    public TMHMEditor6()
    {
        InitializeComponent();
        AddForgettableHMsButton();
        if (Main.ExeFSPath == null) { WinFormsUtil.Alert("No exeFS code to load."); Close(); }
        string[] files = Directory.GetFiles(Main.ExeFSPath);
        if (!File.Exists(files[0]) || !Path.GetFileNameWithoutExtension(files[0]).Contains("code")) { WinFormsUtil.Alert("No .code.bin detected."); Close(); }
        data = File.ReadAllBytes(files[0]);
        if (data.Length % 0x200 != 0) { WinFormsUtil.Alert(".code.bin not decompressed. Aborting."); Close(); }
        offset = Util.IndexOfBytes(data, Signature, 0x400000, 0) + 8;
        codebin = files[0];
        movelist[0] = "";
        SetupDGV();
        GetList();
        RandSettings.GetFormSettings(this, groupBox1.Controls);
    }

    private static readonly byte[] Signature = [0xD4, 0x00, 0xAE, 0x02, 0xAF, 0x02, 0xB0, 0x02];
    private readonly string codebin;
    private readonly string[] movelist = Main.Config.GetText(TextName.MoveNames);
    private readonly int offset = Main.Config.ORAS ? 0x004A67EE : 0x00464796; // Default
    private readonly byte[] data;
    private int dataoffset;
    private Button B_ForgettableHMs;
    private void AddForgettableHMsButton()
    {
        B_ForgettableHMs = new Button
        {
            Location = new System.Drawing.Point(13, CHK_RandomizeField.Bottom + 8),
            Name = "B_ForgettableHMs",
            Size = new System.Drawing.Size(180, 23),
            TabIndex = 999,
            Text = "Make HMs forgettable",
            UseVisualStyleBackColor = true,
        };

        B_ForgettableHMs.Click += B_ForgettableHMs_Click;

        groupBox1.Controls.Add(B_ForgettableHMs);
        B_ForgettableHMs.BringToFront();

        int requiredHeight = B_ForgettableHMs.Bottom + 10;

        if (groupBox1.Height < requiredHeight)
            groupBox1.Height = requiredHeight;

        int requiredClientHeight = groupBox1.Bottom + 10;

        if (ClientSize.Height < requiredClientHeight)
            ClientSize = new System.Drawing.Size(ClientSize.Width, requiredClientHeight);
    }
    private void B_ForgettableHMs_Click(object sender, EventArgs e)
    {
        if (WinFormsUtil.Prompt(
            MessageBoxButtons.YesNo,
            "Make HMs forgettable?",
            "This will patch code.bin so HM moves can be forgotten normally. Make a backup before continuing.") != DialogResult.Yes)
        {
            return;
        }

        bool patched = PatchForgettableHMs();

        WinFormsUtil.Alert(patched
            ? "HMs should now be forgettable. Save/close the editor to write code.bin."
            : "Could not find a known HM forget restriction pattern in code.bin.");
    }

    private bool PatchForgettableHMs()
    {
        if (Main.Config.ORAS)
            return PatchForgettableHMsORAS();

        return PatchForgettableHMsXY();
    }
    private bool PatchForgettableHMsORAS()
    {
        byte[] original =
        [
            0x3C, 0x30, 0x9F, 0xE5, // ldr r3, [pc, #0x3c]
        0x00, 0x10, 0xA0, 0xE3, // mov r1, #0
        0x81, 0x20, 0x83, 0xE0, // add r2, r3, r1, lsl #1
        0xB8, 0xCB, 0xD2, 0xE1, // ldrh ip, [r2, #0xb8]
        0x00, 0x00, 0x5C, 0xE1, // cmp ip, r0
        0xBA, 0x2B, 0xD2, 0x11, // ldrhne r2, [r2, #0xba]
        0x00, 0x00, 0x52, 0x11, // cmpne r2, r0
        0x06, 0x00, 0x00, 0x0A, // beq
        0x02, 0x10, 0x81, 0xE2, // add r1, r1, #2
        0x06, 0x00, 0x51, 0xE3, // cmp r1, #6
        0xF6, 0xFF, 0xFF, 0x3A, // blo
        0x01, 0x1C, 0x40, 0xE2, // sub r1, r0, #0x100
        0x23, 0x10, 0x51, 0xE2, // subs r1, r1, #0x23
        0x00, 0x00, 0xA0, 0x13, // movne r0, #0
        0x00, 0x00, 0x00, 0x1A, // bne
        0x01, 0x00, 0xA0, 0xE3, // mov r0, #1
        0x1E, 0xFF, 0x2F, 0xE1, // bx lr
        0xEE, 0x67, 0x5A, 0x00, // pointer to TM/HM table
    ];

        byte[] patch =
        [
            0x00, 0x00, 0xA0, 0xE3, // mov r0, #0
        0x1E, 0xFF, 0x2F, 0xE1, // bx lr
    ];

        int patchOffset = IndexOfBytes(data, original);

        if (patchOffset < 0)
        {
            // Por si ya está parcheado en el offset conocido de ORAS.
            const int knownORASOffset = 0x2B7090;

            if (data.Length > knownORASOffset + patch.Length &&
                data.Skip(knownORASOffset).Take(patch.Length).SequenceEqual(patch))
            {
                return true;
            }

            return false;
        }

        Array.Copy(patch, 0, data, patchOffset, patch.Length);
        return true;
    }
    private bool PatchForgettableHMsXY()
    {
        byte[] original =
        [
            0x0F, 0x00, 0x50, 0xE3, // cmp r0, #15  / Cut
        0x13, 0x00, 0x50, 0x13, // cmpne r0, #19 / Fly
        0x05, 0x00, 0x00, 0x0A, // beq true

        0x39, 0x00, 0x50, 0xE3, // cmp r0, #57 / Surf
        0x46, 0x00, 0x50, 0x13, // cmpne r0, #70 / Strength
        0x02, 0x00, 0x00, 0x0A, // beq true

        0x7F, 0x00, 0x50, 0xE3, // cmp r0, #127 / Waterfall
        0x00, 0x00, 0xA0, 0x13, // movne r0, #0
        0x00, 0x00, 0x00, 0x1A, // bne return

        0x01, 0x00, 0xA0, 0xE3, // mov r0, #1
        0x1E, 0xFF, 0x2F, 0xE1, // bx lr
    ];

        byte[] patch =
        [
            0x00, 0x00, 0xA0, 0xE3, // mov r0, #0
        0x1E, 0xFF, 0x2F, 0xE1, // bx lr
    ];

        int patchOffset = IndexOfBytes(data, original);

        if (patchOffset < 0)
        {
            const int knownXYOffset = 0x29F434;

            if (data.Length > knownXYOffset + patch.Length &&
                data.Skip(knownXYOffset).Take(patch.Length).SequenceEqual(patch))
            {
                return true;
            }

            return false;
        }

        Array.Copy(patch, 0, data, patchOffset, patch.Length);
        return true;
    }
    private static int IndexOfBytes(byte[] source, byte[] pattern)
    {
        if (pattern.Length == 0 || source.Length < pattern.Length)
            return -1;

        for (int i = 0; i <= source.Length - pattern.Length; i++)
        {
            bool match = true;

            for (int j = 0; j < pattern.Length; j++)
            {
                if (source[i + j] == pattern[j])
                    continue;

                match = false;
                break;
            }

            if (match)
                return i;
        }

        return -1;
    }
    private void GetDataOffset()
    {
        dataoffset = offset; // reset
    }

    private void SetupDGV()
    {
        dgvTM.Columns.Clear(); dgvHM.Columns.Clear();
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
        dgvHM.Columns.Add((DataGridViewColumn)dgvIndex.Clone());
        dgvHM.Columns.Add((DataGridViewColumn)dgvMove.Clone());
    }

    private List<ushort> tms = [];
    private List<ushort> hms = [];

    private void GetList()
    {
        tms = [];
        hms = [];
        dgvTM.Rows.Clear();

        GetDataOffset();
        for (int i = 0; i < 92; i++) // 1-92 TMs stored sequentially
            tms.Add(BitConverter.ToUInt16(data, dataoffset + (2 * i)));
        for (int i = 92; i < 92 + 5; i++)
            hms.Add(BitConverter.ToUInt16(data, dataoffset + (2 * i)));
        if (Main.Config.ORAS)
        {
            hms.Add(BitConverter.ToUInt16(data, dataoffset + (2 * 97)));
            for (int i = 98; i < 106; i++)
                tms.Add(BitConverter.ToUInt16(data, dataoffset + (2 * i)));
            hms.Add(BitConverter.ToUInt16(data, dataoffset + (2 * 106)));
        }
        else
        {
            for (int i = 97; i < 105; i++)
                tms.Add(BitConverter.ToUInt16(data, dataoffset + (2 * i)));
        }

        ushort[] tmlist = [.. tms];
        ushort[] hmlist = [.. hms];
        for (int i = 0; i < tmlist.Length; i++)
        { dgvTM.Rows.Add(); dgvTM.Rows[i].Cells[0].Value = (i + 1).ToString(); dgvTM.Rows[i].Cells[1].Value = movelist[tmlist[i]]; }
        for (int i = 0; i < hmlist.Length; i++)
        { dgvHM.Rows.Add(); dgvHM.Rows[i].Cells[0].Value = (i + 1).ToString(); dgvHM.Rows[i].Cells[1].Value = movelist[hmlist[i]]; }
    }

    private void SetList()
    {
        // Gather TM/HM list.
        tms = [];
        hms = [];
        for (int i = 0; i < dgvTM.Rows.Count; i++)
            tms.Add((ushort)Array.IndexOf(movelist, dgvTM.Rows[i].Cells[1].Value));

        for (int i = 0; i < dgvHM.Rows.Count; i++)
            hms.Add((ushort)Array.IndexOf(movelist, dgvHM.Rows[i].Cells[1].Value));

        ushort[] tmlist = [.. tms];
        ushort[] hmlist = [.. hms];

        // Set TM/HM list in
        for (int i = 0; i < 92; i++)
            Array.Copy(BitConverter.GetBytes(tmlist[i]), 0, data, offset + (2 * i), 2);
        for (int i = 92; i < 92 + 5; i++)
            Array.Copy(BitConverter.GetBytes(hmlist[i - 92]), 0, data, offset + (2 * i), 2);
        if (Main.Config.ORAS)
        {
            Array.Copy(BitConverter.GetBytes(hmlist[5]), 0, data, offset + (2 * 97), 2);
            for (int i = 98; i < 106; i++)
                Array.Copy(BitConverter.GetBytes(tmlist[i - 6]), 0, data, offset + (2 * i), 2);
            Array.Copy(BitConverter.GetBytes(hmlist[6]), 0, data, offset + (2 * 106), 2);
        }
        else
        {
            for (int i = 97; i < 105; i++)
                Array.Copy(BitConverter.GetBytes(tmlist[i - 5]), 0, data, offset + (2 * i), 2);
        }

        // Set Move Text Descriptions back into Item Text File
        string[] itemDescriptions = Main.Config.GetText(TextName.ItemFlavor);
        string[] moveDescriptions = Main.Config.GetText(TextName.MoveFlavor);
        for (int i = 1 - 1; i <= 92 - 1; i++) // TM01 - TM92
            itemDescriptions[328 + i] = moveDescriptions[tmlist[i]];
        for (int i = 93 - 1; i <= 95 - 1; i++) // TM92 - TM95
            itemDescriptions[618 + i - 92] = moveDescriptions[tmlist[i]];
        for (int i = 96 - 1; i <= 100 - 1; i++) // TM96 - TM100
            itemDescriptions[690 + i - 95] = moveDescriptions[tmlist[i]];
        for (int i = 1 - 1; i <= 5 - 1; i++) // HM01 - HM05
            itemDescriptions[420 + i] = moveDescriptions[hmlist[i]];
        if (Main.Config.ORAS)
        {
            itemDescriptions[425] = moveDescriptions[hmlist[5]]; // HM06
            itemDescriptions[737] = moveDescriptions[hmlist[6]]; // HM07
        }
        Main.Config.SetText(TextName.ItemFlavor, itemDescriptions);
    }

    private void Form_Closing(object sender, FormClosingEventArgs e)
    {
        SetList();
        File.WriteAllBytes(codebin, data);
        RandSettings.SetFormSettings(this, groupBox1.Controls);
    }

    private void B_RandomTM_Click(object sender, EventArgs e)
    {
        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Randomize TMs? Cannot undo.", "Move compatibility will be the same as the base TMs.") != DialogResult.Yes) return;
        if (CHK_RandomizeHM.Checked && WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Randomizing HMs can halt story progression!", "Continue anyway?") != DialogResult.Yes) return;

        int[] randomMoves = Enumerable.Range(1, movelist.Length - 1).Select(i => i).ToArray();
        Util.Shuffle(randomMoves);

        int[] hm_xy = [015, 019, 057, 070, 127];
        int[] hm_ao = [.. hm_xy, .. new[] { 249, 291 }];
        int[] field = [148, 249, 290]; // TMs with field effects
        int[] banned = [165, 621]; // Struggle and Hyperspace Fury
        int ctr = 0;

        for (int i = 0; i < dgvTM.Rows.Count; i++)
        {
            // randomize all TMs
            if (CHK_RandomizeField.Checked)
            {
                while (banned.Contains(randomMoves[ctr])) ctr++;
                dgvTM.Rows[i].Cells[1].Value = movelist[randomMoves[ctr++]];
            }

            // randomize all TMs, no Field Moves
            else
            {
                int val = Array.IndexOf(movelist, dgvTM.Rows[i].Cells[1].Value);
                if (hm_xy.Contains(val) || hm_ao.Contains(val) || field.Contains(val)) continue; // skip banned moves
                while (hm_xy.Contains(randomMoves[ctr]) || hm_ao.Contains(randomMoves[ctr]) || field.Contains(randomMoves[ctr]) || banned.Contains(randomMoves[ctr])) ctr++;
                dgvTM.Rows[i].Cells[1].Value = movelist[randomMoves[ctr++]];
            }
        }

        if (CHK_RandomizeHM.Checked)
        {
            for (int j = 0; j < dgvHM.Rows.Count; j++)
            {
                while (banned.Contains(randomMoves[ctr])) ctr++;
                dgvHM.Rows[j].Cells[1].Value = movelist[randomMoves[ctr++]];
            }
        }
        RandomizationSessionState.MarkAction("tms.randomize");
        WinFormsUtil.Alert("Randomized!");
    }

    internal static void GetTMHMList(out ushort[] TMs, out ushort[] HMs)
    {
        TMs = [];
        HMs = [];
        if (Main.ExeFSPath == null) return;
        string[] files = Directory.GetFiles(Main.ExeFSPath);
        if (!File.Exists(files[0]) || !Path.GetFileNameWithoutExtension(files[0]).Contains("code")) return;
        byte[] data = File.ReadAllBytes(files[0]);
        int dataoffset = Util.IndexOfBytes(data, Signature, 0x400000, 0) + 8;
        if (data.Length % 0x200 != 0) return;

        List<ushort> tms = [];
        List<ushort> hms = [];

        for (int i = 0; i < 92; i++) // 1-92 TMs stored sequentially
            tms.Add(BitConverter.ToUInt16(data, dataoffset + (2 * i)));
        for (int i = 92; i < 92 + 5; i++)
            hms.Add(BitConverter.ToUInt16(data, dataoffset + (2 * i)));
        if (Main.Config.ORAS)
        {
            hms.Add(BitConverter.ToUInt16(data, dataoffset + (2 * 97)));
            for (int i = 98; i < 106; i++)
                tms.Add(BitConverter.ToUInt16(data, dataoffset + (2 * i)));
            hms.Add(BitConverter.ToUInt16(data, dataoffset + (2 * 106)));
        }
        else
        {
            for (int i = 97; i < 105; i++)
                tms.Add(BitConverter.ToUInt16(data, dataoffset + (2 * i)));
        }

        TMs = [.. tms];
        HMs = [.. hms];
    }
}