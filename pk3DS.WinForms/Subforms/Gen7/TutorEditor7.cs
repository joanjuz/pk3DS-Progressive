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
        B_Randomize.Visible = false;
        AddFreeTutorsButton();

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
            Location = new System.Drawing.Point(B_Randomize.Left, B_Randomize.Top),
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

    private readonly string[] locationsTutor =
    [
        "Big Wave Beach",
        "Heahea Beach",
        "Ula'ula Beach",
        "Battle Tree",
    ];

    private void B_Save_Click(object sender, EventArgs e)
    {
        if (entryBPMove > -1) SetListBPMove();
        File.WriteAllBytes(CROPath, data);

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
        WinFormsUtil.Alert("Not currently implemented.");
    }
}