using pk3DS.Core;
using pk3DS.Core.CTR;
using pk3DS.Core.Modding.Research;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Forms;

namespace pk3DS.WinForms;

internal sealed class BatchRomBuildOptions
{
    public string TemplatePath { get; set; } = string.Empty;
    public string OutputDirectory { get; set; } = string.Empty;
    public string BaseName { get; set; } = "Randomized";
    public int Count { get; set; } = 4;
    public bool Trimmed { get; set; }
    public bool RestoreBackups { get; set; } = true;
}

internal sealed class BatchRomBuilderDialog : Form
{
    private readonly TextBox TB_Template = new();
    private readonly TextBox TB_Output = new();
    private readonly TextBox TB_BaseName = new();
    private readonly NumericUpDown NUD_Count = new();
    private readonly ComboBox CB_BuildType = new();
    private readonly CheckBox CHK_RestoreBackups = new();

    public BatchRomBuildOptions Options { get; private set; }

    public BatchRomBuilderDialog(string templateDirectory, string game, string outputDirectory)
    {
        Text = "Batch ROM Builder";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(640, 300);

        int leftLabel = 16;
        int leftControl = 150;
        int width = 390;
        int y = 18;

        Controls.Add(MakeLabel("Global template", leftLabel, y + 4));
        TB_Template.SetBounds(leftControl, y, width, 24);
        Controls.Add(TB_Template);
        var browseTemplate = new Button { Text = "...", Width = 58, Height = 24, Left = leftControl + width + 8, Top = y };
        browseTemplate.Click += (_, _) => BrowseTemplate(templateDirectory);
        Controls.Add(browseTemplate);

        y += 42;
        Controls.Add(MakeLabel("ROMs to build", leftLabel, y + 4));
        NUD_Count.SetBounds(leftControl, y, 90, 24);
        NUD_Count.Minimum = 1;
        NUD_Count.Maximum = 50;
        NUD_Count.Value = 4;
        Controls.Add(NUD_Count);

        y += 42;
        Controls.Add(MakeLabel("Output folder", leftLabel, y + 4));
        TB_Output.SetBounds(leftControl, y, width, 24);
        TB_Output.Text = outputDirectory;
        Controls.Add(TB_Output);
        var browseOutput = new Button { Text = "...", Width = 58, Height = 24, Left = leftControl + width + 8, Top = y };
        browseOutput.Click += (_, _) => BrowseOutput();
        Controls.Add(browseOutput);

        y += 42;
        Controls.Add(MakeLabel("Base file name", leftLabel, y + 4));
        TB_BaseName.SetBounds(leftControl, y, 220, 24);
        TB_BaseName.Text = $"{game}_Randomized";
        Controls.Add(TB_BaseName);

        Controls.Add(MakeLabel("Build type", 390, y + 4));
        CB_BuildType.SetBounds(465, y, 150, 24);
        CB_BuildType.DropDownStyle = ComboBoxStyle.DropDownList;
        CB_BuildType.Items.AddRange(new object[] { "Full .3DS", "Trimmed .3DS" });
        CB_BuildType.SelectedIndex = 0;
        Controls.Add(CB_BuildType);

        y += 42;
        CHK_RestoreBackups.Text = "Restore pk3DS original-file backups before each ROM";
        CHK_RestoreBackups.AutoSize = true;
        CHK_RestoreBackups.Checked = true;
        CHK_RestoreBackups.SetBounds(leftControl, y, 420, 24);
        Controls.Add(CHK_RestoreBackups);

        var info = new Label
        {
            AutoSize = false,
            Left = leftControl,
            Top = y + 28,
            Width = 465,
            Height = 34,
            Text = "Every ROM gets a new int32 seed and is rebuilt from the same clean staging copy; randomization never stacks from ROM #1 into ROM #2.",
        };
        Controls.Add(info);

        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 92, Height = 28, Left = 430, Top = 260 };
        var build = new Button { Text = "Build ROMs", Width = 100, Height = 28, Left = 530, Top = 260 };
        build.Click += (_, _) => AcceptOptions();
        Controls.Add(cancel);
        Controls.Add(build);
        CancelButton = cancel;
        AcceptButton = build;

