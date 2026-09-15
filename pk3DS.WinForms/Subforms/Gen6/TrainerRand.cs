using pk3DS.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pk3DS.Core.Randomizers;

namespace pk3DS.WinForms;

public partial class TrainerRand : Form
{
    public TrainerRand(List<TrainerLevelCapRule> levelCapRules = null)
    {
        InitializeComponent();
        LevelCapRules = levelCapRules?.Select(r => r.Clone()).ToList() ?? [];
        MoveRules = TrainerMoveRule.FromLevelCapRules(LevelCapRules);
        AddProgressiveBSTControls();
        CB_Moves.SelectedIndex = 1;
        var trClassnorep = new List<string>();
        trClassnorep.AddRange(trClass.Where(tclass => !trClassnorep.Contains(tclass) && !tclass.StartsWith("[~")));
        trClassnorep.Sort();
        RandSettings.GetFormSettings(this, Controls);
        ConfigureModernTrainerLayout();

        if (TrainerRandomizerTemplateFile.TryGetCurrent(CurrentTrainerTemplateGame, out var currentTemplate))
            ApplyTrainerTemplate(currentTemplate, showMessage: false);

    }
    private void ShowManualBSTDialog()
    {
        using var form = new Form
        {
            Text = "Manual Progressive BST Settings",
            StartPosition = FormStartPosition.CenterParent,
            Size = new System.Drawing.Size(560, 360),
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

    //private readonly string[] trName = Main.Config.GetText(TextName.TrainerNames);
    private readonly string[] trClass = Main.Config.GetText(TextName.TrainerClasses);
    //private readonly List<string> trClassnorep;
    private static readonly int[] Legendary = Legal.Legendary_6;
    private static readonly int[] Mythical = Legal.Mythical_6;

    private CheckBox CHK_ProgressiveBST;
    private Button B_SetManualBST;
    private CheckBox CHK_LevelCaps;
    private Button B_SetLevelCaps;
    private Button B_SetTrainerMoveRules;
    private Button B_TrainerTemplate;
    private CheckBox CHK_RandomDoubleBattles;
    private NumericUpDown NUD_DoubleBattleChance;
    private CheckBox CHK_SmartHeldItems;
    private CheckBox CHK_ItemClause;
    private ComboBox CB_SmartHeldItemMode;
    private CheckBox CHK_BetterMovesets;

    private List<ProgressiveBSTRule> ProgressiveBSTRules = GetDefaultProgressiveBSTRules();
    private List<TrainerLevelCapRule> LevelCapRules = [];
    private List<TrainerMoveRule> MoveRules = [];
    private bool ApplyCapsToPreviousTrainers = true;
    private int PreviousTrainerGap = 2;
    private decimal RegularTrainerCurvePower = 1.6m;
    private bool GuaranteeMegaInImportantBattles = false;


    private void ConfigureModernTrainerLayout()
    {
        SuspendLayout();

        Text = "Trainer Randomizer";
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        AutoScroll = true;
        MinimumSize = new Size(1000, 730);
        MaximumSize = Size.Empty;
        ClientSize = new Size(1020, 760);
        BackColor = ModernUI.Background;

        const int margin = 16;
        const int gap = 14;
        int columnWidth = (ClientSize.Width - margin * 2 - gap) / 2;

        var title = new Label
        {
            Text = "Trainer Randomizer",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
            Location = new Point(margin, 12),
        };

        var subtitle = new Label
        {
            Text = "Configure Pokémon, progression, movesets, held items, and difficulty rules.",
            AutoSize = true,
            ForeColor = ModernUI.MutedText,
            Location = new Point(margin, 38),
        };

        Controls.Add(title);
        Controls.Add(subtitle);

        B_TrainerTemplate.Size = new Size(120, 30);
        B_TrainerTemplate.Location = new Point(ClientSize.Width - margin - B_TrainerTemplate.Width, 20);
        B_TrainerTemplate.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        Controls.Add(B_TrainerTemplate);
        B_TrainerTemplate.BringToFront();

        var pokemon = CreateSection("Pokémon pool & progression", margin, 70, columnWidth, 340);
        var moves = CreateSection("Movesets", margin + columnWidth + gap, 70, columnWidth, 340);
        var items = CreateSection("Held items & battle stats", margin, 428, columnWidth, 226);
        var trainer = CreateSection("Trainer class, gifts & AI", margin + columnWidth + gap, 428, columnWidth, 226);

        AddSection(pokemon);
        AddSection(moves);
        AddSection(items);
        AddSection(trainer);

        LayoutPokemonSection(pokemon);
        LayoutMovesSection(moves);
        LayoutItemsSection(items);
        LayoutTrainerSection(trainer);

        B_OK.Text = "Apply";
        B_OK.Size = new Size(96, 32);
        B_OK.Location = new Point(ClientSize.Width - margin - B_OK.Width, ClientSize.Height - 48);
        B_OK.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

        B_Cancel.Size = new Size(96, 32);
        B_Cancel.Location = new Point(B_OK.Left - 106, B_OK.Top);
        B_Cancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

        Controls.Add(B_OK);
        Controls.Add(B_Cancel);
        B_OK.BringToFront();
        B_Cancel.BringToFront();

        ModernUI.Apply(this);
        ResumeLayout(false);
    }

    private static GroupBox CreateSection(string text, int x, int y, int width, int height) => new()
    {
        Text = text,
        Location = new Point(x, y),
        Size = new Size(width, height),
        Anchor = AnchorStyles.Top | AnchorStyles.Left,
        BackColor = ModernUI.Background,
        Padding = new Padding(12),
    };

    private void AddSection(GroupBox section)
    {
        Controls.Add(section);
        section.BringToFront();
    }

    private static void Move(Control parent, Control control, int x, int y, int width = 0, int height = 0)
    {
        if (control.Parent != null)
            control.Parent.Controls.Remove(control);

        parent.Controls.Add(control);
        control.Location = new Point(x, y);
        control.Anchor = AnchorStyles.Top | AnchorStyles.Left;

        if (width > 0)
            control.Width = width;
        if (height > 0)
            control.Height = height;
    }

    private static Label SmallLabel(string text, int x, int y) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = ModernUI.MutedText,
        Location = new Point(x, y),
    };

    private void LayoutPokemonSection(GroupBox section)
    {
        Move(section, CHK_RandomPKM, 16, 24);
        Move(section, GB_Tweak, 16, 50, section.Width - 32, 150);
        GB_Tweak.Text = "Species sources";
        GB_Tweak.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        CHK_G1.Location = new Point(12, 24);
        CHK_G2.Location = new Point(12, 46);
        CHK_G3.Location = new Point(12, 68);
        CHK_G4.Location = new Point(84, 24);
        CHK_G5.Location = new Point(84, 46);
        CHK_G6.Location = new Point(84, 68);

        CHK_L.Location = new Point(164, 24);
        CHK_E.Location = new Point(164, 46);
        CHK_BST.Location = new Point(292, 24);
        CHK_ProgressiveBST.Location = new Point(292, 52);
        B_SetManualBST.Location = new Point(292, 84);
        B_SetManualBST.Size = new Size(132, 30);

        Move(section, CHK_StoryMEvos, 16, 214, 260);
        Move(section, CHK_RandomMegaForm, 16, 244, 200);
        Move(section, CHK_ForceFullyEvolved, 16, 274, 180);
        Move(section, NUD_ForceFullyEvolved, 190, 272, 52);

        Move(section, CHK_TypeTheme, 270, 214, 190);
        Move(section, CHK_GymTrainers, 270, 244, 190);
        Move(section, CHK_GymE4Only, 270, 274, 190);
    }

    private void LayoutMovesSection(GroupBox section)
    {
        L_Moves.Text = "Source";
        Move(section, L_Moves, 16, 28);
        Move(section, CB_Moves, 100, 24, 160);
        Move(section, CHK_BetterMovesets, 16, 58);
        Move(section, B_SetTrainerMoveRules, 230, 52, 170, 32);

        section.Controls.Add(SmallLabel("Quality filters", 16, 96));
        Move(section, CHK_ForceHighPower, 16, 122);
        Move(section, NUD_ForceHighPower, 290, 120, 52);
        Move(section, CHK_NoFixedDamage, 16, 148);
        Move(section, CHK_Damage, 16, 174);
        Move(section, NUD_Damage, 290, 172, 52);
        Move(section, CHK_STAB, 16, 200);
        Move(section, NUD_STAB, 290, 198, 52);
    }

    private void LayoutItemsSection(GroupBox section)
    {
        Move(section, CHK_RandomItems, 16, 28);
        Move(section, CHK_SmartHeldItems, 16, 56);
        Move(section, CHK_ItemClause, 16, 84);
        Move(section, CB_SmartHeldItemMode, 164, 52, 150);

        section.Controls.Add(SmallLabel("Stats", 16, 118));
        Move(section, CHK_RandomAbilities, 16, 142);
        Move(section, CHK_MaxDiffPKM, 250, 142);
    }

    private void LayoutTrainerSection(GroupBox section)
    {
        section.Controls.Add(SmallLabel("Levels", 16, 28));
        Move(section, CHK_Level, 16, 52);
        Move(section, NUD_Level, 168, 50, 60);
        Move(section, CHK_LevelCaps, 232, 52);
        Move(section, B_SetLevelCaps, 340, 46, 110, 32);

        Move(section, CHK_RandomDoubleBattles, 16, 84);
        Move(section, NUD_DoubleBattleChance, 168, 82, 60);

        section.Controls.Add(SmallLabel("Team size", 16, 116));
        Move(section, L_MinPKM, 16, 138, 58);
        Move(section, NUD_RMin, 76, 138, 45);
        Move(section, L_MaxPKM, 132, 138, 58);
        Move(section, NUD_RMax, 192, 138, 45);
        Move(section, CHK_6PKM, 264, 138);

        Move(section, CHK_RandomClass, 16, 164);
        Move(section, CHK_IgnoreSpecialClass, 174, 164);
        Move(section, CHK_OnlySingles, 328, 164);

        Move(section, CHK_RandomGift, 16, 190);
        Move(section, NUD_GiftPercent, 206, 188, 58);
        Move(section, label1, 270, 191);
        Move(section, CHK_MaxDiffAI, 318, 190);
    }


    private void B_Close_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void B_Save_Click(object sender, EventArgs e)
    {
        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Randomize all? Cannot undo.", "Double check Randomization settings before continuing.") != DialogResult.Yes)
            return;

        RSTE.rPKM = CHK_RandomPKM.Checked;
        RSTE.rSmart = CHK_BST.Checked && !CHK_ProgressiveBST.Checked;
        RSTE.rProgressiveBST = CHK_ProgressiveBST.Checked;
        RSTE.rProgressiveBSTRules = ProgressiveBSTRules
            .Select(r => r.Clone())
            .ToList();
        RSTE.rLevel = CHK_Level.Checked;
        RSTE.rLevelMultiplier = NUD_Level.Value;
        RSTE.rUseLevelCaps = CHK_LevelCaps.Checked;
        RSTE.rLevelCapRules = LevelCapRules.Select(r => r.Clone()).ToList();
        RSTE.rLevelCapsApplyPrevious = ApplyCapsToPreviousTrainers;
        RSTE.rLevelCapPreviousGap = PreviousTrainerGap;
        RSTE.rLevelCapCurvePower = RegularTrainerCurvePower;
        RSTE.rLevelCapsGuaranteeMega = GuaranteeMegaInImportantBattles;
        RSTE.rTrainerMoveRules = MoveRules.Select(r => r.Clone()).ToList();
        RSTE.rRandomDoubleBattles = CHK_RandomDoubleBattles.Checked;
        RSTE.rRandomDoubleBattleChance = (int)NUD_DoubleBattleChance.Value;
        RSTE.rNoFixedDamage = CHK_NoFixedDamage.Checked;
        RSTE.rBetterMovesets = CHK_BetterMovesets?.Checked ?? false;

        RSTE.rMove = CB_Moves.SelectedIndex == 1;
        RSTE.rNoMove = CB_Moves.SelectedIndex == 2;
        RSTE.rMetronome = CB_Moves.SelectedIndex == 3;
        if (RSTE.rMove)
        {
            RSTE.rDMG = CHK_Damage.Checked;
            if (RSTE.rDMG)
                RSTE.rDMGCount = (int)NUD_Damage.Value;
            RSTE.rSTAB = CHK_STAB.Checked;
            if (RSTE.rSTAB)
                RSTE.rSTABCount = (int)NUD_STAB.Value;
        }
        RSTE.rItem = CHK_RandomItems.Checked;
        RSTE.rBanBadTrainerItems = CHK_RandomItems.Checked && CHK_SmartHeldItems != null && CHK_SmartHeldItems.Checked;
        RSTE.rItemClause = CHK_RandomItems.Checked && CHK_ItemClause != null && CHK_ItemClause.Checked;
        RSTE.rSmartTrainerItems = false; // v4.9.4 legacy global smart disabled; per-trainer Smart Items comes from Move Rules. // v4.9.2 force smart items in Gen6
        RSTE.rSmartTrainerItemMode = CB_SmartHeldItemMode == null || CB_SmartHeldItemMode.SelectedIndex < 0 ? 1 : CB_SmartHeldItemMode.SelectedIndex;
        RSTE.rAbility = CHK_RandomAbilities.Checked;
        RSTE.rDiffIV = CHK_MaxDiffPKM.Checked;

        RSTE.rClass = CHK_RandomClass.Checked;
        if (RSTE.rClass)
        {
            RSTE.rIgnoreClass = CHK_IgnoreSpecialClass.Checked
                ? Main.Config.ORAS
                    ? Legal.SpecialClasses_ORAS
                    : Legal.SpecialClasses_XY
                : [];
            RSTE.rOnlySingles = CHK_OnlySingles.Checked;
        }
        RSTE.rGift = CHK_RandomGift.Checked;
        RSTE.rGiftPercent = NUD_GiftPercent.Value;
        RSTE.rDiffAI = CHK_MaxDiffAI.Checked;
        RSTE.rTypeTheme = CHK_TypeTheme.Checked;
        RSTE.rTypeGymTrainers = CHK_GymTrainers.Checked;
        RSTE.rGymE4Only = CHK_GymE4Only.Checked;
        RSTE.rMinPKM = NUD_RMin.Value;
        RSTE.rMaxPKM = NUD_RMax.Value;
        RSTE.r6PKM = CHK_6PKM.Checked;
        RSTE.rRandomMegas = CHK_RandomMegaForm.Checked;
        RSTE.rForceFullyEvolved = CHK_ForceFullyEvolved.Checked;
        RSTE.rForceFullyEvolvedLevel = NUD_ForceFullyEvolved.Value;
        RSTE.rForceHighPower = CHK_ForceHighPower.Checked;
        RSTE.rForceHighPowerLevel = NUD_ForceHighPower.Value;

        if (CHK_StoryMEvos.Checked)
        {
            RSTE.rEnsureMEvo = Main.Config.ORAS
                ? [178, 235, 557, 583, 687, 698, 699, 700, 701, 713, 906, 907, 908, 909, 910, 911, 912, 913, 942, 944, 946,
                ]
                : [188, 263, 276, 277, 519, 520, 521, 526, 599, 600, 601];
        }
        else
        {
            RSTE.rEnsureMEvo = [];
        }

        RSTE.rThemedClasses = new bool[trClass.Length];
        RSTE.rSpeciesRand = new SpeciesRandomizer(Main.Config)
        {
            G1 = CHK_G1.Checked,
            G2 = CHK_G2.Checked,
            G3 = CHK_G3.Checked,
            G4 = CHK_G4.Checked,
            G5 = CHK_G5.Checked,
            G6 = CHK_G6.Checked,

            L = CHK_L.Checked,
            E = CHK_E.Checked,
            Shedinja = true,

            rBST = CHK_BST.Checked && !CHK_ProgressiveBST.Checked,
            rEXP = false,
        };
        RSTE.rSpeciesRand.Initialize();

        // add Legendary/Mythical to final evolutions if checked
        if (CHK_L.Checked) RSTE.rFinalEvo = [.. RSTE.rFinalEvo, .. Legendary];
        if (CHK_E.Checked) RSTE.rFinalEvo = [.. RSTE.rFinalEvo, .. Mythical];

        RSTE.rDoRand = true;
        RandSettings.SetFormSettings(this, Controls);
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        TrainerRandomizerTemplateFile.SetCurrent(CaptureTrainerTemplate(), CurrentTrainerTemplateGame);
        RandSettings.SetFormSettings(this, Controls);
        base.OnFormClosing(e);
    }

    private string CurrentTrainerTemplateGame => Main.Config.ORAS ? "ORAS" : "XY";

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
                BetterMovesets = CHK_BetterMovesets.Checked,
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
            CHK_BetterMovesets.Checked = moves.BetterMovesets;
            CHK_ForceHighPower.Checked = moves.ForceHighPower;
            SetNumericValue(NUD_ForceHighPower, moves.HighPowerLevel);
            CHK_NoFixedDamage.Checked = moves.NoFixedDamage;
            CHK_Damage.Checked = moves.EnsureDamagingMoves;
            SetNumericValue(NUD_Damage, moves.DamagingMoveCount);
            CHK_STAB.Checked = moves.EnsureSTABMoves;
            SetNumericValue(NUD_STAB, moves.STABMoveCount);
        }

