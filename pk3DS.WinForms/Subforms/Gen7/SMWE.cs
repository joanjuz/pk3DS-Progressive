using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using pk3DS.Core;
using pk3DS.Core.CTR;
using pk3DS.Core.Randomizers;

namespace pk3DS.WinForms;

public partial class SMWE : Form
{
    public SMWE(LazyGARCFile ed, LazyGARCFile zd, LazyGARCFile wd)
    {
        InitializeComponent();

        PB_DayIcon.Image = Properties.Resources.sun;
        PB_NightIcon.Image = Properties.Resources.moon;
        PB_DayIcon.SizeMode = PictureBoxSizeMode.CenterImage;
        PB_NightIcon.SizeMode = PictureBoxSizeMode.CenterImage;

        font = L_Location.Font;

        speciesList[0] = "(None)";
        var locationList = Main.Config.GetText(TextName.metlist_000000);
        locationList = GetGoodLocationList(locationList);

        nup_spec = LoadFormeNUD();
        cb_spec = LoadSpeciesComboBoxes();
        rate_spec = LoadRateNUD();

        encdata = ed;
        var areas = Area7.GetArray(ed, zd, wd, locationList);
        Areas = [.. areas.OrderBy(a => a.Zones[0].Name)];

        LoadData();
        AddProgressiveWildControls();
        AddAdvancedWildLevelScaler();
        RandSettings.GetFormSettings(this, GB_Tweak.Controls);

        if (WildRandomizerTemplateFile.TryGetCurrent(CurrentWildTemplateGame, out var currentWildTemplate))
        {
            ApplyWildTemplate(currentWildTemplate, showMessage: false);
        }
        else if (WildRandomizerTemplateFile.TryLoadLastState(CurrentWildTemplateGame, out var savedWildTemplate))
        {
            ApplyProgressiveWildRanges(savedWildTemplate.ProgressiveBST?.Ranges);
        }

        SyncProgressiveWildUI();

        var weather = string.Format("If weather is active, create a random number.{0}If 0, use slot 0.{0}If <= 10, use slot 1.{0}Else, pick an SOS table and a slot.", Environment.NewLine);
        new ToolTip().SetToolTip(L_AddSOS, weather);
        var sos = new[] { L_SOS1, L_SOS2, L_SOS3, L_SOS4, L_SOS5, L_SOS6, L_SOS7 };
        var rates = new[] { 1, 1, 1, 10, 10, 10, 67 };
        for (int i = 0; i < sos.Length; i++)
            new ToolTip().SetToolTip(sos[i], $"Table Selection Rate: {rates[i]}%");

        // ExportEncounters("um", "uu");
    }

    private NumericUpDown[] LoadRateNUD()
    {
        var list = new[] { NUP_Rate1, NUP_Rate2, NUP_Rate3, NUP_Rate4, NUP_Rate5, NUP_Rate6, NUP_Rate7, NUP_Rate8, NUP_Rate9, NUP_Rate10 };
        foreach (var nup in list)
            nup.ValueChanged += UpdateEncounterRate;
        return list;
    }

    private ComboBox[][] LoadSpeciesComboBoxes()
    {
        var list = new[] {
            [CB_Enc01, CB_Enc02, CB_Enc03, CB_Enc04, CB_Enc05, CB_Enc06, CB_Enc07, CB_Enc08, CB_Enc09, CB_Enc10],
            [CB_Enc11, CB_Enc12, CB_Enc13, CB_Enc14, CB_Enc15, CB_Enc16, CB_Enc17, CB_Enc18, CB_Enc19, CB_Enc20],
            [CB_Enc21, CB_Enc22, CB_Enc23, CB_Enc24, CB_Enc25, CB_Enc26, CB_Enc27, CB_Enc28, CB_Enc29, CB_Enc30],
            [CB_Enc31, CB_Enc32, CB_Enc33, CB_Enc34, CB_Enc35, CB_Enc36, CB_Enc37, CB_Enc38, CB_Enc39, CB_Enc40],
            [CB_Enc41, CB_Enc42, CB_Enc43, CB_Enc44, CB_Enc45, CB_Enc46, CB_Enc47, CB_Enc48, CB_Enc49, CB_Enc50],
            [CB_Enc51, CB_Enc52, CB_Enc53, CB_Enc54, CB_Enc55, CB_Enc56, CB_Enc57, CB_Enc58, CB_Enc59, CB_Enc60],
            [CB_Enc61, CB_Enc62, CB_Enc63, CB_Enc64, CB_Enc65, CB_Enc66, CB_Enc67, CB_Enc68, CB_Enc69, CB_Enc70],
            [CB_Enc71, CB_Enc72, CB_Enc73, CB_Enc74, CB_Enc75, CB_Enc76, CB_Enc77, CB_Enc78, CB_Enc79, CB_Enc80],
            new[] {CB_WeatherEnc1, CB_WeatherEnc2, CB_WeatherEnc3, CB_WeatherEnc4, CB_WeatherEnc5, CB_WeatherEnc6},
        };
        foreach (var cb_l in list)
        {
            foreach (var cb in cb_l)
            {
                cb.Items.AddRange(speciesList);
                cb.SelectedIndex = 0;
                cb.SelectedIndexChanged += UpdateSpeciesForm;
            }
        }

        return list;
    }