        string preferred = Directory.Exists(templateDirectory)
            ? Directory.GetFiles(templateDirectory, $"rom_template_{game.ToLowerInvariant()}*.json")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault()
            : null;
        if (preferred is null && Directory.Exists(templateDirectory))
            preferred = Directory.GetFiles(templateDirectory, "rom_template*.json")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            TB_Template.Text = preferred;
            TB_BaseName.Text = Path.GetFileNameWithoutExtension(preferred);
        }
    }

    private static Label MakeLabel(string text, int x, int y) => new()
    {
        Text = text,
        AutoSize = true,
        Left = x,
        Top = y,
    };

    private void BrowseTemplate(string templateDirectory)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select Global ROM Template",
            Filter = "Global ROM template (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = Directory.Exists(templateDirectory) ? templateDirectory : AppDomain.CurrentDomain.BaseDirectory,
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        TB_Template.Text = dialog.FileName;
        TB_BaseName.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
    }

    private void BrowseOutput()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the folder where the generated .3ds files will be written.",
            SelectedPath = Directory.Exists(TB_Output.Text) ? TB_Output.Text : string.Empty,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            TB_Output.Text = dialog.SelectedPath;
    }

    private void AcceptOptions()
    {
        if (!File.Exists(TB_Template.Text))
        {
            WinFormsUtil.Alert("Select a valid Global ROM Template first.");
            return;
        }
        if (string.IsNullOrWhiteSpace(TB_Output.Text))
        {
            WinFormsUtil.Alert("Select an output folder.");
            return;
        }
        if (string.IsNullOrWhiteSpace(TB_BaseName.Text))
        {
            WinFormsUtil.Alert("Enter a base file name.");
            return;
        }

        string safeName = SanitizeFileName(TB_BaseName.Text.Trim());
        if (safeName.Length == 0)
        {
            WinFormsUtil.Alert("The base file name contains no usable characters.");
            return;
        }

        Options = new BatchRomBuildOptions
        {
            TemplatePath = Path.GetFullPath(TB_Template.Text),
            OutputDirectory = Path.GetFullPath(TB_Output.Text),
            BaseName = safeName,
            Count = (int)NUD_Count.Value,
            Trimmed = CB_BuildType.SelectedIndex == 1,
            RestoreBackups = CHK_RestoreBackups.Checked,
        };
        DialogResult = DialogResult.OK;
        Close();
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Trim().TrimEnd('.');
    }
}

/// <summary>
/// Suppresses confirmation/alert message boxes while a saved recipe is replayed.
/// Validation errors still abort the current ROM build instead of being ignored.
/// </summary>
internal static class BatchRuntime
{
    private static readonly Queue<DialogResult> PromptResults = new();
    private static Action<string> logger;

    internal static bool IsActive { get; private set; }

    internal static IDisposable Begin(Action<string> log)
    {
        if (IsActive)
            throw new InvalidOperationException("A batch replay is already active.");
        IsActive = true;
        logger = log;
        PromptResults.Clear();
        return new Scope();
    }

    internal static void QueuePromptResults(params DialogResult[] results)
    {
        foreach (var result in results)
            PromptResults.Enqueue(result);
    }

    internal static DialogResult GetPromptResult(MessageBoxButtons buttons)
    {
        if (PromptResults.Count != 0)
            return PromptResults.Dequeue();

        return buttons switch
        {
            MessageBoxButtons.OK => DialogResult.OK,
            MessageBoxButtons.OKCancel => DialogResult.OK,
            MessageBoxButtons.RetryCancel => DialogResult.Retry,
            MessageBoxButtons.AbortRetryIgnore => DialogResult.Ignore,
            MessageBoxButtons.YesNo => DialogResult.Yes,
            MessageBoxButtons.YesNoCancel => DialogResult.Yes,
            _ => DialogResult.OK,
        };
    }

    internal static void Log(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            logger?.Invoke(message);
    }

    private sealed class Scope : IDisposable
    {
        public void Dispose()
        {
            PromptResults.Clear();
            logger = null;
            IsActive = false;
        }
    }
}

internal static class BatchWorkspace
{
    internal static int PrepareCleanCopy(string sourceRoot, string targetRoot, GameConfig sourceConfig, bool restoreBackups)
    {
        if (Directory.Exists(targetRoot))
            Directory.Delete(targetRoot, true);

        CopyDirectory(sourceRoot, targetRoot);
        if (!restoreBackups)
            return 0;

        return OverlayPk3DSBackups(sourceRoot, targetRoot, sourceConfig);
    }

    private static void CopyDirectory(string sourceRoot, string targetRoot)
    {
        Directory.CreateDirectory(targetRoot);
        foreach (string directory in Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceRoot, directory);
            Directory.CreateDirectory(Path.Combine(targetRoot, relative));
        }

