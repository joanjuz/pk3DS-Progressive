using pk3DS.Core;
using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace pk3DS.WinForms;

public partial class TutorEditor7 : Form
{
    private readonly string CROPath = Path.Combine(Main.RomFSPath, "Shop.cro");

    public TutorEditor7()
    {
        if (!File.Exists(CROPath))
        {
            WinFormsUtil.Error("CRO does not exist! Closing.", CROPath);
            Close();
        }
        InitializeComponent();
        B_Randomize.Visible = Main.Config.USUM;
        AddFreeTutorsButton();
        AddTutorSanityCheckBox();

        data = File.ReadAllBytes(CROPath);
        len_BPTutor = data.Skip(0x52D2).Take(4).ToArray();

        SetupDGV();
        CB_LocationBPMove.Items.AddRange(locationsTutor);
        CB_LocationBPMove.SelectedIndex = 0;
    }

    private void AddFreeTutorsButton()
    {
        B_FreeTutors = new Button
        {
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
            Location = new System.Drawing.Point(B_Randomize.Right + 6, B_Randomize.Top),
            Name = "B_FreeTutors",
            Size = new System.Drawing.Size(80, B_Randomize.Height),
            TabIndex = B_Randomize.TabIndex,
            Text = "Free Tutors",
            UseVisualStyleBackColor = true,
        };

        B_FreeTutors.Click += B_FreeTutors_Click;
        Controls.Add(B_FreeTutors);
        B_FreeTutors.BringToFront();
    }

    private const int ofs_BPTutor = 0x54DE;
    private readonly byte[] len_BPTutor;

    private readonly string[] movelist = Main.Config.GetText(TextName.MoveNames);
    private readonly byte[] data;
    private Button B_FreeTutors;
    private bool freeTutorsOnSave;
    private bool randomizeTutorsOnSave;
    private CheckBox CHK_TutorSanity;

    // Fixed Gen 7 special/type tutors represented by PersonalInfo.TypeTutors bits.
    private static readonly ushort[] TypeTutorMoves =
    [
        520, 519, 518, 338, 307, 308, 434, 620,
    ];

    private readonly string[] locationsTutor =
    [
        "Big Wave Beach",
        "Heahea Beach",
        "Ula'ula Beach",
        "Battle Tree",
    ];

    private void AddTutorSanityCheckBox()
    {
        // Make room for this option and for the later Follow Evolutions checkbox.
        const int minimumClientWidth = 520;
        if (ClientSize.Width < minimumClientWidth)
            ClientSize = new System.Drawing.Size(minimumClientWidth, ClientSize.Height);

        MinimumSize = new System.Drawing.Size(
            Math.Max(MinimumSize.Width, Width),
            MinimumSize.Height);

        CHK_TutorSanity = new CheckBox
        {
            Name = "CHK_TutorSanity",
            AutoSize = true,
            Location = new System.Drawing.Point(B_FreeTutors.Right + 14, B_FreeTutors.Top + 3),
            Text = "Tutor Sanity",
            UseVisualStyleBackColor = true,
        };

        CHK_TutorSanity.CheckedChanged += CHK_TutorSanity_CheckedChanged;
        Controls.Add(CHK_TutorSanity);
        CHK_TutorSanity.BringToFront();
    }

    private void CHK_TutorSanity_CheckedChanged(object sender, EventArgs e)
    {
        if (CHK_TutorSanity.Checked)
            RandomizationSessionState.MarkAction("move-tutors.sanity");
        else
            RandomizationSessionState.RemoveAction("move-tutors.sanity");
    }
    private void B_Save_Click(object sender, EventArgs e)
    {
        if (entryBPMove > -1)
            SetListBPMove();

        if (CHK_TutorSanity?.Checked == true)
            ApplyTutorSanity();

        File.WriteAllBytes(CROPath, data);

        if (randomizeTutorsOnSave)
            RandomizationSessionState.MarkAction("move-tutors.randomize");

        if (freeTutorsOnSave)
            RandomizationSessionState.MarkAction("move-tutors.free", ("price", "0"));

        Close();
    }

    private void B_Cancel_Click(object sender, EventArgs e) => Close();

    private void SetupDGV()
    {
        dgvmvMove.Items.AddRange(movelist); // add only the Names
    }

    private int entryBPMove = -1;

    private void ChangeIndexBPMove(object sender, EventArgs e)
    {
        if (entryBPMove > -1) SetListBPMove();
        entryBPMove = CB_LocationBPMove.SelectedIndex;
        GetListBPMove();
    }