    private NumericUpDown[][] LoadFormeNUD()
    {
        var list = new[] {
            [NUP_Forme01, NUP_Forme02, NUP_Forme03, NUP_Forme04, NUP_Forme05, NUP_Forme06, NUP_Forme07, NUP_Forme08, NUP_Forme09, NUP_Forme10,
            ],
            [NUP_Forme11, NUP_Forme12, NUP_Forme13, NUP_Forme14, NUP_Forme15, NUP_Forme16, NUP_Forme17, NUP_Forme18, NUP_Forme19, NUP_Forme20,
            ],
            [NUP_Forme21, NUP_Forme22, NUP_Forme23, NUP_Forme24, NUP_Forme25, NUP_Forme26, NUP_Forme27, NUP_Forme28, NUP_Forme29, NUP_Forme30,
            ],
            [NUP_Forme31, NUP_Forme32, NUP_Forme33, NUP_Forme34, NUP_Forme35, NUP_Forme36, NUP_Forme37, NUP_Forme38, NUP_Forme39, NUP_Forme40,
            ],
            [NUP_Forme41, NUP_Forme42, NUP_Forme43, NUP_Forme44, NUP_Forme45, NUP_Forme46, NUP_Forme47, NUP_Forme48, NUP_Forme49, NUP_Forme50,
            ],
            [NUP_Forme51, NUP_Forme52, NUP_Forme53, NUP_Forme54, NUP_Forme55, NUP_Forme56, NUP_Forme57, NUP_Forme58, NUP_Forme59, NUP_Forme60,
            ],
            [NUP_Forme61, NUP_Forme62, NUP_Forme63, NUP_Forme64, NUP_Forme65, NUP_Forme66, NUP_Forme67, NUP_Forme68, NUP_Forme69, NUP_Forme70,
            ],
            [NUP_Forme71, NUP_Forme72, NUP_Forme73, NUP_Forme74, NUP_Forme75, NUP_Forme76, NUP_Forme77, NUP_Forme78, NUP_Forme79, NUP_Forme80,
            ],
            new [] { NUP_WeatherForme1, NUP_WeatherForme2, NUP_WeatherForme3, NUP_WeatherForme4, NUP_WeatherForme5, NUP_WeatherForme6 },
        };

        foreach (var nup_l in list)
        {
            foreach (var nup in nup_l)
                nup.ValueChanged += UpdateSpeciesForm;
        }

        return list;
    }

    private readonly Area7[] Areas;
    private readonly LazyGARCFile encdata;
    private readonly string[] speciesList = Main.Config.GetText(TextName.SpeciesNames);
    private readonly Font font;
    private readonly NumericUpDown[][] nup_spec;
    private readonly ComboBox[][] cb_spec;
    private readonly NumericUpDown[] rate_spec;

    private int TotalEncounterRate => rate_spec.Sum(nup => (int)nup.Value);
    private bool loadingdata;
    private EncounterTable CurrentTable;

    private void LoadData()
    {
        loadingdata = true;

        CB_LocationID.Items.Clear();
        CB_LocationID.Items.AddRange(Areas.Select(a => a.Name).ToArray());

        CB_SlotRand.SelectedIndex = 0;
        CB_LocationID.SelectedIndex = 0;

        loadingdata = false;
        ChangeMap(null, null);
    }

    private void ChangeMap(object sender, EventArgs e)
    {
        loadingdata = true;
        CB_TableID.Items.Clear();
        if (Areas[CB_LocationID.SelectedIndex].HasTables)
        {
            for (int i = 0; i < Areas[CB_LocationID.SelectedIndex].Tables.Count; i += 2)
            {
                CB_TableID.Items.Add($"{(i / 2) + 1} (Day)");
                CB_TableID.Items.Add($"{(i / 2) + 1} (Night)");
            }
        }
        else
        {
            CB_TableID.Items.Add("(None)");
        }

        CB_TableID.SelectedIndex = 0;
        loadingdata = false;
        UpdatePanel(sender, e);
    }

    private void UpdatePanel(object sender, EventArgs e)
    {
        if (loadingdata)
            return;
        loadingdata = true;

        var Map = Areas[CB_LocationID.SelectedIndex];
        GB_Encounters.Enabled = Map.HasTables;
        if (!Map.HasTables)
        {
            loadingdata = false;
            return;
        }
        CurrentTable = new EncounterTable(Map.Tables[CB_TableID.SelectedIndex].Data);
        LoadTable(CurrentTable);

        loadingdata = false;
        RefreshTableImages(Map);
    }

    private void RefreshTableImages(Area7 Map)
    {
        int base_id = CB_TableID.SelectedIndex / 2;
        base_id *= 2;
        PB_DayTable.Image = Map.Tables[base_id].GetTableImg(font);
        PB_NightTable.Image = Map.Tables[base_id + 1].GetTableImg(font);
    }

    private void LoadTable(EncounterTable table)
    {
        NUP_Min.Value = table.MinLevel;
        NUP_Max.Minimum = table.MinLevel;
        NUP_Max.Value = table.MaxLevel;
        for (int slot = 0; slot < table.Encounter7s.Length; slot++)
        {
            for (int i = 0; i < table.Encounter7s[slot].Length; i++)
            {
                var sl = table.Encounter7s[slot];
                if (slot == 8)
                    sl = table.AdditionalSOS;
                rate_spec[i].Value = table.Rates[i];
                cb_spec[slot][i].SelectedIndex = (int)sl[i].Species;
                nup_spec[slot][i].Value = (int)sl[i].Forme;
            }
        }
    }

    private void UpdateMinMax(object sender, EventArgs e)
    {
        if (loadingdata)
            return;
        loadingdata = true;
        int min = (int)NUP_Min.Value;
        int max = (int)NUP_Max.Value;
        if (max < min)
        {
            max = min;
            NUP_Max.Value = max;
            NUP_Max.Minimum = min;
        }
        CurrentTable.MinLevel = min;
        CurrentTable.MaxLevel = max;
        loadingdata = false;
    }

