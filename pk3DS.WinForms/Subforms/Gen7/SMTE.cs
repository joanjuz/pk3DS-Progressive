using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Windows.Forms;

using pk3DS.Core;
using pk3DS.Core.Randomizers;
using pk3DS.Core.Structures;

namespace pk3DS.WinForms;

public partial class SMTE : Form
{
    private CheckBox CHK_SmartHeldItems;
    private CheckBox CHK_ItemClause;
    private ComboBox CB_SmartHeldItemMode;
    private ComboBox CB_SmartHeldItemModeImportant;
    private ComboBox CB_SmartHeldItemModeBoss;
    private CheckBox CHK_BetterMovesets;
    private CheckBox CHK_BetterMovesetsNormalTrainers;
    private CheckBox CHK_BetterMovesetsImportantTrainers;
    private CheckBox CHK_BetterMovesetsBosses;

    private CheckBox CHK_SmartItemsNormalTrainers;
    private CheckBox CHK_SmartItemsImportantTrainers;
    private CheckBox CHK_SmartItemsBosses;
    private readonly LearnsetRandomizer learn = new(Main.Config, Main.Config.Learnsets);
    private readonly TrainerData7[] Trainers;
    private string[][] AltForms;
    private static int[] SpecialClasses;
    // Universal Pokemon Randomizer ZX trainer classification.
    // Important = RIVAL*, FRIEND*, *STRONG.
    // Boss = ELITE*, CHAMPION*, UBER*, *LEADER.
    //
    // Keep these lists local to the trainer-template feature so the older
    // pk3DS Legal.ImportantTrainers_* arrays remain untouched elsewhere.
    private static readonly int[] UPRZXImportantTrainers_SM =
    [
        006, 007, 008, 009, 010, 011, 012, 013, 014,
        052, 074, 075, 076, 077, 078, 079, 082, 083, 084, 089,
        129, 132, 144, 146, 167, 185,
        215, 216, 217, 218, 219, 220, 221, 222,
        238, 239, 240, 241,
        356, 357, 358, 360,
        396, 398, 401, 405, 410, 412, 413, 414,
        415, 416, 417, 418, 419,
        435, 438, 439, 440, 441,
        447, 448, 449, 450, 451, 452,
        467, 477, 478, 479, 481, 482, 483, 484,
    ];

    private static readonly int[] UPRZXBossTrainers_SM =
    [
        023, 090, 131, 138, 149, 152, 153, 154, 155, 156, 158,
        235, 236,
        349, 350, 351, 352, 359, 400, 403,
    ];

    private static readonly int[] UPRZXImportantTrainers_USUM =
    [
        009, 010, 011, 012, 013, 014,
        052, 074, 075, 076, 077, 078, 079, 082, 083, 084, 089,
        132, 144, 146, 185,
        215, 216, 217, 218, 219, 220, 221, 222,
        238, 239, 240, 241,
        356, 357, 358,
        396, 398, 401, 405, 410, 412,
        415, 416, 417, 418, 419,
        438, 439, 440, 441,
        447, 448, 449, 450, 451, 452,
        477, 478, 479,
        491, 492, 493, 494, 495, 496,
        498, 499, 500, 501, 502, 503, 504, 505, 506, 507,
        561, 623, 648, 649, 651, 652,
    ];

    private static readonly int[] UPRZXBossTrainers_USUM =
    [
        023, 090, 131, 138, 149, 153, 154, 156,
        235, 236,
        350, 351, 352, 359,
        489, 490, 497, 508,
        541, 542, 543, 558, 559, 560, 562, 572, 573, 580,
        644, 645, 647, 650,
    ];

    private static readonly int[] ImportantTrainers = Main.Config.USUM
        ? UPRZXImportantTrainers_USUM
        : UPRZXImportantTrainers_SM;

    private static readonly int[] BossTrainers = Main.Config.USUM
        ? UPRZXBossTrainers_USUM
        : UPRZXBossTrainers_SM;
    private static int[] FinalEvo = Legal.FinalEvolutions_7;
    private static readonly int[] Legendary = Main.Config.USUM ? Legal.Legendary_USUM : Legal.Legendary_SM;
    private static readonly int[] Mythical = Main.Config.USUM ? Legal.Mythical_USUM : Legal.Mythical_SM;
    private static Dictionary<int, int[]> MegaDictionary;
    private int index = -1;
    private PictureBox[] pba;
    private CheckBox[] AIBits;
    private CheckBox CHK_ProgressiveBST;
    private Button B_SetManualBST;
    private CheckBox CHK_LevelCaps;
    private Button B_SetLevelCaps;
    private Button B_SetTrainerMoveRules;
    private Button B_TrainerTemplate;
    private CheckBox CHK_RandomDoubleBattles;
    private NumericUpDown NUD_DoubleBattleChance;
    private CheckBox CHK_BanBadItems;

    private List<ProgressiveBSTRule> ProgressiveBSTRules = GetDefaultProgressiveBSTRules();
    private List<TrainerLevelCapRule> LevelCapRules = [];
    private List<TrainerMoveRule> MoveRules = [];
    private bool ApplyCapsToPreviousTrainers = true;
    private int PreviousTrainerGap = 2;
    private decimal RegularTrainerCurvePower = 1.6m;
    private bool GuaranteeMegaInImportantBattles = false;
    // The Rules tab temporarily expands the randomizer workspace.
    // Other randomizer tabs keep their original dimensions.
    private bool Gen7RulesWorkspaceHooked;
    private TabPage Gen7RulesTab;
    private int Gen7BaseRandomizerHeight = -1;
    private int Gen7BaseTrainerDataHeight = -1;
    private int Gen7BaseTeamLabelTop = -1;
    private int[] Gen7BaseTeamTops;


    //private readonly byte[][] trclass;
    private readonly byte[][] trdata;
    private readonly byte[][] trpoke;
    private readonly string[] abilitylist = Main.Config.GetText(TextName.AbilityNames);
    private readonly string[] movelist = Main.Config.GetText(TextName.MoveNames);
    private readonly string[] itemlist = Main.Config.GetText(TextName.ItemNames);
    private readonly string[] specieslist = Main.Config.GetText(TextName.SpeciesNames);
    private readonly string[] types = Main.Config.GetText(TextName.Types);
    private readonly string[] natures = Main.Config.GetText(TextName.Natures);
    private readonly string[] forms = Enumerable.Range(0, 1000).Select(i => i.ToString("000")).ToArray();
    private readonly string[] trName = Main.Config.GetText(TextName.TrainerNames);
    private readonly string[] trClass = Main.Config.GetText(TextName.TrainerClasses);
    //private readonly TextData trText = Main.Config.GetTextData(TextName.TrainerText);
    private readonly TextData TrainerNames;

    public SMTE(byte[][] trd, byte[][] trp)
    {
        //trclass = trc;
        trdata = trd;
        trpoke = trp;
        TrainerNames = new TextData(trName);
        InitializeComponent();

        // Allow an exact trainer shiny chance from 0.00% through 100.00%.
        // Keep the control inside the existing RandSettings/Global Template flow.
        NUD_Shiny.DecimalPlaces = 2;
        NUD_Shiny.Increment = 0.01m;
        NUD_Shiny.Minimum = 0m;
        NUD_Shiny.Maximum = 100m;
        NUD_Shiny.Left = 264;
        NUD_Shiny.Width = 64;
        AddSmartHeldItemControls();
        AddBetterMovesetControls();
        AddProgressiveBSTControls();

        mnuView.Click += ClickView;
        mnuSet.Click += ClickSet;
        mnuDelete.Click += ClickDelete;
        Trainers = new TrainerData7[trdata.Length];
        Setup();
        LevelCapRules = GetLevelCapCandidates();
        MoveRules = TrainerMoveRule.FromLevelCapRules(LevelCapRules);
        AddLevelCapControls();
        FixGen7TrainerOptionsLayout();
        AddBanBadItemsControl();
        PlaceGen7ImplementedOptionsInRulesTab();
        foreach (var pb in pba)
            pb.Click += ClickSlot;

        CB_TrainerID.SelectedIndex = 0;
        CB_Moves.SelectedIndex = 0;
        MegaDictionary = GiftEditor6.GetMegaDictionary(Main.Config);

        if (CHK_RandomClass.Checked)
        {
            SpecialClasses = CHK_IgnoreSpecialClass.Checked
                ? Main.Config.USUM
                    ? Legal.SpecialClasses_USUM
                    : Legal.SpecialClasses_SM
                : [];
        }

        RandSettings.GetFormSettings(this, Tab_Rand.Controls);

        if (TrainerRandomizerTemplateFile.TryGetCurrent(CurrentTrainerTemplateGame, out var currentTemplate))
            ApplyTrainerTemplate(currentTemplate, showMessage: false);
        // RandSettings can restore the original CHK_RandomItems coordinates
        // after our custom Rules layout has already run. Re-apply the layout
        // last so Random Held Items and Smart Items cannot overlap.
        PlaceGen7ImplementedOptionsInRulesTab();
    }
    private sealed class ProgressiveBSTRule
    {
        public int MinLevel { get; set; }
        public int MaxLevel { get; set; }
        public int MinBST { get; set; }
        public int MaxBST { get; set; }
        public bool FullRandom { get; set; }

