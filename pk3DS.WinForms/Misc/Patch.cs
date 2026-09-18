using System;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace pk3DS.WinForms;

public partial class Patch : Form
{
    public Patch()
    {
        InitializeComponent();
        ConfigureModernLayout();
        RTB_GARCs.Clear();
        CHKLB_GARCs.Items.Clear();
        foreach (string s in Main.Config.Files.Select(file => file.Name))
            CHKLB_GARCs.Items.Add(s);

        if (File.Exists("patch.ini"))
            RTB_GARCs.Lines = File.ReadAllLines("patch.txt", Encoding.Unicode);
    }

    private void ConfigureModernLayout()
    {
        Text = "Patch Manager · Utilities";
        MinimumSize = new Size(680, 430);
        ClientSize = new Size(680, 430);

        CHKLB_GARCs.Location = new Point(16, 38);
        CHKLB_GARCs.Size = new Size(210, 300);

        var title = new Label
        {
            Text = "Patch Manager",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
            Location = new Point(16, 12),
        };
        Controls.Add(title);

        label1.Location = new Point(248, 38);
        textBox1.Location = new Point(248, 58);
        textBox1.Width = 160;
        CHK_Lang.Location = new Point(248, 90);

        label2.Location = new Point(248, 122);
        RTB_GARCs.Location = new Point(248, 142);
        RTB_GARCs.Size = new Size(160, 196);

        B_CheckAll.Location = new Point(16, 350);
        B_CheckAll.Size = new Size(100, 30);
        B_CheckNone.Location = new Point(126, 350);
        B_CheckNone.Size = new Size(100, 30);

        var fieldGroup = new GroupBox
        {
            Text = "Field Items",
            Location = new Point(430, 38),
            Size = new Size(230, 164),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };

        var fieldInfo = new Label
        {
            Text = "Dump, shuffle, or randomize visible and hidden pickups using the field_items.txt template.",
            Location = new Point(14, 26),
            Size = new Size(198, 48),
        };

        Controls.Remove(B_DumpFieldItems);
        Controls.Remove(B_RandomizeFieldItems);
        B_DumpFieldItems.Location = new Point(14, 82);
        B_DumpFieldItems.Size = new Size(198, 32);
        B_RandomizeFieldItems.Location = new Point(14, 120);
        B_RandomizeFieldItems.Size = new Size(198, 32);

        fieldGroup.Controls.Add(fieldInfo);
        fieldGroup.Controls.Add(B_DumpFieldItems);
        fieldGroup.Controls.Add(B_RandomizeFieldItems);
        Controls.Add(fieldGroup);

        var rebuildGroup = new GroupBox
        {
            Text = "Build",
            Location = new Point(430, 214),
            Size = new Size(230, 92),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };

        Controls.Remove(B_PatchCIA);
        B_PatchCIA.Location = new Point(14, 34);
        B_PatchCIA.Size = new Size(198, 32);
        rebuildGroup.Controls.Add(B_PatchCIA);
        Controls.Add(rebuildGroup);
    }

    internal static bool PatchExeFS(string path, string[] oldstr, string[] newstr, string oldROM, string newROM, ref string result, string outPath = null)
    {
        int ctr = 0;
        if (oldstr.Length != newstr.Length)
        {
            result = "Input replacements do not match output replacements.";
            return false;
        }

        string text = File.ReadAllText(path, Encoding.Unicode);
        if (!text.Contains(newROM))
        {
            result = "ExeFS\\.code.bin is not a patchable ExeFS (no rom2: found).";
            return false;
        }
        for (int i = 0; i < oldstr.Length; i++)
        {
            string oldString = (oldROM + oldstr[i]).Replace(Path.DirectorySeparatorChar, '/');
            string patchedStr = (newROM + oldstr[i]).Replace(Path.DirectorySeparatorChar, '/');
            string newString = (newROM + newstr[i]).Replace(Path.DirectorySeparatorChar, '/');

            bool old = text.Contains(oldString);
            bool patched = text.Contains(patchedStr);
            if (!old && !patched)
                result += "Does not contain " + oldstr + Environment.NewLine;
            else
                ctr++;

            if (old)
                text = text.Replace(oldString, newString);
            if (patched)
                text = text.Replace(patchedStr, newString + "\0");
        }

        if (ctr == 0)
        { result = "Did not find the old path strings to replace."; return false; }
        result += $"Redirected {ctr} file paths.";
        Directory.CreateDirectory(Directory.GetParent(outPath).Name);
        File.WriteAllText(outPath ?? path, text, Encoding.Unicode);
        return true;
    }

    internal static string ExportGARCs(string[] garcPaths, string[] newPaths, string parentRomFS, string patchFolder)
    {
        // Stuff files into new patch folder
        for (int i = 0; i < garcPaths.Length; i++)
        {
            if ((garcPaths[i] ?? "").Length == 0) continue;
            string oldPath = parentRomFS + garcPaths[i];
            string newPath = patchFolder + newPaths[i];
            string folder = Path.GetDirectoryName(newPath);
            Directory.CreateDirectory(folder);
            File.Copy(oldPath, newPath);
        }
        return patchFolder;
    }