    private void UpdateSpeciesForm(object sender, EventArgs e)
    {
        if (loadingdata)
            return;

        var cur_pb = CB_TableID.SelectedIndex % 2 == 0 ? PB_DayTable : PB_NightTable;
        var cur_img = cur_pb.Image;

        object[][] source = sender is NumericUpDown ? nup_spec : cb_spec;
        int table = Array.FindIndex(source, t => t.Contains(sender));
        int slot = Array.IndexOf(source[table], sender);

        var cb_l = cb_spec[table];
        var nup_l = nup_spec[table];
        var species = (uint)cb_l[slot].SelectedIndex;
        var form = (uint)nup_l[slot].Value;
        if (table == 8)
        {
            CurrentTable.AdditionalSOS[slot].Species = species;
            CurrentTable.AdditionalSOS[slot].Forme = form;
        }
        CurrentTable.Encounter7s[table][slot].Species = species;
        CurrentTable.Encounter7s[table][slot].Forme = form;

        using (var g = Graphics.FromImage(cur_img))
        {
            int x = 40 * slot;
            int y = 30 * (table + 1);
            if (table == 8)
            {
                x = (40 * slot) + 60;
                y = 270;
            }
            var pnt = new Point(x, y);
            g.SetClip(new Rectangle(pnt.X, pnt.Y, 40, 30), CombineMode.Replace);
            g.Clear(Color.Transparent);

            var enc = CurrentTable.Encounter7s[table][slot];
            g.DrawImage(enc.Species == 0 ? Properties.Resources.empty : WinFormsUtil.GetSprite((int)enc.Species, (int)enc.Forme, 0, 0, Main.Config), pnt);
        }

        cur_pb.Image = cur_img;
    }

    private void UpdateEncounterRate(object sender, EventArgs e)
    {
        if (loadingdata)
            return;

        var cur_pb = CB_TableID.SelectedIndex % 2 == 0 ? PB_DayTable : PB_NightTable;
        var cur_img = cur_pb.Image;

        int slot = Array.IndexOf(rate_spec, sender);
        int rate = (int)((NumericUpDown)sender).Value;
        CurrentTable.Rates[slot] = rate;

        using (var g = Graphics.FromImage(cur_img))
        {
            var pnt = new PointF((40 * slot) + 10, 10);
            g.SetClip(new Rectangle((int)pnt.X, (int)pnt.Y, 40, 14), CombineMode.Replace);
            g.Clear(Color.Transparent);
            g.DrawString($"{rate}%", font, Brushes.Black, pnt);
        }

        cur_pb.Image = cur_img;

        var sum = TotalEncounterRate;
        GB_Encounters.Text = $"Encounters ({sum}%)";
    }

    private byte[] CopyTable;
    private int CopyCount;

    private void B_Copy_Click(object sender, EventArgs e)
    {
        var Map = Areas[CB_LocationID.SelectedIndex];
        if (!Map.HasTables)
        {
            WinFormsUtil.Alert("No tables to copy.");
            return;
        }
        CurrentTable.Write();
        CopyTable = (byte[])CurrentTable.Data.Clone();
        CopyCount = CurrentTable.Encounter7s[0].Count(z => z.Species != 0);
        B_Paste.Enabled = B_PasteAll.Enabled = true;
        WinFormsUtil.Alert("Copied table data.");
    }

    private void B_Paste_Click(object sender, EventArgs e)
    {
        var Map = Areas[CB_LocationID.SelectedIndex];
        if (!Map.HasTables)
        {
            WinFormsUtil.Alert("No table to paste to.");
            return;
        }
        CurrentTable.Reset(CopyTable);
        loadingdata = true;
        LoadTable(CurrentTable);
        var area = Areas[CB_LocationID.SelectedIndex];
        area.Tables[CB_TableID.SelectedIndex] = CurrentTable;
        loadingdata = false;
        RefreshTableImages(Map);
        System.Media.SystemSounds.Asterisk.Play();
    }

    private void B_PasteAll_Click(object sender, EventArgs e)
    {
        var Map = Areas[CB_LocationID.SelectedIndex];
        if (!Map.HasTables)
        {
            WinFormsUtil.Alert("No table to paste to.");
            return;
        }
        B_Paste_Click(sender, e);
        foreach (var t in Map.Tables.Where(t => CopyCount == t.Encounter7s[0].Count(z => z.Species != 0)))
            t.Reset(CopyTable);
    }

    private void B_Save_Click(object sender, EventArgs e)
    {
        var sum = TotalEncounterRate;
        if (sum != 100 && sum != 0)
        {
            WinFormsUtil.Error("Encounter rates must add up to either 0% or 100%.");
            return;
        }

        CurrentTable.Write();
        var area = Areas[CB_LocationID.SelectedIndex];
        area.Tables[CB_TableID.SelectedIndex] = CurrentTable;

        // Set data back to GARC
        encdata[area.FileNumber] = Area7.GetDayNightTableBinary(area.Tables);
    }

    private void B_Export_Click(object sender, EventArgs e)
    {
        B_Save_Click(sender, e);

        Directory.CreateDirectory("encdata");
        foreach (var Map in Areas)
        {
            var packed = Area7.GetDayNightTableBinary(Map.Tables);
            File.WriteAllBytes(Path.Combine("encdata", Map.FileNumber.ToString()), packed);
        }
        WinFormsUtil.Alert("Exported all tables!");
    }

    private void DumpTables(object sender, EventArgs e)
    {
        using var sfd = new SaveFileDialog { FileName = "EncounterTables.txt" };
        if (sfd.ShowDialog() != DialogResult.OK)
            return;
        var sb = new StringBuilder();
        foreach (var Map in Areas)
            sb.Append(Map.GetSummary(speciesList));
        File.WriteAllText(sfd.FileName, sb.ToString());
    }

    // Randomization & Bulk Modification
    private void B_Randomize_Click(object sender, EventArgs e)
    {
        bool progressive = CHK_ProgressiveWildBST?.Checked ?? false;

        if (progressive && !ValidateProgressiveWildBSTRules(ProgressiveWildBSTRules, showMessage: true))
            return;

        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Randomize all? Cannot undo.", "Double check Randomization settings at the bottom left."))
            return;

        Enabled = false;
        try
        {
            ExecuteRandomization();
        }
        finally
        {
            Enabled = true;
        }

        UpdatePanel(null, null);

        if (progressive)
        {
            RandomizationSessionState.RemoveAction("wild-encounters.randomize");
            RandomizationSessionState.MarkAction(
                "wild-encounters.progressive",
                ("progression", "area-max-valid-table"),
                ("rules", SerializeProgressiveWildBSTRules()));
        }
        else
        {
            RandomizationSessionState.RemoveAction("wild-encounters.progressive");
            RandomizationSessionState.MarkAction("wild-encounters.randomize");
        }

        WinFormsUtil.Alert(
            progressive
                ? "Progressively randomized all Wild Encounters by area!"
                : "Randomized all Wild Encounters according to specification!",
            "Press the Dump Tables button to view the new Wild Encounter information!");
    }

