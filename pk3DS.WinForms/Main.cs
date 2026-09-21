/*----------------------------------------------------------------------------*/
/*--  This program is free software: you can redistribute it and/or modify  --*/
/*--  it under the terms of the GNU General Public License as published by  --*/
/*--  the Free Software Foundation, either version 3 of the License, or     --*/
/*--  (at your option) any later version.                                   --*/
/*--                                                                        --*/
/*--  This program is distributed in the hope that it will be useful,       --*/
/*--  but WITHOUT ANY WARRANTY; without even the implied warranty of        --*/
/*--  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the          --*/
/*--  GNU General Public License for more details.                          --*/
/*--                                                                        --*/
/*--  You should have received a copy of the GNU General Public License     --*/
/*--  along with this program. If not, see <http://www.gnu.org/licenses/>.  --*/
/*----------------------------------------------------------------------------*/

using pk3DS.Core;
using pk3DS.Core.CTR;
using pk3DS.Core.Structures.PersonalInfo;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace pk3DS.WinForms;

public sealed partial class Main : Form
{
    public Main()
    {
        // Initialize the Main Form
        InitializeComponent();
        EnsureCatchZonesButtonVisible();
        ConfigureModernDashboard();
        ConfigureGlobalTemplateMenu();

        // Prepare DragDrop Functionality
        AllowDrop = TB_Path.AllowDrop = true;
        DragEnter += TabMain_DragEnter;
        DragDrop += TabMain_DragDrop;
        TB_Path.DragEnter += TabMain_DragEnter;
        TB_Path.DragDrop += TabMain_DragDrop;
        foreach (var t in TC_RomFS.TabPages.OfType<TabPage>())
        {
            t.AllowDrop = true;
            t.DragEnter += TabMain_DragEnter;
            t.DragDrop += TabMain_DragDrop;
        }

        // Reload Previous Editing Files if the file exists
        var settings = Properties.Settings.Default;
        CB_Lang.SelectedIndex = settings.Language;
        var path = settings.GamePath;
        if (!string.IsNullOrWhiteSpace(path))
        {
            try
            {
                OpenQuick(path);
            }
            catch (Exception ex)
            {
                WinFormsUtil.Error($"Unable to automatically load the previously opened ROM dump located at -- {path}.", ex.Message);
                ResetStatus();
            }
        }

        string[] args = Environment.GetCommandLineArgs();
        string filename = args.Length > 0 ? Path.GetFileNameWithoutExtension(args[0]).ToLower() : "";
        skipBoth = filename.Contains("3DSkip");

        const string randset = RandSettings.FileName;
        if (File.Exists(randset))
            RandSettings.Load(File.ReadAllLines(randset));
    }