    private void B_PatchCIA_Click(object sender, EventArgs e)
    {
        string patchFolder = $"Patch ({DateTime.Now:yy-MM-dd@HH-mm-ss})";
        try
        {
            string[] garcs = GetGARCs();
            string[] garcPaths = GetPaths(garcs);

            const string oldROM = "rom:";
            const string newROM = "rom2:";
            const string oldA = "\\a\\";
            const string newA = "\\a";

            string[] newPaths = (string[])garcPaths.Clone();

            // Patch the reference
            for (int i = 0; i < newPaths.Length; i++)
            {
                int posA = newPaths[i].LastIndexOf(oldA, StringComparison.Ordinal);
                newPaths[i] = posA == -1 ? null : newPaths[i].Remove(posA, oldA.Length).Insert(posA, newA);
            }
            string result = "";
            string ExeFS = Directory.GetFiles(Main.ExeFSPath)[0];
            if (!File.Exists(ExeFS) || !Path.GetFileNameWithoutExtension(ExeFS).Contains("code")) { throw new Exception("No .code.bin detected."); }
            if (!PatchExeFS(ExeFS, garcPaths, newPaths, oldROM, newROM, ref result, Path.Combine(patchFolder, ".code.bin")))
                throw new Exception(result);

            WinFormsUtil.Alert("Patch contents saved to:" + Environment.NewLine + ExportGARCs(garcPaths, newPaths, Main.RomFSPath, patchFolder), result);
        }
        catch (Exception ex)
        {
            WinFormsUtil.Error("Could not create patch:", ex.ToString());
            if (Directory.Exists(patchFolder)) Directory.Delete(patchFolder, true);
        }
    }



    private void B_DumpFieldItems_Click(object sender, EventArgs e)
    {
        try
        {
            var result = FieldItemDumper.DumpCsv();
            using var sfd = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"field_items_dump_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            };

            if (sfd.ShowDialog(this) != DialogResult.OK)
                return;

            File.WriteAllText(sfd.FileName, result.Csv, new UTF8Encoding(false));
            WinFormsUtil.Alert($"Dumped {result.Count} field item entries.", sfd.FileName);
        }
        catch (Exception ex)
        {
            WinFormsUtil.Error("Could not dump field items:", ex.ToString());
        }
    }



    private async void B_RandomizeFieldItems_Click(object sender, EventArgs e)
    {
        try
        {
            if (DialogResult.Yes != WinFormsUtil.Prompt(
                MessageBoxButtons.YesNo,
                "Randomize field items?",
                "This will shuffle detected field items in-place.\n\nDefault safe mode:\n- Visible + hidden items are included.\n- Mega Stones are included in the normal item pool.\n- TMs/HMs are excluded because pk3DS already randomizes TMs.\n- Key/story-like items are excluded.\n- The suspicious repeated ORAS Potion tail is skipped.\n\nMake a backup before continuing."))
                return;

            SetFieldItemButtonsEnabled(false);
            UseWaitCursor = true;
            Text = "Patch Manager · Randomizing field items...";

            var result = await Task.Run(FieldItemDumper.RandomizeDefault);
            RandomizationSessionState.MarkAction(
                "field-items.randomize",
                ("template", Path.GetFileName(FieldItemRandomizerTemplate.DefaultPath)));
            WinFormsUtil.Alert("Field items randomized.", result.Summary);
        }
        catch (Exception ex)
        {
            WinFormsUtil.Error("Could not randomize field items:", ex.ToString());
        }
        finally
        {
            UseWaitCursor = false;
            Text = "Patch Manager · Utilities";
            SetFieldItemButtonsEnabled(true);
        }
    }

    private void SetFieldItemButtonsEnabled(bool enabled)
    {
        B_DumpFieldItems.Enabled = enabled;
        B_RandomizeFieldItems.Enabled = enabled;
    }
    private string[] GetGARCs()
    {
        var sc = new StringCollection();
        foreach (int indexChecked in CHKLB_GARCs.CheckedIndices)
            sc.Add(CHKLB_GARCs.Items[indexChecked].ToString());

        string[] rtbLines = RTB_GARCs.Lines;
        foreach (string s in rtbLines.Where(s => s.Length == 7 && !sc.Contains(s.Replace('/', Path.DirectorySeparatorChar))))
            sc.Add(s.Replace('/', Path.DirectorySeparatorChar));

        string[] garcs = new string[sc.Count];
        sc.CopyTo(garcs, 0);
        return garcs.Distinct().ToArray();
    }

    private string[] GetPaths(string[] sc)
    {
        bool languages = CHK_Lang.Checked;
        var paths = new StringCollection();
        foreach (string s in sc)
        {
            if (!languages || (s != "gametext" && s != "storytext"))
            {
                paths.Add(Main.GetGARCFileName(s, Main.Language));
            }
            else
            {
                for (int l = 0; l < 8; l++)
                    paths.Add(Main.GetGARCFileName(s, l));
            }
        }

        string[] garcs = new string[paths.Count];
        paths.CopyTo(garcs, 0);
        return garcs;
    }

    private void B_CheckAll_Click(object sender, EventArgs e)
    {
        for (int i = 0; i < CHKLB_GARCs.Items.Count; i++)
            CHKLB_GARCs.SetItemChecked(i, true);
    }

    private void B_CheckNone_Click(object sender, EventArgs e)
    {
        for (int i = 0; i < CHKLB_GARCs.Items.Count; i++)
            CHKLB_GARCs.SetItemChecked(i, false);
    }

    private void SavePatch(object sender, FormClosingEventArgs e)
    {
        if (RTB_GARCs.Text.Length > 0)
        {
            try { File.WriteAllLines("patch.ini", RTB_GARCs.Lines, Encoding.Unicode); } catch { }
        }
    }
}