    private void ExecuteRandomization()
    {
        bool progressive = CHK_ProgressiveWildBST?.Checked ?? false;

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
            rBST = !progressive && CHK_BST.Checked,
        };
        rnd.Initialize();

        var form = new FormRandomizer(Main.Config)
        {
            AllowMega = CHK_MegaForm.Checked,
            AllowAlolanForm = true,
        };

        if (progressive)
        {
            var progressiveWild = new ProgressiveWildRandomizer
            {
                RandSpec = rnd,
                RandForm = form,
                Rules = ProgressiveWildBSTRules
                    .Select(rule => rule.Clone())
                    .ToList(),
                TableRandomizationOption = CB_SlotRand.SelectedIndex,
                LevelAmplifier = NUD_LevelAmp.Value,
                ModifyLevel = CHK_Level.Checked,
                USUM = Main.Config.USUM,
                AllCanCallAllies = CHK_AllCanCallAllies?.Checked ?? false,
            };

            progressiveWild.Execute(Areas, encdata);
            return;
        }

        var wild7 = new Wild7Randomizer
        {
            RandSpec = rnd,
            RandForm = form,
            TableRandomizationOption = CB_SlotRand.SelectedIndex,
            LevelAmplifier = NUD_LevelAmp.Value,
            ModifyLevel = CHK_Level.Checked,
            AllCanCallAllies = CHK_AllCanCallAllies?.Checked ?? false,
        };
        wild7.Execute(Areas, encdata);
    }

    private CheckBox? CHK_ProgressiveWildBST;
    private CheckBox? CHK_AllCanCallAllies;
    private Button? B_SetProgressiveWildBST;
    private Button? B_WildTemplate;

    private System.Collections.Generic.List<ProgressiveBSTRule> ProgressiveWildBSTRules =
        GetDefaultProgressiveWildBSTRules();

    private void AddProgressiveWildControls()
    {
        if (GB_Tweak.Controls.ContainsKey("CHK_ProgressiveWildBST"))
            return;

        int y = GB_Tweak.Controls
            .Cast<Control>()
            .Select(control => control.Bottom)
            .DefaultIfEmpty(10)
            .Max() + 8;

        CHK_ProgressiveWildBST = new CheckBox
        {
            Name = "CHK_ProgressiveWildBST",
            Text = "Progressive BST",
            AutoSize = true,
            Location = new System.Drawing.Point(8, y + 3),
        };

        B_SetProgressiveWildBST = new Button
        {
            Name = "B_SetProgressiveWildBST",
            Text = "Set BST ranges...",
            Size = new System.Drawing.Size(130, 24),
            Location = new System.Drawing.Point(145, y),
            Enabled = false,
        };

        B_WildTemplate = new Button
        {
            Name = "B_WildTemplate",
            Text = "Template...",
            Size = new System.Drawing.Size(130, 24),
            Location = new System.Drawing.Point(145, y + 28),
        };

        CHK_AllCanCallAllies = new CheckBox
        {
            Name = "CHK_AllCanCallAllies",
            Text = "All can call allies",
            AutoSize = true,
            Location = new System.Drawing.Point(8, y + 31),
        };

        CHK_ProgressiveWildBST.CheckedChanged += (_, _) =>
        {
            if (CHK_ProgressiveWildBST.Checked)
                CHK_BST.Checked = false;

            SyncProgressiveWildUI();
        };

        CHK_BST.CheckedChanged += (_, _) =>
        {
            if (CHK_BST.Checked && CHK_ProgressiveWildBST.Checked)
                CHK_ProgressiveWildBST.Checked = false;
        };

        B_SetProgressiveWildBST.Click += (_, _) =>
            ShowProgressiveWildBSTDialog();

        B_WildTemplate.Click += (_, _) =>
            ShowWildTemplateMenu();

        GB_Tweak.Controls.Add(CHK_ProgressiveWildBST);
        GB_Tweak.Controls.Add(B_SetProgressiveWildBST);
        GB_Tweak.Controls.Add(B_WildTemplate);
        GB_Tweak.Controls.Add(CHK_AllCanCallAllies);

        new ToolTip().SetToolTip(
            CHK_ProgressiveWildBST,
            "Uses one configurable BST tier for the whole encounter area. " +
            "Low-level fishing or special tables do not make a late-game area weak.");

        new ToolTip().SetToolTip(
            CHK_AllCanCallAllies,
            "Fills empty normal SOS slots for wild Pokemon that have a regular encounter slot. " +
            "Existing SOS allies and weather SOS slots are preserved.");

        int required = Math.Max(B_SetProgressiveWildBST.Bottom, B_WildTemplate.Bottom) + 10;
        if (GB_Tweak.Height < required)
        {
            int extra = required - GB_Tweak.Height;
            GB_Tweak.Height += extra;
            ClientSize = new System.Drawing.Size(
                ClientSize.Width,
                ClientSize.Height + extra);
        }
    }

    private void SyncProgressiveWildUI()
    {
        if (CHK_ProgressiveWildBST is null)
            return;

        if (CHK_ProgressiveWildBST.Checked)
            CHK_BST.Checked = false;

        if (B_SetProgressiveWildBST is not null)
            B_SetProgressiveWildBST.Enabled =
                CHK_ProgressiveWildBST.Checked;
    }

    private string CurrentWildTemplateGame => Main.Config.USUM ? "USUM" : "SM";

    private void ShowWildTemplateMenu()
    {
        if (B_WildTemplate is null)
            return;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Load template...", null, (_, _) => LoadWildTemplate());
        menu.Items.Add("Save current template...", null, (_, _) => SaveWildTemplate());
        menu.Show(B_WildTemplate, new System.Drawing.Point(0, B_WildTemplate.Height));
    }

    private void LoadWildTemplate()
    {
        try
        {
            Directory.CreateDirectory(WildRandomizerTemplateFile.TemplateDirectory);
            using var dialog = new OpenFileDialog
            {
                Title = "Load Wild Encounter template",
                Filter = "Wild Encounter template (*.json)|*.json|All files (*.*)|*.*",
                InitialDirectory = WildRandomizerTemplateFile.TemplateDirectory,
                CheckFileExists = true,
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            var template = WildRandomizerTemplateFile.Load(dialog.FileName, CurrentWildTemplateGame);
            WildRandomizerTemplateFile.SetCurrent(template, CurrentWildTemplateGame);
            WildRandomizerTemplateFile.SaveLastState(template, CurrentWildTemplateGame);
            ApplyWildTemplate(template, showMessage: false);
            WinFormsUtil.Alert("Wild Encounter template loaded successfully.");
        }
        catch (Exception ex)
        {
            WinFormsUtil.Alert($"Could not load Wild Encounter template.\n\n{ex.Message}");
        }
    }

    private void SaveWildTemplate()
    {
        try
        {
            Directory.CreateDirectory(WildRandomizerTemplateFile.TemplateDirectory);
            using var dialog = new SaveFileDialog
            {
                Title = "Save Wild Encounter template",
                Filter = "Wild Encounter template (*.json)|*.json",
                InitialDirectory = WildRandomizerTemplateFile.TemplateDirectory,
                FileName = $"wild_randomizer_{CurrentWildTemplateGame.ToLowerInvariant()}.json",
                AddExtension = true,
                DefaultExt = "json",
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            var template = CaptureWildTemplate();
            WildRandomizerTemplateFile.SetCurrent(template, CurrentWildTemplateGame);
            WildRandomizerTemplateFile.Save(dialog.FileName, template, CurrentWildTemplateGame);
            WildRandomizerTemplateFile.SaveLastState(template, CurrentWildTemplateGame);
            WinFormsUtil.Alert("Wild Encounter template saved successfully.");
        }
        catch (Exception ex)
        {
            WinFormsUtil.Alert($"Could not save Wild Encounter template.\n\n{ex.Message}");
        }
    }

    private WildRandomizerTemplate CaptureWildTemplate()
    {
        return new WildRandomizerTemplate
        {
            Name = $"{CurrentWildTemplateGame} wild encounter randomizer",
            Game = CurrentWildTemplateGame,
            G1 = CHK_G1.Checked,
            G2 = CHK_G2.Checked,
            G3 = CHK_G3.Checked,
            G4 = CHK_G4.Checked,
            G5 = CHK_G5.Checked,
            G6 = CHK_G6.Checked,
            G7 = CHK_G7.Checked,
            Legendaries = CHK_L.Checked,
            Events = CHK_E.Checked,
            MegaForms = CHK_MegaForm.Checked,
            SimilarBST = CHK_BST.Checked,
            AllCanCallAllies = CHK_AllCanCallAllies?.Checked ?? false,
            SlotRandomizationOption = CB_SlotRand.SelectedIndex,
            ModifyLevel = CHK_Level.Checked,
            LevelAmplifier = NUD_LevelAmp.Value,
            AdvancedLevelFlat = (int)(NUD_WildLevelFlat?.Value ?? 0),
            AdvancedLevelMultiplier = (int)(NUD_WildLevelMultiplier?.Value ?? 100),
            AdvancedKeepRange = CHK_WildLevelKeepRange?.Checked ?? true,
            ProgressiveBST = new WildProgressiveBSTTemplate
            {
                Enabled = CHK_ProgressiveWildBST?.Checked ?? false,
                Ranges = ProgressiveWildBSTRules
                    .OrderBy(rule => rule.MinLevel)
                    .Select(rule => new WildProgressiveBSTTemplateRule
                    {
                        MinLevel = rule.MinLevel,
                        MaxLevel = rule.MaxLevel,
                        MinBST = rule.MinBST,
                        MaxBST = rule.MaxBST,
                        FullRandom = rule.FullRandom,
                    })
                    .ToList(),
            },
        };
    }

    internal void ApplyWildTemplate(WildRandomizerTemplate template, bool showMessage = true)
    {
        WildRandomizerTemplateFile.Validate(template, CurrentWildTemplateGame);
        CHK_ProgressiveWildBST.Checked = false;

        CHK_G1.Checked = template.G1;
        CHK_G2.Checked = template.G2;
        CHK_G3.Checked = template.G3;
        CHK_G4.Checked = template.G4;
        CHK_G5.Checked = template.G5;
        CHK_G6.Checked = template.G6;
        CHK_G7.Checked = template.G7;
        CHK_L.Checked = template.Legendaries;
        CHK_E.Checked = template.Events;
        CHK_MegaForm.Checked = template.MegaForms;
        CHK_BST.Checked = template.SimilarBST;
        if (CHK_AllCanCallAllies is not null)
            CHK_AllCanCallAllies.Checked = template.AllCanCallAllies;

        if (template.SlotRandomizationOption >= 0 && template.SlotRandomizationOption < CB_SlotRand.Items.Count)
            CB_SlotRand.SelectedIndex = template.SlotRandomizationOption;

        CHK_Level.Checked = template.ModifyLevel;
        SetWildNumeric(NUD_LevelAmp, template.LevelAmplifier);

        if (NUD_WildLevelFlat is not null)
            SetWildNumeric(NUD_WildLevelFlat, template.AdvancedLevelFlat);
        if (NUD_WildLevelMultiplier is not null)
            SetWildNumeric(NUD_WildLevelMultiplier, template.AdvancedLevelMultiplier);
        if (CHK_WildLevelKeepRange is not null)
            CHK_WildLevelKeepRange.Checked = template.AdvancedKeepRange;

        ApplyProgressiveWildRanges(template.ProgressiveBST?.Ranges);
        CHK_ProgressiveWildBST.Checked = template.ProgressiveBST?.Enabled ?? false;
        SyncProgressiveWildUI();

        if (showMessage)
            WinFormsUtil.Alert("Wild Encounter settings applied.");
    }

    private void ApplyProgressiveWildRanges(System.Collections.Generic.IEnumerable<WildProgressiveBSTTemplateRule>? ranges)
    {
        if (ranges is null)
            return;

        var candidate = ranges
            .Select(rule => new ProgressiveBSTRule
            {
                MinLevel = rule.MinLevel,
                MaxLevel = rule.MaxLevel,
                MinBST = rule.MinBST,
                MaxBST = rule.MaxBST,
                FullRandom = rule.FullRandom,
            })
            .OrderBy(rule => rule.MinLevel)
            .ToList();

        if (candidate.Count == 0)
            return;
        if (!ValidateProgressiveWildBSTRules(candidate, showMessage: false))
            throw new InvalidDataException("Invalid Progressive Wild BST ranges.");

        ProgressiveWildBSTRules = candidate;
    }

    internal void ApplyProgressiveWildRules(string serialized)
    {
        if (string.IsNullOrWhiteSpace(serialized))
            throw new InvalidDataException("Progressive Wild action has no saved BST ranges.");

        var rules = new System.Collections.Generic.List<ProgressiveBSTRule>();
        foreach (string token in serialized.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] fields = token.Split(',');
            if (fields.Length != 5 ||
                !int.TryParse(fields[0], out int minLevel) ||
                !int.TryParse(fields[1], out int maxLevel) ||
                !int.TryParse(fields[2], out int minBST) ||
                !int.TryParse(fields[3], out int maxBST) ||
                !bool.TryParse(fields[4], out bool fullRandom))
            {
                throw new InvalidDataException($"Invalid Progressive Wild rule '{token}'.");
            }

            rules.Add(new ProgressiveBSTRule
            {
                MinLevel = minLevel,
                MaxLevel = maxLevel,
                MinBST = minBST,
                MaxBST = maxBST,
                FullRandom = fullRandom,
            });
        }

        rules = rules.OrderBy(rule => rule.MinLevel).ToList();
        if (!ValidateProgressiveWildBSTRules(rules, showMessage: false))
            throw new InvalidDataException("Invalid Progressive Wild BST ranges.");

        ProgressiveWildBSTRules = rules;
    }

    private static void SetWildNumeric(NumericUpDown control, decimal value)
    {
        control.Value = Math.Max(control.Minimum, Math.Min(control.Maximum, value));
    }

    private void ShowProgressiveWildBSTDialog()
    {
        using var form = new Form
        {
            Text = "Progressive Wild BST Settings",
            StartPosition = FormStartPosition.CenterParent,
            Size = new System.Drawing.Size(570, 370),
            MinimizeBox = false,
            MaximizeBox = false,
            FormBorderStyle = FormBorderStyle.FixedDialog,
        };

        var editableRules =
            new System.ComponentModel.BindingList<ProgressiveBSTRule>(
                ProgressiveWildBSTRules
                    .Select(rule => rule.Clone())
                    .OrderBy(rule => rule.MinLevel)
                    .ToList());

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
            Width = 85,
        });

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ProgressiveBSTRule.MaxLevel),
            HeaderText = "Max Level",
            Width = 85,
        });

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ProgressiveBSTRule.MinBST),
            HeaderText = "Min BST",
            Width = 85,
        });

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ProgressiveBSTRule.MaxBST),
            HeaderText = "Max BST",
            Width = 85,
        });

        grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(ProgressiveBSTRule.FullRandom),
            HeaderText = "Full Random",
            Width = 100,
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
            Width = 110,
        };

        reset.Click += (_, _) =>
        {
            editableRules.Clear();

            foreach (var rule in GetDefaultProgressiveWildBSTRules())
                editableRules.Add(rule);
        };

        ok.Click += (_, _) =>
        {
            grid.EndEdit();

            var candidate = editableRules
                .Select(rule => rule.Clone())
                .OrderBy(rule => rule.MinLevel)
                .ToList();

            if (!ValidateProgressiveWildBSTRules(
                    candidate,
                    showMessage: true))
            {
                return;
            }

            ProgressiveWildBSTRules = candidate;

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

    private static bool ValidateProgressiveWildBSTRules(
        System.Collections.Generic.List<ProgressiveBSTRule> rules,
        bool showMessage)
    {
        bool Fail(string message)
        {
            if (showMessage)
                WinFormsUtil.Alert(message);

            return false;
        }

        if (rules.Count == 0)
            return Fail("You must define at least one Progressive Wild BST range.");

        var sorted = rules
            .OrderBy(rule => rule.MinLevel)
            .ToList();

        foreach (var rule in sorted)
        {
            if (rule.MinLevel < 1 ||
                rule.MaxLevel > 100 ||
                rule.MinLevel > rule.MaxLevel)
            {
                return Fail(
                    "Levels must be between 1 and 100, " +
                    "and Min Level may not exceed Max Level.");
            }

            if (!rule.FullRandom &&
                (rule.MinBST < 1 ||
                 rule.MaxBST > 999 ||
                 rule.MinBST > rule.MaxBST))
            {
                return Fail(
                    "BST must be between 1 and 999, " +
                    "and Min BST may not exceed Max BST.");
            }
        }

        if (sorted[0].MinLevel != 1)
            return Fail("Progressive Wild ranges must start at level 1.");

        if (sorted[^1].MaxLevel != 100)
            return Fail("Progressive Wild ranges must finish at level 100.");

        for (int i = 1; i < sorted.Count; i++)
        {
            if (sorted[i].MinLevel != sorted[i - 1].MaxLevel + 1)
            {
                return Fail(
                    "Progressive Wild level ranges must cover levels 1-100 " +
                    "without gaps or overlaps.");
            }
        }

        return true;
    }

    private static System.Collections.Generic.List<ProgressiveBSTRule>
        GetDefaultProgressiveWildBSTRules()
    {
        return
        [
            new ProgressiveBSTRule
            {
                MinLevel = 1,
                MaxLevel = 10,
                MinBST = 180,
                MaxBST = 320,
            },
            new ProgressiveBSTRule
            {
                MinLevel = 11,
                MaxLevel = 20,
                MinBST = 220,
                MaxBST = 380,
            },
            new ProgressiveBSTRule
            {
                MinLevel = 21,
                MaxLevel = 30,
                MinBST = 280,
                MaxBST = 450,
            },
            new ProgressiveBSTRule
            {
                MinLevel = 31,
                MaxLevel = 40,
                MinBST = 340,
                MaxBST = 520,
            },
            new ProgressiveBSTRule
            {
                MinLevel = 41,
                MaxLevel = 50,
                MinBST = 400,
                MaxBST = 580,
            },
            new ProgressiveBSTRule
            {
                MinLevel = 51,
                MaxLevel = 100,
                MinBST = 480,
                MaxBST = 680,
            },
        ];
    }

    private string SerializeProgressiveWildBSTRules()
    {
        return string.Join(
            ";",
            ProgressiveWildBSTRules
                .OrderBy(rule => rule.MinLevel)
                .Select(rule =>
                    $"{rule.MinLevel}," +
                    $"{rule.MaxLevel}," +
                    $"{rule.MinBST}," +
                    $"{rule.MaxBST}," +
                    $"{rule.FullRandom}"));
    }

    private Button? B_AdvancedWildLevels;
    private NumericUpDown? NUD_WildLevelFlat;
    private NumericUpDown? NUD_WildLevelMultiplier;
    private CheckBox? CHK_WildLevelKeepRange;

    private void AddAdvancedWildLevelScaler()
    {
        if (GB_Tweak.Controls.ContainsKey("B_AdvancedWildLevels"))
            return;

        int y = GB_Tweak.Controls.Cast<Control>().Select(z => z.Bottom).DefaultIfEmpty(10).Max() + 8;

        var title = new Label
        {
            Name = "L_AdvancedWildLevels",
            Text = "Wild level scaling",
            AutoSize = true,
            Location = new System.Drawing.Point(8, y + 4),
        };

        NUD_WildLevelFlat = new NumericUpDown
        {
            Name = "NUD_WildLevelFlat",
            Minimum = -100,
            Maximum = 100,
            Value = 0,
            Width = 52,
            Location = new System.Drawing.Point(120, y),
        };

        var flatLabel = new Label
        {
            Text = "+ flat",
            AutoSize = true,
            Location = new System.Drawing.Point(176, y + 4),
        };

        NUD_WildLevelMultiplier = new NumericUpDown
        {
            Name = "NUD_WildLevelMultiplier",
            Minimum = 1,
            Maximum = 500,
            Value = 100,
            Width = 58,
            Location = new System.Drawing.Point(230, y),
        };

        var multLabel = new Label
        {
            Text = "%",
            AutoSize = true,
            Location = new System.Drawing.Point(292, y + 4),
        };

        CHK_WildLevelKeepRange = new CheckBox
        {
            Name = "CHK_WildLevelKeepRange",
            Text = "Keep min/max range",
            Checked = true,
            AutoSize = true,
            Location = new System.Drawing.Point(320, y + 2),
        };

        B_AdvancedWildLevels = new Button
        {
            Name = "B_AdvancedWildLevels",
            Text = "Apply Wild Levels",
            Size = new System.Drawing.Size(128, 24),
            Location = new System.Drawing.Point(470, y - 1),
        };
        B_AdvancedWildLevels.Click += ApplyAdvancedWildLevelScaling;

        GB_Tweak.Controls.Add(title);
        GB_Tweak.Controls.Add(NUD_WildLevelFlat);
        GB_Tweak.Controls.Add(flatLabel);
        GB_Tweak.Controls.Add(NUD_WildLevelMultiplier);
        GB_Tweak.Controls.Add(multLabel);
        GB_Tweak.Controls.Add(CHK_WildLevelKeepRange);
        GB_Tweak.Controls.Add(B_AdvancedWildLevels);

        int required = B_AdvancedWildLevels.Bottom + 10;
        if (GB_Tweak.Height < required)
        {
            int extra = required - GB_Tweak.Height;
            GB_Tweak.Height += extra;
            ClientSize = new System.Drawing.Size(ClientSize.Width, ClientSize.Height + extra);
        }
    }

    private void ApplyAdvancedWildLevelScaling(object? sender, EventArgs e)
    {
        if (DialogResult.Yes != WinFormsUtil.Prompt(
            MessageBoxButtons.YesNo,
            "Apply wild level scaling?",
            "This will modify every current wild encounter level range. Cannot undo."))
        {
            return;
        }

        Enabled = false;
        int changed = 0;
        bool keepRange = CHK_WildLevelKeepRange?.Checked ?? true;

        foreach (var area in Areas)
        {
            var tables = area.Tables;
            foreach (var table in tables)
            {
                int oldMin = table.MinLevel;
                int oldMax = table.MaxLevel;

                if (oldMin <= 0 && oldMax <= 0)
                    continue;

                int newMin;
                int newMax;

                if (keepRange)
                {
                    newMin = ScaleAdvancedWildLevel(oldMin > 0 ? oldMin : oldMax);
                    newMax = ScaleAdvancedWildLevel(oldMax > 0 ? oldMax : oldMin);
                }
                else
                {
                    int newLevel = ScaleAdvancedWildLevel(Math.Max(oldMin, oldMax));
                    newMin = newMax = newLevel;
                }

                if (newMax < newMin)
                    (newMin, newMax) = (newMax, newMin);

                if (table.MinLevel == newMin && table.MaxLevel == newMax)
                    continue;

                table.MinLevel = newMin;
                table.MaxLevel = newMax;
                table.Write();
                changed++;
            }

            encdata[area.FileNumber] = Area7.GetDayNightTableBinary(tables);
        }

        Enabled = true;
        UpdatePanel(sender, e);
        RandomizationSessionState.MarkAction(
            "wild-encounters.scale-levels",
            ("flat", (NUD_WildLevelFlat?.Value ?? 0).ToString()),
            ("multiplier", (NUD_WildLevelMultiplier?.Value ?? 100).ToString()),
            ("keepRange", (CHK_WildLevelKeepRange?.Checked ?? true).ToString()));
        WinFormsUtil.Alert("Wild levels scaled!", $"Updated {changed} encounter slots.");
    }

    private int ScaleAdvancedWildLevel(int level)
    {
        int flat = (int)(NUD_WildLevelFlat?.Value ?? 0);
        decimal multiplier = (NUD_WildLevelMultiplier?.Value ?? 100) / 100m;
        int scaled = (int)Math.Round((level * multiplier) + flat, MidpointRounding.AwayFromZero);
        return Math.Max(1, Math.Min(100, scaled));
    }

    private void CopySOS_Click(object sender, EventArgs e)
    {
        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Copy initial species to SOS slots?", "Cannot undo.") != DialogResult.Yes)
            return;

        // first table is copied to all other tables except weather (last)
        for (int i = 1; i < nup_spec.Length - 1; i++)
        {
            for (int s = 0; s < nup_spec[i].Length; s++) // slot copy
            {
                nup_spec[i][s].Value = nup_spec[0][s].Value;
                cb_spec[i][s].SelectedIndex = cb_spec[0][s].SelectedIndex;
            }
        }
        RandomizationSessionState.MarkAction("wild-encounters.copy-sos");
        WinFormsUtil.Alert("All initial species copied to SOS slots!");
    }

    private void ModifyAllLevelRanges(object sender, EventArgs e)
    {
        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Modify all current Level ranges?", "Cannot undo."))
            return;

        // Disable Interface while modifying
        Enabled = false;

        // Cycle through each location to modify levels
        var amp = NUD_LevelAmp.Value;
        foreach (var area in Areas)
        {
            var tables = area.Tables;
            foreach (var table in tables)
            {
                table.MinLevel = Randomizer.GetModifiedLevel(table.MinLevel, amp);
                table.MaxLevel = Randomizer.GetModifiedLevel(table.MaxLevel, amp);
                table.Write();
            }
            encdata[area.FileNumber] = Area7.GetDayNightTableBinary(tables);
        }

        // Enable Interface... modification complete.
        Enabled = true;
        RandomizationSessionState.MarkAction("wild-encounters.modify-levels");
        WinFormsUtil.Alert("Modified all Level ranges according to specification!", "Press the Dump Tables button to view the new Level ranges!");

        UpdatePanel(sender, e);
    }

    // Utility
    /// <summary>
    /// Moves the sub-location names into the location name string entry.
    /// </summary>
    /// <param name="list">Raw location list</param>
    /// <returns>Cleaned location list</returns>
    public static string[] GetGoodLocationList(string[] list)
    {
        var bad = list;
        var good = (string[])bad.Clone();
        for (int i = 0; i < bad.Length; i += 2)
        {
            var nextLoc = bad[i + 1];
            if (!string.IsNullOrWhiteSpace(nextLoc) && nextLoc[0] != '[')
                good[i] += $" ({nextLoc})";
            if (i > 0 && !string.IsNullOrWhiteSpace(good[i]) && good.Take(i - 1).Contains(good[i]))
                good[i] += $" ({good.Take(i - 1).Count(s => s == good[i]) + 1})";
        }
        return good;
    }

    public void ExportEncounters(string gameID, string ident, bool sm)
    {
        var reg = Gen7SlotDumper.GetRegularBinary(Areas, sm);
        var sos = Gen7SlotDumper.GetSOSBinary(Areas, Main.Config.Personal, sm);

        File.WriteAllBytes($"encounter_{gameID}.pkl", Mini.PackMini(reg, ident));
        File.WriteAllBytes($"encounter_{gameID}_sos.pkl", Mini.PackMini(sos, ident));
    }

    private void SMWE_FormClosing(object sender, FormClosingEventArgs e)
    {
        try
        {
            var template = CaptureWildTemplate();
            WildRandomizerTemplateFile.SetCurrent(template, CurrentWildTemplateGame);
            WildRandomizerTemplateFile.SaveLastState(template, CurrentWildTemplateGame);
        }
        catch
        {
            // Closing the editor should not be blocked by a persistence failure.
        }

        RandSettings.SetFormSettings(this, GB_Tweak.Controls);
    }
}