        var result = TrainerRandomizerTemplateFile.ApplyTrainerRules(template, LevelCapRules, MoveRules);

        if (template.LevelCaps is not null)
        {
            ApplyCapsToPreviousTrainers = template.LevelCaps.ApplyToPreviousTrainers;
            PreviousTrainerGap = template.LevelCaps.PreviousTrainerGap;
            CHK_LevelCaps.Checked = template.LevelCaps.Enabled && LevelCapRules.Count > 0;
        }

        string warning = BuildTemplateWarning(result);
        string message = $"Template loaded. BST ranges: {ProgressiveBSTRules.Count}; level caps applied: {result.LevelCapsApplied}; trainer move rules applied: {result.MoveRulesApplied}.";
        if (warning.Length != 0)
            message += $"\n\n{warning}";
        if (showMessage)
            WinFormsUtil.Alert(message);
    }

    private static void SetNumericValue(NumericUpDown control, int value)
    {
        control.Value = Math.Min(control.Maximum, Math.Max(control.Minimum, value));
    }

    private static string BuildTemplateWarning(TrainerTemplateApplyResult result)
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

        TrainerMoveRulesDialog.Edit(this, ref MoveRules);
    }

    private void AddProgressiveBSTControls()
    {
        // Hacer la ventana más ancha para que quepa el botón manual.
        const int targetWidth = 560;

        MaximumSize = new System.Drawing.Size(700, 700);
        MinimumSize = new System.Drawing.Size(targetWidth, Height);

        ClientSize = new System.Drawing.Size(targetWidth, ClientSize.Height);

        // Ensanchar el grupo Options.
        GB_Tweak.Width = ClientSize.Width - GB_Tweak.Left - 16;

        // Mover OK y Cancel hacia la derecha.
        B_OK.Left = ClientSize.Width - B_OK.Width - 16;
        B_Cancel.Left = B_OK.Left;

        CHK_ProgressiveBST = new CheckBox
        {
            AutoSize = true,
            Location = new System.Drawing.Point(CHK_BST.Left, CHK_BST.Bottom + 6),
            Name = "CHK_ProgressiveBST",
            TabIndex = 999,
            Text = "Progressive BST",
            UseVisualStyleBackColor = true,
            Enabled = CHK_RandomPKM.Checked,
        };

        B_SetManualBST = new Button
        {
            Location = new System.Drawing.Point(CHK_ProgressiveBST.Right + 16, CHK_ProgressiveBST.Top - 3),
            Name = "B_SetManualBST",
            Size = new System.Drawing.Size(150, 23),
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

        GB_Tweak.Controls.Add(CHK_ProgressiveBST);
        GB_Tweak.Controls.Add(B_SetManualBST);

        CHK_ProgressiveBST.BringToFront();
        B_SetManualBST.BringToFront();

        CHK_LevelCaps = new CheckBox
        {
            AutoSize = true,
            Location = new System.Drawing.Point(220, CHK_Level.Top),
            Name = "CHK_LevelCaps",
            TabIndex = 1001,
            Text = "Level Caps",
            UseVisualStyleBackColor = true,
            Enabled = LevelCapRules.Count > 0,
        };

        B_SetLevelCaps = new Button
        {
            Location = new System.Drawing.Point(330, CHK_Level.Top - 3),
            Name = "B_SetLevelCaps",
            Size = new System.Drawing.Size(145, 23),
            TabIndex = 1002,
            Text = "Set level caps",
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
            Location = new System.Drawing.Point(B_SetLevelCaps.Right + 8, CHK_Level.Top - 3),
            Name = "B_SetTrainerMoveRules",
            Size = new System.Drawing.Size(145, 23),
            TabIndex = 1005,
            Text = "Move rules",
            UseVisualStyleBackColor = true,
            Enabled = MoveRules.Count > 0,
        };

        B_SetTrainerMoveRules.Click += (_, _) => ShowTrainerMoveRulesDialog();

        B_TrainerTemplate = new Button
        {
            Name = "B_TrainerTemplate",
            Size = new System.Drawing.Size(120, 30),
            TabIndex = 1006,
            Text = "Template...",
            UseVisualStyleBackColor = true,
        };
        B_TrainerTemplate.Click += (_, _) => ShowTrainerTemplateMenu();

        Controls.Add(CHK_LevelCaps);
        Controls.Add(B_SetLevelCaps);
        Controls.Add(B_SetTrainerMoveRules);
        Controls.Add(B_TrainerTemplate);
        CHK_LevelCaps.BringToFront();
        B_SetLevelCaps.BringToFront();
        B_SetTrainerMoveRules.BringToFront();
        B_TrainerTemplate.BringToFront();

        CHK_RandomDoubleBattles = new CheckBox
        {
            AutoSize = true,
            Location = new System.Drawing.Point(CHK_LevelCaps.Left, CHK_LevelCaps.Bottom + 8),
            Name = "CHK_RandomDoubleBattles",
            TabIndex = 1003,
            Text = "Random double battles",
            UseVisualStyleBackColor = true,
        };

        NUD_DoubleBattleChance = new NumericUpDown
        {
            Location = new System.Drawing.Point(B_SetLevelCaps.Left, CHK_RandomDoubleBattles.Top - 2),
            Name = "NUD_DoubleBattleChance",
            Size = new System.Drawing.Size(55, 20),
            TabIndex = 1004,
            Minimum = 0,
            Maximum = 100,
            Value = 15,
            Enabled = false,
        };

        var L_DoubleBattlePercent = new Label
        {
            AutoSize = true,
            Location = new System.Drawing.Point(NUD_DoubleBattleChance.Right + 5, NUD_DoubleBattleChance.Top + 3),
            Name = "L_DoubleBattlePercent",
            Text = "% double",
        };

        CHK_RandomDoubleBattles.CheckedChanged += (_, _) =>
        {
            NUD_DoubleBattleChance.Enabled = CHK_RandomDoubleBattles.Checked;
        };

        Controls.Add(CHK_RandomDoubleBattles);
        Controls.Add(NUD_DoubleBattleChance);
        Controls.Add(L_DoubleBattlePercent);
        CHK_RandomDoubleBattles.BringToFront();
        NUD_DoubleBattleChance.BringToFront();
        L_DoubleBattlePercent.BringToFront();

        CHK_BetterMovesets = new CheckBox
        {
            AutoSize = true,
            Location = new System.Drawing.Point(CB_Moves.Right + 12, CB_Moves.Top + 2),
            Name = "CHK_BetterMovesets",
            TabIndex = 1006,
            Text = "Better Movesets",
            UseVisualStyleBackColor = true,
            Checked = false,
        };

        Controls.Add(CHK_BetterMovesets);
        CHK_BetterMovesets.BringToFront();


        CHK_SmartHeldItems = new CheckBox
        {
            AutoSize = true,
            Location = new System.Drawing.Point(220, CHK_RandomItems.Top),
            Name = "CHK_BanBadItems",
            TabIndex = 1010,
            Text = "Ban Bad Items",
            UseVisualStyleBackColor = true,
            Enabled = CHK_RandomItems.Checked,
        };

        CHK_ItemClause = new CheckBox
        {
            AutoSize = true,
            Location = new System.Drawing.Point(CHK_SmartHeldItems.Left, CHK_SmartHeldItems.Bottom + 6),
            Name = "CHK_ItemClause",
            TabIndex = 1011,
            Text = "Item Clause",
            UseVisualStyleBackColor = true,
            Enabled = CHK_RandomItems.Checked,
        };

        CB_SmartHeldItemMode = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new System.Drawing.Point(320, CHK_RandomItems.Top - 2),
            Name = "CB_SmartHeldItemMode",
            Size = new System.Drawing.Size(105, 21),
            TabIndex = 1012,
            Enabled = CHK_RandomItems.Checked,
        };

        CB_SmartHeldItemMode.Items.AddRange(new object[] { "Normal", "Strong", "Competitive" });
        CB_SmartHeldItemMode.SelectedIndex = 1;

        CHK_RandomItems.CheckedChanged += (_, _) =>
        {
            CHK_SmartHeldItems.Enabled = CHK_RandomItems.Checked;
            CHK_ItemClause.Enabled = CHK_RandomItems.Checked;
            // v4.9.4 mode combo removed; smart item quality is controlled by Move Rules.
        };

        Controls.Add(CHK_SmartHeldItems);
        Controls.Add(CHK_ItemClause);
        // v4.9.4 mode combo removed; smart item quality is controlled by Move Rules.
        CHK_SmartHeldItems.BringToFront();
        CHK_ItemClause.BringToFront();
        // v4.9.4 mode combo removed; smart item quality is controlled by Move Rules.
        int requiredTopWidth = Math.Max(Math.Max(Math.Max(B_SetTrainerMoveRules.Right, L_DoubleBattlePercent.Right), CHK_BetterMovesets.Right), CB_SmartHeldItemMode.Right) + 16;
        if (ClientSize.Width < requiredTopWidth)
            ClientSize = new System.Drawing.Size(requiredTopWidth, ClientSize.Height);

        int requiredGroupWidth = B_SetManualBST.Right + 12;

        if (GB_Tweak.Width < requiredGroupWidth)
            GB_Tweak.Width = requiredGroupWidth;

        int requiredFormWidth = GB_Tweak.Right + 16;

        int requiredFormHeight = Math.Max(CHK_RandomDoubleBattles.Bottom + 44, CHK_ItemClause.Bottom + 48);
        if (ClientSize.Height < requiredFormHeight)
            ClientSize = new System.Drawing.Size(ClientSize.Width, requiredFormHeight);

        if (ClientSize.Width < requiredFormWidth)
            ClientSize = new System.Drawing.Size(requiredFormWidth, ClientSize.Height);

        // Reacomodar de nuevo por si la ventana creció más.
        B_OK.Left = ClientSize.Width - B_OK.Width - 16;
        B_Cancel.Left = B_OK.Left;
    }


    private void CHK_RandomPKM_CheckedChanged(object sender, EventArgs e)
    {
        GB_Tweak.Enabled =
            CHK_G1.Checked = CHK_G2.Checked = CHK_G3.Checked =
                CHK_G4.Checked = CHK_G5.Checked = CHK_G6.Checked =
                    CHK_L.Checked = CHK_E.Checked = CHK_StoryMEvos.Checked = CHK_ForceFullyEvolved.Checked =
                        CHK_RandomPKM.Checked;

        CHK_TypeTheme.Checked = CHK_GymTrainers.Checked = CHK_GymE4Only.Checked =
            CHK_BST.Checked = CHK_6PKM.Checked = CHK_RandomMegaForm.Checked = false;

        if (CHK_ProgressiveBST is not null)
        {
            CHK_ProgressiveBST.Enabled = CHK_RandomPKM.Checked;
            CHK_ProgressiveBST.Checked = false;
        }

        if (B_SetManualBST is not null)
            B_SetManualBST.Enabled = CHK_RandomPKM.Checked && CHK_ProgressiveBST.Checked;
    }

    private void CHK_Level_CheckedChanged(object sender, EventArgs e)
    {
        NUD_Level.Enabled = CHK_Level.Checked;
    }

    private void ChangeLevelPercent(object sender, EventArgs e)
    {
        CHK_Level.Checked = NUD_Level.Value != 0;
    }

    private void CHK_RandomGift_CheckedChanged(object sender, EventArgs e)
    {
        NUD_GiftPercent.Enabled = CHK_RandomGift.Checked;
        NUD_GiftPercent.Value = Convert.ToDecimal(CHK_RandomGift.Checked) * 15;
    }

    private void ChangeGiftPercent(object sender, EventArgs e)
    {
        CHK_RandomGift.Checked = NUD_GiftPercent.Value != 0;
    }

    private void CHK_TypeTheme_CheckedChanged(object sender, EventArgs e)
    {
        CHK_GymTrainers.Enabled = CHK_GymTrainers.Checked = CHK_GymE4Only.Enabled = CHK_TypeTheme.Checked;
        if (!CHK_TypeTheme.Checked)
            CHK_GymTrainers.Checked = CHK_GymE4Only.Checked = false;
    }

    private void CHK_RandomClass_CheckedChanged(object sender, EventArgs e)
    {
        CHK_IgnoreSpecialClass.Enabled = CHK_IgnoreSpecialClass.Checked =
            CHK_OnlySingles.Enabled = CHK_OnlySingles.Checked = CHK_RandomClass.Checked;
    }

    private void ChangeMoveRandomization(object sender, EventArgs e)
    {
        CHK_Damage.Checked = CHK_STAB.Checked =
            CHK_Damage.Enabled = CHK_STAB.Enabled =
                NUD_Damage.Enabled = NUD_STAB.Enabled = CB_Moves.SelectedIndex == 1;

        CHK_ForceHighPower.Enabled = CHK_ForceHighPower.Checked = NUD_ForceHighPower.Enabled =
            CHK_NoFixedDamage.Enabled = CHK_NoFixedDamage.Checked = CB_Moves.SelectedIndex is 1 or 2;
    }
    private void CHK_6PKM_CheckedChanged(object sender, EventArgs e)
    {
        //if (CB_Moves.SelectedIndex == 0)
        //    CHK_6PKM.Checked = false;
    }
}