    private void GetListBPMove()
    {
        dgvmv.Rows.Clear();
        int count = len_BPTutor[entryBPMove];
        dgvmv.Rows.Add(count);
        var ofs = ofs_BPTutor + (len_BPTutor.Take(entryBPMove).Sum(z => z) * 4);
        for (int i = 0; i < count; i++)
        {
            dgvmv.Rows[i].Cells[0].Value = i.ToString();
            dgvmv.Rows[i].Cells[1].Value = movelist[BitConverter.ToUInt16(data, ofs + (4 * i))];
            dgvmv.Rows[i].Cells[2].Value = BitConverter.ToUInt16(data, ofs + (4 * i) + 2).ToString();
        }
    }

    private void SetListBPMove()
    {
        int count = dgvmv.Rows.Count;
        var ofs = ofs_BPTutor + (len_BPTutor.Take(entryBPMove).Sum(z => z) * 4);
        for (int i = 0; i < count; i++)
        {
            int item = Array.IndexOf(movelist, dgvmv.Rows[i].Cells[1].Value);
            Array.Copy(BitConverter.GetBytes((ushort)item), 0, data, ofs + (4 * i), 2);
            string p = dgvmv.Rows[i].Cells[2].Value.ToString();
            if (int.TryParse(p, out var price))
                Array.Copy(BitConverter.GetBytes((ushort)price), 0, data, ofs + (4 * i) + 2, 2);
        }
    }

    private void B_FreeTutors_Click(object sender, EventArgs e)
    {
        if (DialogResult.Yes != WinFormsUtil.Prompt(
            MessageBoxButtons.YesNo,
            "Set every USUM Move Tutor price to 0 BP?",
            "All four Move Tutor locations will become free. Move selections are not changed."))
        {
            return;
        }

        if (entryBPMove > -1)
            SetListBPMove();

        for (int location = 0; location < len_BPTutor.Length; location++)
        {
            int count = len_BPTutor[location];
            int ofs = ofs_BPTutor + (len_BPTutor.Take(location).Sum(z => z) * 4);
            for (int i = 0; i < count; i++)
            {
                data[ofs + (4 * i) + 2] = 0;
                data[ofs + (4 * i) + 3] = 0;
            }
        }

        freeTutorsOnSave = true;

        if (entryBPMove > -1)
            GetListBPMove();

        WinFormsUtil.Alert(
            "Move Tutors are now free!",
            "Every Move Tutor price was set to 0 BP. Click Save to write Shop.cro.");
    }

    private void B_Randomize_Click(object sender, EventArgs e)
    {
        if (!Main.Config.USUM)
        {
            WinFormsUtil.Alert("Move Tutor randomization is currently supported for USUM only.");
            return;
        }

        if (DialogResult.Yes != WinFormsUtil.Prompt(
            MessageBoxButtons.YesNo,
            "Randomize all USUM Move Tutor moves?",
            "Tutor prices and the number of slots at each location will be preserved."))
        {
            return;
        }

        if (entryBPMove > -1)
            SetListBPMove();

        int[] banned = [.. Legal.Z_Moves, .. new[] { 165, 621, 166, 226 }];
        int[] randomMoves = Enumerable.Range(1, movelist.Length - 1)
            .Where(move => !banned.Contains(move) && !string.IsNullOrWhiteSpace(movelist[move]))
            .ToArray();

        int slotCount = len_BPTutor.Sum(z => z);
        if (randomMoves.Length < slotCount)
        {
            WinFormsUtil.Error(
                "Not enough valid moves to randomize every Tutor slot.",
                $"Valid moves: {randomMoves.Length}; Tutor slots: {slotCount}.");
            return;
        }

        Util.Shuffle(randomMoves);

        int moveIndex = 0;
        for (int location = 0; location < len_BPTutor.Length; location++)
        {
            int count = len_BPTutor[location];
            int ofs = ofs_BPTutor + (len_BPTutor.Take(location).Sum(z => z) * 4);

            for (int i = 0; i < count; i++)
            {
                ushort move = (ushort)randomMoves[moveIndex++];
                Array.Copy(BitConverter.GetBytes(move), 0, data, ofs + (4 * i), 2);
                // The following 2 bytes are the BP price and are intentionally preserved.
            }
        }

        randomizeTutorsOnSave = true;

        if (entryBPMove > -1)
            GetListBPMove();

        WinFormsUtil.Alert(
            "Move Tutors randomized!",
            $"{slotCount} unique Tutor moves were assigned. Prices were preserved. Click Save to write Shop.cro.");
    }