public static class Extensions
{
    public static Bitmap GetTableImg(this EncounterTable table, Font font)
    {
        var img = new Bitmap(10 * 40, 10 * 30);
        using var g = Graphics.FromImage(img);
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        for (int i = 0; i < table.Rates.Length; i++)
            g.DrawString($"{table.Rates[i]}%", font, Brushes.Black, new PointF((40 * i) + 10, 10));
        g.DrawString("Weather: ", font, Brushes.Black, new PointF(10, 280));

        // Draw Sprites
        for (int i = 0; i < table.Encounter7s.Length - 1; i++)
        {
            for (int j = 0; j < table.Encounter7s[i].Length; j++)
            {
                var slot = table.Encounter7s[i][j];
                var sprite = GetSprite((int)slot.Species, (int)slot.Forme);
                g.DrawImage(sprite, new Point(40 * j, 30 * (i + 1)));
            }
        }

        for (int i = 0; i < table.AdditionalSOS.Length; i++)
        {
            var slot = table.AdditionalSOS[i];
            var sprite = GetSprite((int)slot.Species, (int)slot.Forme);
            g.DrawImage(sprite, new Point((40 * i) + 60, 270));
        }

        static Bitmap GetSprite(int species, int form)
        {
            return species == 0
                ? Properties.Resources.empty
                : WinFormsUtil.GetSprite(species, form, 0, 0, Main.Config);
        }

        return img;
    }
}