        foreach (string source in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceRoot, source);
            string target = Path.Combine(targetRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(source, target, true);
        }
    }

    internal static void DeleteDirectoryBestEffort(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return;

        Exception last = null;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                Directory.Delete(path, true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                last = ex;
                Thread.Sleep(150 * (attempt + 1));
            }
        }

        if (Directory.Exists(path) && last is not null)
            throw new IOException($"Could not delete batch staging directory '{path}'.", last);
    }

    private static int OverlayPk3DSBackups(string sourceRoot, string targetRoot, GameConfig config)
    {
        string sourceRomFS = config.RomFS;
        string sourceExeFS = config.ExeFS;
        string stageRomFS = Path.Combine(targetRoot, Path.GetRelativePath(sourceRoot, sourceRomFS));
        string stageExeFS = string.IsNullOrWhiteSpace(sourceExeFS)
            ? string.Empty
            : Path.Combine(targetRoot, Path.GetRelativePath(sourceRoot, sourceExeFS));

        string gameFolder = new DirectoryInfo(sourceRomFS).Parent?.Name ?? new DirectoryInfo(sourceRoot).Name;
        string gameBackup = Path.Combine(GameBackup.bakpath, gameFolder);
        string bakA = Path.Combine(gameBackup, GameBackup.baka);
        string bakExeFS = Path.Combine(gameBackup, GameBackup.bakexefs);
        string bakDll = Path.Combine(gameBackup, GameBackup.bakdll);
        int restored = 0;

        // GARC backups use pk3DS' "name (relativegarcpath)" naming convention.
        if (Directory.Exists(bakA))
        {
            foreach (var file in config.Files)
            {
                string garc = config.GetGARCFileName(file.Name);
                string backupName = $"{file.Name} ({garc.Replace(Path.DirectorySeparatorChar.ToString(), string.Empty)})";
                string source = Path.Combine(bakA, backupName);
                string target = Path.Combine(stageRomFS, garc);
                if (CopyIfExists(source, target))
                    restored++;
            }
        }

        if (Directory.Exists(bakExeFS) && !string.IsNullOrWhiteSpace(stageExeFS))
            restored += OverlayExeFSBackups(bakExeFS, stageExeFS);

        // Restore every backed CRO/CRS/CRR file, including custom-edit targets such
        // as Shop.cro or Battle.cro when they are present in the backup set.
        if (Directory.Exists(bakDll))
            restored += OverlayDirectory(bakDll, stageRomFS);

        return restored;
    }

    private static int OverlayExeFSBackups(string sourceRoot, string targetRoot)
    {
        int count = 0;
        foreach (string source in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(source);
            // pk3DS editors expect a decompressed code binary. If an old backup is
            // compressed, keep the already-loaded source project's decompressed copy
            // instead of triggering the normal asynchronous decompression prompt.
            if (name.Contains("code", StringComparison.OrdinalIgnoreCase) && new FileInfo(source).Length % 0x200 != 0)
                continue;

            string relative = Path.GetRelativePath(sourceRoot, source);
            string target = Path.Combine(targetRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(source, target, true);
            count++;
        }
        return count;
    }

    private static int OverlayDirectory(string sourceRoot, string targetRoot)
    {
        int count = 0;
        foreach (string source in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceRoot, source);
            string target = Path.Combine(targetRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(source, target, true);
            count++;
        }
        return count;
    }

    private static bool CopyIfExists(string source, string target)
    {
        if (!File.Exists(source))
            return false;
        Directory.CreateDirectory(Path.GetDirectoryName(target));
        File.Copy(source, target, true);
        return true;
    }
}