    private ushort[] GetCurrentTutorMoveList()
    {
        var result = new System.Collections.Generic.List<ushort>(len_BPTutor.Sum(z => z));

        for (int location = 0; location < len_BPTutor.Length; location++)
        {
            int count = len_BPTutor[location];
            int ofs = ofs_BPTutor + (len_BPTutor.Take(location).Sum(z => z) * 4);

            if (ofs < 0 || ofs + (count * 4) > data.Length)
                return [];

            for (int i = 0; i < count; i++)
                result.Add(BitConverter.ToUInt16(data, ofs + (4 * i)));
        }

        return [.. result];
    }

    private void ApplyTutorSanity()
    {
        if (!Main.Config.USUM)
        {
            if (!BatchRuntime.IsActive)
                WinFormsUtil.Alert("Tutor Sanity is currently supported for USUM only.");
            return;
        }

        ushort[] currentBeachTutors = GetCurrentTutorMoveList();
        int entryCount = Math.Min(Main.SpeciesStat.Length, Main.Config.Learnsets.Length);

        int compatibilityAdded = 0;
        int entriesChanged = 0;

        for (int i = 1; i < entryCount; i++)
        {
            var personal = Main.SpeciesStat[i];
            var learnset = Main.Config.Learnsets[i];

            if (personal is null || learnset is null)
                continue;

            var levelUpMoves = learnset.Moves.ToHashSet();
            bool changed = false;

            // Fixed Gen 7 special/type tutors.
            int typeTutorCount = Math.Min(TypeTutorMoves.Length, personal.TypeTutors.Length);
            for (int tutor = 0; tutor < typeTutorCount; tutor++)
            {
                int move = TypeTutorMoves[tutor];

                if (move <= 0 || personal.TypeTutors[tutor] || !levelUpMoves.Contains(move))
                    continue;

                personal.TypeTutors[tutor] = true;
                compatibilityAdded++;
                changed = true;
            }

            // USUM beach / BP tutors. Compatibility bits are slot-based.
            var specialTutors = personal.SpecialTutors;
            if (specialTutors.Length > 0 && specialTutors[0] is not null)
            {
                int beachTutorCount = Math.Min(currentBeachTutors.Length, specialTutors[0].Length);

                for (int tutor = 0; tutor < beachTutorCount; tutor++)
                {
                    int move = currentBeachTutors[tutor];

                    if (move <= 0 || specialTutors[0][tutor] || !levelUpMoves.Contains(move))
                        continue;

                    specialTutors[0][tutor] = true;
                    compatibilityAdded++;
                    changed = true;
                }

                personal.SpecialTutors = specialTutors;
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

        RandomizationSessionState.MarkAction("move-tutors.sanity");

        string detail = compatibilityAdded == 0
            ? "Tutor Sanity found no missing level-up Tutor compatibility."
            : $"Added {compatibilityAdded} Tutor compatibility flag(s) across {entriesChanged} Pokemon/form entr{(entriesChanged == 1 ? "y" : "ies")}.";

        if (BatchRuntime.IsActive)
            BatchRuntime.Log(detail);
        else
            WinFormsUtil.Alert("Tutor Sanity complete!", detail);
    }
    internal static ushort[] GetTutorMoveList()
    {
        if (!Main.Config.USUM || string.IsNullOrWhiteSpace(Main.RomFSPath))
            return [];

        string croPath = Path.Combine(Main.RomFSPath, "Shop.cro");
        if (!File.Exists(croPath))
            return [];

        byte[] tutorData = File.ReadAllBytes(croPath);
        const int countOffset = 0x52D2;
        const int tutorOffset = 0x54DE;

        if (tutorData.Length < countOffset + 4)
            return [];

        byte[] counts = tutorData.Skip(countOffset).Take(4).ToArray();
        var result = new System.Collections.Generic.List<ushort>(counts.Sum(z => z));

        for (int location = 0; location < counts.Length; location++)
        {
            int count = counts[location];
            int ofs = tutorOffset + (counts.Take(location).Sum(z => z) * 4);

            if (ofs < 0 || ofs + (count * 4) > tutorData.Length)
                return [];

            for (int i = 0; i < count; i++)
                result.Add(BitConverter.ToUInt16(tutorData, ofs + (4 * i)));
        }

        return [.. result];
    }
}