    private void ConfigureGlobalTemplateMenu()
    {
        var menu = new ToolStripMenuItem("Global ROM Template");
        menu.DropDownItems.Add("Load global template...", null, (_, _) => LoadGlobalRandomizationTemplate());
        menu.DropDownItems.Add("Save current setup...", null, (_, _) => SaveGlobalRandomizationTemplate());

        randomizationToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());
        randomizationToolStripMenuItem.DropDownItems.Add(menu);
        randomizationToolStripMenuItem.DropDownItems.Add(
            "Batch ROM Builder...", null, async (_, _) => await ShowBatchRomBuilder());
    }

    private string CurrentGlobalTemplateGame
    {
        get
        {
            if (Config is null)
                return string.Empty;
            if (Config.USUM)
                return "USUM";
            if (Config.SM)
                return "SM";
            if (Config.ORAS)
                return "ORAS";
            if (Config.XY)
                return "XY";
            return Config.Version.ToString();
        }
    }

    private void SaveGlobalRandomizationTemplate()
    {
        if (Config is null)
        {
            WinFormsUtil.Alert("Load a game before saving a global ROM template.");
            return;
        }

        try
        {
            Directory.CreateDirectory(GlobalRandomizationTemplateFile.TemplateDirectory);
            string game = CurrentGlobalTemplateGame;

            using var dialog = new SaveFileDialog
            {
                Title = "Save global ROM template",
                Filter = "Global ROM template (*.json)|*.json",
                InitialDirectory = GlobalRandomizationTemplateFile.TemplateDirectory,
                FileName = $"rom_template_{game.ToLowerInvariant()}.json",
                AddExtension = true,
                DefaultExt = "json",
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            string name = Path.GetFileNameWithoutExtension(dialog.FileName);
            var template = GlobalRandomizationTemplateFile.Capture(name, game, Config.Generation);
            GlobalRandomizationTemplateFile.Save(dialog.FileName, template, game);

            string trainer = template.Trainer is null ? "no trainer-specific state" : "trainer state included";
            string wild = template.Wild is null ? "no Wild Encounter state" : "Wild Encounter state included";
            string caps = template.LevelCaps is null ? "no Player Level Caps state" : "Player Level Caps state included";
            WinFormsUtil.Alert(
                "Global ROM template saved!",
                $"{Path.GetFileName(dialog.FileName)}\n{template.Actions.Count} recorded action(s); {trainer}; {wild}; {caps}.\n\nSaved in custom_balance_templates.");
        }
        catch (Exception ex)
        {
            WinFormsUtil.Alert("Could not save global ROM template.", ex.Message);
        }
    }

    private void LoadGlobalRandomizationTemplate()
    {
        if (Config is null)
        {
            WinFormsUtil.Alert("Load a game before loading a global ROM template.");
            return;
        }

        try
        {
            Directory.CreateDirectory(GlobalRandomizationTemplateFile.TemplateDirectory);
            string game = CurrentGlobalTemplateGame;

            using var dialog = new OpenFileDialog
            {
                Title = "Load global ROM template",
                Filter = "Global ROM template (*.json)|*.json|All files (*.*)|*.*",
                InitialDirectory = GlobalRandomizationTemplateFile.TemplateDirectory,
                CheckFileExists = true,
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            var template = GlobalRandomizationTemplateFile.Load(dialog.FileName, game);
            var warnings = GlobalRandomizationTemplateFile.Apply(template, game);

            string message =
                $"Loaded '{template.Name}'.\n" +
                $"Recorded actions: {template.Actions.Count}.\n" +
                $"Trainer state: {(template.Trainer is null ? "not included" : "included")}.\n" +
                $"Wild Encounter state: {(template.Wild is null ? "not included" : "included")}.\n" +
                $"Player Level Caps state: {(template.LevelCaps is null ? "not included" : "included")}.\n\n" +
                "Randomizer windows opened from now on will use the loaded settings.";

            if (warnings.Count != 0)
                message += "\n\nDependency warnings:\n- " + string.Join("\n- ", warnings);

            WinFormsUtil.Alert("Global ROM template loaded!", message);
        }
        catch (Exception ex)
        {
            WinFormsUtil.Alert("Could not load global ROM template.", ex.Message);
        }
    }

    private async Task ShowBatchRomBuilder()
    {
        if (Config is null || string.IsNullOrWhiteSpace(RomFSPath) || string.IsNullOrWhiteSpace(ExeFSPath) || string.IsNullOrWhiteSpace(ExHeaderPath))
        {
            WinFormsUtil.Alert("Load a complete extracted game (RomFS + ExeFS + ExHeader) before using Batch ROM Builder.");
            return;
        }

        if (Config.Generation != 7)
        {
            WinFormsUtil.Alert(
                "Batch ROM Builder v1 currently supports Generation 7 only.",
                "USUM is the primary supported target. Gen 6 can be added after this workflow is validated.");
            return;
        }

        string originalRoot = Directory.Exists(TB_Path.Text)
            ? Path.GetFullPath(TB_Path.Text)
            : new DirectoryInfo(RomFSPath).Parent?.FullName;
        if (string.IsNullOrWhiteSpace(originalRoot) || !Directory.Exists(originalRoot))
        {
            WinFormsUtil.Alert("Could not determine the loaded extracted-game root folder.");
            return;
        }

        string sourceParent = Directory.GetParent(originalRoot)?.FullName ?? originalRoot;
        string defaultOutput = Path.Combine(sourceParent, Path.GetFileName(originalRoot) + "_BatchROMs");
        Directory.CreateDirectory(GlobalRandomizationTemplateFile.TemplateDirectory);

        using var dialog = new BatchRomBuilderDialog(
            GlobalRandomizationTemplateFile.TemplateDirectory,
            CurrentGlobalTemplateGame,
            defaultOutput);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        BatchRomBuildOptions options = dialog.Options;
        if (IsSameOrChildPath(options.OutputDirectory, originalRoot))
        {
            WinFormsUtil.Alert(
                "Choose an output folder outside the loaded extracted-game folder.",
                "Otherwise generated .3ds files would be copied into later staging ROMs and waste a large amount of disk space.");
            return;
        }

        GlobalRandomizationTemplate template;
        string game = CurrentGlobalTemplateGame;
        try
        {
            template = GlobalRandomizationTemplateFile.Load(options.TemplatePath, game);
        }
        catch (Exception ex)
        {
            WinFormsUtil.Alert("Could not load the selected Global ROM Template.", ex.Message);
            return;
        }

        if (template.Generation != 7)
        {
            WinFormsUtil.Alert($"The selected template is Generation {template.Generation}; Batch ROM Builder v1 requires Generation 7.");
            return;
        }

        string[] unsupported = BatchGen7ActionExecutor.GetUnsupported(template.Actions);
        if (unsupported.Length != 0)
        {
            WinFormsUtil.Alert(
                "This template contains actions that Batch ROM Builder does not know how to replay yet:",
                string.Join(Environment.NewLine, unsupported.Select(z => "- " + z)),
                "Nothing was changed.");
            return;
        }

        if (template.Actions is null || template.Actions.Count == 0)
        {
            WinFormsUtil.Alert("The selected Global ROM Template contains no recorded actions to replay.");
            return;
        }

        List<string> dependencyWarnings;
        try
        {
            dependencyWarnings = GlobalRandomizationTemplateFile.Apply(template, game);
        }
        catch (Exception ex)
        {
            WinFormsUtil.Alert("The Global ROM Template could not be applied.", ex.Message);
            return;
        }

        if (dependencyWarnings.Count != 0)
        {
            string warningText = string.Join(Environment.NewLine, dependencyWarnings.Select(z => "- " + z));
            if (WinFormsUtil.Prompt(
                    MessageBoxButtons.YesNo,
                    "Some files used by this template have changed since it was saved:",
                    warningText,
                    "Continue using the current files anyway?") != DialogResult.Yes)
                return;
        }

        if (template.ActionCoverageVersion < 2 &&
            WinFormsUtil.Prompt(
                MessageBoxButtons.YesNo,
                $"This template has action coverage version {template.ActionCoverageVersion}.",
                "Version 2 or newer is recommended because older templates may not contain every operation that was used to build the ROM.",
                "Continue anyway?") != DialogResult.Yes)
        {
            return;
        }

        string buildKind = options.Trimmed ? "Trimmed .3DS" : "Full .3DS";
        if (WinFormsUtil.Prompt(
                MessageBoxButtons.YesNo,
                $"Build {options.Count} randomized ROM(s)?",
                $"Template: {Path.GetFileName(options.TemplatePath)}",
                $"Output: {options.OutputDirectory}",
                $"Build type: {buildKind}",
                "Each ROM will receive a different int32 seed and all recorded randomization actions will run again from the same clean staging source.") != DialogResult.Yes)
        {
            return;
        }

        Directory.CreateDirectory(options.OutputDirectory);
        string logPath = Path.Combine(options.OutputDirectory, options.BaseName + "_batch.txt");
        var log = new List<string>
        {
            "pk3DS Batch ROM Builder",
            $"Template: {options.TemplatePath}",
            $"Game: {game}",
            $"Build type: {buildKind}",
            $"ROM count: {options.Count}",
            $"Restore pk3DS backups: {options.RestoreBackups}",
            $"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            string.Empty,
        };
        File.WriteAllLines(logPath, log, Encoding.UTF8);

        GameConfig originalConfig = Config;
        string activeStage = null;
        var usedSeeds = new HashSet<int>();
        int completed = 0;

        Enabled = false;
        UseWaitCursor = true;

        try
        {
            for (int i = 1; i <= options.Count; i++)
            {
                int seed = CreateBatchSeed(usedSeeds);
                activeStage = Path.Combine(sourceParent, $".pk3ds_batch_{Guid.NewGuid():N}");
                string outputPath = Path.Combine(options.OutputDirectory, $"{options.BaseName}_{i:00}.3ds");

                UpdateStatus($"[Batch {i}/{options.Count}] Preparing clean staging copy...", false);
                int restored = await Task.Run(() =>
                    BatchWorkspace.PrepareCleanCopy(originalRoot, activeStage, originalConfig, options.RestoreBackups));

                using (BatchRuntime.Begin(message => UpdateStatus($"[Batch {i}/{options.Count}] {message}")))
                {
                    OpenQuick(activeStage);
                    if (Config is null || Config.Generation != 7 || !string.Equals(CurrentGlobalTemplateGame, game, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("The staging copy did not reopen as the same Generation 7 game.");

                    GlobalRandomizationTemplateFile.Apply(template, game);
                    Util.ReseedRand(seed);
                    BatchRuntime.Log($"Seed = {seed}");
                    BatchRuntime.Log($"Replaying {template.Actions.Count} action(s)...");
                    BatchGen7ActionExecutor.Execute(template);

                    if (File.Exists(outputPath))
                        File.Delete(outputPath);

                    UpdateStatus($"[Batch {i}/{options.Count}] Rebuilding {Path.GetFileName(outputPath)}...");
                    string exeFS = ExeFSPath;
                    string romFS = RomFSPath;
                    string exHeader = ExHeaderPath;
                    await Task.Run(() =>
                    {
                        var exh = new Exheader(exHeader);
                        CTRUtil.BuildROM(
                            true,
                            "Nintendo",
                            exeFS,
                            romFS,
                            exHeader,
                            exh.GetSerial(),
                            outputPath,
                            options.Trimmed,
                            pBar1,
                            RTB_Status);
                    });

                    if (!File.Exists(outputPath))
                        throw new IOException($"CTRUtil finished without creating '{outputPath}'.");

                    completed++;
                    log.Add($"ROM {i:00}");
                    log.Add($"Seed: {seed}");
                    log.Add($"File: {outputPath}");
                    log.Add($"Original backup files restored into staging: {restored}");
                    log.Add(string.Empty);
                    File.WriteAllLines(logPath, log, Encoding.UTF8);

                    // Reopen the untouched source before the next iteration. OpenQuick
                    // resets session state, so reapply the template to leave the normal
                    // pk3DS UI configured exactly as it was for the batch recipe.
                    OpenQuick(originalRoot);
                    if (Config is null)
                        throw new InvalidOperationException("Could not reopen the original extracted game after building a batch ROM.");
                    GlobalRandomizationTemplateFile.Apply(template, game);
                }

                string stageToDelete = activeStage;
                activeStage = null;
                try
                {
                    await Task.Run(() => BatchWorkspace.DeleteDirectoryBestEffort(stageToDelete));
                }
                catch (Exception cleanupEx)
                {
                    log.Add($"Warning: staging cleanup failed for {stageToDelete}: {cleanupEx.Message}");
                    File.WriteAllLines(logPath, log, Encoding.UTF8);
                    UpdateStatus($"[Batch {i}/{options.Count}] Warning: could not delete staging folder.");
                }
                UpdateStatus($"[Batch {i}/{options.Count}] Complete: {Path.GetFileName(outputPath)}");
            }

            log.Add($"Completed: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            File.WriteAllLines(logPath, log, Encoding.UTF8);
            WinFormsUtil.Alert(
                "Batch ROM build complete!",
                $"Created {completed} ROM(s).",
                $"Output folder: {options.OutputDirectory}",
                $"Seeds/log: {logPath}");
        }
        catch (Exception ex)
        {
            log.Add($"FAILED after {completed}/{options.Count} ROM(s): {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            log.Add(ex.ToString());
            try { File.WriteAllLines(logPath, log, Encoding.UTF8); } catch { }

            WinFormsUtil.Alert(
                "Batch ROM Builder stopped because an error occurred.",
                ex.Message,
                $"Completed ROMs were kept. Log: {logPath}");
        }
        finally
        {
            // A failure may have happened while a staging project was loaded. Always
            // return pk3DS to the user's original extracted game when possible before
            // deleting that staging folder.
            if (!string.Equals(Path.GetFullPath(TB_Path.Text ?? string.Empty), originalRoot, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using (BatchRuntime.Begin(message => UpdateStatus("[Batch cleanup] " + message)))
                    {
                        OpenQuick(originalRoot);
                        if (Config is not null)
                            GlobalRandomizationTemplateFile.Apply(template, game);
                    }
                }
                catch
                {
                    // The build log already contains the primary failure. Do not hide it
                    // with a secondary cleanup exception.
                }
            }

            if (!string.IsNullOrWhiteSpace(activeStage))
            {
                try { await Task.Run(() => BatchWorkspace.DeleteDirectoryBestEffort(activeStage)); }
                catch { }
            }

            UseWaitCursor = false;
            Enabled = true;
        }
    }

    private static int CreateBatchSeed(HashSet<int> usedSeeds)
    {
        while (true)
        {
            byte[] bytes = new byte[sizeof(int)];
            RandomNumberGenerator.Fill(bytes);
            int seed = BitConverter.ToInt32(bytes, 0);
            if (usedSeeds.Add(seed))
                return seed;
        }
    }

    private static bool IsSameOrChildPath(string candidatePath, string parentPath)
    {
        string candidate = Path.GetFullPath(candidatePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string parent = Path.GetFullPath(parentPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(candidate, parent, StringComparison.OrdinalIgnoreCase))
            return true;

        return candidate.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
               candidate.StartsWith(parent + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private void ConfigureModernDashboard()
    {
        Text = "pk3DS Progressive Randomizer";
        MinimumSize = new Size(740, 380);
        if (Width < 740 || Height < 380)
            Size = new Size(740, 380);

        L_Game.Text = "No game loaded";
        L_Game.ForeColor = ModernUI.Danger;
        TB_Path.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        TB_Path.Location = new Point(170, 6);
        TB_Path.Width = Math.Max(260, ClientSize.Width - 190);

        TC_RomFS.SizeMode = TabSizeMode.Normal;
        TC_RomFS.Appearance = TabAppearance.Normal;
        TC_RomFS.Location = new Point(12, 48);
        TC_RomFS.Size = new Size(ClientSize.Width - 24, ClientSize.Height - 86);

        Tab_RomFS.Text = "RomFS";
        Tab_ExeFS.Text = "ExeFS";
        Tab_CRO.Text = "CRO/Data";
        Tab_Output.Text = "Log";

        FormatLauncherButtons(FLP_RomFS);
        FormatLauncherButtons(FLP_ExeFS);
        FormatLauncherButtons(FLP_CRO);

        pBar1.Height = 16;
        pBar1.Location = new Point(12, ClientSize.Height - 28);
        pBar1.Width = ClientSize.Width - 24;

        Resize += (_, _) =>
        {
            TC_RomFS.Size = new Size(ClientSize.Width - 24, ClientSize.Height - 86);
            pBar1.Location = new Point(12, ClientSize.Height - 28);
            pBar1.Width = ClientSize.Width - 24;
            TB_Path.Width = Math.Max(260, ClientSize.Width - 190);
        };
    }

    private static void FormatLauncherButtons(FlowLayoutPanel panel)
    {
        if (panel == null)
            return;

        panel.WrapContents = true;
        panel.AutoScroll = true;
        panel.Padding = new Padding(14, 12, 14, 12);

        foreach (Button button in panel.Controls.OfType<Button>())
        {
            button.Size = new Size(138, 28);
            button.Margin = new Padding(4, 4, 4, 6);
            button.TextAlign = ContentAlignment.MiddleCenter;
        }
    }

    private void EnsureCatchZonesButtonVisible()
    {
        if (FLP_RomFS == null || B_CatchZones == null)
            return;

        B_CatchZones.Text = "Enable Catch Zones";
        B_CatchZones.Size = new System.Drawing.Size(138, 30);
        B_CatchZones.Visible = true;
        B_CatchZones.Enabled = true;

        if (FLP_RomFS.Controls.Contains(B_CatchZones))
            FLP_RomFS.Controls.Remove(B_CatchZones);

        int index = FLP_RomFS.Controls.Contains(B_Wild)
            ? FLP_RomFS.Controls.GetChildIndex(B_Wild) + 1
            : FLP_RomFS.Controls.Count;

        FLP_RomFS.Controls.Add(B_CatchZones);
        FLP_RomFS.Controls.SetChildIndex(B_CatchZones, Math.Min(index, FLP_RomFS.Controls.Count - 1));
    }

    internal static GameConfig Config;
    public static string RomFSPath;
    public static string ExeFSPath;
    public static string ExHeaderPath;
    private volatile int threads;
    internal static volatile int Language;
    internal static SMDH SMDH;
    private uint HANSgameID; // for exporting RomFS/ExeFS with correct X8 gameID
    private readonly bool skipBoth;
    public static PersonalInfo[] SpeciesStat => Config.Personal.Table;

    internal static void SaveGameText()
    {
        var g = Config.GARCGameText;
        string[][] files = Config.GameTextStrings;
        var originalFiles = g.Files;
        byte[][] serialized = files.Select(x => TextFile.GetBytes(Config, x)).ToArray();

        g.Files = serialized;
        try
        {
            g.Save();
        }
        catch
        {
            g.Files = originalFiles;
            throw;
        }
    }

    // Main Form Methods
    private void L_About_Click(object sender, EventArgs e)
    {
        new About().ShowDialog();
    }

    private void L_GARCInfo_Click(object sender, EventArgs e)
    {
        if (RomFSPath == null)
            return;

        string s = "Game Type: " + Config.Version + Environment.NewLine;
        s = Config.Files.Select(file => file.Name).Aggregate(s, (current, t) => current + string.Format(Environment.NewLine + "{0} - {1}", t, Config.GetGARCFileName(t)));

        var copyPrompt = WinFormsUtil.Prompt(MessageBoxButtons.YesNo, s, "Copy to Clipboard?");
        if (copyPrompt != DialogResult.Yes)
            return;

        try { Clipboard.SetText(s); }
        catch { WinFormsUtil.Alert("Unable to copy to Clipboard."); }
    }

    private void L_Game_Click(object sender, EventArgs e) => new EnhancedRestore(Config).ShowDialog();

    private void B_Open_Click(object sender, EventArgs e)
    {
        using var fbd = new FolderBrowserDialog();
        if (fbd.ShowDialog() == DialogResult.OK)
            OpenQuick(fbd.SelectedPath);
    }

    private void ChangeLanguage(object sender, EventArgs e)
    {
        if (InvokeRequired)
            Invoke((MethodInvoker)delegate { Language = CB_Lang.SelectedIndex; });
        else Language = CB_Lang.SelectedIndex;
        if (Config != null)
            Config.Language = Language;
        Menu_Options.DropDown.Close();
        if (!Tab_RomFS.Enabled || Config == null)
            return;

        if ((Config.XY || Config.ORAS) && Language > 7)
        {
            WinFormsUtil.Alert("Language not available for games. Defaulting to English.");
            if (InvokeRequired)
                Invoke((MethodInvoker)delegate { CB_Lang.SelectedIndex = 2; });
            else CB_Lang.SelectedIndex = 2;
            return; // set event re-triggers this method
        }

        UpdateProgramTitle();
        Config.InitializeGameText();
        Properties.Settings.Default.Language = Language;
        Properties.Settings.Default.Save();
    }

    private void Menu_Exit_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void CloseForm(object sender, FormClosingEventArgs e)
    {
        if (Config == null)
            return;
        var g = Config.GARCGameText;
        string[][] files = Config.GameTextStrings;
        g.Files = files.Select(x => TextFile.GetBytes(Config, x)).ToArray();
        g.Save();

        try
        {
            var text = RandSettings.Save();
            File.WriteAllLines(RandSettings.FileName, text, Encoding.Unicode);
        }
        catch
        {
            // ignored
        }
    }

    private void OpenQuick(string path)
    {
        if (ThreadActive())
            return;

        try
        {
            if (!Directory.Exists(path)) // File
                OpenFile(path);
            else // Directory
                OpenDirectory(path);
        }
        catch (Exception ex)
        {
            WinFormsUtil.Error($"Failed to open -- {path}", ex.Message);
            ResetStatus();
        }
    }

    private void OpenFile(string path)
    {
        if (!File.Exists(path))
            return;

        var fi = new FileInfo(path);
        if (fi.Name.Contains("code.bin")) // Compress/Decompress .code.bin
        {
            OpenExeFSCodeBinary(path, fi);
        }
        else if (fi.Name.Contains("exe", StringComparison.OrdinalIgnoreCase)) // Unpack exefs
        {
            OpenExeFSCombined(path, fi);
        }
        else if (fi.Name.Contains("rom", StringComparison.OrdinalIgnoreCase))
        {
            WinFormsUtil.Alert("RomFS unpacking not implemented.");
        }
        else
        {
            var dr = WinFormsUtil.Prompt(MessageBoxButtons.YesNoCancel, "Unpack sub-files?", "Cancel: Abort");
            if (dr == DialogResult.Cancel)
                return;
            bool recurse = dr == DialogResult.Yes;
            ToolsUI.OpenARC(path, pBar1, recurse);
        }
    }

    private void OpenExeFSCombined(string path, FileInfo fi)
    {
        if (fi.Length % 0x200 != 0)
            return;
        var dir = Path.GetDirectoryName(path);
        if (dir is null)
            return;

        var prompt = WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Detected ExeFS.bin.", "Unpack?");
        if (prompt != DialogResult.Yes)
            return;

        new Thread(() =>
        {
            Interlocked.Increment(ref threads);
            ExeFS.UnpackExeFS(path, dir);
            Interlocked.Decrement(ref threads);
            WinFormsUtil.Alert("Unpacked!");
        }).Start();
    }

    private void OpenExeFSCodeBinary(string path, FileInfo fi)
    {
        if (fi.Length % 0x200 == 0)
        {
            var prompt = WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Detected Decompressed code.bin.", "Compress? File will be replaced.");
            if (prompt != DialogResult.Yes)
                return;
            new Thread(() =>
            {
                Interlocked.Increment(ref threads);
                new BLZCoder(["-en", path], pBar1);
                Interlocked.Decrement(ref threads);
                WinFormsUtil.Alert("Compressed!");
            }).Start();
        }
        else
        {
            var prompt = WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Detected Compressed code.bin.", "Decompress? File will be replaced.");
            if (prompt != DialogResult.Yes)
                return;
            new Thread(() =>
            {
                Interlocked.Increment(ref threads);
                new BLZCoder(["-d", path], pBar1);
                Interlocked.Decrement(ref threads);
                WinFormsUtil.Alert("Decompressed!");
            }).Start();
        }
    }

    private void OpenDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;

        // Check for ROMFS/EXEFS/EXHEADER
        RomFSPath = ExeFSPath = null; // Reset
        Config = null;
        GlobalRandomizationTemplateFile.ResetSession();

        string[] folders = Directory.GetDirectories(path);
        int count = folders.Length;

        // Find RomFS folder
        foreach (string f in folders.Where(f => new DirectoryInfo(f).Name.Contains("rom", StringComparison.OrdinalIgnoreCase) && Directory.Exists(f)))
            CheckIfRomFS(f);
        // Find ExeFS folder
        foreach (string f in folders.Where(f => new DirectoryInfo(f).Name.Contains("exe", StringComparison.OrdinalIgnoreCase) && Directory.Exists(f)))
            CheckIfExeFS(f);

        if (count > 3)
            WinFormsUtil.Alert("pk3DS will function best if you keep your Game Files folder clean and free of unnecessary folders.");

        // Enable buttons if applicable
        Tab_RomFS.Enabled = Menu_Restore.Enabled = Tab_CRO.Enabled = Menu_CRO.Enabled = Menu_Shuffler.Enabled = RomFSPath != null;
        Tab_ExeFS.Enabled = RomFSPath != null && ExeFSPath != null;
        if (RomFSPath != null && Config != null)
        {
            ToggleSubEditors();
            string newtext = $"Game Loaded: {Config.Version}";
            if (L_Game.Text != newtext && Directory.Exists("personal"))
            {
                Directory.Delete("personal", true);
            } // Force reloading of personal data if the game is switched.

            L_Game.Text = newtext;
            TB_Path.Text = path;
        }
        else if (ExeFSPath != null)
        {
            L_Game.Text = "ExeFS loaded - no RomFS";
            TB_Path.Text = path;
        }
        else
        {
            L_Game.Text = "No Game Loaded";
            TB_Path.Text = "";
        }

        if (RomFSPath != null)
        {
            // Trigger Data Loading
            if (RTB_Status.Text.Length > 0)
                RTB_Status.Clear();

            UpdateStatus("Data found! Loading persistent data for subforms...", false);
            try
            {
                if (Config is not null)
                {
                    if (ExeFSPath is not null)
                        Config.Initialize(RomFSPath, ExeFSPath, Language);
                    Config.BackupFiles();
                }
            }
            catch (Exception ex)
            {
                WinFormsUtil.Error("Failed to load game data from romfs. Please double check your ROM dump is correct.", ex.Message);
                ResetStatus();
                return;
            }
        }

        UpdateProgramTitle();

        // Enable Rebuilding options if all files have been found
        CheckIfExHeader(path);
        Menu_ExeFS.Enabled = ExeFSPath != null;
        Menu_RomFS.Enabled = Menu_Restore.Enabled = Menu_GARCs.Enabled = RomFSPath != null;
        Menu_Patch.Enabled = RomFSPath != null && ExeFSPath != null;
        Menu_3DS.Enabled = RomFSPath != null && ExeFSPath != null && ExHeaderPath != null;
        Menu_Trimmed3DS.Enabled = RomFSPath != null && ExeFSPath != null && ExHeaderPath != null;

        // Change L_Game if RomFS and ExeFS exists to a better descriptor
        SMDH = ExeFSPath != null
            ? File.Exists(Path.Combine(ExeFSPath, "icon.bin")) ? new SMDH(Path.Combine(ExeFSPath, "icon.bin")) : null
            : null;
        HANSgameID = SMDH != null ? (SMDH.AppSettings?.StreetPassID ?? 0) : 0;
        L_Game.Visible = SMDH == null && RomFSPath != null;
        TB_Path.Select(TB_Path.TextLength, 0);
        // Method finished.
        System.Media.SystemSounds.Asterisk.Play();
        ResetStatus();
        Properties.Settings.Default.GamePath = path;
        Properties.Settings.Default.Save();
    }

    private void B_ExtractCXI_Click(object sender, EventArgs e)
    {
        const string l1 = "Extracting a CXI requires multiple GB of disc space and takes some time to complete.";
        const string l2 = "If you want to continue, press OK to select your CXI and then select your output directory. For best results, make sure the output directory is an empty directory.";
        var prompt = WinFormsUtil.Prompt(MessageBoxButtons.OKCancel, l1, l2);
        if (prompt != DialogResult.OK)
            return;

        using var ofd = new OpenFileDialog { Title = "Select CXI", Filter = "CXI files (*.cxi)|*.cxi" };
        if (ofd.ShowDialog() != DialogResult.OK)
            return;

        using var fbd = new FolderBrowserDialog();
        DialogResult result = fbd.ShowDialog();
        if (result != DialogResult.OK)
            return;

        var inputCXI = ofd.FileName;
        ExtractNCCH(inputCXI, fbd.SelectedPath);
    }

    private void B_Extract3DS_Click(object sender, EventArgs e)
    {
        const string l1 = "Extracting a 3DS file requires multiple GB of disc space and takes some time to complete.";
        const string l2 = "If you want to continue, press OK to select your CXI and then select your output directory. For best results, make sure the output directory is an empty directory.";
        var prompt = WinFormsUtil.Prompt(MessageBoxButtons.OKCancel, l1, l2);
        if (prompt != DialogResult.OK)
            return;

        using var ofd = new OpenFileDialog { Title = "Select 3DS", Filter = "3DS files (*.3ds)|*.3ds" };
        if (ofd.ShowDialog() != DialogResult.OK)
            return;

        using var fbd = new FolderBrowserDialog();
        DialogResult result = fbd.ShowDialog();
        if (result != DialogResult.OK)
            return;

        var input3DS = ofd.FileName;
        ExtractNCSD(input3DS, fbd.SelectedPath);
    }

    private void ExtractNCCH(string ncchPath, string outputDirectory)
    {
        if (!File.Exists(ncchPath))
            return;

        var ncch = new NCCH();

        new Thread(() =>
        {
            Interlocked.Increment(ref threads);
            ncch.ExtractNCCHFromFile(ncchPath, outputDirectory, RTB_Status, pBar1);
            Interlocked.Decrement(ref threads);
            WinFormsUtil.Alert("Extraction complete!");
        }).Start();
    }

    private void ExtractNCSD(string ncsdPath, string outputDirectory)
    {
        if (!File.Exists(ncsdPath))
            return;

        var ncsd = new NCSD();
        new Thread(() =>
        {
            Interlocked.Increment(ref threads);
            ncsd.ExtractFilesFromNCSD(ncsdPath, outputDirectory, RTB_Status, pBar1);
            Interlocked.Decrement(ref threads);
            WinFormsUtil.Alert("Extraction complete!");
        }).Start();
    }

    private void ToggleSubEditors()
    {
        // Hide all buttons
        foreach (var f in from TabPage t in TC_RomFS.TabPages from f in t.Controls.OfType<FlowLayoutPanel>() select f)
        {
            for (int i = f.Controls.Count - 1; i >= 0; i--)
                f.Controls.Remove(f.Controls[i]);
        }

        B_MoveTutor.Visible = Config.ORAS; // Default false unless loaded

        Control[] romfs, exefs, cro;

        switch (Config.Generation)
        {
            case 6:
                romfs = [B_GameText, B_StoryText, B_Personal, B_Evolution, B_LevelUp, B_Wild,B_CatchZones, B_MegaEvo, B_EggMove, B_Trainer, B_Item, B_Move, B_Maison, B_TitleScreen, B_OWSE,
                ];
                exefs = [B_MoveTutor, B_TMHM, B_Mart, B_Pickup, B_OPower, B_ShinyRate];
                cro = [B_TypeChart, B_Starter, B_Gift, B_Static];
                B_MoveTutor.Visible = Config.ORAS; // Default false unless loaded
                break;
            case 7:
                romfs = [B_GameText, B_StoryText, B_Personal, B_Evolution, B_LevelUp, B_Wild, B_MegaEvo, B_EggMove, B_Trainer, B_Item, B_Move, B_Royal, B_Pickup, B_OWSE,
                ];
                exefs = [B_TM, B_TypeChart, B_ShinyRate];
                cro = [B_Mart, B_MoveTutor];
                B_MoveTutor.Visible = Config.USUM;

                if (Config.Version != GameVersion.SMDEMO)
                    romfs = [.. romfs, .. new[] { B_Static }];
                break;
            default:
                romfs = exefs = cro = [new Label { Text = "No editors available." }];
                break;
        }

        FLP_RomFS.Controls.AddRange(romfs);
        FLP_ExeFS.Controls.AddRange(exefs);
        AddGen6TradePatchButtonIfNeeded();
        AddUSUMMoveRelearnerButtonIfNeeded();
        FLP_CRO.Controls.AddRange(cro);
        AddPlayerLevelCapsButtonIfNeeded();
        AddCroRecipeButtonIfNeeded();
    }

    private Button B_USUMMoveRelearner;

    private void AddUSUMMoveRelearnerButtonIfNeeded()
    {
        if (Config?.USUM != true)
            return;

        B_USUMMoveRelearner ??= new Button
        {
            Name = "B_USUMMoveRelearner",
            Size = new System.Drawing.Size(138, 28),
            Margin = new Padding(4, 4, 4, 6),
            Text = "USUM Relearner",
            UseVisualStyleBackColor = true,
        };

        B_USUMMoveRelearner.Click -= B_USUMMoveRelearner_Click;
        B_USUMMoveRelearner.Click += B_USUMMoveRelearner_Click;

        if (!FLP_ExeFS.Controls.Contains(B_USUMMoveRelearner))
            FLP_ExeFS.Controls.Add(B_USUMMoveRelearner);
    }

    private void B_USUMMoveRelearner_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;

        if (Config?.USUM != true)
        {
            WinFormsUtil.Alert(
                "This patch is only available for Ultra Sun / Ultra Moon.");
            return;
        }

        if (ExeFSPath == null)
        {
            WinFormsUtil.Alert(
                "No ExeFS loaded.",
                "Load an unpacked USUM ExeFS folder with decompressed code.bin first.");
            return;
        }

        if (DialogResult.Yes != WinFormsUtil.Prompt(
            MessageBoxButtons.YesNo,
            "Enable the USUM Move Relearner patch?",
            "This applies both supplied ASM behaviors to code.bin:",
            "- Pokémon Center café NPCs open the Move Relearner.",
            "- The relearner only offers level-up moves the Pokémon could already know at its current level.",
            "",
            "A backup of code.bin is created the first time the patch is applied."))
        {
            return;
        }

        try
        {
            string report = USUMMoveRelearnerPatcher.Apply(ExeFSPath);
            WinFormsUtil.Alert("USUM Move Relearner patch applied!", report);
        }
        catch (Exception ex)
        {
            WinFormsUtil.Error("USUM Move Relearner patch failed.", ex.Message);
        }
    }

    private Button B_ORASTradePatch;

    private void AddGen6TradePatchButtonIfNeeded()
    {
        if (Config?.Generation != 6)
            return;

        B_ORASTradePatch ??= new Button
        {
            Name = "B_ORASTradePatch",
            Size = new System.Drawing.Size(140, 23),
            Text = "Gen6 Trade Patch",
            UseVisualStyleBackColor = true,
        };

        B_ORASTradePatch.Click -= B_ORASTradePatch_Click;
        B_ORASTradePatch.Click += B_ORASTradePatch_Click;

        if (!FLP_ExeFS.Controls.Contains(B_ORASTradePatch))
            FLP_ExeFS.Controls.Add(B_ORASTradePatch);
    }

    private void B_ORASTradePatch_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;

        if (ExeFSPath == null)
        {
            WinFormsUtil.Alert(
                "No ExeFS loaded.",
                "Load an unpacked Gen 6 ExeFS folder with decompressed code.bin first.");
            return;
        }

        string patchMessage = Config.ORAS
            ? "This patches code.bin so ORAS in-game trades accept any Pokemon.\n" +
              "It also enables the known PC/Box selector patch.\n\n" +
              "You can optionally randomize the Pokemon given by trade NPCs."
            : "This patches code.bin so X/Y in-game trades accept any Pokemon.\n\n" +
              "You can optionally randomize the Pokemon given by trade NPCs.";

        if (DialogResult.Yes != WinFormsUtil.Prompt(
            MessageBoxButtons.YesNo,
            "Apply Gen 6 trade patch?",
            patchMessage,
            "Continue?"))
        {
            return;
        }

        bool randomizeOffers = DialogResult.Yes == WinFormsUtil.Prompt(
            MessageBoxButtons.YesNo,
            "Randomize NPC trade offers too?",
            "Yes = also randomize the Pokemon given by trade NPCs.",
            "No = only apply accept-any trade patch.");

        try
        {
            string report = Gen6TradePatcher.ApplyGen6TradePatch(ExeFSPath, Config, randomizeOffers);
            RandomizationSessionState.MarkAction(
                "trade-patch.apply",
                ("randomizeOffers", randomizeOffers.ToString()));
            WinFormsUtil.Alert("Gen 6 trade patch applied!", report);
        }
        catch (Exception ex)
        {
            WinFormsUtil.Error("Gen 6 trade patch failed.", ex.Message);
        }
    }
    private void UpdateProgramTitle() => Text = GetProgramTitle();

    private static string GetProgramTitle()
    {
        // 0 - JP
        // 1 - EN
        // 2 - FR
        // 3 - DE
        // 4 - IT
        // 5 - ES
        // 6 - CHS
        // 7 - KO
        // 8 -
        // 11 - CHT
        if (SMDH?.AppSettings == null)
            return "pk3DS";
        int[] AILang = [0, 0, 1, 2, 4, 3, 5, 7, 8, 9, 6, 11];
        return "pk3DS - " + SMDH.AppInfo[AILang[Language]].ShortDescription;
    }

    private static GameConfig CheckGameType(string[] files)
    {
        try
        {
            if (files.Length > 1000)
                return null;
            var parent = Directory.GetParent(files[0]);
            if (parent is null)
                return null;

            string[] fileArr = Directory.GetFiles(Path.Combine(parent.FullName, "a"), "*", SearchOption.AllDirectories);
            int fileCount = fileArr.Count(file => Path.GetFileName(file).Length == 1);
            return new GameConfig(fileCount);
        }
        catch { }
        return null;
    }

    private static bool CheckIfRomFS(string path)
    {
        string[] top = Directory.GetDirectories(path);
        var fi = new FileInfo(top[top.Length > 1 ? 1 : 0]);
        // Check to see if the folder is romfs
        if (fi.Name == "a")
        {
            string[] files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
            var cfg = CheckGameType(files);

            if (cfg == null)
            {
                RomFSPath = null;
                Config = null;
                WinFormsUtil.Error("File count does not match expected game count.", "Files: " + files.Length);
                return false;
            }

            RomFSPath = path;
            Config = cfg;
            return true;
        }
        WinFormsUtil.Error("Folder does not contain an 'a' folder in the top level.");
        RomFSPath = null;
        return false;
    }

    private bool CheckIfExeFS(string path)
    {
        string[] files = Directory.GetFiles(path);
        if (files.Length == 1 && string.Equals(Path.GetFileName(files[0]), "exefs.bin", StringComparison.OrdinalIgnoreCase))
        {
            // Prompt if the user wants to unpack the ExeFS.
            if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Detected ExeFS binary.", "Unpack?"))
                return false;

            // User wanted to unpack. Unpack.
            if (!ExeFS.UnpackExeFS(files[0], path))
                return false; // on unpack fail

            // Remove ExeFS binary after unpacking
            File.Delete(files[0]);

            files = Directory.GetFiles(path);
            // unpack successful, continue onward!
        }

        if (files.Length != 3 && files.Length != 4)
            return false;

        var fi = new FileInfo(files[0]);
        if (!fi.Name.Contains("code"))
        {
            if (new FileInfo(files[1]).Name != "code.bin")
                return false;

            File.Move(files[1], Path.Combine(Path.GetDirectoryName(files[1]), ".code.bin"));
            files = Directory.GetFiles(path);
            fi = new FileInfo(files[0]);
        }
        if (fi.Length % 0x200 != 0 && WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Detected Compressed code binary.", "Decompress? File will be replaced.") == DialogResult.Yes)
            new Thread(() => { Interlocked.Increment(ref threads); new BLZCoder(["-d", files[0]], pBar1); Interlocked.Decrement(ref threads); WinFormsUtil.Alert("Decompressed!"); }).Start();

        ExeFSPath = path;
        return true;
    }

    private static bool CheckIfExHeader(string path)
    {
        ExHeaderPath = null;
        // Input folder path should contain the ExHeader.
        string[] files = Directory.GetFiles(path);
        foreach (string fp in from s in files let f = new FileInfo(s) where (f.Name.StartsWith("exh", StringComparison.OrdinalIgnoreCase) || f.Name.StartsWith("decryptedexh", StringComparison.OrdinalIgnoreCase)) && f.Length == 0x800 select s)
            ExHeaderPath = fp;

        return ExHeaderPath != null;
    }

    private bool ThreadActive()
    {
        if (threads <= 0)
            return false;
        WinFormsUtil.Alert("Please wait for all operations to finish first."); return true;
    }

    private void TabMain_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) is true)
            e.Effect = DragDropEffects.Copy;
    }

    private void TabMain_DragDrop(object sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] { Length: not 0 } files)
            return;
        string path = files[0]; // open first D&D
        OpenQuick(path);
    }

    // RomFS Subform Items
    private void RebuildRomFS(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        if (RomFSPath == null)
            return;
        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Rebuild RomFS?") != DialogResult.Yes)
            return;

        var sfd = new SaveFileDialog
        {
            FileName = HANSgameID != 0 ? HANSgameID.ToString("X8") + ".romfs" : "romfs.bin",
            Filter = "HANS RomFS|*.romfs|Binary File|*.bin|All Files|*.*",
        };
        sfd.FilterIndex = HANSgameID != 0 ? 0 : sfd.Filter.Length - 1;

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            new Thread(() =>
            {
                UpdateStatus(Environment.NewLine + "Building RomFS binary. Please wait until the program finishes.");

                Interlocked.Increment(ref threads);
                RomFS.BuildRomFS(RomFSPath, sfd.FileName, RTB_Status, pBar1);
                Interlocked.Decrement(ref threads);

                UpdateStatus("RomFS binary saved." + Environment.NewLine);
                WinFormsUtil.Alert("Wrote RomFS binary:", sfd.FileName);
            }).Start();
        }
    }

    private void B_GameText_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        new Thread(() =>
        {
            var g = Config.GARCGameText;
            string[][] files = Config.GameTextStrings;
            Invoke(() => new TextEditor(files, "gametext").ShowDialog());
            g.Files = TryWriteText(files, g);
            g.Save();
        }).Start();
    }

    private void B_StoryText_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        new Thread(() =>
        {
            var g = Config.GetGARCData("storytext");
            string[][] files = g.Files.Select(file => new TextFile(Config, file).Lines).ToArray();
            Invoke(() => new TextEditor(files, "storytext").ShowDialog());
            g.Files = TryWriteText(files, g);
            g.Save();
        }).Start();
    }

    private static byte[][] TryWriteText(string[][] files, GARCFile g)
    {
        byte[][] data = new byte[files.Length][];
        var errata = new List<string>();
        for (int i = 0; i < data.Length; i++)
        {
            try
            {
                data[i] = TextFile.GetBytes(Config, files[i]);
            }
            catch (Exception ex)
            {
                errata.Add($"File {i:000} | {ex.Message}");
                // revert changes
                data[i] = g.GetFile(i);
            }
        }
        if (errata.Count == 0)
            return data;

        string[] options =
        [
            "Cancel: Discard all changes",
            "Yes: Save changes, dump errata/failed text",
            "No: Save changes, don't dump errata/failed text",
        ];
        var dr = WinFormsUtil.Prompt(MessageBoxButtons.YesNoCancel, "Errors found while attempting to save text."
                                                                    + Environment.NewLine + "Example: " + errata[0],
            string.Join(Environment.NewLine, options));
        if (dr == DialogResult.Cancel)
            return g.Files; // discard
        if (dr == DialogResult.No)
            return data;

        const string txt_errata = "text_errata.txt";
        const string txt_failed = "text_failed.txt";
        File.WriteAllLines(txt_errata, errata);
        TextEditor.ExportTextFile(txt_failed, true, files);

        WinFormsUtil.Alert("Saved text files to path: " + Application.StartupPath,
            txt_errata + Environment.NewLine + txt_failed);

        return data;
    }

    private void B_Maison_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        DialogResult dr;
        switch (Config.Generation)
        {
            case 6:
                dr = WinFormsUtil.Prompt(MessageBoxButtons.YesNoCancel, "Edit Super Maison instead of Normal Maison?", "Yes = Super, No = Normal, Cancel = Abort");
                break;
            case 7:
                dr = WinFormsUtil.Prompt(MessageBoxButtons.YesNoCancel, "Edit Battle Royal instead of Battle Tree?", "Yes = Royal, No = Tree, Cancel = Abort");
                break;
            default:
                return;
        }
        if (dr == DialogResult.Cancel)
            return;

        new Thread(() =>
        {
            bool super = dr == DialogResult.Yes;
            string c = super ? "S" : "N";
            var trdata = Config.GetGARCData("maisontr" + c);
            var trpoke = Config.GetGARCData("maisonpk" + c);
            byte[][] trd = trdata.Files;
            byte[][] trp = trpoke.Files;
            switch (Config.Generation)
            {
                case 6:
                    Invoke(() => new MaisonEditor6(trd, trp, super).ShowDialog());
                    break;
                case 7:
                    Invoke(() => new MaisonEditor7(trd, trp, super).ShowDialog());
                    break;
            }
            trdata.Files = trd;
            trpoke.Files = trp;
            trdata.Save();
            trpoke.Save();
        }).Start();
    }

    private void B_Personal_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        new Thread(() =>
        {
            byte[][] d = Config.GARCPersonal.Files;
            switch (Config.Generation)
            {
                case 6:
                    Invoke(() => new PersonalEditor6(d).ShowDialog());
                    break;
                case 7:
                    Invoke(() => new PersonalEditor7(d).ShowDialog());
                    break;
            }
            // Set Master Table back
            for (int i = 0; i < d.Length - 1; i++)
                d[i].CopyTo(d[^1], i * d[i].Length);

            Config.GARCPersonal.Files = d;
            Config.GARCPersonal.Save();
            Config.InitializePersonal();
        }).Start();
    }

    private void B_Trainer_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        new Thread(() =>
        {
            var trclass = Config.GetGARCData("trclass");
            var trdata = Config.GetGARCData("trdata");
            var trpoke = Config.GetGARCData("trpoke");
            byte[][] trc = trclass.Files;
            byte[][] trd = trdata.Files;
            byte[][] trp = trpoke.Files;

            switch (Config.Generation)
            {
                case 6:
                    Invoke(() => new RSTE(trd, trp).ShowDialog());
                    break;
                case 7:
                    Invoke(() => new SMTE(trd, trp).ShowDialog());
                    break;
            }
            trclass.Files = trc;
            trdata.Files = trd;
            trpoke.Files = trp;
            trclass.Save();
            trdata.Save();
            trpoke.Save();
        }).Start();
    }

    private void B_Wild_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        new Thread(() =>
        {
            string[] files;
            Action action;
            switch (Config.Generation)
            {
                case 6:
                    files = ["encdata"];
                    if (Config.ORAS)
                        action = () => new RSWE().ShowDialog();
                    else if (Config.XY)
                        action = () => new XYWE().ShowDialog();
                    else return;

                    Invoke((MethodInvoker)delegate { Enabled = false; });
                    FileGet(files, false);
                    Invoke(action);
                    FileSet(files);
                    Invoke((MethodInvoker)delegate { Enabled = true; });
                    break;
                case 7:
                    Invoke((MethodInvoker)delegate { Enabled = false; });
                    Interlocked.Increment(ref threads);

                    files = ["encdata", "zonedata", "worlddata"];
                    UpdateStatus($"GARC Get: {files[0]}... ");
                    var ed = Config.GetlzGARCData(files[0]);
                    UpdateStatus($"GARC Get: {files[1]}... ");
                    var zd = Config.GetlzGARCData(files[1]);
                    UpdateStatus($"GARC Get: {files[2]}... ");
                    var wd = Config.GetlzGARCData(files[2]);
                    UpdateStatus("Running SMWE... ");
                    action = () => new SMWE(ed, zd, wd).ShowDialog();
                    Invoke(action);

                    UpdateStatus($"GARC Set: {files[0]}... ");
                    ed.Save();
                    ResetStatus();
                    Interlocked.Decrement(ref threads);
                    Invoke((MethodInvoker)delegate { Enabled = true; });
                    break;
                default:
                    return;
            }
        }).Start();
    }

    private void B_CatchZones_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        if (Config?.XY != true && Config?.ORAS != true)
        {
            WinFormsUtil.Alert("Catch zones are only available for XY/ORAS.");
            return;
        }
        if (!CatchZonePatchSync.HasPatchForCurrentGame())
        {
            WinFormsUtil.Alert(
                "No catch-zone patch was found for this game.",
                "Expected catch_zone_patches/XY.json or catch_zone_patches/ORAS.json next to pk3DS.exe.");
            return;
        }

        if (DialogResult.Yes != WinFormsUtil.Prompt(
                MessageBoxButtons.YesNo,
                "Enable catch zones for this ROM?",
                "pk3DS will patch the encounter/map files extracted from your own ROM dump."))
            return;

        new Thread(() =>
        {
            string[] files = ["encdata", "mapGR"];
            try
            {
                Invoke((MethodInvoker)delegate { Enabled = false; });

                FileGet(files, false);

                string folderPatchReport = CatchZonePatchSync.ApplyCurrentGameWorkingDirectoryPatches();
                if (!string.IsNullOrWhiteSpace(folderPatchReport))
                    UpdateStatus(folderPatchReport.Replace(Environment.NewLine, " | "));

                FileSet(files);

                string rawPatchReport = CatchZonePatchSync.ApplyCurrentGameRawPatches(RomFSPath);
                if (!string.IsNullOrWhiteSpace(rawPatchReport))
                    UpdateStatus(rawPatchReport.Replace(Environment.NewLine, " | "));

                Invoke(() => WinFormsUtil.Alert("Catch zones enabled for this ROM."));
            }
            catch (Exception ex)
            {
                Invoke(() => WinFormsUtil.Error("Failed to enable catch zones.", ex.Message));
            }
            finally
            {
                Invoke((MethodInvoker)delegate { Enabled = true; });
            }
        }).Start();
    }

    private void B_OWSE_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "The OverWorld/Script Editor is not recommended for most users and is still a work-in-progress.", "Continue anyway?"))
            return;
        switch (Config.Generation)
        {
            case 6:
                RunOWSE6();
                return;
            case 7:
                RunOWSE7();
                return;
        }
    }

    private void RunOWSE6()
    {
        Enabled = false;
        new Thread(() =>
        {
            bool reload = ModifierKeys is Keys.Control or (Keys.Alt | Keys.Control);
            string[] files = ["encdata", "storytext", "mapGR", "mapMatrix"];
            if (reload || files.Sum(t => Directory.Exists(t) ? 0 : 1) != 0) // Dev bypass if all exist already
                FileGet(files, false);

            // Don't set any data back. Just view.
            {
                var g = Config.GetGARCData("storytext");
                string[][] tfiles = g.Files.Select(file => new TextFile(Config, file).Lines).ToArray();
                Invoke(() => new OWSE().Show());
                Invoke(() => new TextEditor(tfiles, "storytext").Show());
                while (Application.OpenForms.Count > 1)
                    Thread.Sleep(200);
            }
            Invoke((MethodInvoker)delegate { Enabled = true; });
            FileSet(files);
        }).Start();
    }

    private void RunOWSE7()
    {
        Enabled = false;
        new Thread(() =>
        {
            var files = new[] { "encdata", "zonedata", "worlddata" };
            UpdateStatus($"GARC Get: {files[0]}... ");
            var ed = Config.GetlzGARCData(files[0]);
            UpdateStatus($"GARC Get: {files[1]}... ");
            var zd = Config.GetlzGARCData(files[1]);
            UpdateStatus($"GARC Get: {files[2]}... ");
            //var wd = Config.GetlzGARCData(files[2]);

            var g = Config.GetGARCData("storytext");
            string[][] tfiles = g.Files.Select(file => new TextFile(Config, file).Lines).ToArray();
            Invoke(() => new TextEditor(tfiles, "storytext").Show());
            Invoke(() => new OWSE7(ed, zd).Show());
            while (Application.OpenForms.Count > 1)
                Thread.Sleep(200);
            Invoke((MethodInvoker)delegate { Enabled = true; });
        }).Start();
    }

    private void B_Evolution_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        new Thread(() =>
        {
            var g = Config.GetGARCData("evolution");
            byte[][] d = g.Files;
            switch (Config.Generation)
            {
                case 6:
                    Invoke(() => new EvolutionEditor6(d).ShowDialog());
                    break;
                case 7:
                    Invoke(() => new EvolutionEditor7(d).ShowDialog());
                    break;
            }
            g.Files = d;
            Config.InitializeEvos();
            g.Save();
        }).Start();
    }

    private void B_MegaEvo_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        new Thread(() =>
        {
            var g = Config.GetGARCData("megaevo");
            byte[][] d = g.Files;
            switch (Config.Generation)
            {
                case 6:
                    Invoke(() => new MegaEvoEditor6(d).ShowDialog());
                    break;
                case 7:
                    Invoke(() => new MegaEvoEditor7(d).ShowDialog());
                    break;
            }
            g.Files = d;
            g.Save();
        }).Start();
    }

    private void B_Item_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        new Thread(() =>
        {
            var g = Config.GetGARCData("item");
            byte[][] d = g.Files;
            switch (Config.Generation)
            {
                case 6:
                    Invoke(() => new ItemEditor6(d).ShowDialog());
                    break;
                case 7:
                    Invoke(() => new ItemEditor7(d).ShowDialog());
                    break;
            }
            g.Files = d;
            g.Save();
        }).Start();
    }

    private void B_Move_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        new Thread(() =>
        {
            var g = Config.GARCMoves;
            byte[][] Moves;
            switch (Config.Generation)
            {
                case 6:
                    bool isMini = Config.ORAS;
                    Moves = isMini ? Mini.UnpackMini(g.GetFile(0), "WD") : g.Files;
                    Invoke(() => new MoveEditor6(Moves).ShowDialog());
                    g.Files = isMini ? [Mini.PackMini(Moves, "WD")] : Moves;
                    break;
                case 7:
                    Moves = Mini.UnpackMini(g.GetFile(0), "WD");
                    Invoke(() => new MoveEditor7(Moves).ShowDialog());
                    g.Files = [Mini.PackMini(Moves, "WD")];
                    break;
            }
            g.Save();
            Config.InitializeMoves();
        }).Start();
    }

    private void B_LevelUp_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        new Thread(() =>
        {
            byte[][] d = Config.GARCLearnsets.Files;
            switch (Config.Generation)
            {
                case 6:
                    Invoke(() => new LevelUpEditor6(d).ShowDialog());
                    break;
                case 7:
                    Invoke(() => new LevelUpEditor7(d).ShowDialog());
                    break;
            }
            Config.GARCLearnsets.Files = d;
            Config.GARCLearnsets.Save();
            Config.InitializeLearnset();
        }).Start();
    }

    private void B_EggMove_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        new Thread(() =>
        {
            var g = Config.GetGARCData("eggmove");
            byte[][] d = g.Files;
            switch (Config.Generation)
            {
                case 6:
                    Invoke(() => new EggMoveEditor6(d).ShowDialog());
                    break;
                case 7:
                    Invoke(() => new EggMoveEditor7(d).ShowDialog());
                    break;
            }
            g.Files = d;
            g.Save();
        }).Start();
    }

    private void B_TitleScreen_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        new Thread(() =>
        {
            string[] files = ["titlescreen"];
            FileGet(files); // Compressed files exist, handled in the other form since there's so many
            Invoke(() => new TitleScreenEditor6().ShowDialog());
            FileSet(files);
        }).Start();
    }
    // RomFS File Requesting Method Wrapper
    private void FileGet(string[] files, bool skipDecompression = true, bool skipGet = false)
    {
        if (skipGet || skipBoth)
            return;
        foreach (string toEdit in files)
        {
            string GARC = Config.GetGARCFileName(toEdit);
            UpdateStatus($"GARC Get: {toEdit} @ {GARC}... ");
            ThreadGet(Path.Combine(RomFSPath, GARC), toEdit, true, skipDecompression);
            while (threads > 0) Thread.Sleep(50);
            ResetStatus();
        }
    }

    private void FileSet(IEnumerable<string> files, bool keep = false)
    {
        if (skipBoth)
            return;
        foreach (string toEdit in files)
        {
            string GARC = Config.GetGARCFileName(toEdit);
            UpdateStatus($"GARC Set: {toEdit} @ {GARC}... ");
            ThreadSet(Path.Combine(RomFSPath, GARC), toEdit, 4); // 4 bytes for Gen6
            while (threads > 0) Thread.Sleep(50);
            if (!keep && Directory.Exists(toEdit)) Directory.Delete(toEdit, true);
            ResetStatus();
        }
    }

    // ExeFS Subform Items
    private void RebuildExeFS(object sender, EventArgs e)
    {
        if (ExeFSPath == null)
            return;
        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Rebuild ExeFS?") != DialogResult.Yes)
            return;

        string[] files = Directory.GetFiles(ExeFSPath);
        int file = 0;
        if (files[1].Contains("code"))
            file = 1;

        var sfd = new SaveFileDialog
        {
            FileName = HANSgameID != 0 ? HANSgameID.ToString("X8") + ".exefs" : "exefs.bin",
            Filter = "HANS ExeFS|*.exefs|Binary File|*.bin|All Files|*.*",
        };
        sfd.FilterIndex = HANSgameID != 0 ? 0 : sfd.Filter.Length - 1;

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            new Thread(() =>
            {
                Interlocked.Increment(ref threads);
                new BLZCoder(["-en", files[file]], pBar1);
                WinFormsUtil.Alert("Compressed!");
                ExeFS.PackExeFS(Directory.GetFiles(ExeFSPath), sfd.FileName);
                Interlocked.Decrement(ref threads);
            }).Start();
        }
    }

    private void B_Pickup_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        switch (Config.Generation)
        {
            case 6:
                if (ExeFSPath != null) new PickupEditor6().Show();
                break;
            case 7:
                var pickup = Config.GetlzGARCData("pickup");
                Invoke(() => new PickupEditor7(pickup).ShowDialog());
                break;
        }
    }

    private void B_TMHM_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        if (ExeFSPath == null)
            return;
        switch (Config.Generation)
        {
            case 6: new TMHMEditor6().Show(); break;
            case 7: new TMEditor7().Show(); break;
        }
    }

    private void B_Mart_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        switch (Config.Generation)
        {
            case 6:
                if (ExeFSPath != null) new MartEditor6().Show();
                break;

            case 7:
                if (ThreadActive())
                    return;
                if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "CRO Editing causes crashes if you do not patch the RO module.", "In order to patch the RO module, your device must be running Custom Firmware (for example, Luma3DS).", "Continue anyway?"))
                    return;
                if (RomFSPath != null) (Config.USUM ? new MartEditor7UU() : (Form)new MartEditor7()).Show();
                break;
        }
    }

    private void B_MoveTutor_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        switch (Config.Generation)
        {
            case 6:
                if (ExeFSPath != null) new TutorEditor6().Show();
                break;
            case 7:
                if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "CRO Editing causes crashes if you do not patch the RO module.", "In order to patch the RO module, your device must be running Custom Firmware (for example, Luma3DS).", "Continue anyway?"))
                    return;
                if (RomFSPath != null) new TutorEditor7().Show();
                break;
        }
    }

    private void B_OPower_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        if (ExeFSPath != null) new OPower().Show();
    }

    private void B_ShinyRate_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        if (ExeFSPath != null) new ShinyRate().ShowDialog();
    }

    // CRO Subform Items
    private void PatchCRO_CRR(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        if (RomFSPath == null)
            return;
        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Rebuilding CRO/CRR is not necessary if you patch the RO module.", "Continue?"))
            return;
        new Thread(() =>
        {
            Interlocked.Increment(ref threads);
            CRO.E_HashCRR(Path.Combine(RomFSPath, ".crr", "static.crr"), RomFSPath, true, /* true // don't patch crr for now */ false, RTB_Status, pBar1);
            Interlocked.Decrement(ref threads);

            WinFormsUtil.Alert("CRO's and CRR have been updated.",
                "If you have made any modifications, it is required that the RSA Verification check be patched on the system in order for the modified CROs to load (ie, no file redirection like NTR's layeredFS).");
        }).Start();
    }

    private void B_Starter_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "CRO Editing causes crashes if you do not patch the RO module.", "In order to patch the RO module, your device must be running Custom Firmware (for example, Luma3DS).", "Continue anyway?"))
            return;
        string CRO = Path.Combine(RomFSPath, "DllPoke3Select.cro");
        string CRO2 = Path.Combine(RomFSPath, "DllField.cro");
        if (!File.Exists(CRO))
        {
            WinFormsUtil.Error("File Missing!", "DllPoke3Select.cro was not found in your RomFS folder!");
            return;
        }
        if (!File.Exists(CRO2))
        {
            WinFormsUtil.Error("File Missing!", "DllField.cro was not found in your RomFS folder!");
            return;
        }
        new StarterEditor6().ShowDialog();
    }

    private void B_TypeChart_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;

        switch (Config.Generation)
        {
            case 6:
                if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "CRO Editing causes crashes if you do not patch the RO module.", "In order to patch the RO module, your device must be running Custom Firmware (for example, Luma3DS).", "Continue anyway?"))
                    return;
                string CRO = Path.Combine(RomFSPath, "DllBattle.cro");
                if (!File.Exists(CRO))
                {
                    WinFormsUtil.Error("File Missing!", "DllBattle.cro was not found in your RomFS folder!");
                    return;
                }
                new TypeChart6().ShowDialog();
                break;
            case 7:
                new TypeChart7().ShowDialog();
                break;
        }
    }

    private void B_Gift_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "CRO Editing causes crashes if you do not patch the RO module.", "In order to patch the RO module, your device must be running Custom Firmware (for example, Luma3DS).", "Continue anyway?"))
            return;
        string CRO = Path.Combine(RomFSPath, "DllField.cro");
        if (!File.Exists(CRO))
        {
            WinFormsUtil.Error("File Missing!", "DllField.cro was not found in your RomFS folder!");
            return;
        }
        new GiftEditor6().ShowDialog();
    }

    private void B_Static_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;

        if (Config.Generation == 7)
        {
            new Thread(() =>
            {
                var esg = Config.GetGARCData("encounterstatic");
                byte[][] es = esg.Files;

                Invoke(() => new StaticEncounterEditor7(es).ShowDialog());
                esg.Files = es;
                esg.Save();
            }).Start();
            return;
        }

        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "CRO Editing causes crashes if you do not patch the RO module.", "In order to patch the RO module, your device must be running Custom Firmware (for example, Luma3DS).", "Continue anyway?"))
            return;
        string CRO = Path.Combine(RomFSPath, "DllField.cro");
        if (!File.Exists(CRO))
        {
            WinFormsUtil.Error("File Missing!", "DllField.cro was not found in your RomFS folder!");
            return;
        }
        new StaticEncounterEditor6().ShowDialog();
    }

    // CXI Building
    private void B_RebuildTrimmed3DS_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;

        var sfd = new SaveFileDialog
        {
            FileName = "newROM.3ds",
            Filter = "Binary File|*.*",
        };
        if (sfd.ShowDialog() != DialogResult.OK)
            return;
        string path = sfd.FileName;

        new Thread(() =>
        {
            Interlocked.Increment(ref threads);
            var exh = new Exheader(ExHeaderPath);
            CTRUtil.BuildROM(true, "Nintendo", ExeFSPath, RomFSPath, ExHeaderPath, exh.GetSerial(), path,
                true, pBar1, RTB_Status);
            Interlocked.Decrement(ref threads);
        }).Start();
    }

    // 3DS Building
    private void B_Rebuild3DS_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;

        var sfd = new SaveFileDialog
        {
            FileName = "newROM.3ds",
            Filter = "Binary File|*.*",
        };
        if (sfd.ShowDialog() != DialogResult.OK)
            return;
        string path = sfd.FileName;

        new Thread(() =>
        {
            Interlocked.Increment(ref threads);
            var exh = new Exheader(ExHeaderPath);
            CTRUtil.BuildROM(true, "Nintendo", ExeFSPath, RomFSPath, ExHeaderPath, exh.GetSerial(), path,
                false, pBar1, RTB_Status);
            Interlocked.Decrement(ref threads);
        }).Start();
    }

    // Extra Tools
    private void L_SubTools_Click(object sender, EventArgs e)
    {
        new ToolsUI().ShowDialog();
    }

    private void B_Patch_Click(object sender, EventArgs e)
    {
        new Patch().ShowDialog();
    }

    private void Menu_BLZ_Click(object sender, EventArgs e)
    {
        var ofd = new OpenFileDialog();
        if (DialogResult.OK != ofd.ShowDialog())
            return;

        string path = ofd.FileName;
        var fi = new FileInfo(path);
        if (fi.Length > 15 * 1024 * 1024) // 15MB
        { WinFormsUtil.Error("File too big!", fi.Length + " bytes."); return; }

        if (ModifierKeys != Keys.Control && fi.Length % 0x200 == 0 && WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Detected Decompressed Binary.", "Compress? File will be replaced.") == DialogResult.Yes)
            new Thread(() => { Interlocked.Increment(ref threads); new BLZCoder(["-en", path], pBar1); Interlocked.Decrement(ref threads); WinFormsUtil.Alert("Compressed!"); }).Start();
        else if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Detected Compressed Binary", "Decompress? File will be replaced.") == DialogResult.Yes)
            new Thread(() => { Interlocked.Increment(ref threads); new BLZCoder(["-d", path], pBar1); Interlocked.Decrement(ref threads); WinFormsUtil.Alert("Decompressed!"); }).Start();
    }

    private void Menu_LZ11_Click(object sender, EventArgs e)
    {
        var ofd = new OpenFileDialog();
        if (DialogResult.OK != ofd.ShowDialog())
            return;

        string path = ofd.FileName;
        var fi = new FileInfo(path);
        if (fi.Length > 15 * 1024 * 1024) // 15MB
        { WinFormsUtil.Error("File too big!", fi.Length + " bytes."); return; }

        byte[] data = File.ReadAllBytes(path);
        string predict = data[0] == 0x11 ? "compressed" : "decompressed";
        var dr = WinFormsUtil.Prompt(MessageBoxButtons.YesNoCancel, $"Detected {predict} file. Do what?",
            "Yes = Decompress\nNo = Compress\nCancel = Abort");
        new Thread(() =>
        {
            Interlocked.Increment(ref threads);
            if (dr == DialogResult.Yes)
            {
                try
                {
                    LZSS.Decompress(path, Path.Combine(Directory.GetParent(path).FullName, "dec_" + Path.GetFileNameWithoutExtension(path) + ".bin"));
                }
                catch (Exception err) { WinFormsUtil.Alert("Tried decompression, may have worked:", err.ToString()); }
                WinFormsUtil.Alert("File Decompressed!", path);
            }
            if (dr == DialogResult.No)
            {
                LZSS.Compress(path, Path.Combine(Directory.GetParent(path).FullName, Path.GetFileNameWithoutExtension(path).Replace("_dec", "") + ".lz"));
                WinFormsUtil.Alert("File Compressed!", path);
            }
            Interlocked.Decrement(ref threads);
        }).Start();
    }

    private void Menu_SMDH_Click(object sender, EventArgs e)
    {
        new Icon().ShowDialog();
    }

    private void Menu_Shuffler_Click(object sender, EventArgs e)
    {
        new Shuffler().ShowDialog();
    }

    // GARC Requests
    internal static string GetGARCFileName(string requestedGARC, int lang)
    {
        var garc = Config.GetGARCReference(requestedGARC);
        if (garc.LanguageVariant)
            garc = garc.GetRelativeGARC(lang);

        return garc.Reference;
    }

    private bool GetGARC(string infile, string outfolder, bool PB, bool bypassExt = false)
    {
        if (skipBoth && Directory.Exists(outfolder))
        {
            UpdateStatus("Skipped - Exists!", false);
            Interlocked.Decrement(ref threads);
            return true;
        }
        try
        {
            bool success = GarcUtil.UnpackGARC(infile, outfolder, bypassExt, PB ? pBar1 : null, L_Status, true);
            UpdateStatus(string.Format(success ? "Success!" : "Failed!"), false);
            Interlocked.Decrement(ref threads);
            return success;
        }
        catch (Exception e) { WinFormsUtil.Error("Could not get the GARC:", e.ToString()); Interlocked.Decrement(ref threads); return false; }
    }

    private bool SetGARC(string outfile, string infolder, int padBytes, bool PB)
    {
        if (skipBoth || (ModifierKeys == Keys.Control && WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Cancel writing data back to GARC?") == DialogResult.Yes))
        { Interlocked.Decrement(ref threads); UpdateStatus("Aborted!", false); return false; }

        try
        {
            bool success = GarcUtil.PackGARC(infolder, outfile, Config.GARCVersion, padBytes, PB ? pBar1 : null, L_Status, true);
            Interlocked.Decrement(ref threads);
            UpdateStatus(string.Format(success ? "Success!" : "Failed!"), false);
            return success;
        }
        catch (Exception e) { WinFormsUtil.Error("Could not set the GARC back:", e.ToString()); Interlocked.Decrement(ref threads); return false; }
    }

    private void ThreadGet(string infile, string outfolder, bool PB = true, bool bypassExt = false)
    {
        Interlocked.Increment(ref threads);
        if (Directory.Exists(outfolder))
        {
            try { Directory.Delete(outfolder, true); }
            catch { }
        }

        new Thread(() => GetGARC(infile, outfolder, PB, bypassExt)).Start();
    }

    private void ThreadSet(string outfile, string infolder, int padBytes, bool PB = true)
    {
        Interlocked.Increment(ref threads);
        new Thread(() => SetGARC(outfile, infolder, padBytes, PB)).Start();
    }

    // Update RichTextBox
    private void UpdateStatus(string status, bool preBreak = true)
    {
        string newtext = (preBreak ? Environment.NewLine : "") + status;
        try
        {
            if (RTB_Status.InvokeRequired)
            {
                RTB_Status.Invoke((MethodInvoker)delegate
                {
                    RTB_Status.AppendText(newtext);
                    RTB_Status.SelectionStart = RTB_Status.Text.Length;
                    RTB_Status.ScrollToCaret();
                    L_Status.Text = RTB_Status.Lines[^1].Split([" @"], StringSplitOptions.None)[0];
                });
            }
            else
            {
                RTB_Status.AppendText(newtext);
                RTB_Status.SelectionStart = RTB_Status.Text.Length;
                RTB_Status.ScrollToCaret();
                L_Status.Text = RTB_Status.Lines[^1].Split([" @"], StringSplitOptions.None)[0];
            }
        }
        catch { }
    }

    private void ResetStatus()
    {
        try
        {
            if (L_Status.InvokeRequired)
            {
                L_Status.Invoke((MethodInvoker)(() => L_Status.Text = ""));
            }
            else
            {
                L_Status.Text = "";
            }
        }
        catch { }
    }

    private void SetInt32SeedToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Reseed RNG?", "If yes, copy the 32 bit (not hex) integer seed to the clipboard before hitting Yes."))
            return;

        string val = string.Empty;
        try { val = Clipboard.GetText(); }
        catch { }
        if (int.TryParse(val, out int seed))
        {
            Util.ReseedRand(seed);
            RandomizationSessionState.MarkAction("rng.seed", ("value", seed.ToString()));
            WinFormsUtil.Alert($"Reseeded RNG to seed: {seed}");
            return;
        }
        WinFormsUtil.Alert("Unable to set seed.");
    }
}