internal static class BatchGen7ActionExecutor
{
    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        "rng.seed",
        "moves.balance",
        "moves.randomize",
        "moves.metronome",
        "personal.randomize",
        "personal.modify-all",
        "pokemon-stats.apply",
        "evolutions.normalize",
        "evolutions.randomize",
        "evolutions.remove-trade",
        "evolutions.every-level",
        "level-up-moves.randomize",
        "level-up-moves.metronome",
        "egg-moves.randomize",
        "tms.randomize",
        "tms.sanity",
        "tms.follow-evolutions",
        "trainers.randomize",
        "wild-encounters.randomize",
        "wild-encounters.progressive",
        "wild-encounters.scale-levels",
        "wild-encounters.copy-sos",
        "wild-encounters.modify-levels",
        "starters.randomize",
        "static-encounters.randomize",
        "static-encounters.modify-levels",
        "static-encounters.totem-level-caps",
        "static-encounters.totem-bst",
        "trades.accept-any",
        "trades.randomize-offers",
        "trades.hide-species-names",
        "pickup.randomize",
        "marts.randomize",
        "marts.randomize-bp",
        "marts.expanded-layout",
        "marts.add-rare-candies",
        "marts.add-ev-items",
        "marts.free-mega-stones",
        "marts.ban-ability-capsule",
        "move-tutors.randomize",
        "move-tutors.sanity",
        "move-tutors.follow-evolutions",
        "move-tutors.free",
        "field-items.randomize",
        "shiny-rate.apply",
        "mega-evolution.unlock-from-start",
        "battle.persistent-consumables",
        "player.level-caps",
    };

    internal static string[] GetUnsupported(IEnumerable<GlobalRandomizationAction> actions)
        => (actions ?? Enumerable.Empty<GlobalRandomizationAction>())
            .Where(z => z != null && !string.IsNullOrWhiteSpace(z.Id) && !Supported.Contains(z.Id))
            .Select(z => z.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(z => z, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    internal static void Execute(GlobalRandomizationTemplate template)
    {
        if (Main.Config is null || Main.Config.Generation != 7)
            throw new NotSupportedException("Batch ROM Builder currently supports Gen 7 projects (SM/USUM).");

        var actions = (template.Actions ?? new List<GlobalRandomizationAction>())
            .Where(z => z != null && !string.IsNullOrWhiteSpace(z.Id))
            .ToDictionary(z => z.Id, z => z, StringComparer.OrdinalIgnoreCase);

        RunMoves(actions);
        RunPersonal(actions);
        RunEvolutions(actions);
        RunLevelUpMoves(actions);
        RunEggMoves(actions);
        RunTMs(actions);
        RunStaticEncounters(actions);
        RunWildEncounters(actions, template.Wild);
        RunPickup(actions);
        RunMarts(actions);
        RunTutors(actions);
        RunFieldItems(actions);
        RunShinyRate(actions);
        RunTrainers(actions); // last: Better Movesets sees final moves/learnsets/stats.

        RunGameplayQoLPatches(actions);
        RunPlayerLevelCaps(actions, template.LevelCaps);
        Main.SaveGameText();
    }

    private static bool Has(Dictionary<string, GlobalRandomizationAction> actions, string id)
        => actions.ContainsKey(id);

    private static GlobalRandomizationAction Get(Dictionary<string, GlobalRandomizationAction> actions, string id)
        => actions.TryGetValue(id, out var action) ? action : null;

    private static void RunMoves(Dictionary<string, GlobalRandomizationAction> actions)
    {
        if (!Has(actions, "moves.balance") && !Has(actions, "moves.randomize") && !Has(actions, "moves.metronome"))
            return;

        BatchRuntime.Log("Moves...");
        var g = Main.Config.GARCMoves;
        byte[][] moves = Mini.UnpackMini(g.GetFile(0), "WD");
        using var form = new MoveEditor7(moves);

        if (Has(actions, "moves.randomize")) InvokeEvent(form, "B_RandAll_Click");
        if (Has(actions, "moves.metronome")) InvokeEvent(form, "B_Metronome_Click");
        // Deterministic custom balance is applied last so its explicit values win.
        if (Has(actions, "moves.balance")) InvokeEvent(form, "B_BalanceMoves_Click");
        Invoke(form, "SetEntry");

        g.Files = new[] { Mini.PackMini(moves, "WD") };
        g.Save();
        Main.Config.InitializeMoves();
    }

    private static void RunPersonal(Dictionary<string, GlobalRandomizationAction> actions)
    {
        if (!Has(actions, "personal.randomize") && !Has(actions, "personal.modify-all") && !Has(actions, "pokemon-stats.apply"))
            return;

        BatchRuntime.Log("Pokemon personal data...");
        byte[][] data = Main.Config.GARCPersonal.Files;
        using var form = new PersonalEditor7(data);

        // Deterministic balance comes last so explicit species overrides win over
        // general randomization settings.
        if (Has(actions, "personal.randomize")) InvokeEvent(form, "B_Randomize_Click");
        if (Has(actions, "personal.modify-all")) InvokeEvent(form, "B_ModifyAll");
        if (Has(actions, "pokemon-stats.apply")) InvokeEvent(form, "B_PokemonStatsTemplate_Click");
        Invoke(form, "SaveEntry");

        for (int i = 0; i < data.Length - 1; i++)
            data[i].CopyTo(data[data.Length - 1], i * data[i].Length);
        Main.Config.GARCPersonal.Files = data;
        Main.Config.GARCPersonal.Save();
        Main.Config.InitializePersonal();
    }

    private static void RunEvolutions(Dictionary<string, GlobalRandomizationAction> actions)
    {
        string[] ids = { "evolutions.randomize", "evolutions.every-level", "evolutions.remove-trade", "evolutions.normalize" };
        if (!ids.Any(id => Has(actions, id)))
            return;

        BatchRuntime.Log("Evolutions...");
        var g = Main.Config.GetGARCData("evolution");
        byte[][] data = g.Files;
        using var form = new EvolutionEditor7(data);

        if (Has(actions, "evolutions.randomize")) InvokeEvent(form, "B_RandAll_Click");
        if (Has(actions, "evolutions.every-level")) InvokeEvent(form, "B_EveryLevel_Click");
        if (Has(actions, "evolutions.remove-trade")) InvokeEvent(form, "B_Trade_Click");
        if (Has(actions, "evolutions.normalize")) InvokeEvent(form, "B_NormalizeEvolutions_Click");
        Invoke(form, "SetList");

        g.Files = data;
        g.Save();
        Main.Config.InitializeEvos();
    }

    private static void RunLevelUpMoves(Dictionary<string, GlobalRandomizationAction> actions)
    {
        if (!Has(actions, "level-up-moves.randomize") && !Has(actions, "level-up-moves.metronome"))
            return;

        BatchRuntime.Log("Level-up moves...");
        byte[][] data = Main.Config.GARCLearnsets.Files;
        using var form = new LevelUpEditor7(data);
        if (Has(actions, "level-up-moves.randomize")) InvokeEvent(form, "B_RandAll_Click");
        if (Has(actions, "level-up-moves.metronome")) InvokeEvent(form, "B_Metronome_Click");
        Invoke(form, "SetList");
        Main.Config.GARCLearnsets.Files = data;
        Main.Config.GARCLearnsets.Save();
        Main.Config.InitializeLearnset();
    }

    private static void RunEggMoves(Dictionary<string, GlobalRandomizationAction> actions)
    {
        if (!Has(actions, "egg-moves.randomize"))
            return;

        BatchRuntime.Log("Egg moves...");
        var g = Main.Config.GetGARCData("eggmove");
        byte[][] data = g.Files;
        using var form = new EggMoveEditor7(data);
        InvokeEvent(form, "B_RandAll_Click");
        Invoke(form, "Form_Closing", form, new FormClosingEventArgs(CloseReason.None, false));
        g.Files = data;
        g.Save();
    }

    private static void RunTMs(Dictionary<string, GlobalRandomizationAction> actions)
    {
        if (!Has(actions, "tms.randomize") && !Has(actions, "tms.sanity") && !Has(actions, "tms.follow-evolutions"))
            return;

        BatchRuntime.Log("TMs...");
        using var form = new TMEditor7();

        if (Has(actions, "tms.randomize"))
            InvokeEvent(form, "B_RandomTM_Click");

        if (Has(actions, "tms.sanity"))
            SetCheckBox(form, "CHK_TMSanity", true);

        if (Has(actions, "tms.follow-evolutions"))
            SetCheckBox(form, "CHK_TMFollowEvolutions", true);

        Invoke(form, "Form_Closing", form, new FormClosingEventArgs(CloseReason.None, false));
    }

    private static void RunTrainers(Dictionary<string, GlobalRandomizationAction> actions)
    {
        if (!Has(actions, "trainers.randomize"))
            return;

        BatchRuntime.Log("Trainers...");
        var trdata = Main.Config.GetGARCData("trdata");
        var trpoke = Main.Config.GetGARCData("trpoke");
        byte[][] trd = trdata.Files;
        byte[][] trp = trpoke.Files;
        using var form = new SMTE(trd, trp);
        InvokeEvent(form, "B_Randomize_Click");
        Invoke(form, "SaveEntry");
        trdata.Files = trd;
        trpoke.Files = trp;
        trdata.Save();
        trpoke.Save();
    }

    private static void RunWildEncounters(
        Dictionary<string, GlobalRandomizationAction> actions,
        WildRandomizerTemplate wildTemplate)
    {
        string[] ids =
        {
            "wild-encounters.randomize",
            "wild-encounters.progressive",
            "wild-encounters.scale-levels",
            "wild-encounters.copy-sos",
            "wild-encounters.modify-levels",
        };

        if (!ids.Any(id => Has(actions, id)))
            return;

        bool normal = Has(actions, "wild-encounters.randomize");
        bool progressive = Has(actions, "wild-encounters.progressive");

        if (normal && progressive)
            throw new InvalidDataException("Global template contains both normal and Progressive Wild randomization actions.");

        BatchRuntime.Log("Wild encounters...");
        var ed = Main.Config.GetlzGARCData("encdata");
        var zd = Main.Config.GetlzGARCData("zonedata");
        var wd = Main.Config.GetlzGARCData("worlddata");
        using var form = new SMWE(ed, zd, wd);

        if (wildTemplate is not null)
            form.ApplyWildTemplate(wildTemplate, showMessage: false);

        if (progressive && wildTemplate is null)
        {
            var action = Get(actions, "wild-encounters.progressive");
            if (action?.Parameters is null || !action.Parameters.TryGetValue("rules", out string serializedRules))
                throw new InvalidDataException("Progressive Wild requires Wild template state or saved BST rules.");

            form.ApplyProgressiveWildRules(serializedRules);
        }

        if (normal)
        {
            SetCheckBox(form, "CHK_ProgressiveWildBST", false);
            InvokeEvent(form, "B_Randomize_Click");
        }
        else if (progressive)
        {
            SetCheckBox(form, "CHK_ProgressiveWildBST", true);
            InvokeEvent(form, "B_Randomize_Click");
        }

        if (Has(actions, "wild-encounters.scale-levels"))
        {
            ApplyWildScaleParameters(form, Get(actions, "wild-encounters.scale-levels"));
            InvokeEvent(form, "ApplyAdvancedWildLevelScaling");
        }
        if (Has(actions, "wild-encounters.copy-sos")) InvokeEvent(form, "CopySOS_Click");
        if (Has(actions, "wild-encounters.modify-levels")) InvokeEvent(form, "ModifyAllLevelRanges");
        InvokeEvent(form, "B_Save_Click");
        ed.Save();
    }

    private static void ApplyWildScaleParameters(Form form, GlobalRandomizationAction action)
    {
        if (action?.Parameters is null)
            return;
        if (action.Parameters.TryGetValue("flat", out string flat) && decimal.TryParse(flat, out decimal flatValue))
            SetNumeric(form, "NUD_WildLevelFlat", flatValue);
        if (action.Parameters.TryGetValue("multiplier", out string mult) && decimal.TryParse(mult, out decimal multValue))
            SetNumeric(form, "NUD_WildLevelMultiplier", multValue);
        if (action.Parameters.TryGetValue("keepRange", out string keep) && bool.TryParse(keep, out bool keepValue))
            SetCheckBox(form, "CHK_WildLevelKeepRange", keepValue);
    }

    private static void RunStaticEncounters(Dictionary<string, GlobalRandomizationAction> actions)
    {
        string[] ids = { "starters.randomize", "static-encounters.randomize", "static-encounters.modify-levels", "static-encounters.totem-level-caps", "trades.accept-any", "trades.randomize-offers", "trades.hide-species-names", "static-encounters.totem-bst" };
        if (!ids.Any(id => Has(actions, id)))
            return;

        BatchRuntime.Log("Starters / static encounters / trades...");
        var g = Main.Config.GetGARCData("encounterstatic");
        byte[][] data = g.Files;
        using var form = new StaticEncounterEditor7(data);
        if (Has(actions, "starters.randomize")) InvokeEvent(form, "B_Starters_Click");
        if (Has(actions, "static-encounters.randomize")) InvokeEvent(form, "B_RandAll_Click");
        if (Has(actions, "trades.accept-any")) InvokeEvent(form, "B_TradeAnyRequest_Click");
        if (Has(actions, "trades.randomize-offers")) InvokeEvent(form, "B_TradeAcceptAnyRandomOffer_Click");
        if (Has(actions, "trades.hide-species-names")) Invoke(form, "ApplyHideTradeSpeciesNames");
        if (Has(actions, "static-encounters.modify-levels")) InvokeEvent(form, "ModifyLevels");
        if (Has(actions, "static-encounters.totem-bst"))
            Invoke(form, "ApplyTotemBSTFromTemplate", Get(actions, "static-encounters.totem-bst"));
        if (Has(actions, "static-encounters.totem-level-caps"))
            Invoke(form, "ApplyTotemLevelCapsFromTemplate", Get(actions, "static-encounters.totem-level-caps"));
        InvokeEvent(form, "B_Save_Click");
        g.Files = data;
        g.Save();
    }

    private static void RunPickup(Dictionary<string, GlobalRandomizationAction> actions)
    {
        if (!Has(actions, "pickup.randomize"))
            return;
        BatchRuntime.Log("Pickup...");
        var pickup = Main.Config.GetlzGARCData("pickup");
        using var form = new PickupEditor7(pickup);
        InvokeEvent(form, "B_Randomize_Click");
        InvokeEvent(form, "B_Save_Click");
    }

    private static void RunMarts(Dictionary<string, GlobalRandomizationAction> actions)
    {
        string[] ids = { "marts.randomize", "marts.randomize-bp", "marts.expanded-layout", "marts.add-rare-candies", "marts.add-ev-items", "marts.free-mega-stones", "marts.ban-ability-capsule" };
        if (!ids.Any(id => Has(actions, id)))
            return;

        BatchRuntime.Log("Marts / BP items...");
        if (Main.Config.USUM)
        {
            using var form = new MartEditor7UU();
            ApplyMartActions(form, actions, usum: true);
            return;
        }

        if (Has(actions, "marts.free-mega-stones"))
            throw new NotSupportedException("Free Mega Stones batch action is only available in the USUM Mart Editor.");

        using (var form = new MartEditor7())
            ApplyMartActions(form, actions, usum: false);
    }

    private static void ApplyMartActions(Form form, Dictionary<string, GlobalRandomizationAction> actions, bool usum)
    {
        if (Has(actions, "marts.expanded-layout"))
        {
            if (!usum)
                throw new NotSupportedException("Expanded Marts batch action is currently supported only for USUM.");

            Invoke(
                form,
                "ApplyExpandedMartsFromTemplate",
                Get(actions, "marts.expanded-layout"));
        }

        if (Has(actions, "marts.randomize"))
        {
            var action = Get(actions, "marts.randomize");
            bool specialOnly = GetBool(action, "specialOnly", false);
            SetOptionalCheckBoxFromParameter(form, "CHK_XItems", action, "keepXItems");
            BatchRuntime.QueuePromptResults(DialogResult.Yes, specialOnly ? DialogResult.Yes : DialogResult.No);
            if (usum)
                Invoke(form, "RandomizeItems");
            else
                InvokeEvent(form, "B_Randomize_Click");
        }

        if (Has(actions, "marts.randomize-bp"))
        {
            BatchRuntime.QueuePromptResults(DialogResult.Yes);
            if (usum)
                Invoke(form, "RandomizeBPItems");
            else
                InvokeEvent(form, "B_RandomizeBP_Click");
        }

        // Deterministic progression items are applied after inventory randomization.
        if (Has(actions, "marts.add-rare-candies"))
        {
            if (usum)
            {
                Invoke(
                    form,
                    "ApplyRareCandiesFromTemplate",
                    Get(actions, "marts.add-rare-candies"));
            }
            else
            {
                InvokeEvent(
                    form,
                    "B_AddRareCandies_Click");
            }
        }
        if (Has(actions, "marts.add-ev-items"))
        {
            if (usum)
            {
                Invoke(
                    form,
                    "ApplyEVItemsFromTemplate",
                    Get(actions, "marts.add-ev-items"));
            }
            else
            {
                InvokeEvent(
                    form,
                    "B_AddEVItems_Click");
            }
        }
        if (Has(actions, "marts.free-mega-stones")) InvokeEvent(form, "B_FreeMegaStones_Click");
        if (Has(actions, "marts.ban-ability-capsule"))
        {
            if (!usum)
                throw new NotSupportedException("Ban Ability Capsule batch action is currently implemented for the USUM Mart Editor.");
            InvokeEvent(form, "B_BanAbilityCapsule_Click");
        }
        InvokeEvent(form, "B_Save_Click");
    }

    private static void RunTutors(Dictionary<string, GlobalRandomizationAction> actions)
    {
        if (!Has(actions, "move-tutors.randomize") && !Has(actions, "move-tutors.sanity") && !Has(actions, "move-tutors.follow-evolutions") && !Has(actions, "move-tutors.free"))
            return;

        if (!Main.Config.USUM)
            throw new NotSupportedException("Move Tutor batch actions are currently supported for USUM only.");

        BatchRuntime.Log("Move Tutors...");
        using var form = new TutorEditor7();

        if (Has(actions, "move-tutors.randomize"))
        {
            BatchRuntime.QueuePromptResults(DialogResult.Yes);
            InvokeEvent(form, "B_Randomize_Click");
        }

        if (Has(actions, "move-tutors.free"))
        {
            BatchRuntime.QueuePromptResults(DialogResult.Yes);
            InvokeEvent(form, "B_FreeTutors_Click");
        }

        if (Has(actions, "move-tutors.sanity"))
            SetCheckBox(form, "CHK_TutorSanity", true);

        if (Has(actions, "move-tutors.follow-evolutions"))
            SetCheckBox(form, "CHK_TutorFollowEvolutions", true);

        InvokeEvent(form, "B_Save_Click");
    }

    private static void RunFieldItems(Dictionary<string, GlobalRandomizationAction> actions)
    {
        if (!Has(actions, "field-items.randomize"))
            return;
        BatchRuntime.Log("Field items...");
        var result = FieldItemDumper.RandomizeDefault();
        BatchRuntime.Log(result.Summary);
    }
    private static void RunGameplayQoLPatches(
        Dictionary<string, GlobalRandomizationAction> actions)
    {
        bool mega =
            Has(actions, "mega-evolution.unlock-from-start");

        bool persistentConsumables =
            Has(actions, "battle.persistent-consumables");

        if (!mega && !persistentConsumables)
            return;

        BatchRuntime.Log("Gameplay QoL patches...");

        if (mega)
        {
            int changed =
                Gen7MegaEventFlagPatcher.Apply();

            BatchRuntime.Log(
                changed == 0
                    ? "Mega Evolution from Start was already enabled."
                    : "Mega Evolution from Start enabled.");
        }

        if (persistentConsumables)
        {
            int changed =
                Gen7PersistentConsumablesPatcher.Apply();

            BatchRuntime.Log(
                changed == 0
                    ? "Persistent Battle Consumables were already enabled."
                    : $"Persistent Battle Consumables enabled ({changed} instruction(s) changed).");
        }
    }


    private static void RunPlayerLevelCaps(
        Dictionary<string, GlobalRandomizationAction> actions,
        LevelCapTemplate template)
    {
        if (!Has(actions, Gen7LevelCapPatcher.ActionId))
            return;

        if (Main.Config?.USUM != true)
            throw new NotSupportedException("Player Level Caps batch replay is currently supported only for USUM.");

        if (template is null)
        {
            throw new InvalidDataException(
                "The Global ROM Template requests Player Level Caps, but it does not contain a Player Level Caps table.");
        }

        LevelCapTemplateFile.Validate(template, "USUM");
        LevelCapTable table = template.ToTable();

        BatchRuntime.Log("Player Level Caps...");
        int changed = Gen7LevelCapPatcher.Apply(table, out string report);
        BatchRuntime.Log(
            changed == 0
                ? "Player Level Caps were already enabled."
                : $"Player Level Caps enabled ({changed} binary file(s) changed).");

        foreach (string line in report.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries))
            BatchRuntime.Log(line);
    }


    private static void RunShinyRate(Dictionary<string, GlobalRandomizationAction> actions)
    {
        var action = Get(actions, "shiny-rate.apply");
        if (action is null)
            return;

        BatchRuntime.Log("Shiny rate...");
        using var form = new ShinyRate();
        if (action.Parameters.TryGetValue("rerolls", out string rerolls) && decimal.TryParse(rerolls, out decimal value))
            SetNumeric(form, "NUD_Rerolls", value);
        if (action.Parameters.TryGetValue("everythingShiny", out string shiny) && bool.TryParse(shiny, out bool isShiny))
            SetCheckBox(form, "CHK_EverythingShiny", isShiny);
        InvokeEvent(form, "B_Save_Click");
    }

    private static void SetOptionalCheckBoxFromParameter(Control root, string controlName, GlobalRandomizationAction action, string parameterName)
    {
        if (action?.Parameters is null || !action.Parameters.TryGetValue(parameterName, out string raw) || !bool.TryParse(raw, out bool value))
            return;
        SetCheckBox(root, controlName, value);
    }

    private static bool GetBool(GlobalRandomizationAction action, string key, bool fallback)
    {
        if (action?.Parameters is null || !action.Parameters.TryGetValue(key, out string value))
            return fallback;
        return bool.TryParse(value, out bool result) ? result : fallback;
    }

    private static void SetNumeric(Control root, string name, decimal value)
    {
        var control = root.Controls.Find(name, true).OfType<NumericUpDown>().FirstOrDefault();
        if (control is null)
            return;
        control.Value = Math.Max(control.Minimum, Math.Min(control.Maximum, value));
    }

    private static void SetCheckBox(Control root, string name, bool value)
    {
        var control = root.Controls.Find(name, true).OfType<CheckBox>().FirstOrDefault();
        if (control is not null)
            control.Checked = value;
    }

    private static void InvokeEvent(object target, string methodName)
        => Invoke(target, methodName, target, EventArgs.Empty);

    private static object Invoke(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(target.GetType().FullName, methodName);
        try
        {
            return method.Invoke(target, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }
}