        public ProgressiveBSTRule Clone()
        {
            return new ProgressiveBSTRule
            {
                MinLevel = MinLevel,
                MaxLevel = MaxLevel,
                MinBST = MinBST,
                MaxBST = MaxBST,
                FullRandom = FullRandom,
            };
        }
    }
    private static List<ProgressiveBSTRule> GetDefaultProgressiveBSTRules()
    {
        return
        [
            new ProgressiveBSTRule { MinLevel = 1,  MaxLevel = 10,  MinBST = 180, MaxBST = 320, FullRandom = false },
        new ProgressiveBSTRule { MinLevel = 11, MaxLevel = 20,  MinBST = 220, MaxBST = 380, FullRandom = false },
        new ProgressiveBSTRule { MinLevel = 21, MaxLevel = 30,  MinBST = 280, MaxBST = 450, FullRandom = false },
        new ProgressiveBSTRule { MinLevel = 31, MaxLevel = 40,  MinBST = 340, MaxBST = 520, FullRandom = false },
        new ProgressiveBSTRule { MinLevel = 41, MaxLevel = 50,  MinBST = 400, MaxBST = 580, FullRandom = false },
        new ProgressiveBSTRule { MinLevel = 51, MaxLevel = 100, MinBST = 480, MaxBST = 680, FullRandom = false },
    ];
    }
    private void ShowManualBSTDialog()
    {
        using var form = new Form
        {
            Text = "Manual Progressive BST Settings",
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(560, 360),
            MinimizeBox = false,
            MaximizeBox = false,
            FormBorderStyle = FormBorderStyle.FixedDialog,
        };

        var editableRules = new System.ComponentModel.BindingList<ProgressiveBSTRule>(
            ProgressiveBSTRules.Select(r => r.Clone()).ToList()
        );

        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            DataSource = editableRules,
            AllowUserToAddRows = true,
            AllowUserToDeleteRows = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
        };

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ProgressiveBSTRule.MinLevel),
            HeaderText = "Min Level",
            Width = 80,
        });

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ProgressiveBSTRule.MaxLevel),
            HeaderText = "Max Level",
            Width = 80,
        });

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ProgressiveBSTRule.MinBST),
            HeaderText = "Min BST",
            Width = 80,
        });

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ProgressiveBSTRule.MaxBST),
            HeaderText = "Max BST",
            Width = 80,
        });

        grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(ProgressiveBSTRule.FullRandom),
            HeaderText = "Full Random",
            Width = 95,
        });

        grid.DataError += (_, e) =>
        {
            e.ThrowException = false;
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 42,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(6),
        };

        var ok = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.None,
            Width = 80,
        };

        var cancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Width = 80,
        };

        var reset = new Button
        {
            Text = "Reset Defaults",
            Width = 105,
        };

        reset.Click += (_, _) =>
        {
            editableRules.Clear();

            foreach (var rule in GetDefaultProgressiveBSTRules())
                editableRules.Add(rule);
        };

        ok.Click += (_, _) =>
        {
            grid.EndEdit();

            var candidateRules = editableRules
                .Select(r => r.Clone())
                .OrderBy(r => r.MinLevel)
                .ToList();

            if (!ValidateProgressiveBSTRules(candidateRules))
                return;

            ProgressiveBSTRules = candidateRules;

            form.DialogResult = DialogResult.OK;
            form.Close();
        };

        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(reset);

        form.Controls.Add(grid);
        form.Controls.Add(buttons);

        form.ShowDialog(this);
    }
    private static bool ValidateProgressiveBSTRules(List<ProgressiveBSTRule> rules)
    {
        if (rules.Count == 0)
        {
            WinFormsUtil.Alert("You must define at least one BST range.");
            return false;
        }

        foreach (var rule in rules)
        {
            if (rule.MinLevel < 1 || rule.MaxLevel > 100 || rule.MinLevel > rule.MaxLevel)
            {
                WinFormsUtil.Alert("Invalid level range detected. Levels must be between 1 and 100, and Min Level must be less than or equal to Max Level.");
                return false;
            }

            if (!rule.FullRandom && (rule.MinBST < 1 || rule.MaxBST > 999 || rule.MinBST > rule.MaxBST))
            {
                WinFormsUtil.Alert("Invalid BST range detected. BST must be between 1 and 999, and Min BST must be less than or equal to Max BST.");
                return false;
            }
        }

        for (int i = 1; i < rules.Count; i++)
        {
            if (rules[i].MinLevel <= rules[i - 1].MaxLevel)
            {
                WinFormsUtil.Alert("Overlapping level ranges detected. Please make sure level ranges do not overlap.");
                return false;
            }
        }

        return true;
    }
    private ProgressiveBSTRule GetProgressiveBSTRule(int level)
    {
        var rule = ProgressiveBSTRules.FirstOrDefault(r => level >= r.MinLevel && level <= r.MaxLevel);

        if (rule is not null)
            return rule;

        var fallback = SpeciesRandomizer.GetProgressiveBSTRange(level);

        return new ProgressiveBSTRule
        {
            MinLevel = level,
            MaxLevel = level,
            MinBST = fallback.MinBST,
            MaxBST = fallback.MaxBST,
            FullRandom = false,
        };
    }
    private int GetProgressiveRandomSpecies(SpeciesRandomizer rnd, int oldSpecies, int type, int level)
    {
        var rule = GetProgressiveBSTRule(level);

        if (rule.FullRandom)
        {
            return type == -1
                ? rnd.GetRandomSpecies(oldSpecies)
                : rnd.GetRandomSpeciesType(oldSpecies, type);
        }

        return rnd.GetRandomSpeciesProgressiveBST(
            oldSpecies,
            type,
            rule.MinBST,
            rule.MaxBST
        );
    }
    private List<TrainerLevelCapRule> GetLevelCapCandidates()
    {
        var candidates = new List<TrainerLevelCapRule>();

        foreach (int id in ImportantTrainers
            .Concat(BossTrainers)
            .Distinct()
            .Where(id => id > 0 && id < Trainers.Length))
        {
            var trainer = Trainers[id];
            if (trainer.Pokemon.Count == 0)
                continue;

            candidates.Add(new TrainerLevelCapRule
            {
                Enabled = true,
                TrainerID = id,
                Group = BossTrainers.Contains(id) ? "Boss" : "Important",
                Trainer = GetTrainerDisplayName(trainer),
                CurrentAceLevel = GetAceLevel(trainer),
                LevelCap = 0,
            });
        }

        return candidates
            .OrderBy(r => r.CurrentAceLevel)
            .ThenBy(r => r.TrainerID)
            .ToList();
    }

    private string GetTrainerDisplayName(TrainerData7 trainer)
    {
        string name = string.IsNullOrWhiteSpace(trainer.Name) ? "UNKNOWN" : trainer.Name;
        string cls = trainer.TrainerClass < trClass.Length ? trClass[trainer.TrainerClass] : string.Empty;

        return string.IsNullOrWhiteSpace(cls) ? name : $"{cls} {name}";
    }

    private string CurrentTrainerTemplateGame => Main.Config.USUM ? "USUM" : "SM";

    private void ShowTrainerTemplateMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Load template...", null, (_, _) => LoadTrainerTemplate());
        menu.Items.Add("Save current template...", null, (_, _) => SaveTrainerTemplate());
        menu.Show(B_TrainerTemplate, new Point(0, B_TrainerTemplate.Height));
    }

    private void LoadTrainerTemplate()
    {
        try
        {
            Directory.CreateDirectory(TrainerRandomizerTemplateFile.TemplateDirectory);
            using var dialog = new OpenFileDialog
            {
                Title = "Load trainer randomizer template",
                Filter = "Trainer template (*.json)|*.json|All files (*.*)|*.*",
                InitialDirectory = TrainerRandomizerTemplateFile.TemplateDirectory,
                CheckFileExists = true,
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            var template = TrainerRandomizerTemplateFile.Load(dialog.FileName, CurrentTrainerTemplateGame);
            TrainerRandomizerTemplateFile.SetCurrent(template, CurrentTrainerTemplateGame);
            ApplyTrainerTemplate(template);
        }
        catch (Exception ex)
        {
            WinFormsUtil.Alert($"Could not load trainer template.\n\n{ex.Message}");
        }
    }

    private void SaveTrainerTemplate()
    {
        try
        {
            Directory.CreateDirectory(TrainerRandomizerTemplateFile.TemplateDirectory);
            using var dialog = new SaveFileDialog
            {
                Title = "Save trainer randomizer template",
                Filter = "Trainer template (*.json)|*.json",
                InitialDirectory = TrainerRandomizerTemplateFile.TemplateDirectory,
                FileName = $"trainer_randomizer_{CurrentTrainerTemplateGame.ToLowerInvariant()}.json",
                AddExtension = true,
                DefaultExt = "json",
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            var template = CaptureTrainerTemplate();
            TrainerRandomizerTemplateFile.SetCurrent(template, CurrentTrainerTemplateGame);
            TrainerRandomizerTemplateFile.Save(dialog.FileName, template, CurrentTrainerTemplateGame);
            WinFormsUtil.Alert("Trainer template saved successfully.");
        }
        catch (Exception ex)
        {
            WinFormsUtil.Alert($"Could not save trainer template.\n\n{ex.Message}");
        }
    }

    private TrainerRandomizerTemplate CaptureTrainerTemplate()
    {
        return new TrainerRandomizerTemplate
        {
            Name = $"{CurrentTrainerTemplateGame} trainer randomizer",
            Game = CurrentTrainerTemplateGame,
            ProgressiveBST = new ProgressiveBSTTemplate
            {
                Enabled = CHK_ProgressiveBST.Checked,
                Ranges = ProgressiveBSTRules.Select(r => new ProgressiveBSTTemplateRule
                {
                    MinLevel = r.MinLevel,
                    MaxLevel = r.MaxLevel,
                    MinBST = r.MinBST,
                    MaxBST = r.MaxBST,
                    FullRandom = r.FullRandom,
                }).ToList(),
            },
            MoveSettings = new TrainerMoveSettingsTemplate
            {
                Source = (TrainerMoveSource)Math.Clamp(CB_Moves.SelectedIndex, 0, 3),

                RandomDoubleBattles = CHK_RandomDoubleBattles?.Checked ?? false,
                DoubleBattleChance = NUD_DoubleBattleChance is null ? 0 : (int)NUD_DoubleBattleChance.Value,
                MaxTrainerAI = CHK_MaxAI.Checked,

                RandomHeldItems = CHK_RandomItems.Checked,
                BanBadItems = CHK_BanBadItems?.Checked ?? false,
                ItemClause = CHK_ItemClause?.Checked ?? false,
                BetterMovesets = CHK_BetterMovesets.Checked,
                BetterMovesetsNormalTrainers = CHK_BetterMovesetsNormalTrainers?.Checked ?? true,
                BetterMovesetsImportantTrainers = CHK_BetterMovesetsImportantTrainers?.Checked ?? true,
                BetterMovesetsBosses = CHK_BetterMovesetsBosses?.Checked ?? true,
                SmartItems = CHK_SmartHeldItems?.Checked ?? false,
                SmartItemsNormalTrainers = CHK_SmartItemsNormalTrainers?.Checked ?? true,
                SmartItemsImportantTrainers = CHK_SmartItemsImportantTrainers?.Checked ?? true,
                SmartItemsBosses = CHK_SmartItemsBosses?.Checked ?? true,
                // Keep the legacy value synchronized with Normal Trainers.
                SmartItemMode = GetSmartTrainerItemMode("Normal"),
                SmartItemModeNormalTrainers = GetSmartTrainerItemMode("Normal"),
                SmartItemModeImportantTrainers = GetSmartTrainerItemMode("Important"),
                SmartItemModeBosses = GetSmartTrainerItemMode("Boss"),
                ForceHighPower = CHK_ForceHighPower.Checked,
                HighPowerLevel = (int)NUD_ForceHighPower.Value,
                NoFixedDamage = CHK_NoFixedDamage.Checked,
                EnsureDamagingMoves = CHK_Damage.Checked,
                DamagingMoveCount = (int)NUD_Damage.Value,
                EnsureSTABMoves = CHK_STAB.Checked,
                STABMoveCount = (int)NUD_STAB.Value,
            },
            LevelCaps = new TrainerLevelCapsTemplate
            {
                Enabled = CHK_LevelCaps.Checked,
                ApplyToPreviousTrainers = ApplyCapsToPreviousTrainers,
                PreviousTrainerGap = PreviousTrainerGap,
                ResetUnlistedTrainers = true,
                Trainers = LevelCapRules.Where(r => r.Enabled).Select(r => new TrainerLevelCapTemplateEntry
                {
                    TrainerID = r.TrainerID,
                    Use = true,
                    LevelCap = r.LevelCap,
                    Mega = r.GuaranteeMega,
                    ZMove = r.GuaranteeZMove,
                }).ToList(),
            },
            TrainerMoveRules = new TrainerMoveRulesTemplate
            {
                ResetUnlistedTrainers = true,
                Trainers = MoveRules.Where(r => r.Enabled).Select(r => new TrainerMoveRuleTemplateEntry
                {
                    TrainerID = r.TrainerID,
                    Use = true,
                    MinMovePower = r.MinMovePower,
                    StrongStat = r.UseStrongestAttackStat,
                    MixedTolerance = r.MixedTolerance,
                    AllowStatusMoves = r.AllowStatusMoves,
                    BetterMovesets = r.BetterMovesets,
                    SmartItems = r.SmartItems,
                    EVs = r.OverrideEVs,
                }).ToList(),
            },
        };
    }

    private void ApplyTrainerTemplate(TrainerRandomizerTemplate template, bool showMessage = true)
    {
        if (template.ProgressiveBST is not null)
        {
            ProgressiveBSTRules = (template.ProgressiveBST.Ranges ?? [])
                .Select(r => new ProgressiveBSTRule
                {
                    MinLevel = r.MinLevel,
                    MaxLevel = r.MaxLevel,
                    MinBST = r.MinBST,
                    MaxBST = r.MaxBST,
                    FullRandom = r.FullRandom,
                })
                .OrderBy(r => r.MinLevel)
                .ToList();

            if (template.ProgressiveBST.Enabled)
                CHK_RandomPKM.Checked = true;
            CHK_ProgressiveBST.Checked = template.ProgressiveBST.Enabled;
        }

        if (template.MoveSettings is not null)
        {
            var moves = template.MoveSettings;
            CB_Moves.SelectedIndex = Math.Clamp((int)moves.Source, 0, Math.Max(0, CB_Moves.Items.Count - 1));
            if (moves.RandomDoubleBattles.HasValue)
                CHK_RandomDoubleBattles.Checked = moves.RandomDoubleBattles.Value;

            if (moves.DoubleBattleChance.HasValue)
                SetTemplateNumericValue(NUD_DoubleBattleChance, moves.DoubleBattleChance.Value);

            if (moves.MaxTrainerAI.HasValue)
                CHK_MaxAI.Checked = moves.MaxTrainerAI.Value;

            if (moves.RandomHeldItems.HasValue)
                CHK_RandomItems.Checked = moves.RandomHeldItems.Value;

            if (moves.BanBadItems.HasValue && CHK_BanBadItems is not null)
                CHK_BanBadItems.Checked = moves.BanBadItems.Value;

            if (moves.ItemClause.HasValue && CHK_ItemClause is not null)
                CHK_ItemClause.Checked = moves.ItemClause.Value;
            CHK_BetterMovesets.Checked = moves.BetterMovesets;
            CHK_BetterMovesetsNormalTrainers.Checked = moves.BetterMovesetsNormalTrainers;
            CHK_BetterMovesetsImportantTrainers.Checked = moves.BetterMovesetsImportantTrainers;
            CHK_BetterMovesetsBosses.Checked = moves.BetterMovesetsBosses;

            CHK_SmartHeldItems.Checked = moves.SmartItems;
            CHK_SmartItemsNormalTrainers.Checked = moves.SmartItemsNormalTrainers;
            CHK_SmartItemsImportantTrainers.Checked = moves.SmartItemsImportantTrainers;
            CHK_SmartItemsBosses.Checked = moves.SmartItemsBosses;
            int legacySmartItemMode = Math.Clamp(moves.SmartItemMode, 0, 2);

            CB_SmartHeldItemMode.SelectedIndex =
                moves.SmartItemModeNormalTrainers >= 0
                    ? Math.Clamp(moves.SmartItemModeNormalTrainers, 0, 2)
                    : legacySmartItemMode;

            CB_SmartHeldItemModeImportant.SelectedIndex =
                moves.SmartItemModeImportantTrainers >= 0
                    ? Math.Clamp(moves.SmartItemModeImportantTrainers, 0, 2)
                    : legacySmartItemMode;

            CB_SmartHeldItemModeBoss.SelectedIndex =
                moves.SmartItemModeBosses >= 0
                    ? Math.Clamp(moves.SmartItemModeBosses, 0, 2)
                    : legacySmartItemMode;

            UpdateBetterMovesetsSubmenuState();
            UpdateSmartItemsSubmenuState();
            CHK_ForceHighPower.Checked = moves.ForceHighPower;
            SetTemplateNumericValue(NUD_ForceHighPower, moves.HighPowerLevel);
            CHK_NoFixedDamage.Checked = moves.NoFixedDamage;
            CHK_Damage.Checked = moves.EnsureDamagingMoves;
            SetTemplateNumericValue(NUD_Damage, moves.DamagingMoveCount);
            CHK_STAB.Checked = moves.EnsureSTABMoves;
            SetTemplateNumericValue(NUD_STAB, moves.STABMoveCount);
        }

        var result = TrainerRandomizerTemplateFile.ApplyTrainerRules(template, LevelCapRules, MoveRules);

        if (template.LevelCaps is not null)
        {
            ApplyCapsToPreviousTrainers = template.LevelCaps.ApplyToPreviousTrainers;
            PreviousTrainerGap = template.LevelCaps.PreviousTrainerGap;
            CHK_LevelCaps.Checked = template.LevelCaps.Enabled && LevelCapRules.Count > 0;
        }

        string warning = BuildTrainerTemplateWarning(result);
        string message = $"Template loaded. BST ranges: {ProgressiveBSTRules.Count}; level caps applied: {result.LevelCapsApplied}; trainer move rules applied: {result.MoveRulesApplied}.";
        if (warning.Length != 0)
            message += $"\n\n{warning}";
        if (showMessage)
            WinFormsUtil.Alert(message);
    }

    private static void SetTemplateNumericValue(NumericUpDown control, int value)
    {
        control.Value = Math.Min(control.Maximum, Math.Max(control.Minimum, value));
    }

    private static string BuildTrainerTemplateWarning(TrainerTemplateApplyResult result)
    {
        var parts = new List<string>();
        if (result.UnknownLevelCapTrainerIDs.Count != 0)
            parts.Add($"Level Cap trainer IDs not found in this game: {string.Join(", ", result.UnknownLevelCapTrainerIDs)}");
        if (result.UnknownMoveRuleTrainerIDs.Count != 0)
            parts.Add($"Move Rule trainer IDs not found in this game: {string.Join(", ", result.UnknownMoveRuleTrainerIDs)}");
        return string.Join("\n", parts);
    }

    private void ShowLevelCapDialog()
    {
        if (LevelCapRules.Count == 0)
        {
            WinFormsUtil.Alert("No important trainers were detected for this game.");
            return;
        }

        TrainerLevelCapDialog.Edit(
            this,
            ref LevelCapRules,
            ref ApplyCapsToPreviousTrainers,
            ref PreviousTrainerGap,
            ref RegularTrainerCurvePower,
            ref GuaranteeMegaInImportantBattles
        );
    }

    private void ShowTrainerMoveRulesDialog()
    {
        if (MoveRules.Count == 0)
        {
            WinFormsUtil.Alert("No important trainers were detected for this game.");
            return;
        }

        TrainerMoveRulesDialog.Edit(this, ref MoveRules, showPerTrainerBetterSmart: false);
    }

    private int GetSlot(object sender)
    {
        var send = ((sender as ToolStripItem)?.Owner as ContextMenuStrip)?.SourceControl ?? sender as PictureBox;
        return Array.IndexOf(pba, send);
    }

    private void ClickSlot(object sender, EventArgs e)
    {
        switch (ModifierKeys)
        {
            case Keys.Control: ClickView(sender, e); break;
            case Keys.Shift: ClickSet(sender, e); break;
            case Keys.Alt: ClickDelete(sender, e); break;
        }
    }

    private void ClickView(object sender, EventArgs e)
    {
        int slot = GetSlot(sender);
        if (pba[slot].Image == null)
        { SystemSounds.Exclamation.Play(); return; }

        // Load the PKM
        var pk = Trainers[index].Pokemon[slot];
        if (pk.Species != 0)
        {
            try { PopulateFieldsTP7(pk); }
            catch { }
            // Visual to display what slot is currently loaded.
            GetSlotColor(slot, Properties.Resources.slotView);
        }
        else
        {
            SystemSounds.Exclamation.Play();
        }
    }

    private void ClickSet(object sender, EventArgs e)
    {
        int slot = GetSlot(sender);
        if (CB_Species.SelectedIndex == 0)
        { WinFormsUtil.Alert("Can't set empty slot."); return; }

        var pk = PrepareTP7();
        var tr = Trainers[index];
        if (slot < tr.NumPokemon)
        {
            tr.Pokemon[slot] = pk;
        }
        else
        {
            tr.Pokemon.Add(pk);
            slot = tr.Pokemon.Count - 1;
            Trainers[index].NumPokemon = (int)++NUD_NumPoke.Value;
        }

        GetQuickFiller(pba[slot], pk);
        GetSlotColor(slot, Properties.Resources.slotSet);
    }

    private void ClickDelete(object sender, EventArgs e)
    {
        int slot = GetSlot(sender);

        if (slot < Trainers[index].NumPokemon)
        {
            Trainers[index].Pokemon.RemoveAt(slot);
            Trainers[index].NumPokemon = (int)--NUD_NumPoke.Value;
        }

        PopulateTeam(Trainers[index]);
        GetSlotColor(slot, Properties.Resources.slotDel);
    }

    private void PopulateTeam(TrainerData7 tr)
    {
        for (int i = 0; i < tr.NumPokemon; i++)
            GetQuickFiller(pba[i], tr.Pokemon[i]);
        for (int i = tr.NumPokemon; i < 6; i++)
            pba[i].Image = null;
    }

    private void GetSlotColor(int slot, Image color)
    {
        foreach (PictureBox t in pba)
            t.BackgroundImage = null;

        pba[slot].BackgroundImage = color;
    }

    private static void GetQuickFiller(PictureBox pb, TrainerPoke7 pk)
    {
        Bitmap rawImg = WinFormsUtil.GetSprite(pk.Species, pk.Form, pk.Gender, pk.Item, Main.Config, pk.Shiny);
        pb.Image = WinFormsUtil.ScaleImage(rawImg, 2);
    }

    // Top Level Functions
    private void RefreshFormAbility(object sender, EventArgs e)
    {
        if (index < 0)
            return;
        pkm.Form = CB_Forme.SelectedIndex;
        RefreshPKMSlotAbility();
    }

    private void RefreshSpeciesAbility(object sender, EventArgs e)
    {
        if (index < 0)
            return;
        pkm.Species = (ushort)CB_Species.SelectedIndex;
        FormUtil.SetForms(CB_Species.SelectedIndex, CB_Forme, AltForms);
        RefreshPKMSlotAbility();
    }

    private void RefreshPKMSlotAbility()
    {
        int previousAbility = CB_Ability.SelectedIndex;

        int species = CB_Species.SelectedIndex;
        int formnum = CB_Forme.SelectedIndex;
        species = Main.SpeciesStat[species].FormeIndex(species, formnum);

        CB_Ability.Items.Clear();
        CB_Ability.Items.Add("Any (1 or 2)");
        CB_Ability.Items.Add(abilitylist[Main.SpeciesStat[species].Abilities[0]] + " (1)");
        CB_Ability.Items.Add(abilitylist[Main.SpeciesStat[species].Abilities[1]] + " (2)");
        CB_Ability.Items.Add(abilitylist[Main.SpeciesStat[species].Abilities[2]] + " (H)");

        CB_Ability.SelectedIndex = previousAbility;
    }

    private static string GetEntryTitle(string str, int i) => $"{str} - {i:000}";


    private void AddSmartHeldItemControls()
    {
        try
        {
            CHK_SmartHeldItems ??= new CheckBox
            {
                Name = "CHK_SmartHeldItems",
                Text = "Smart Items",
                AutoSize = true,
                Checked = false,
            };

            CHK_SmartItemsNormalTrainers ??= new CheckBox
            {
                Name = "CHK_SmartItemsNormalTrainers",
                Text = "Normal Trainers",
                AutoSize = true,
                Checked = true,
            };

            CHK_SmartItemsImportantTrainers ??= new CheckBox
            {
                Name = "CHK_SmartItemsImportantTrainers",
                Text = "Important Trainers",
                AutoSize = true,
                Checked = true,
            };

            CHK_SmartItemsBosses ??= new CheckBox
            {
                Name = "CHK_SmartItemsBosses",
                Text = "Bosses",
                AutoSize = true,
                Checked = true,
            };

            CB_SmartHeldItemMode ??= new ComboBox
            {
                Name = "CB_SmartHeldItemModeNormal",
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 105,
            };

            CB_SmartHeldItemModeImportant ??= new ComboBox
            {
                Name = "CB_SmartHeldItemModeImportant",
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 105,
            };

            CB_SmartHeldItemModeBoss ??= new ComboBox
            {
                Name = "CB_SmartHeldItemModeBoss",
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 105,
            };

            void SetupModeCombo(ComboBox combo)
            {
                if (combo.Items.Count != 0)
                    return;

                combo.Items.AddRange(new object[] { "Normal", "Strong", "Competitive" });
                combo.SelectedIndex = 1;
            }

            SetupModeCombo(CB_SmartHeldItemMode);
            SetupModeCombo(CB_SmartHeldItemModeImportant);
            SetupModeCombo(CB_SmartHeldItemModeBoss);

            CHK_ItemClause ??= new CheckBox
            {
                Name = "CHK_ItemClause",
                Text = "Item Clause",
                AutoSize = true,
                Checked = false,
            };

            var randomItems = Controls.Find("CHK_RandomItems", true).FirstOrDefault() as CheckBox;
            Control parent = randomItems?.Parent;
            parent ??= Tab_PKM2;
            parent ??= this;

            foreach (Control control in new Control[]
            {
                CHK_SmartHeldItems,
                CHK_SmartItemsNormalTrainers,
                CHK_SmartItemsImportantTrainers,
                CHK_SmartItemsBosses,
                CB_SmartHeldItemMode,
                CB_SmartHeldItemModeImportant,
                CB_SmartHeldItemModeBoss,
                CHK_ItemClause,
            })
            {
                if (!parent.Controls.Contains(control))
                    parent.Controls.Add(control);
            }

            int x = randomItems is not null ? randomItems.Left : 6;
            int y = randomItems is not null ? randomItems.Bottom + 5 : 122;

            CHK_SmartHeldItems.Location = new Point(x, y);
            CHK_SmartItemsNormalTrainers.Location = new Point(x + 20, y + 24);
            CHK_SmartItemsImportantTrainers.Location = new Point(x + 145, y + 24);
            CHK_SmartItemsBosses.Location = new Point(x + 285, y + 24);

            CB_SmartHeldItemMode.Location = new Point(x + 20, y + 47);
            CB_SmartHeldItemModeImportant.Location = new Point(x + 145, y + 47);
            CB_SmartHeldItemModeBoss.Location = new Point(x + 285, y + 47);

            CHK_ItemClause.Location = new Point(x, y + 78);

            CHK_SmartHeldItems.CheckedChanged += (_, _) => UpdateSmartItemsSubmenuState();

            CHK_SmartItemsNormalTrainers.CheckedChanged += (_, _) => UpdateSmartItemsSubmenuState();
            CHK_SmartItemsImportantTrainers.CheckedChanged += (_, _) => UpdateSmartItemsSubmenuState();
            CHK_SmartItemsBosses.CheckedChanged += (_, _) => UpdateSmartItemsSubmenuState();

            if (randomItems is not null)
                randomItems.CheckedChanged += (_, _) => UpdateSmartItemsSubmenuState();

            UpdateSmartItemsSubmenuState();
        }
        catch
        {
            // UI-only helper.
        }
    }

    private void UpdateSmartItemsSubmenuState()
    {
        if (CHK_SmartHeldItems is null)
            return;

        var randomItems = Controls.Find("CHK_RandomItems", true).FirstOrDefault() as CheckBox;
        bool randomItemsEnabled = randomItems?.Checked ?? true;
        bool showSubmenu = CHK_SmartHeldItems.Checked;

        CHK_SmartHeldItems.Enabled = randomItemsEnabled;

        foreach (Control control in new Control[]
        {
            CHK_SmartItemsNormalTrainers,
            CHK_SmartItemsImportantTrainers,
            CHK_SmartItemsBosses,
            CB_SmartHeldItemMode,
            CB_SmartHeldItemModeImportant,
            CB_SmartHeldItemModeBoss,
        })
        {
            if (control is null)
                continue;

            control.Visible = showSubmenu;
        }

        if (CHK_SmartItemsNormalTrainers is not null)
            CHK_SmartItemsNormalTrainers.Enabled = randomItemsEnabled && showSubmenu;
        if (CHK_SmartItemsImportantTrainers is not null)
            CHK_SmartItemsImportantTrainers.Enabled = randomItemsEnabled && showSubmenu;
        if (CHK_SmartItemsBosses is not null)
            CHK_SmartItemsBosses.Enabled = randomItemsEnabled && showSubmenu;

        if (CB_SmartHeldItemMode is not null)
            CB_SmartHeldItemMode.Enabled =
                randomItemsEnabled && showSubmenu && (CHK_SmartItemsNormalTrainers?.Checked ?? false);

        if (CB_SmartHeldItemModeImportant is not null)
            CB_SmartHeldItemModeImportant.Enabled =
                randomItemsEnabled && showSubmenu && (CHK_SmartItemsImportantTrainers?.Checked ?? false);

        if (CB_SmartHeldItemModeBoss is not null)
            CB_SmartHeldItemModeBoss.Enabled =
                randomItemsEnabled && showSubmenu && (CHK_SmartItemsBosses?.Checked ?? false);

        if (CHK_ItemClause is not null)
            CHK_ItemClause.Enabled = randomItemsEnabled;

        if (!randomItemsEnabled)
            CHK_SmartHeldItems.Checked = false;
    }

    private bool UseSmartTrainerItems()
        => CHK_SmartHeldItems is not null && CHK_SmartHeldItems.Checked;

    private int GetSmartTrainerItemMode(string trainerGroup)
    {
        ComboBox combo = trainerGroup switch
        {
            "Boss" => CB_SmartHeldItemModeBoss,
            "Important" => CB_SmartHeldItemModeImportant,
            _ => CB_SmartHeldItemMode,
        };

        return combo is null
            ? 1
            : Math.Clamp(combo.SelectedIndex, 0, 2);
    }

    private bool UseItemClause()
        => CHK_RandomItems.Checked && CHK_ItemClause is not null && CHK_ItemClause.Checked;
    private void AddBetterMovesetControls()
    {
        try
        {
            CHK_BetterMovesets ??= new CheckBox
            {
                Name = "CHK_BetterMovesets",
                Text = "Better Movesets",
                AutoSize = true,
                Checked = false,
            };

            CHK_BetterMovesetsNormalTrainers ??= new CheckBox
            {
                Name = "CHK_BetterMovesetsNormalTrainers",
                Text = "Normal Trainers",
                AutoSize = true,
                Checked = true,
            };

            CHK_BetterMovesetsImportantTrainers ??= new CheckBox
            {
                Name = "CHK_BetterMovesetsImportantTrainers",
                Text = "Important Trainers",
                AutoSize = true,
                Checked = true,
            };

            CHK_BetterMovesetsBosses ??= new CheckBox
            {
                Name = "CHK_BetterMovesetsBosses",
                Text = "Bosses",
                AutoSize = true,
                Checked = true,
            };

            Control parent = CB_Moves?.Parent;
            parent ??= Tab_Rand;
            parent ??= this;

            foreach (Control control in new Control[]
            {
                CHK_BetterMovesets,
                CHK_BetterMovesetsNormalTrainers,
                CHK_BetterMovesetsImportantTrainers,
                CHK_BetterMovesetsBosses,
            })
            {
                if (!parent.Controls.Contains(control))
                    parent.Controls.Add(control);
            }

            int x = CB_Moves is not null ? CB_Moves.Right + 12 : 220;
            int y = CB_Moves is not null ? CB_Moves.Top + 2 : 270;

            CHK_BetterMovesets.Location = new Point(x, y);
            CHK_BetterMovesetsNormalTrainers.Location = new Point(x + 20, y + 24);
            CHK_BetterMovesetsImportantTrainers.Location = new Point(x + 135, y + 24);
            CHK_BetterMovesetsBosses.Location = new Point(x + 270, y + 24);

            CHK_BetterMovesets.CheckedChanged += (_, _) => UpdateBetterMovesetsSubmenuState();
            UpdateBetterMovesetsSubmenuState();

            CHK_BetterMovesets.BringToFront();
            CHK_BetterMovesetsNormalTrainers.BringToFront();
            CHK_BetterMovesetsImportantTrainers.BringToFront();
            CHK_BetterMovesetsBosses.BringToFront();
        }
        catch
        {
            // UI-only helper.
        }
    }

    private void UpdateBetterMovesetsSubmenuState()
    {
        if (CHK_BetterMovesets is null)
            return;

        bool showSubmenu = CHK_BetterMovesets.Checked;

        foreach (Control control in new Control[]
        {
            CHK_BetterMovesetsNormalTrainers,
            CHK_BetterMovesetsImportantTrainers,
            CHK_BetterMovesetsBosses,
        })
        {
            if (control is null)
                continue;

            control.Visible = showSubmenu;
            control.Enabled = showSubmenu;
        }
    }

    private bool UseBetterMovesets()
        => CHK_BetterMovesets is not null && CHK_BetterMovesets.Checked;
    private void Setup()
    {
        AltForms = forms.Select(_ => Enumerable.Range(0, 100).Select(i => i.ToString()).ToArray()).ToArray();
        CB_TrainerID.Items.Clear();
        for (int i = 0; i < trdata.Length; i++)
            CB_TrainerID.Items.Add(GetEntryTitle(trName[i] ?? "UNKNOWN", i));

        CB_Trainer_Class.Items.Clear();
        for (int i = 0; i < trClass.Length; i++)
            CB_Trainer_Class.Items.Add(GetEntryTitle(trClass[i], i));

        Trainers[0] = new TrainerData7();

        for (int i = 1; i < trdata.Length; i++)
        {
            Trainers[i] = new TrainerData7(trdata[i], trpoke[i])
            {
                Name = trName[i],
                ID = i,
            };
        }

        specieslist[0] = "---";
        abilitylist[0] = itemlist[0] = movelist[0] = "(None)";
        pba = [PB_Team1, PB_Team2, PB_Team3, PB_Team4, PB_Team5, PB_Team6];
        AIBits = [CHK_AI0, CHK_AI1, CHK_AI2, CHK_AI3, CHK_AI4, CHK_AI5, CHK_AI6, CHK_AI7];

        CB_Species.Items.Clear();
        CB_Species.Items.AddRange(specieslist);

        CB_Move1.Items.Clear();
        CB_Move2.Items.Clear();
        CB_Move3.Items.Clear();
        CB_Move4.Items.Clear();
        foreach (string s in movelist)
        {
            CB_Move1.Items.Add(s);
            CB_Move2.Items.Add(s);
            CB_Move3.Items.Add(s);
            CB_Move4.Items.Add(s);
        }

        CB_HPType.DataSource = types.Skip(1).Take(16).ToArray();
        CB_HPType.SelectedIndex = 0;

        CB_Nature.Items.Clear();
        CB_Nature.Items.AddRange(natures.Take(25).ToArray());

        CB_Item.Items.Clear();
        CB_Item.Items.AddRange(itemlist);

        CB_Gender.Items.Clear();
        CB_Gender.Items.Add("- / Genderless/Random");
        CB_Gender.Items.Add("♂ / Male");
        CB_Gender.Items.Add("♀ / Female");

        CB_Forme.Items.Add("");

        CB_Species.SelectedIndex = 0;
        CB_Item_1.Items.Clear();
        CB_Item_2.Items.Clear();
        CB_Item_3.Items.Clear();
        CB_Item_4.Items.Clear();
        foreach (string s in itemlist)
        {
            CB_Item_1.Items.Add(s);
            CB_Item_2.Items.Add(s);
            CB_Item_3.Items.Add(s);
            CB_Item_4.Items.Add(s);
        }

        CB_Money.Items.Clear();
        for (int i = 0; i < 256; i++)
        { CB_Money.Items.Add(i.ToString()); }

        CB_TrainerID.SelectedIndex = 0;
        index = 0;
        pkm = new TrainerPoke7();
        PopulateFieldsTP7(pkm);
    }

    private void ChangeTrainerIndex(object sender, EventArgs e)
    {
        SaveEntry();
        LoadEntry();
        if (TC_trdata.SelectedIndex == TC_trdata.TabCount - 1) // last
            TC_trdata.SelectedIndex = 0;
    }

    private void SaveEntry()
    {
        if (index < 0)
            return;
        var tr = Trainers[index];
        PrepareTR7(tr);
        SaveData(tr, index);
        TrainerNames[index] = TB_TrainerName.Text;
    }

    private void SaveData(TrainerData7 tr, int i)
    {
        tr.Write(out byte[] trd, out byte[] trp);
        trdata[i] = trd;
        trpoke[i] = trp;
    }

    private void LoadEntry()
    {
        index = CB_TrainerID.SelectedIndex;
        var tr = Trainers[index];

        loading = true;
        TB_TrainerName.Text = TrainerNames[index];

        PopulateFieldsTD7(tr);
        loading = false;
    }

    private bool loading;
    private TrainerPoke7 pkm;

    private void PopulateFieldsTP7(TrainerPoke7 pk)
    {
        pkm = pk.Clone();

        int spec = pkm.Species, form = pkm.Form;

        CB_Species.SelectedIndex = spec;
        CB_Forme.SelectedIndex = form;
        CB_Ability.SelectedIndex = pkm.Ability;
        CB_Item.SelectedIndex = pkm.Item;
        CHK_Shiny.Checked = pkm.Shiny;
        CB_Gender.SelectedIndex = pkm.Gender;

        CB_Move1.SelectedIndex = pkm.Move1;
        CB_Move2.SelectedIndex = pkm.Move2;
        CB_Move3.SelectedIndex = pkm.Move3;
        CB_Move4.SelectedIndex = pkm.Move4;

        updatingStats = true;
        CB_Nature.SelectedIndex = pkm.Nature;
        NUD_Level.Value = Math.Min(NUD_Level.Maximum, pkm.Level);

        TB_HPIV.Text = pkm.IV_HP.ToString();
        TB_ATKIV.Text = pkm.IV_ATK.ToString();
        TB_DEFIV.Text = pkm.IV_DEF.ToString();
        TB_SPAIV.Text = pkm.IV_SPA.ToString();
        TB_SPEIV.Text = pkm.IV_SPE.ToString();
        TB_SPDIV.Text = pkm.IV_SPD.ToString();

        TB_HPEV.Text = pkm.EV_HP.ToString();
        TB_ATKEV.Text = pkm.EV_ATK.ToString();
        TB_DEFEV.Text = pkm.EV_DEF.ToString();
        TB_SPAEV.Text = pkm.EV_SPA.ToString();
        TB_SPEEV.Text = pkm.EV_SPE.ToString();
        TB_SPDEV.Text = pkm.EV_SPD.ToString();
        updatingStats = false;
        UpdateStats(null, null);
    }

    private TrainerPoke7 PrepareTP7()
    {
        var pk = pkm.Clone();
        pk.Species = CB_Species.SelectedIndex;
        pk.Form = CB_Forme.SelectedIndex;
        pk.Level = (byte)NUD_Level.Value;
        pk.Ability = CB_Ability.SelectedIndex;
        pk.Item = CB_Item.SelectedIndex;
        pk.Shiny = CHK_Shiny.Checked;
        pk.Nature = CB_Nature.SelectedIndex;
        pk.Gender = CB_Gender.SelectedIndex;

        pk.Move1 = CB_Move1.SelectedIndex;
        pk.Move2 = CB_Move2.SelectedIndex;
        pk.Move3 = CB_Move3.SelectedIndex;
        pk.Move4 = CB_Move4.SelectedIndex;

        pk.IV_HP = WinFormsUtil.ToInt32(TB_HPIV);
        pk.IV_ATK = WinFormsUtil.ToInt32(TB_ATKIV);
        pk.IV_DEF = WinFormsUtil.ToInt32(TB_DEFIV);
        pk.IV_SPA = WinFormsUtil.ToInt32(TB_SPAIV);
        pk.IV_SPE = WinFormsUtil.ToInt32(TB_SPEIV);
        pk.IV_SPD = WinFormsUtil.ToInt32(TB_SPDIV);

        pk.EV_HP = WinFormsUtil.ToInt32(TB_HPEV);
        pk.EV_ATK = WinFormsUtil.ToInt32(TB_ATKEV);
        pk.EV_DEF = WinFormsUtil.ToInt32(TB_DEFEV);
        pk.EV_SPA = WinFormsUtil.ToInt32(TB_SPAEV);
        pk.EV_SPE = WinFormsUtil.ToInt32(TB_SPEEV);
        pk.EV_SPD = WinFormsUtil.ToInt32(TB_SPDEV);

        return pk;
    }

    private void PopulateFieldsTD7(TrainerData7 tr)
    {
        // Load Trainer Data
        CB_Trainer_Class.SelectedIndex = tr.TrainerClass;
        NUD_NumPoke.Value = tr.NumPokemon;
        CB_Item_1.SelectedIndex = tr.Item1;
        CB_Item_2.SelectedIndex = tr.Item2;
        CB_Item_3.SelectedIndex = tr.Item3;
        CB_Item_4.SelectedIndex = tr.Item4;
        CB_Money.SelectedIndex = tr.Money;
        CB_Mode.SelectedIndex = (int)tr.Mode;
        LoadAIBits((uint)tr.AI);
        CHK_Flag.Checked = tr.Flag;
        PopulateTeam(tr);
    }

    private void PrepareTR7(TrainerData7 tr)
    {
        tr.TrainerClass = (byte)CB_Trainer_Class.SelectedIndex;
        tr.NumPokemon = (byte)NUD_NumPoke.Value;
        tr.Item1 = CB_Item_1.SelectedIndex;
        tr.Item2 = CB_Item_2.SelectedIndex;
        tr.Item3 = CB_Item_3.SelectedIndex;
        tr.Item4 = CB_Item_4.SelectedIndex;
        tr.Money = CB_Money.SelectedIndex;
        tr.Mode = (BattleMode)CB_Mode.SelectedIndex;
        tr.AI = (int)SaveAIBits();
        tr.Flag = CHK_Flag.Checked;
    }

    private void LoadAIBits(uint val)
    {
        for (int i = 0; i < AIBits.Length; i++)
            AIBits[i].Checked = ((val >> i) & 1) == 1;
    }

    private uint SaveAIBits()
    {
        uint val = 0;
        for (int i = 0; i < AIBits.Length; i++)
            val |= AIBits[i].Checked ? 1u << i : 0;
        return val;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SaveEntry();
        if (TrainerNames.Modified)
            Main.Config.SetText(TextName.TrainerNames, TrainerNames.Lines);
        TrainerRandomizerTemplateFile.SetCurrent(CaptureTrainerTemplate(), CurrentTrainerTemplateGame);
        RandSettings.SetFormSettings(this, Tab_Rand.Controls);
        base.OnFormClosing(e);
    }

    // Dumping
    private void DumpTxt(object sender, EventArgs e)
    {
        var sfd = new SaveFileDialog { FileName = "Trainers.txt" };
        if (sfd.ShowDialog() != DialogResult.OK)
            return;
        var sb = new StringBuilder();
        foreach (var Trainer in Trainers)
            sb.Append(GetTrainerString(Trainer));
        File.WriteAllText(sfd.FileName, sb.ToString());
    }

    private string GetTrainerString(TrainerData7 tr)
    {
        var sb = new StringBuilder();
        sb.AppendLine("======");
        sb.Append(tr.ID).Append(" - ").Append(trClass[tr.TrainerClass]).Append(' ').AppendLine(tr.Name);
        sb.AppendLine("======");
        sb.Append("Pokemon: ").Append(tr.NumPokemon).AppendLine();
        for (int i = 0; i < tr.NumPokemon; i++)
        {
            if (tr.Pokemon[i].Shiny)
                sb.Append("Shiny ");
            sb.Append(specieslist[tr.Pokemon[i].Species]);
            sb.Append(" (Lv. ").Append(tr.Pokemon[i].Level).Append(") ");
            if (tr.Pokemon[i].Item > 0)
                sb.Append('@').Append(itemlist[tr.Pokemon[i].Item]);

            if (tr.Pokemon[i].Nature != 0)
                sb.Append(" (Nature: ").Append(natures[tr.Pokemon[i].Nature]).Append(')');

            sb.Append(" (Moves: ").AppendJoin("/", tr.Pokemon[i].Moves.Select(m => m == 0 ? "(None)" : movelist[m])).Append(')');
            sb.Append(" IVs: ").AppendJoin("/", tr.Pokemon[i].IVs);
            sb.Append(" EVs: ").AppendJoin("/", tr.Pokemon[i].EVs);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private void UpdateNumPokemon(object sender, EventArgs e)
    {
        if (index < 0)
            return;
        Trainers[index].NumPokemon = (int)NUD_NumPoke.Value;
    }

    private void UpdateTrainerName(object sender, EventArgs e)
    {
        if (loading)
            return;
        string str = TB_TrainerName.Text;
        CB_TrainerID.Items[index] = GetEntryTitle(str, index);
    }

    private static bool updatingStats;

    private void UpdateStats(object sender, EventArgs e)
    {
        if (updatingStats)
            return;
        var tb_iv = new[] { TB_HPIV, TB_ATKIV, TB_DEFIV, TB_SPEIV, TB_SPAIV, TB_SPDIV };
        var tb_ev = new[] { TB_HPEV, TB_ATKEV, TB_DEFEV, TB_SPEEV, TB_SPAEV, TB_SPDEV };
        for (int i = 0; i < 6; i++)
        {
            updatingStats = true;
            if (WinFormsUtil.ToInt32(tb_iv[i]) > 31)
                tb_iv[i].Text = "31";
            if (WinFormsUtil.ToInt32(tb_ev[i]) > 255)
                tb_ev[i].Text = "255";
            updatingStats = false;
        }

        int species = CB_Species.SelectedIndex;
        species = Main.SpeciesStat[species].FormeIndex(species, CB_Forme.SelectedIndex);
        var p = Main.SpeciesStat[species];
        int level = (int)NUD_Level.Value;
        int Nature = CB_Nature.SelectedIndex;

        ushort[] Stats = new ushort[6];
        Stats[0] = (ushort)(p.HP == 1 ? 1 : ((Util.ToInt32(TB_HPIV.Text) + (2 * p.HP) + (Util.ToInt32(TB_HPEV.Text) / 4) + 100) * level / 100) + 10);
        Stats[1] = (ushort)(((Util.ToInt32(TB_ATKIV.Text) + (2 * p.ATK) + (Util.ToInt32(TB_ATKEV.Text) / 4)) * level / 100) + 5);
        Stats[2] = (ushort)(((Util.ToInt32(TB_DEFIV.Text) + (2 * p.DEF) + (Util.ToInt32(TB_DEFEV.Text) / 4)) * level / 100) + 5);
        Stats[4] = (ushort)(((Util.ToInt32(TB_SPAIV.Text) + (2 * p.SPA) + (Util.ToInt32(TB_SPAEV.Text) / 4)) * level / 100) + 5);
        Stats[5] = (ushort)(((Util.ToInt32(TB_SPDIV.Text) + (2 * p.SPD) + (Util.ToInt32(TB_SPDEV.Text) / 4)) * level / 100) + 5);
        Stats[3] = (ushort)(((Util.ToInt32(TB_SPEIV.Text) + (2 * p.SPE) + (Util.ToInt32(TB_SPEEV.Text) / 4)) * level / 100) + 5);

        // Account for nature
        int incr = (Nature / 5) + 1;
        int decr = (Nature % 5) + 1;
        if (incr != decr)
        {
            Stats[incr] *= 11;
            Stats[incr] /= 10;
            Stats[decr] *= 9;
            Stats[decr] /= 10;
        }

        Stat_HP.Text = Stats[0].ToString();
        Stat_ATK.Text = Stats[1].ToString();
        Stat_DEF.Text = Stats[2].ToString();
        Stat_SPA.Text = Stats[4].ToString();
        Stat_SPD.Text = Stats[5].ToString();
        Stat_SPE.Text = Stats[3].ToString();

        TB_IVTotal.Text = tb_iv.Sum(WinFormsUtil.ToInt32).ToString();
        TB_EVTotal.Text = tb_ev.Sum(WinFormsUtil.ToInt32).ToString();

        // Recolor the Stat Labels based on boosted stats.
        {
            incr--;
            decr--;
            Label[] labarray = [Label_ATK, Label_DEF, Label_SPE, Label_SPA, Label_SPD];
            // Reset Label Colors
            foreach (Label label in labarray)
                label.ResetForeColor();

            // Set Colored StatLabels only if Nature isn't Neutral
            if (incr != decr)
            {
                labarray[incr].ForeColor = Color.Red;
                labarray[decr].ForeColor = Color.Blue;
            }
        }
        var ivs = tb_iv.Select(tb => WinFormsUtil.ToInt32(tb) & 1).ToArray();
        updatingStats = true;
        CB_HPType.SelectedIndex = 15 * (ivs[0] + (2 * ivs[1]) + (4 * ivs[2]) + (8 * ivs[3]) + (16 * ivs[4]) + (32 * ivs[5])) / 63;
        updatingStats = false;
    }

    private void UpdateHPType(object sender, EventArgs e)
    {
        if (updatingStats)
            return;
        var tb_iv = new[] { TB_HPIV, TB_ATKIV, TB_DEFIV, TB_SPAIV, TB_SPDIV, TB_SPEIV };
        int[] newIVs = SetHPIVs(CB_HPType.SelectedIndex, tb_iv.Select(WinFormsUtil.ToInt32).ToArray());
        updatingStats = true;
        TB_HPIV.Text = newIVs[0].ToString();
        TB_ATKIV.Text = newIVs[1].ToString();
        TB_DEFIV.Text = newIVs[2].ToString();
        TB_SPAIV.Text = newIVs[3].ToString();
        TB_SPDIV.Text = newIVs[4].ToString();
        TB_SPEIV.Text = newIVs[5].ToString();
        updatingStats = false;
    }

    public static int[] SetHPIVs(int type, int[] ivs)
    {
        for (int i = 0; i < 6; i++)
            ivs[i] = (ivs[i] & 0x1E) + hpivs[type, i];
        return ivs;
    }

    private static readonly int[,] hpivs = {
        { 1, 1, 0, 0, 0, 0 }, // Fighting
        { 0, 0, 0, 0, 0, 1 }, // Flying
        { 1, 1, 0, 0, 0, 1 }, // Poison
        { 1, 1, 1, 0, 0, 1 }, // Ground
        { 1, 1, 0, 1, 0, 0 }, // Rock
        { 1, 0, 0, 1, 0, 1 }, // Bug
        { 1, 0, 1, 1, 0, 1 }, // Ghost
        { 1, 1, 1, 1, 0, 1 }, // Steel
        { 1, 0, 1, 0, 1, 0 }, // Fire
        { 1, 0, 0, 0, 1, 1 }, // Water
        { 1, 0, 1, 0, 1, 1 }, // Grass
        { 1, 1, 1, 0, 1, 1 }, // Electric
        { 1, 0, 1, 1, 1, 0 }, // Psychic
        { 1, 0, 0, 1, 1, 1 }, // Ice
        { 1, 0, 1, 1, 1, 1 }, // Dragon
        { 1, 1, 1, 1, 1, 1 }, // Dark
    };

    private static readonly int[] usualBan = [165, 621, 166, 226];

    private List<TrainerLevelCapStage> BuildLevelCapStages()
    {
        if (!CHK_LevelCaps.Checked || LevelCapRules.Count == 0)
            return [];

        var stages = new List<TrainerLevelCapStage>();
        foreach (var rule in LevelCapRules.Where(r => r.Enabled))
        {
            if (rule.TrainerID <= 0 || rule.TrainerID >= Trainers.Length)
                continue;

            var trainer = Trainers[rule.TrainerID];
            if (trainer.Pokemon.Count == 0)
                continue;

            int ace = GetAceLevel(trainer);
            int cap = rule.LevelCap == 0 ? ace : rule.LevelCap;

            stages.Add(new TrainerLevelCapStage
            {
                TrainerID = rule.TrainerID,
                OriginalAceLevel = ace,
                LevelCap = ClampLevel(cap),
                GuaranteeMega = rule.GuaranteeMega,
                GuaranteeZMove = rule.GuaranteeZMove,
                MinMovePower = ClampMovePower(rule.MinMovePower),
            });
        }

        return stages
            .OrderBy(s => s.OriginalAceLevel)
            .ThenBy(s => s.TrainerID)
            .ToList();
    }

    private static int GetAceLevel(TrainerData7 trainer)
    {
        return trainer.Pokemon.Count == 0 ? 1 : trainer.Pokemon.Max(pk => pk.Level);
    }

    private int? GetTrainerTargetLevel(int trainerID, int trainerAce, List<TrainerLevelCapStage> stages, out bool forceExactLevel)
    {
        forceExactLevel = false;

        if (!CHK_LevelCaps.Checked || stages.Count == 0)
            return null;

        var exact = stages.FirstOrDefault(s => s.TrainerID == trainerID);
        if (exact is not null)
        {
            forceExactLevel = true;
            return exact.LevelCap;
        }

        if (!ApplyCapsToPreviousTrainers)
            return null;

        var next = stages.FirstOrDefault(s => trainerAce <= s.OriginalAceLevel);
        if (next is null)
            return null;

        int nextIndex = stages.IndexOf(next);
        TrainerLevelCapStage previous = nextIndex > 0 ? stages[nextIndex - 1] : null;

        // Preserve the game's original trainer curve instead of flattening everyone far below the cap.
        // If the next important trainer moved by +N levels, regular trainers before it also move by +N.
        int endTarget = ClampLevel(next.LevelCap - PreviousTrainerGap);
        int deltaToNextCap = endTarget - next.OriginalAceLevel;
        int target = ClampLevel(trainerAce + deltaToNextCap);

        // Do not let trainers after a previous cap fall below that previous cap when caps were raised.
        if (previous is not null && trainerAce > previous.OriginalAceLevel)
            target = Math.Max(target, previous.LevelCap);

        return ClampLevel(target);
    }

    private bool ShouldGuaranteeMega(int trainerID, int trainerAce, List<TrainerLevelCapStage> stages)
    {
        return stages.Any(s => s.TrainerID == trainerID && s.GuaranteeMega);
    }

    private bool ShouldGuaranteeZMove(int trainerID, List<TrainerLevelCapStage> stages)
    {
        return stages.Any(s => s.TrainerID == trainerID && s.GuaranteeZMove);
    }

    private TrainerMoveRule GetTrainerMoveRule(int trainerID)
    {
        var rule = MoveRules?.FirstOrDefault(r => r.Enabled && r.TrainerID == trainerID);
        return rule?.Clone();
    }

    private static int ClampMovePower(int power)
    {
        if (power < 0)
            return 0;
        return power > 250 ? 250 : power;
    }

    private static bool IsProtectedOpeningBattle(int trainerAce) => trainerAce <= 5;

    private static int ClampLevel(int level)
    {
        if (level < 1)
            return 1;
        return level > 100 ? 100 : level;
    }

    private static int ApplyLevelTarget(int currentLevel, int? targetLevel, bool forceExactLevel, int trainerAce)
    {
        if (targetLevel is null)
            return currentLevel;

        int target = ClampLevel(targetLevel.Value);
        if (forceExactLevel)
            return target;

        int delta = target - trainerAce;
        return ClampLevel(currentLevel + delta);
    }


    private void ApplyRandomDoubleBattle(TrainerData7 tr, int trainerAce, int[] protectedBattleRoyalIDs)
    {
        if (CHK_RandomDoubleBattles is null || !CHK_RandomDoubleBattles.Checked || NUD_DoubleBattleChance.Value <= 0)
            return;

        if (protectedBattleRoyalIDs.Contains(tr.ID))
            return;

        // Only convert regular single battles. Do not overwrite existing double/multi/special modes.
        if ((int)tr.Mode != 0)
            return;

        if (Util.Random32() % 100 >= NUD_DoubleBattleChance.Value)
            return;

        EnsureAtLeastTwoPokemon(tr);
        tr.Mode = BattleMode.Doubles;
        tr.AI |= (int)TrainerAI.Doubles;
    }

    private static void EnsureAtLeastTwoPokemon(TrainerData7 tr)
    {
        if (tr.NumPokemon >= 2 || tr.Pokemon.Count >= 2)
        {
            tr.NumPokemon = Math.Max(tr.NumPokemon, Math.Min(tr.Pokemon.Count, 6));
            return;
        }

        if (tr.Pokemon.Count == 0)
            return;

        var source = tr.Pokemon[0];
        tr.Pokemon.Add(new TrainerPoke7
        {
            Species = source.Species,
            Form = source.Form,
            Level = source.Level,
            Gender = source.Gender,
            Item = source.Item,
            Ability = source.Ability,
            Nature = source.Nature,
            IVs = source.IVs?.ToArray(),
            EVs = source.EVs?.ToArray(),
            Moves = source.Moves?.ToArray(),
            Shiny = source.Shiny,
        });

        tr.NumPokemon = 2;
    }

    private void B_Randomize_Click(object sender, EventArgs e)
    {
        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Randomize all? Cannot undo.", "Double check Randomization settings in the Randomizer Options tab.") != DialogResult.Yes) return;

        CB_TrainerID.SelectedIndex = 0;
        var rnd = new SpeciesRandomizer(Main.Config)
        {
            G1 = CHK_G1.Checked,
            G2 = CHK_G2.Checked,
            G3 = CHK_G3.Checked,
            G4 = CHK_G4.Checked,
            G5 = CHK_G5.Checked,
            G6 = CHK_G6.Checked,
            G7 = CHK_G7.Checked,

            E = CHK_E.Checked,
            L = CHK_L.Checked,
            rBST = CHK_BST.Checked && !CHK_ProgressiveBST.Checked,
        };
        rnd.Initialize();

        // add Legendary/Mythical to final evolutions if checked
        if (CHK_L.Checked) FinalEvo = [.. FinalEvo, .. Legendary];
        if (CHK_E.Checked) FinalEvo = [.. FinalEvo, .. Mythical];

        var banned = new List<int>(usualBan.Concat(Legal.Z_Moves)); // Struggle, Hyperspace Fury, Dark Void
        if (CHK_NoFixedDamage.Checked)
            banned.AddRange(MoveRandomizer.FixedDamageMoves);
        var move = new MoveRandomizer(Main.Config)
        {
            BannedMoves = banned,
            rSTABCount = (int)NUD_STAB.Value,
            rDMG = CHK_Damage.Checked,
            rDMGCount = (int)NUD_Damage.Value,
            rSTAB = CHK_STAB.Checked,
        };

        var items = Randomizer.GetRandomItemList();

        if (CHK_BanBadItems is not null && CHK_BanBadItems.Checked)
        {
            int[] cleanItems = SmartTrainerItemPicker.GetBanBadItemPool(items);
            if (cleanItems.Length > 0)
                items = cleanItems;
        }

        bool anySmartItems = UseSmartTrainerItems();

        if (anySmartItems)
            items = SmartTrainerItemPicker.AddSmartTrainerItemPoolExtras(items);
        var levelCapStages = BuildLevelCapStages();

        int progressTotal = Math.Max(1, Trainers.Length);
        using var progress = TrainerRandomizeProgress.Show(this, "Randomizing Trainers", progressTotal);

        for (int i = 0; i < Trainers.Length; i++)
        {
            var tr = Trainers[i];
            progress.Report(i + 1, progressTotal, GetTrainerDisplayName(tr));
            if (tr.Pokemon.Count == 0)
                continue;

            int trainerAce = GetAceLevel(tr);
            TrainerImportanceCategory trainerCategory = GetTrainerImportanceCategory(tr.ID);
            bool isImportantTrainer = trainerCategory != TrainerImportanceCategory.Regular;
            string trainerGroup = trainerCategory.ToString();

            // Trainer Properties
            if (CHK_RandomClass.Checked)
            {
                // ignore special classes
                if (CHK_IgnoreSpecialClass.Checked && !SpecialClasses.Contains(tr.TrainerClass))
                {
                    int randClass() => (int)(Util.Random32() % CB_Trainer_Class.Items.Count);
                    int rv; do { rv = randClass(); }
                    while (SpecialClasses.Contains(rv)); // don't allow disallowed classes
                    tr.TrainerClass = (byte)rv;
                }

                // all classes
                else if (!CHK_IgnoreSpecialClass.Checked)
                {
                    int randClass() => (int)(Util.Random32() % CB_Trainer_Class.Items.Count);
                    int rv; do { rv = randClass(); }
                    while (rv == 082); // Lusamine 2 can crash multi battles, skip
                    tr.TrainerClass = (byte)rv;
                }
            }

            var avgBST = (int)tr.Pokemon.Average(pk => Main.SpeciesStat[pk.Species].BST);
            int avgLevel = (int)tr.Pokemon.Average(pk => pk.Level);
            var pinfo = Main.SpeciesStat.OrderBy(pk => Math.Abs(avgBST - pk.BST)).First();
            int avgSpec = Array.IndexOf(Main.SpeciesStat, pinfo);
            int[] royal = [081, 082, 083, 084, 185];

            if (tr.NumPokemon < NUD_RMin.Value)
            {
                for (int p = tr.NumPokemon; p < NUD_RMin.Value; p++)
                {
                    tr.Pokemon.Add(new TrainerPoke7
                    {
                        Species = CHK_ProgressiveBST.Checked
                            ? GetProgressiveRandomSpecies(rnd, avgSpec, -1, avgLevel)
                            : rnd.GetRandomSpecies(avgSpec),
                        Level = avgLevel,
                    });
                }

                tr.NumPokemon = (int)NUD_RMin.Value;
            }
            if (tr.NumPokemon > NUD_RMax.Value)
            {
                tr.Pokemon.RemoveRange((int)NUD_RMax.Value, (int)(tr.NumPokemon - NUD_RMax.Value));
                tr.NumPokemon = (int)NUD_RMax.Value;
            }
            if (CHK_6PKM.Checked && isImportantTrainer)
            {
                for (int g = tr.NumPokemon; g < 6; g++)
                {
                    var range = SpeciesRandomizer.GetProgressiveBSTRange(avgLevel);

                    tr.Pokemon.Add(new TrainerPoke7
                    {
                        Species = CHK_ProgressiveBST.Checked
                            ? GetProgressiveRandomSpecies(rnd, avgSpec, -1, avgLevel)
                            : rnd.GetRandomSpecies(avgSpec),
                        Level = avgLevel,
                    });
                }

                tr.NumPokemon = 6;
            }

            // force 1 pkm to keep forced Battle Royal fair
            if (royal.Contains(tr.ID))
                tr.NumPokemon = 1;

            ApplyRandomDoubleBattle(tr, trainerAce, royal);

            int? targetLevel = GetTrainerTargetLevel(tr.ID, trainerAce, levelCapStages, out bool forceExactLevel);
            bool forceMega = ShouldGuaranteeMega(tr.ID, trainerAce, levelCapStages);
            bool forceZMove = ShouldGuaranteeZMove(tr.ID, levelCapStages);
            var moveRule = GetTrainerMoveRule(tr.ID);
            var randomItemPool = GetTrainerRandomItemPool(items, forceMega, forceZMove);
            int zMoveSlot = GetZMoveSlot(tr.Pokemon.Count, forceMega, forceZMove);
            var usedHeldItems = UseItemClause() ? new HashSet<int>() : null;

            // PKM Properties
            for (int p = 0; p < tr.Pokemon.Count; p++)
            {
                var pk = tr.Pokemon[p];
                if (targetLevel is not null)
                    pk.Level = ApplyLevelTarget(pk.Level, targetLevel, forceExactLevel, trainerAce);
                else if (CHK_Level.Checked)
                    pk.Level = Randomizer.GetModifiedLevel(pk.Level, NUD_LevelBoost.Value);

                ApplyTrainerEVOverride(pk, moveRule);

                if (CHK_RandomPKM.Checked)
                {
                    int Type = CHK_TypeTheme.Checked ? (int)Util.Random32() % 17 : -1;
                    bool forceMegaSlot = forceMega && p == tr.Pokemon.Count - 1;

                    // replaces Megas with another Mega (Dexio and Lysandre in USUM)
                    if (MegaDictionary.Values.Any(z => z.Contains(pk.Item)) || forceMegaSlot)
                    {
                        int[] mega = GetRandomMegaForType(Type, CHK_TypeTheme.Checked, out int species);
                        pk.Species = species;
                        pk.Item = mega[Util.Rand.Next(0, mega.Length)];
                        pk.Form = 0; // allow it to Mega Evolve naturally
                    }

                    // every other pkm
                    else
                    {
                        if (CHK_BST.Checked)
                        {
                            var range = SpeciesRandomizer.GetProgressiveBSTRange(pk.Level);

                            pk.Species = rnd.GetRandomSpeciesProgressiveBST(
                                pk.Species,
                                Type,
                                range.MinBST,
                                range.MaxBST
                            );
                        }
                        else
                        {
                            if (CHK_ProgressiveBST.Checked)
                            {
                                pk.Species = GetProgressiveRandomSpecies(
                                    rnd,
                                    pk.Species,
                                    Type,
                                    pk.Level
                                );
                            }
                            else
                            {
                                pk.Species = rnd.GetRandomSpeciesType(pk.Species, Type);
                            }
                        }
                        // Item is assigned after final moves so Smart Held Items can match the moveset.
                        pk.Form = Randomizer.GetRandomForme(pk.Species, CHK_RandomMegaForm.Checked, true, Main.SpeciesStat);
                    }

                    pk.Gender = 0; // random
                    pk.Nature = (int)(Util.Random32() % CB_Nature.Items.Count); // random
                }

                if (forceMega && !CHK_RandomPKM.Checked && p == tr.Pokemon.Count - 1)
                {
                    int Type = CHK_TypeTheme.Checked ? (int)Util.Random32() % 17 : -1;
                    int[] mega = GetRandomMegaForType(Type, CHK_TypeTheme.Checked, out int species);
                    pk.Species = species;
                    pk.Item = mega[Util.Rand.Next(0, mega.Length)];
                    pk.Form = 0; // allow it to Mega Evolve naturally
                }

                if (CHK_RandomShiny.Checked)
                    // 10,000 equally likely outcomes gives true 0.01% precision.
                    pk.Shiny = Util.Rand.Next(0, 10_000) < NUD_Shiny.Value * 100m;
                if (CHK_RandomAbilities.Checked)
                    pk.Ability = (int)Util.Random32() % 4;
                if (CHK_MaxDiffPKM.Checked)
                    pk.IVs = [31, 31, 31, 31, 31, 31];
                if (CHK_MaxAI.Checked)
                {
                    tr.AI |= (int)(TrainerAI.Basic | TrainerAI.Strong | TrainerAI.Expert | TrainerAI.PokeChange);

                    if (tr.Mode == BattleMode.Doubles)
                        tr.AI |= (int)TrainerAI.Doubles;
                }

                if (CHK_ForceFullyEvolved.Checked && pk.Level >= NUD_ForceFullyEvolved.Value && !FinalEvo.Contains(pk.Species))
                {
                    int randFinalEvo() => (int)(Util.Random32() % FinalEvo.Length);
                    pk.Species = FinalEvo[randFinalEvo()];
                    pk.Form = Randomizer.GetRandomForme(pk.Species, CHK_RandomMegaForm.Checked, true, Main.SpeciesStat);
                }

                pk.Moves = CB_Moves.SelectedIndex switch
                {
                    // Random
                    1 => move.GetRandomMoveset(pk.Species, 4),
                    // Current LevelUp
                    2 => learn.GetCurrentMoves(pk.Species, pk.Form, pk.Level, 4),
                    // Metronome
                    3 => [118, 0, 0, 0],
                    // Otherwise
                    _ => pk.Moves,
                };

                // high-power attacks
                if (CHK_ForceHighPower.Checked && pk.Level >= NUD_ForceHighPower.Value)
                    pk.Moves = learn.GetHighPoweredMoves(pk.Species, pk.Form, 4);

                if (ShouldUseBetterMoveset(trainerGroup) && CB_Moves.SelectedIndex != 3)
                {
                    int teamWeatherMask = GetTeamWeatherSupportMask(tr);
                    pk.Moves = SmartTrainerMovePicker.PickBetterMoveset(
                        pk.Species,
                        pk.Form,
                        pk.Level,
                        pk.Moves,
                        move,
                        learn,
                        moveRule,
                        move.rDMG ? move.rDMGCount : 0,
                        pk.Ability,
                        7,
                        teamWeatherMask
                    );
                }

                if (ShouldApplyMoveRule(moveRule) && CB_Moves.SelectedIndex != 3)
                    pk.Moves = ApplyTrainerMoveRule(pk.Moves, pk.Species, moveRule, move, move.rDMG ? move.rDMGCount : 0);

                if (forceZMove && p == zMoveSlot)
                    EnsureZMove(pk, move);

                // sanitize moves before assigning Smart/Template items so held items see the final moveset
                if (CB_Moves.SelectedIndex > 1) // learn source
                {
                    var moves = pk.Moves;
                    if (move.SanitizeMovesetForBannedMoves(moves, pk.Species))
                        pk.Moves = moves;
                }

                bool canRandomizeItem = CHK_RandomItems.Checked && !(forceMega && p == tr.Pokemon.Count - 1) && !(forceZMove && p == zMoveSlot);
                if (canRandomizeItem && usedHeldItems is not null)
                    usedHeldItems.Remove(pk.Item);

                int[] slotItemPool = ApplyItemClauseToPool(randomItemPool, usedHeldItems);

                if (canRandomizeItem)
                {
                    // GetTrainerMoveRule() only returns Use-checked rules.
                    // When present, its Smart Items checkbox completely overrides
                    // the global Smart Items category setting for this trainer.
                    bool useSmartItems =
                        UseSmartTrainerItems() &&
                        IsSmartItemsCategoryEnabled(trainerGroup);

                    if (useSmartItems)
                    {
                        pk.Item = SmartTrainerItemPicker.Pick(
                            pk.Species,
                            pk.Form,
                            pk.Level,
                            pk.Moves,
                            slotItemPool,
                            pk.Ability,
                            FinalEvo.Contains(pk.Species),
                            GetSmartTrainerItemMode(trainerGroup)
                        );
                    }
                    else
                    {
                        pk.Item = slotItemPool[Util.Random32() % slotItemPool.Length];
                    }
                    TrackItemClause(pk.Item, usedHeldItems);
                }
            }
            SaveData(tr, i);
        }
        RandomizationSessionState.MarkAction("trainers.randomize");
        WinFormsUtil.Alert("Randomized all Trainers according to specification!", "Press the Dump to .TXT button to view the new Trainer information!");
    }

    private static int GetTeamWeatherSupportMask(TrainerData7 tr)
    {
        int mask = 0;
        foreach (var ally in tr.Pokemon)
            mask |= SmartTrainerMovePicker.GetWeatherAbilityMask(ally.Species, ally.Ability);
        return mask;
    }

    private static int[] ApplyItemClauseToPool(IEnumerable<int> itemPool, HashSet<int> usedItems)
    {
        int[] pool = itemPool.Where(i => i > 0).Distinct().ToArray();
        if (usedItems is null || usedItems.Count == 0)
            return pool;

        int[] filtered = pool.Where(item => !usedItems.Contains(item)).ToArray();
        return filtered.Length > 0 ? filtered : pool;
    }

    private static void TrackItemClause(int item, HashSet<int> usedItems)
    {
        if (usedItems is null || item <= 0)
            return;

        usedItems.Add(item);
    }

    private enum TrainerImportanceCategory
    {
        Regular,
        Important,
        Boss,
    }

    private static TrainerImportanceCategory GetTrainerImportanceCategory(int trainerID)
    {
        if (BossTrainers.Contains(trainerID))
            return TrainerImportanceCategory.Boss;

        if (ImportantTrainers.Contains(trainerID))
            return TrainerImportanceCategory.Important;

        return TrainerImportanceCategory.Regular;
    }

    private bool ShouldUseBetterMoveset(string trainerGroup)
    {
        if (!UseBetterMovesets())
            return false;

        return trainerGroup switch
        {
            "Boss" => CHK_BetterMovesetsBosses?.Checked ?? true,
            "Important" => CHK_BetterMovesetsImportantTrainers?.Checked ?? true,
            _ => CHK_BetterMovesetsNormalTrainers?.Checked ?? true,
        };
    }
    private bool IsSmartItemsCategoryEnabled(string trainerGroup)
    {
        return trainerGroup switch
        {
            "Boss" => CHK_SmartItemsBosses?.Checked ?? true,
            "Important" => CHK_SmartItemsImportantTrainers?.Checked ?? true,
            _ => CHK_SmartItemsNormalTrainers?.Checked ?? true,
        };
    }
    private static bool ShouldApplyMoveRule(TrainerMoveRule rule)
    {
        return rule is not null && (rule.MinMovePower > 0 || rule.UseStrongestAttackStat || !rule.AllowStatusMoves);
    }

    private static int[] ApplyTrainerMoveRule(int[] moves, int species, TrainerMoveRule rule, MoveRandomizer move, int minimumDamagingMoves = 0)
    {
        if (!ShouldApplyMoveRule(rule))
            return moves;

        var result = moves.ToArray();
        var used = new HashSet<int>(result.Where(z => z > 0));

        for (int i = 0; i < result.Length; i++)
        {
            int moveID = result[i];
            if (IsMoveAllowedByTrainerRule(moveID, species, rule, move, true, true))
                continue;

            int replacement = GetRandomMoveByTrainerRule(species, rule, used, move);
            if (replacement <= 0)
                continue;

            used.Remove(moveID);
            used.Add(replacement);
            result[i] = replacement;
        }
        return EnsureMinimumDamagingMovesByTrainerRule(result, species, rule, move, minimumDamagingMoves);
    }

    private static int[] EnsureMinimumDamagingMovesByTrainerRule(int[] moves, int species, TrainerMoveRule rule, MoveRandomizer move, int minimumDamagingMoves)
    {
        if (minimumDamagingMoves <= 0)
            return moves;

        var result = moves.ToArray();
        int required = Math.Clamp(minimumDamagingMoves, 0, result.Length);

        int current = result.Count(m => IsDamagingMoveAllowedByTrainerRule(m, species, rule, move, true, true));
        if (current >= required)
            return result;

        var used = new HashSet<int>(result.Where(z => z > 0));

        for (int i = 0; i < result.Length && current < required; i++)
        {
            int moveID = result[i];
            if (IsDamagingMoveAllowedByTrainerRule(moveID, species, rule, move, true, true))
                continue;

            int replacement = GetRandomDamagingMoveByTrainerRule(species, rule, used, move);
            if (replacement <= 0)
                continue;

            used.Remove(moveID);
            used.Add(replacement);
            result[i] = replacement;
            current++;
        }

        return result;
    }

    private static bool IsDamagingMoveAllowedByTrainerRule(int moveID, int species, TrainerMoveRule rule, MoveRandomizer move, bool enforcePower, bool enforceCategory)
    {
        if (!IsMoveAllowedByTrainerRule(moveID, species, rule, move, enforcePower, enforceCategory))
            return false;

        var data = Main.Config.Moves[moveID];
        return data.Category != 0 && data.Power > 0;
    }

    private static int GetRandomDamagingMoveByTrainerRule(int species, TrainerMoveRule rule, HashSet<int> used, MoveRandomizer move)
    {
        int replacement = GetRandomDamagingMoveByTrainerRule(species, rule, used, move, true, true);
        if (replacement > 0)
            return replacement;

        replacement = GetRandomDamagingMoveByTrainerRule(species, rule, used, move, false, true);
        if (replacement > 0)
            return replacement;

        replacement = GetRandomDamagingMoveByTrainerRule(species, rule, used, move, true, false);
        if (replacement > 0)
            return replacement;

        return GetRandomDamagingMoveByTrainerRule(species, rule, used, move, false, false);
    }

    private static int GetRandomDamagingMoveByTrainerRule(int species, TrainerMoveRule rule, HashSet<int> used, MoveRandomizer move, bool enforcePower, bool enforceCategory)
    {
        int maxMove = Math.Min(Main.Config.Info.MaxMoveID, Main.Config.Moves.Length);
        int[] types = species > 0 && species < Main.SpeciesStat.Length
            ? Main.SpeciesStat[species].Types
            : [];

        var eligible = Enumerable.Range(1, maxMove - 1)
            .Where(m => !used.Contains(m))
            .Where(m => IsDamagingMoveAllowedByTrainerRule(m, species, rule, move, enforcePower, enforceCategory))
            .ToList();

        if (eligible.Count == 0)
            return 0;

        var stab = eligible.Where(m => types.Contains(Main.Config.Moves[m].Type)).ToList();
        if (move.rSTAB && stab.Count > 0)
            return stab[(int)(Util.Random32() % stab.Count)];

        return eligible[(int)(Util.Random32() % eligible.Count)];
    }
    private static bool IsMoveAllowedByTrainerRule(int moveID, int species, TrainerMoveRule rule, MoveRandomizer move, bool enforcePower, bool enforceCategory)
    {
        if (moveID <= 0 || moveID >= Main.Config.Moves.Length)
            return false;

        if (move.BannedMoves.Contains(moveID))
            return false;

        var data = Main.Config.Moves[moveID];

        if (data.Category == 0)
            return rule.AllowStatusMoves;

        if (enforcePower && rule.MinMovePower > 0 && data.Power < ClampMovePower(rule.MinMovePower))
            return false;

        if (enforceCategory && rule.UseStrongestAttackStat)
        {
            int preferred = GetPreferredMoveCategory(species, rule.MixedTolerance);
            if (preferred != 0 && data.Category != preferred)
                return false;
        }

        return true;
    }

    private static int GetPreferredMoveCategory(int species, int tolerance)
    {
        if (species <= 0 || species >= Main.SpeciesStat.Length)
            return 0;

        var p = Main.SpeciesStat[species];
        int delta = p.ATK - p.SPA;
        int tol = Math.Max(0, tolerance);

        if (delta > tol)
            return 1; // Physical
        if (-delta > tol)
            return 2; // Special

        return 0; // Mixed: allow Physical and Special
    }

    private static int GetRandomMoveByTrainerRule(int species, TrainerMoveRule rule, HashSet<int> used, MoveRandomizer move)
    {
        int replacement = GetRandomMoveByTrainerRule(species, rule, used, move, true, true);
        if (replacement > 0)
            return replacement;

        replacement = GetRandomMoveByTrainerRule(species, rule, used, move, false, true);
        if (replacement > 0)
            return replacement;

        replacement = GetRandomMoveByTrainerRule(species, rule, used, move, true, false);
        if (replacement > 0)
            return replacement;

        return GetRandomMoveByTrainerRule(species, rule, used, move, false, false);
    }

    private static int GetRandomMoveByTrainerRule(int species, TrainerMoveRule rule, HashSet<int> used, MoveRandomizer move, bool enforcePower, bool enforceCategory)
    {
        int maxMove = Math.Min(Main.Config.Info.MaxMoveID, Main.Config.Moves.Length);
        int[] types = Main.SpeciesStat[species].Types;

        var eligible = Enumerable.Range(1, maxMove - 1)
            .Where(m => !used.Contains(m))
            .Where(m => IsMoveAllowedByTrainerRule(m, species, rule, move, enforcePower, enforceCategory))
            .ToList();

        if (eligible.Count == 0)
            return 0;

        var stab = eligible.Where(m => types.Contains(Main.Config.Moves[m].Type)).ToList();
        if (move.rSTAB && stab.Count > 0)
            return stab[(int)(Util.Random32() % stab.Count)];

        return eligible[(int)(Util.Random32() % eligible.Count)];
    }


    private static readonly Dictionary<int, int> ZCrystalByMoveType = new()
    {
        [0] = 776,
        [1] = 782,
        [2] = 785,
        [3] = 783,
        [4] = 784,
        [5] = 788,
        [6] = 787,
        [7] = 789,
        [8] = 792,
        [9] = 777,
        [10] = 778,
        [11] = 780,
        [12] = 779,
        [13] = 786,
        [14] = 781,
        [15] = 790,
        [16] = 791,
        [17] = 793,
    };

    private static int[] GetTrainerRandomItemPool(int[] items, bool forceMega, bool forceZMove)
    {
        IEnumerable<int> pool = items;
        if (forceMega)
            pool = RemoveMegaStonesFromItemPool(pool);
        if (forceZMove)
            pool = RemoveZCrystalsFromItemPool(pool);
        var result = pool.ToArray();
        return result.Length == 0 ? items : result;
    }

    private static T[] RemoveZCrystalsFromItemPool<T>(IEnumerable<T> items)
    {
        var zCrystals = ZCrystalByMoveType.Values.ToHashSet();
        return items.Where(item => !zCrystals.Contains(Convert.ToInt32(item))).ToArray();
    }

    private static int GetZMoveSlot(int count, bool forceMega, bool forceZMove)
    {
        if (!forceZMove || count <= 0)
            return -1;
        if (forceMega && count > 1)
            return count - 2;
        if (forceMega)
            return -1;
        return count - 1;
    }

    private static void EnsureZMove(TrainerPoke7 pk, MoveRandomizer move)
    {
        if (pk is null || pk.Species <= 0)
            return;
        for (int i = 0; i < pk.Moves.Length; i++)
        {
            int moveID = pk.Moves[i];
            if (TryGetZCrystalForMove(moveID, move, out int crystal))
            {
                pk.Item = crystal;
                return;
            }
        }
        int replacement = GetRandomMoveForZMove(pk.Species, move);
        if (replacement <= 0)
            return;
        pk.Moves[0] = replacement;
        if (TryGetZCrystalForMove(replacement, move, out int replacementCrystal))
            pk.Item = replacementCrystal;
    }

    private static bool TryGetZCrystalForMove(int moveID, MoveRandomizer move, out int crystal)
    {
        crystal = 0;
        if (moveID <= 0 || moveID >= Main.Config.Moves.Length)
            return false;
        if (move.BannedMoves.Contains(moveID))
            return false;
        var data = Main.Config.Moves[moveID];
        if (data.Category == 0 || data.Power <= 0)
            return false;
        return ZCrystalByMoveType.TryGetValue(data.Type, out crystal);
    }

    private static int GetRandomMoveForZMove(int species, MoveRandomizer move)
    {
        int maxMove = Math.Min(Main.Config.Info.MaxMoveID, Main.Config.Moves.Length);
        int[] types = Main.SpeciesStat[species].Types;
        var eligible = Enumerable.Range(1, maxMove - 1).Where(m => TryGetZCrystalForMove(m, move, out _)).ToList();
        if (eligible.Count == 0)
            return 0;
        var stab = eligible.Where(m => types.Contains(Main.Config.Moves[m].Type)).ToList();
        if (move.rSTAB && stab.Count > 0)
            return stab[(int)(Util.Random32() % stab.Count)];
        return eligible[(int)(Util.Random32() % eligible.Count)];
    }

    private static void ApplyTrainerEVOverride(TrainerPoke7 pk, TrainerMoveRule rule)
    {
        if (rule is null || rule.OverrideEVs < 0)
            return;
        int ev = Math.Clamp(rule.OverrideEVs, 0, 252);
        pk.EVs = Enumerable.Repeat(ev, 6).ToArray();
    }
    private void FixGen7TrainerOptionsLayout()
    {
        // Do not overcrowd the Gen7 Main tab. It already contains Random Shinies and level multiplier.
        // Move custom trainer-level options to their own Rules tab.
        try
        {
            Width = Math.Max(Width, 760);
            MinimumSize = new Size(Math.Max(MinimumSize.Width, 760), Math.Max(MinimumSize.Height, 560));

            // Restore/keep vanilla Main tab layout so hidden original options stay visible.
            if (CHK_RandomPKM is not null)
                CHK_RandomPKM.Location = new Point(154, 5);

            if (CHK_BST is not null)
                CHK_BST.Location = new Point(154, 23);

            if (CHK_ProgressiveBST is not null)
            {
                CHK_ProgressiveBST.Text = "Progressive BST";
                CHK_ProgressiveBST.AutoSize = true;
                CHK_ProgressiveBST.Location = new Point(154, 43);
                CHK_ProgressiveBST.BringToFront();
            }

            if (B_SetManualBST is not null)
            {
                B_SetManualBST.Text = "BST ranges";
                B_SetManualBST.Size = new Size(86, 23);
                B_SetManualBST.Location = new Point(260, 40);
                B_SetManualBST.BringToFront();
            }

            if (CHK_RandomShiny is not null)
            {
                CHK_RandomShiny.Location = new Point(154, 92);
                CHK_RandomShiny.BringToFront();
            }

            if (NUD_Shiny is not null)
            {
                NUD_Shiny.Location = new Point(286, 90);
                NUD_Shiny.BringToFront();
            }

            if (L_ShinyPCT is not null)
            {
                L_ShinyPCT.Location = new Point(330, 93);
                L_ShinyPCT.BringToFront();
            }

            if (CHK_Level is not null)
            {
                CHK_Level.Text = "Multiply PKM Level by";
                CHK_Level.AutoSize = true;
                CHK_Level.Location = new Point(154, 112);
                CHK_Level.BringToFront();
            }

            if (NUD_LevelBoost is not null)
            {
                NUD_LevelBoost.Size = new Size(43, 20);
                NUD_LevelBoost.Location = new Point(286, 111);
                NUD_LevelBoost.BringToFront();
            }

            if (TC_rand is null)
                return;

            var rulesTab = TC_rand.TabPages
                .Cast<TabPage>()
                .FirstOrDefault(t => t.Name == "Tab_TrainerRulesCustom" || t.Text == "Rules");

            if (rulesTab is null)
            {
                rulesTab = new TabPage
                {
                    Name = "Tab_TrainerRulesCustom",
                    Text = "Rules",
                    UseVisualStyleBackColor = true,
                };

                TC_rand.TabPages.Add(rulesTab);
            }

            rulesTab.Controls.Clear();

            if (CHK_LevelCaps is not null)
            {
                CHK_LevelCaps.Text = "Level Caps";
                CHK_LevelCaps.AutoSize = true;
                CHK_LevelCaps.Location = new Point(12, 16);
                rulesTab.Controls.Add(CHK_LevelCaps);
            }

            if (B_SetLevelCaps is not null)
            {
                B_SetLevelCaps.Text = "Caps";
                B_SetLevelCaps.Size = new Size(54, 23);
                B_SetLevelCaps.Location = new Point(115, 12);
                rulesTab.Controls.Add(B_SetLevelCaps);
            }

            if (B_SetTrainerMoveRules is not null)
            {
                B_SetTrainerMoveRules.Text = "Moves";
                B_SetTrainerMoveRules.Size = new Size(64, 23);
                B_SetTrainerMoveRules.Location = new Point(176, 12);
                rulesTab.Controls.Add(B_SetTrainerMoveRules);
            }

            if (B_TrainerTemplate is not null)
            {
                B_TrainerTemplate.Text = "Template...";
                B_TrainerTemplate.Size = new Size(92, 23);
                B_TrainerTemplate.Location = new Point(248, 12);
                rulesTab.Controls.Add(B_TrainerTemplate);
            }

            if (CHK_RandomDoubleBattles is not null)
            {
                CHK_RandomDoubleBattles.Text = "Double battles";
                CHK_RandomDoubleBattles.AutoSize = true;
                CHK_RandomDoubleBattles.Location = new Point(12, 48);
                rulesTab.Controls.Add(CHK_RandomDoubleBattles);
            }

            if (NUD_DoubleBattleChance is not null)
            {
                NUD_DoubleBattleChance.Size = new Size(48, 20);
                NUD_DoubleBattleChance.Location = new Point(115, 46);
                rulesTab.Controls.Add(NUD_DoubleBattleChance);
            }

            var percent = Tab_PKM1?.Controls.Find("L_DoubleBattlePercent", true).FirstOrDefault() as Label
                ?? rulesTab.Controls.Find("L_DoubleBattlePercent", true).FirstOrDefault() as Label;

            if (percent is not null)
            {
                percent.Text = "%";
                percent.AutoSize = true;
                percent.Location = new Point(168, 49);
                rulesTab.Controls.Add(percent);
            }

            // Keep this tab compact. Detailed behavior is covered in documentation.
        }
        catch
        {
            // UI-only adjustment. If something is missing in a different build, do not block the editor.
        }
    }

    private void AddBanBadItemsControl()
    {
        try
        {
            CHK_BanBadItems ??= new CheckBox
            {
                AutoSize = true,
                Name = "CHK_BanBadItems",
                TabIndex = 1012,
                Text = "Ban Bad Items",
                UseVisualStyleBackColor = true,
                Enabled = CHK_RandomItems.Checked,
                Checked = true,
            };

            CHK_RandomItems.CheckedChanged += (_, _) =>
            {
                CHK_BanBadItems.Enabled = CHK_RandomItems.Checked;
            };

            Control parent = CB_Moves?.Parent;
            if (parent is null)
                parent = Tab_Rand;
            if (parent is null)
                parent = this;

            CHK_BanBadItems.Location = parent is TabPage
                ? new Point(12, 84)
                : new Point(CHK_RandomItems.Left, CHK_RandomItems.Bottom + 6);

            if (!parent.Controls.Contains(CHK_BanBadItems))
                parent.Controls.Add(CHK_BanBadItems);

            CHK_BanBadItems.BringToFront();
        }
        catch
        {
            // UI-only helper.
        }
    }
    private void PlaceGen7ImplementedOptionsInRulesTab()
    {
        try
        {
            if (TC_rand is null)
                return;

            var rulesTab = TC_rand.TabPages
                .Cast<TabPage>()
                .FirstOrDefault(t => t.Name == "Tab_TrainerRulesCustom" || t.Text == "Rules");

            if (rulesTab is null)
            {
                rulesTab = new TabPage
                {
                    Name = "Tab_TrainerRulesCustom",
                    Text = "Rules",
                    UseVisualStyleBackColor = true,
                };
                TC_rand.TabPages.Add(rulesTab);
            }

            void MoveToRules(Control control, int x, int y, int width = 0, int height = 0)
            {
                if (control is null)
                    return;

                if (control.Parent != rulesTab)
                {
                    control.Parent?.Controls.Remove(control);
                    rulesTab.Controls.Add(control);
                }

                if (width > 0 || height > 0)
                    control.Size = new Size(width > 0 ? width : control.Width, height > 0 ? height : control.Height);

                control.Location = new Point(x, y);
                control.BringToFront();
            }

            GroupBox GetOrCreateRulesGroup(string name, string text, int x, int y, int width, int height)
            {
                var group = rulesTab.Controls.Find(name, false)
                    .OfType<GroupBox>()
                    .FirstOrDefault();

                if (group is null)
                {
                    group = new GroupBox
                    {
                        Name = name,
                        Text = text,
                        BackColor = Color.White,
                        UseCompatibleTextRendering = true,
                    };
                    rulesTab.Controls.Add(group);
                }

                group.Location = new Point(x, y);
                group.Size = new Size(width, height);
                group.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                group.BringToFront();
                return group;
            }

            void MoveToGroup(Control control, GroupBox group, int x, int y, int width = 0, int height = 0)
            {
                if (control is null || group is null)
                    return;

                if (control.Parent != group)
                {
                    control.Parent?.Controls.Remove(control);
                    group.Controls.Add(control);
                }

                if (width > 0 || height > 0)
                    control.Size = new Size(width > 0 ? width : control.Width, height > 0 ? height : control.Height);

                control.Location = new Point(x, y);
                control.BringToFront();
            }

            var randomItems = Controls.Find("CHK_RandomItems", true).FirstOrDefault() as CheckBox;
            var maxIvs = Controls.Find("CHK_MaxDiffPKM", true).FirstOrDefault() as CheckBox;
            var maxAI = Controls.Find("CHK_MaxAI", true).FirstOrDefault() as CheckBox;

            // Difficulty / caps.
            MoveToRules(CHK_LevelCaps, 12, 14);
            MoveToRules(B_SetLevelCaps, 115, 10, 62, 24);
            MoveToRules(B_SetTrainerMoveRules, 185, 10, 70, 24);
            MoveToRules(B_TrainerTemplate, 263, 10, 100, 24);
            MoveToRules(CHK_RandomDoubleBattles, 12, 44);
            MoveToRules(NUD_DoubleBattleChance, 145, 42, 50, 22);

            var percent = Controls.Find("L_DoubleBattlePercent", true).FirstOrDefault() as Label;
            if (percent is not null)
            {
                percent.Text = "%";
                percent.AutoSize = true;
                MoveToRules(percent, 200, 46);
            }

            // Keep Max IV / Max AI in the compact top area.
            MoveToRules(maxIvs, 238, 44);
            MoveToRules(maxAI, 310, 44);

            int groupWidth = Math.Max(390, rulesTab.ClientSize.Width - 16);

            var betterGroup = GetOrCreateRulesGroup(
                "GB_GlobalBetterMovesets",
                "Better Movesets",
                8,
                72,
                groupWidth,
                72
            );

            MoveToGroup(CHK_BetterMovesets, betterGroup, 10, 19);
            MoveToGroup(CHK_BetterMovesetsNormalTrainers, betterGroup, 28, 43);
            MoveToGroup(CHK_BetterMovesetsImportantTrainers, betterGroup, 145, 43);
            MoveToGroup(CHK_BetterMovesetsBosses, betterGroup, 280, 43);

            var itemsGroup = GetOrCreateRulesGroup(
                "GB_GlobalHeldItems",
                "Held Items",
                8,
                150,
                groupWidth,
                136
            );

            // Master row. Leave enough horizontal room for both labels.
            MoveToGroup(randomItems, itemsGroup, 20, 20);
            MoveToGroup(CHK_SmartHeldItems, itemsGroup, 275, 20);

            // Category row.
            MoveToGroup(CHK_SmartItemsNormalTrainers, itemsGroup, 28, 47);
            MoveToGroup(CHK_SmartItemsImportantTrainers, itemsGroup, 145, 47);
            MoveToGroup(CHK_SmartItemsBosses, itemsGroup, 280, 47);

            // Independent quality selector directly below each category.
            MoveToGroup(CB_SmartHeldItemMode, itemsGroup, 28, 70, 105, 23);
            MoveToGroup(CB_SmartHeldItemModeImportant, itemsGroup, 145, 70, 105, 23);
            MoveToGroup(CB_SmartHeldItemModeBoss, itemsGroup, 280, 70, 105, 23);

            MoveToGroup(CHK_BanBadItems, itemsGroup, 28, 104);
            MoveToGroup(CHK_ItemClause, itemsGroup, 165, 104);

            rulesTab.AutoScroll = false;
            rulesTab.MinimumSize = new Size(
                Math.Max(rulesTab.MinimumSize.Width, 420),
                Math.Max(rulesTab.MinimumSize.Height, 290)
            );

            ConfigureGen7RulesWorkspace(rulesTab);
            // Refresh dependencies/visibility after the controls move.
            UpdateBetterMovesetsSubmenuState();
            UpdateSmartItemsSubmenuState();

            if (randomItems is not null)
                CHK_BanBadItems.Enabled = randomItems.Checked;

            // Give the Rules tab enough room so nothing gets clipped.
            rulesTab.AutoScroll = true;
            rulesTab.MinimumSize = new Size(Math.Max(rulesTab.MinimumSize.Width, 420), Math.Max(rulesTab.MinimumSize.Height, 270));
            ConfigureGen7RulesWorkspace(rulesTab);
        }
        catch
        {
            // UI-only adjustment.
        }
    }
    private void ConfigureGen7RulesWorkspace(TabPage rulesTab)
    {
        if (TC_rand is null || TC_trdata is null || rulesTab is null)
            return;

        Gen7RulesTab = rulesTab;

        if (Gen7BaseRandomizerHeight < 0)
        {
            Gen7BaseRandomizerHeight = TC_rand.Height;
            Gen7BaseTrainerDataHeight = TC_trdata.Height;
            Gen7BaseTeamLabelTop = L_Team?.Top ?? -1;

            if (pba is not null)
                Gen7BaseTeamTops = pba.Select(pb => pb?.Top ?? -1).ToArray();
        }

        if (!Gen7RulesWorkspaceHooked)
        {
            TC_rand.SelectedIndexChanged += (_, _) => ApplyGen7RulesWorkspace();
            Gen7RulesWorkspaceHooked = true;
        }

        ApplyGen7RulesWorkspace();
    }

    private void ApplyGen7RulesWorkspace()
    {
        if (TC_rand is null ||
            TC_trdata is null ||
            Gen7RulesTab is null ||
            Gen7BaseRandomizerHeight < 0)
        {
            return;
        }

        bool rulesSelected = TC_rand.SelectedTab == Gen7RulesTab;

        int targetRandomizerHeight = rulesSelected
            ? 325
            : Gen7BaseRandomizerHeight;

        int delta = targetRandomizerHeight - Gen7BaseRandomizerHeight;

        TC_rand.Height = targetRandomizerHeight;
        TC_trdata.Height = Gen7BaseTrainerDataHeight + delta;

        if (L_Team is not null && Gen7BaseTeamLabelTop >= 0)
            L_Team.Top = Gen7BaseTeamLabelTop + delta;

        if (pba is not null && Gen7BaseTeamTops is not null)
        {
            int count = Math.Min(pba.Length, Gen7BaseTeamTops.Length);
            for (int i = 0; i < count; i++)
            {
                if (pba[i] is not null && Gen7BaseTeamTops[i] >= 0)
                    pba[i].Top = Gen7BaseTeamTops[i] + delta;
            }
        }
    }
    private void AddProgressiveBSTControls()
    {
        int y = CHK_BST.Bottom + 6;

        CHK_ProgressiveBST = new CheckBox
        {
            AutoSize = true,
            Location = new Point(CHK_BST.Left, y),
            Name = "CHK_ProgressiveBST",
            Size = new Size(110, 17),
            TabIndex = 999,
            Text = "Progressive BST",
            UseVisualStyleBackColor = true,
        };

        B_SetManualBST = new Button
        {
            Location = new Point(CHK_BST.Left + 120, y - 3),
            Name = "B_SetManualBST",
            Size = new Size(125, 23),
            TabIndex = 1000,
            Text = "Set BST manually",
            UseVisualStyleBackColor = true,
            Enabled = false,
        };

        CHK_ProgressiveBST.CheckedChanged += (_, _) =>
        {
            if (CHK_ProgressiveBST.Checked)
                CHK_BST.Checked = false;

            B_SetManualBST.Enabled = CHK_ProgressiveBST.Checked && CHK_RandomPKM.Checked;
        };

        CHK_BST.CheckedChanged += (_, _) =>
        {
            if (CHK_BST.Checked)
                CHK_ProgressiveBST.Checked = false;
        };

        B_SetManualBST.Click += (_, _) => ShowManualBSTDialog();

        Tab_PKM1.Controls.Add(CHK_ProgressiveBST);
        Tab_PKM1.Controls.Add(B_SetManualBST);
    }
    private void AddLevelCapControls()
    {
        int y = CHK_ProgressiveBST.Bottom + 6;

        CHK_LevelCaps = new CheckBox
        {
            AutoSize = true,
            Location = new Point(CHK_BST.Left, y),
            Name = "CHK_LevelCaps",
            TabIndex = 1001,
            Text = "Level Caps",
            UseVisualStyleBackColor = true,
            Enabled = LevelCapRules.Count > 0,
        };

        B_SetLevelCaps = new Button
        {
            Location = new Point(CHK_BST.Left + 100, y - 3),
            Name = "B_SetLevelCaps",
            Size = new Size(92, 23),
            TabIndex = 1002,
            Text = "Set caps",
            UseVisualStyleBackColor = true,
            Enabled = false,
        };

        CHK_LevelCaps.CheckedChanged += (_, _) =>
        {
            B_SetLevelCaps.Enabled = CHK_LevelCaps.Checked && LevelCapRules.Count > 0;
        };

        B_SetLevelCaps.Click += (_, _) => ShowLevelCapDialog();

        B_SetTrainerMoveRules = new Button
        {
            Location = new Point(B_SetLevelCaps.Right + 8, y - 3),
            Name = "B_SetTrainerMoveRules",
            Size = new Size(105, 23),
            TabIndex = 1005,
            Text = "Move rules",
            UseVisualStyleBackColor = true,
            Enabled = MoveRules.Count > 0,
        };

        B_SetTrainerMoveRules.Click += (_, _) => ShowTrainerMoveRulesDialog();

        B_TrainerTemplate = new Button
        {
            Name = "B_TrainerTemplate",
            Size = new Size(108, 23),
            TabIndex = 1006,
            Text = "Template...",
            UseVisualStyleBackColor = true,
        };
        B_TrainerTemplate.Click += (_, _) => ShowTrainerTemplateMenu();

        Tab_PKM1.Controls.Add(CHK_LevelCaps);
        Tab_PKM1.Controls.Add(B_SetLevelCaps);
        Tab_PKM1.Controls.Add(B_SetTrainerMoveRules);
        Tab_PKM1.Controls.Add(B_TrainerTemplate);
        CHK_LevelCaps.BringToFront();
        B_SetLevelCaps.BringToFront();
        B_SetTrainerMoveRules.BringToFront();
        B_TrainerTemplate.BringToFront();

        int y2 = CHK_LevelCaps.Bottom + 6;
        CHK_RandomDoubleBattles = new CheckBox
        {
            AutoSize = true,
            Location = new Point(CHK_BST.Left, y2),
            Name = "CHK_RandomDoubleBattles",
            TabIndex = 1003,
            Text = "Double battles",
            UseVisualStyleBackColor = true,
        };

        NUD_DoubleBattleChance = new NumericUpDown
        {
            Location = new Point(CHK_BST.Left + 145, y2 - 2),
            Name = "NUD_DoubleBattleChance",
            Size = new Size(55, 20),
            TabIndex = 1004,
            Minimum = 0,
            Maximum = 100,
            Value = 15,
            Enabled = false,
        };

        var L_DoubleBattlePercent = new Label
        {
            AutoSize = true,
            Location = new Point(NUD_DoubleBattleChance.Right + 5, NUD_DoubleBattleChance.Top + 3),
            Name = "L_DoubleBattlePercent",
            Text = "%",
        };

        CHK_RandomDoubleBattles.CheckedChanged += (_, _) =>
        {
            NUD_DoubleBattleChance.Enabled = CHK_RandomDoubleBattles.Checked;
        };

        Tab_PKM1.Controls.Add(CHK_RandomDoubleBattles);
        Tab_PKM1.Controls.Add(NUD_DoubleBattleChance);
        Tab_PKM1.Controls.Add(L_DoubleBattlePercent);
        CHK_RandomDoubleBattles.BringToFront();
        NUD_DoubleBattleChance.BringToFront();
        L_DoubleBattlePercent.BringToFront();
    }

    private void B_HighAttack_Click(object sender, EventArgs e)
    {
        pkm.Species = CB_Species.SelectedIndex;
        pkm.Level = (int)NUD_Level.Value;
        pkm.Form = CB_Forme.SelectedIndex;
        var moves = learn.GetHighPoweredMoves(pkm.Species, pkm.Form, 4);
        SetMoves(moves);
    }

    private void B_CurrentAttack_Click(object sender, EventArgs e)
    {
        pkm.Species = CB_Species.SelectedIndex;
        pkm.Level = (int)NUD_Level.Value;
        pkm.Form = CB_Forme.SelectedIndex;
        var moves = learn.GetCurrentMoves(pkm.Species, pkm.Form, pkm.Level, 4);
        SetMoves(moves);
    }

    private void B_Clear_Click(object sender, EventArgs e) => SetMoves(new int[4]);

    private void SetMoves(IList<int> moves)
    {
        var mcb = new[] { CB_Move1, CB_Move2, CB_Move3, CB_Move4 };
        for (int i = 0; i < mcb.Length; i++)
            mcb[i].SelectedIndex = moves[i];
    }

    // Randomization UI
    private void CB_Moves_SelectedIndexChanged(object sender, EventArgs e)
    {
        CHK_Damage.Checked = CHK_STAB.Checked =
            CHK_Damage.Enabled = CHK_STAB.Enabled =
                NUD_Damage.Enabled = NUD_STAB.Enabled = CB_Moves.SelectedIndex == 1;

        CHK_ForceHighPower.Enabled = CHK_ForceHighPower.Checked = NUD_ForceHighPower.Enabled =
            CHK_NoFixedDamage.Enabled = CHK_NoFixedDamage.Checked = CB_Moves.SelectedIndex is 1 or 2;
    }

    private void CHK_Damage_CheckedChanged(object sender, EventArgs e)
    {
        NUD_Damage.Enabled = CHK_Damage.Checked;
    }

    private void CHK_STAB_CheckedChanged(object sender, EventArgs e)
    {
        NUD_STAB.Enabled = CHK_STAB.Checked;
    }

    private void CHK_RandomPKM_CheckedChanged(object sender, EventArgs e)
    {
        foreach (CheckBox c in new[] { CHK_G1, CHK_G2, CHK_G3, CHK_G4, CHK_G5, CHK_G6, CHK_G7, CHK_L, CHK_E, CHK_BST })
        {
            c.Enabled = CHK_RandomPKM.Checked;
            c.Checked = CHK_RandomPKM.Checked;
        }

        if (CHK_ProgressiveBST is not null)
        {
            CHK_ProgressiveBST.Enabled = CHK_RandomPKM.Checked;

            if (!CHK_RandomPKM.Checked)
                CHK_ProgressiveBST.Checked = false;
        }

        if (B_SetManualBST is not null)
            B_SetManualBST.Enabled = CHK_RandomPKM.Checked && CHK_ProgressiveBST.Checked;
    }

    private void CHK_RandomClass_CheckedChanged(object sender, EventArgs e)
    {
        CHK_IgnoreSpecialClass.Enabled = CHK_RandomClass.Checked;
        if (!CHK_RandomClass.Checked)
            CHK_IgnoreSpecialClass.Checked = false;
    }

    private void CHK_RandomShiny_CheckedChanged(object sender, EventArgs e)
    {
        NUD_Shiny.Enabled = CHK_RandomShiny.Checked;
    }

    private void CHK_Level_CheckedChanged(object sender, EventArgs e)
    {
        NUD_LevelBoost.Enabled = CHK_Level.Checked;
    }


    private static T[] RemoveMegaStonesFromItemPool<T>(IEnumerable<T> items)
    {
        if (MegaDictionary == null || MegaDictionary.Count == 0)
            return items.ToArray();

        var megaStones = MegaDictionary.Values.SelectMany(z => z).ToHashSet();
        return items.Where(item => !megaStones.Contains(Convert.ToInt32(item))).ToArray();
    }
    private static int[] GetRandomMega(out int species)
    {
        int rnd = Util.Rand.Next(0, MegaDictionary.Count - 1);
        species = MegaDictionary.Keys.ElementAt(rnd);
        return MegaDictionary.Values.ElementAt(rnd);
    }

    private static int[] GetRandomMegaForType(int type, bool requireType, out int species)
    {
        int[] stones;
        int tries = 0;
        do
        {
            stones = GetRandomMega(out species);
        }
        while (requireType && type >= 0 && Main.Config.Personal[species].Types.All(z => z != type) && tries++ < 100);

        return stones;
    }
}

