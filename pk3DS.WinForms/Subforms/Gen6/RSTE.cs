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

public partial class RSTE : Form
{
    private static CheckBox CHK_SmartHeldItems;
    private static ComboBox CB_SmartHeldItemMode;
    private readonly LearnsetRandomizer learn = new(Main.Config, Main.Config.Learnsets);

    public RSTE(byte[][] trd, byte[][] trp)
    {
        //trclass = trc;
        trdata = trd;
        trpoke = trp;
        Array.Resize(ref specieslist, Main.Config.MaxSpeciesID + 1);
        MegaDictionary = GiftEditor6.GetMegaDictionary(Main.Config);
        rModelRestricted = Main.Config.ORAS ? Legal.Model_AO : Legal.Model_XY;
        rFinalEvo = Legal.FinalEvolutions_6;

        InitializeComponent();
        TrainerHeldItemTemplate.EnsureDefaultFile();
        TrainerBetterMovesetTemplate.EnsureDefaultFile();
        // String Fetching
        #region Combo Box Arrays
        trpk_pkm = [CB_Pokemon_1_Pokemon, CB_Pokemon_2_Pokemon, CB_Pokemon_3_Pokemon, CB_Pokemon_4_Pokemon, CB_Pokemon_5_Pokemon, CB_Pokemon_6_Pokemon,
        ];
        trpk_lvl = [CB_Pokemon_1_Level,   CB_Pokemon_2_Level,   CB_Pokemon_3_Level,   CB_Pokemon_4_Level,   CB_Pokemon_5_Level,   CB_Pokemon_6_Level,
        ];
        trpk_item = [CB_Pokemon_1_Item,    CB_Pokemon_2_Item,    CB_Pokemon_3_Item,    CB_Pokemon_4_Item,    CB_Pokemon_5_Item,    CB_Pokemon_6_Item,
        ];
        trpk_abil = [CB_Pokemon_1_Ability, CB_Pokemon_2_Ability, CB_Pokemon_3_Ability, CB_Pokemon_4_Ability, CB_Pokemon_5_Ability, CB_Pokemon_6_Ability,
        ];
        trpk_m1 = [CB_Pokemon_1_Move_1,  CB_Pokemon_2_Move_1,  CB_Pokemon_3_Move_1,  CB_Pokemon_4_Move_1,  CB_Pokemon_5_Move_1,  CB_Pokemon_6_Move_1,
        ];
        trpk_m2 = [CB_Pokemon_1_Move_2,  CB_Pokemon_2_Move_2,  CB_Pokemon_3_Move_2,  CB_Pokemon_4_Move_2,  CB_Pokemon_5_Move_2,  CB_Pokemon_6_Move_2,
        ];
        trpk_m3 = [CB_Pokemon_1_Move_3,  CB_Pokemon_2_Move_3,  CB_Pokemon_3_Move_3,  CB_Pokemon_4_Move_3,  CB_Pokemon_5_Move_3,  CB_Pokemon_6_Move_3,
        ];
        trpk_m4 = [CB_Pokemon_1_Move_4,  CB_Pokemon_2_Move_4,  CB_Pokemon_3_Move_4,  CB_Pokemon_4_Move_4,  CB_Pokemon_5_Move_4,  CB_Pokemon_6_Move_4,
        ];
        trpk_IV = [CB_Pokemon_1_IVs,     CB_Pokemon_2_IVs,     CB_Pokemon_3_IVs,     CB_Pokemon_4_IVs,     CB_Pokemon_5_IVs,     CB_Pokemon_6_IVs,
        ];
        trpk_form = [CB_Pokemon_1_Form,    CB_Pokemon_2_Form,    CB_Pokemon_3_Form,    CB_Pokemon_4_Form,    CB_Pokemon_5_Form,    CB_Pokemon_6_Form,
        ];
        trpk_gender = [CB_Pokemon_1_Gender,  CB_Pokemon_2_Gender,  CB_Pokemon_3_Gender,  CB_Pokemon_4_Gender,  CB_Pokemon_5_Gender,  CB_Pokemon_6_Gender,
        ];
        #endregion
        string[] species = Main.Config.GetText(TextName.SpeciesNames);
        AltForms = Main.Config.Personal.GetFormList(species, Main.Config.MaxSpeciesID);
        Setup();
    }

    private readonly string[][] AltForms;
    internal static uint Rand() => Util.Random32();
    private bool start = true;
    private bool loading = true;
    private int index = -1;

    private void AddSmartHeldItemControls()
    {
        try
        {
            if (CHK_SmartHeldItems is null)
            {
                CHK_SmartHeldItems = new CheckBox
                {
                    Name = "CHK_SmartHeldItems",
                    Text = "Smart Items",
                    AutoSize = true,
                    Checked = false,
                };
            }

            if (CB_SmartHeldItemMode is null)
            {
                CB_SmartHeldItemMode = new ComboBox
                {
                    Name = "CB_SmartHeldItemMode",
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Width = 95,
                };

                CB_SmartHeldItemMode.Items.AddRange(new object[] { "Normal", "Strong", "Competitive" });
                CB_SmartHeldItemMode.SelectedIndex = 1;
            }

            var randomItems = Controls.Find("CHK_RandomItems", true).FirstOrDefault() as CheckBox;
            Control parent = randomItems?.Parent ?? this;

            if (!parent.Controls.Contains(CHK_SmartHeldItems))
                parent.Controls.Add(CHK_SmartHeldItems);

            if (!parent.Controls.Contains(CB_SmartHeldItemMode))
                parent.Controls.Add(CB_SmartHeldItemMode);

            int x = randomItems is not null ? randomItems.Left : 6;
            int y = randomItems is not null ? randomItems.Bottom + 5 : 120;

            CHK_SmartHeldItems.Location = new Point(x, y);
            CB_SmartHeldItemMode.Location = new Point(x + 105, y - 2);

            CHK_SmartHeldItems.BringToFront();
            CB_SmartHeldItemMode.BringToFront();
        }
        catch
        {
            // UI-only helper. Do not block the editor if a different build moves controls around.
        }
    }
    #region Global Variables
    private readonly ComboBox[] trpk_pkm;
    private readonly ComboBox[] trpk_lvl;
    private readonly ComboBox[] trpk_item;
    private readonly ComboBox[] trpk_abil;
    private readonly ComboBox[] trpk_m1, trpk_m2, trpk_m3, trpk_m4;
    private readonly ComboBox[] trpk_IV;
    private readonly ComboBox[] trpk_form;
    private readonly ComboBox[] trpk_gender;

    private PictureBox[] pba;

    // Top Level Functions
    //private readonly byte[][] trclass;
    private readonly byte[][] trdata;
    private readonly byte[][] trpoke;
    private readonly string[] abilitylist = Main.Config.GetText(TextName.AbilityNames);
    private readonly string[] movelist = Main.Config.GetText(TextName.MoveNames);
    private readonly string[] itemlist = Main.Config.GetText(TextName.ItemNames);
    private readonly string[] specieslist = Main.Config.GetText(TextName.SpeciesNames);
    private readonly string[] types = Main.Config.GetText(TextName.Types);
    //private readonly string[] forms = Main.Config.GetText(TextName.Forms);
    private string[] trName = Main.Config.GetText(TextName.TrainerNames);
    private readonly string[] trClass = Main.Config.GetText(TextName.TrainerClasses);
    //private readonly string[] trText = Main.Config.GetText(TextName.TrainerText);
    #endregion

    // Ability Loading
    private void RefreshFormAbility(object sender, EventArgs e)
    {
        int i = Array.IndexOf(trpk_form, sender as ComboBox);
        RefreshPKMSlotAbility(i);
    }

    private void RefreshSpeciesAbility(object sender, EventArgs e)
    {
        int i = Array.IndexOf(trpk_pkm, sender as ComboBox);
        FormUtil.SetForms(trpk_pkm[i].SelectedIndex, trpk_form[i], AltForms);
        RefreshPKMSlotAbility(i);
    }

    private void RefreshPKMSlotAbility(int slot)
    {
        int previousAbility = trpk_abil[slot].SelectedIndex;

        int species = trpk_pkm[slot].SelectedIndex;
        int formnum = trpk_form[slot].SelectedIndex;

        int entry = Main.Config.Personal.GetFormIndex(species, formnum);

        trpk_abil[slot].Items.Clear();
        trpk_abil[slot].Items.Add("Any (1 or 2)");
        trpk_abil[slot].Items.Add(abilitylist[Main.SpeciesStat[entry].Abilities[0]] + " (1)");
        trpk_abil[slot].Items.Add(abilitylist[Main.SpeciesStat[entry].Abilities[1]] + " (2)");
        trpk_abil[slot].Items.Add(abilitylist[Main.SpeciesStat[entry].Abilities[2]] + " (H)");

        trpk_abil[slot].SelectedIndex = previousAbility;

        ShowTeams(slot);
    }
    // Set Loading
    private void ChangeTrainerType(object sender, EventArgs e)
    {
        if (start || loading) return;
        int pkm = CB_numPokemon.SelectedIndex;
        {
            for (int i = 0; i < 6; i++) // enable all if the pkm exists
            {
                trpk_pkm[i].Enabled =
                    trpk_gender[i].Enabled =
                        trpk_abil[i].Enabled =
                            trpk_IV[i].Enabled =
                                trpk_lvl[i].Enabled = i < pkm;

                trpk_item[i].Enabled = i < pkm && checkBox_Item.Checked;

                trpk_m1[i].Enabled =
                    trpk_m2[i].Enabled =
                        trpk_m3[i].Enabled =
                            trpk_m4[i].Enabled = i < pkm && checkBox_Moves.Checked;

                if (!trpk_pkm[i].Enabled)
                {
                    trpk_pkm[i].SelectedIndex =
                        trpk_gender[i].SelectedIndex =
                            trpk_form[i].SelectedIndex =
                                trpk_abil[i].SelectedIndex =
                                    trpk_IV[i].SelectedIndex =
                                        trpk_lvl[i].SelectedIndex = 0;
                }
                if (!trpk_item[i].Enabled)
                {
                    trpk_item[i].SelectedIndex = 0;
                }
                if (!trpk_m1[i].Enabled)
                {
                    trpk_m1[i].SelectedIndex =
                        trpk_m2[i].SelectedIndex =
                            trpk_m3[i].SelectedIndex =
                                trpk_m4[i].SelectedIndex = 0;
                }
            }
            for (int i = pkm; i < 6; i++)
                trpk_form[i].Enabled = false;
        }
    }

    // Dumping
    private string GetTRSummary()
    {
        string toret = "======" + Environment.NewLine;

        toret += CB_TrainerID.SelectedIndex + " - " + CB_Trainer_Class.Text[..^6] + " " + CB_TrainerID.Text[..^6] + Environment.NewLine;
        toret += "======" + Environment.NewLine;
        int pkm = CB_numPokemon.SelectedIndex;
        toret += "Pokemon: " + pkm + Environment.NewLine;
        for (int i = 0; i < pkm; i++)
        {
            toret += trpk_pkm[i].Text + " (Lv. " + trpk_lvl[i].SelectedIndex + ") ";
            if (trpk_item[i].SelectedIndex > 0)
                toret += "@" + trpk_item[i].Text;
            if (trpk_abil[i].SelectedIndex != 0)
            {
                string abil = trpk_abil[i].Text;
                abil = abil[..^4];
                toret += " (Ability: " + abil + ")";
            }
            if (checkBox_Moves.Checked)
            {
                toret += " (Moves: ";
                if (trpk_m1[i].SelectedIndex > 0) toret += trpk_m1[i].Text;
                if (trpk_m2[i].SelectedIndex > 0) toret += " / " + trpk_m2[i].Text;
                if (trpk_m3[i].SelectedIndex > 0) toret += " / " + trpk_m3[i].Text;
                if (trpk_m4[i].SelectedIndex > 0) toret += " / " + trpk_m4[i].Text;
                toret += ")";
            }
            toret += " IVs: All " + (Convert.ToInt32(trpk_IV[i].SelectedIndex) / 8);
            toret += Environment.NewLine;
        }
        toret += Environment.NewLine;
        return toret;
    }

    private bool dumping;

    private void B_Dump_Click(object sender, EventArgs e)
    {
        string toret = ""; dumping = true;
        for (int i = 1; i < CB_TrainerID.Items.Count; i++)
        {
            CB_TrainerID.SelectedIndex = i;
            string tdata = GetTRSummary();
            toret += tdata;
        }
        var sfd = new SaveFileDialog { FileName = "Battles.txt", Filter = "Text File|*.txt" };

        SystemSounds.Asterisk.Play();
        if (sfd.ShowDialog() == DialogResult.OK)
        {
            string path = sfd.FileName;
            File.WriteAllText(path, toret, Encoding.Unicode);
        }
        dumping = false;
    }

    // Change Read/Write
    private TrainerData6 tr;

    private void ChangeTrainerIndex(object sender, EventArgs e)
    {
        if (!dumping && index > -1) WriteFile();
        ReadFile(); // Load the new file.
    }

    private void ReadFile()
    {
        if (start) return;
        index = CB_TrainerID.SelectedIndex;
        loading = true;

        if (index == 0) return;

        tabControl1.Enabled = true;
        byte[] trd = trdata[index];
        byte[] trp = trpoke[index];
        tr = new TrainerData6(trd, trp, Main.Config.ORAS);

        // Load Trainer Data
        CB_Trainer_Class.SelectedIndex = tr.Class;
        checkBox_Item.Checked = tr.Item;
        checkBox_Moves.Checked = tr.Moves;
        CB_Battle_Type.SelectedIndex = tr.BattleType;
        CB_numPokemon.SelectedIndex = tr.NumPokemon;
        CB_Item_1.SelectedIndex = tr.Items[0];
        CB_Item_2.SelectedIndex = tr.Items[1];
        CB_Item_3.SelectedIndex = tr.Items[2];
        CB_Item_4.SelectedIndex = tr.Items[3];
        CB_AI.SelectedIndex = tr.AI;
        checkBox_Healer.Checked = tr.Healer;
        CB_Money.SelectedIndex = tr.Money;
        CB_Prize.SelectedIndex = tr.Prize;

        // Load Pokemon Data
        for (int i = 0; i < tr.NumPokemon; i++)
        {
            trpk_IV[i].SelectedIndex = tr.Team[i].IVs;
            trpk_lvl[i].SelectedIndex = tr.Team[i].Level;
            trpk_pkm[i].SelectedIndex = tr.Team[i].Species;
            FormUtil.SetForms(tr.Team[i].Species, trpk_form[i], AltForms);
            trpk_form[i].SelectedIndex = tr.Team[i].Form % trpk_form[i].Items.Count; // stupid X/Y buggy edge cases (220 / 222)
            RefreshPKMSlotAbility(i); // Repopulate Abilities

            trpk_abil[i].SelectedIndex = tr.Team[i].Ability;
            trpk_gender[i].SelectedIndex = tr.Team[i].Gender;

            trpk_item[i].SelectedIndex = tr.Team[i].Item;
            trpk_m1[i].SelectedIndex = tr.Team[i].Moves[0];
            trpk_m2[i].SelectedIndex = tr.Team[i].Moves[1];
            trpk_m3[i].SelectedIndex = tr.Team[i].Moves[2];
            trpk_m4[i].SelectedIndex = tr.Team[i].Moves[3];
        }
        loading = false;
        ChangeTrainerType(null, null); // Prompt cleaning update of PKM fields

        // Refresh Team View
        if (!loading)
        {
            for (int i = 0; i < 6; i++) ShowTeams(i);
            // showText(); // Commented out for now, have to figure out how text is assigned.
        }
    }

    private void WriteFile()
    {
        // Set Trainer Data
        tr.Moves = checkBox_Moves.Checked;
        tr.Item = checkBox_Item.Checked;
        tr.Class = CB_Trainer_Class.SelectedIndex;
        tr.BattleType = (byte)CB_Battle_Type.SelectedIndex;
        tr.NumPokemon = (byte)CB_numPokemon.SelectedIndex;
        if (tr.NumPokemon == 0)
            tr.NumPokemon = 1; // No empty teams!
        tr.Items[0] = (ushort)CB_Item_1.SelectedIndex;
        tr.Items[1] = (ushort)CB_Item_2.SelectedIndex;
        tr.Items[2] = (ushort)CB_Item_3.SelectedIndex;
        tr.Items[3] = (ushort)CB_Item_4.SelectedIndex;
        tr.AI = (byte)CB_AI.SelectedIndex;
        tr.Healer = checkBox_Healer.Checked;
        tr.Money = (byte)CB_Money.SelectedIndex;
        tr.Prize = (ushort)CB_Prize.SelectedIndex;

        // Set Pokemon Data
        Array.Resize(ref tr.Team, tr.NumPokemon);
        for (int i = 0; i < tr.NumPokemon; i++)
        {
            tr.Team[i] ??= new TrainerData6.Pokemon(new byte[100], false, false);
            tr.Team[i].IVs = (byte)trpk_IV[i].SelectedIndex;
            tr.Team[i].Ability = trpk_abil[i].SelectedIndex;
            tr.Team[i].Gender = trpk_gender[i].SelectedIndex;
            tr.Team[i].Level = (ushort)trpk_lvl[i].SelectedIndex;
            tr.Team[i].Species = (ushort)trpk_pkm[i].SelectedIndex;
            tr.Team[i].Form = (ushort)trpk_form[i].SelectedIndex;
            tr.Team[i].Item = (ushort)trpk_item[i].SelectedIndex;

            tr.Team[i].Moves[0] = (ushort)trpk_m1[i].SelectedIndex;
            tr.Team[i].Moves[1] = (ushort)trpk_m2[i].SelectedIndex;
            tr.Team[i].Moves[2] = (ushort)trpk_m3[i].SelectedIndex;
            tr.Team[i].Moves[3] = (ushort)trpk_m4[i].SelectedIndex;
        }
        trdata[index] = tr.Write();
        trpoke[index] = tr.WriteTeam();
    }

    // Image Displays
    private void ChangeTeam(object sender, EventArgs e)
    {
        if (loading) return;

        int gendSlot = Array.IndexOf(trpk_gender, sender as ComboBox);
        int itemSlot = Array.IndexOf(trpk_item, sender as ComboBox);
        ShowTeams(gendSlot < 0 ? itemSlot : gendSlot);
    }

    private void ShowTeams(int i)
    {
        if (tr == null) return;
        if (i >= tr.Team.Length) { pba[i].Image = null; return; }
        Bitmap rawImg = WinFormsUtil.GetSprite(tr.Team[i].Species, tr.Team[i].Form, tr.Team[i].Gender, tr.Team[i].Item, Main.Config);
        pba[i].Image = WinFormsUtil.ScaleImage(rawImg, 2);
    }

    //private void ShowText()
    //{
    //    if (index * 2 >= trText.Length) return;
    //    TB_Text1.Text = trText[index * 2];
    //    TB_Text2.Text = trText[(index * 2) + 1];
    //}

    private void Setup()
    {
        start = true;

        Array.Resize(ref trName, trdata.Length);
        CB_TrainerID.Items.Clear();
        for (int i = 0; i < trdata.Length; i++)
            CB_TrainerID.Items.Add($"{trName[i] ?? "UNKNOWN"} - {i:000}");

        CB_Trainer_Class.Items.Clear();
        for (int i = 0; i < trClass.Length; i++)
            CB_Trainer_Class.Items.Add($"{trClass[i]} - {i:000}");

        specieslist[0] = "---";
        abilitylist[0] = itemlist[0] = movelist[0] = "";
        pba = [PB_Team1, PB_Team2, PB_Team3, PB_Team4, PB_Team5, PB_Team6];

        for (int i = 0; i < 6; i++)
        {
            trpk_pkm[i].Items.Clear();
            trpk_pkm[i].Items.AddRange(specieslist);

            trpk_m1[i].Items.Clear();
            trpk_m2[i].Items.Clear();
            trpk_m3[i].Items.Clear();
            trpk_m4[i].Items.Clear();
            foreach (string s in movelist)
            {
                trpk_m1[i].Items.Add(s);
                trpk_m2[i].Items.Add(s);
                trpk_m3[i].Items.Add(s);
                trpk_m4[i].Items.Add(s);
            }

            trpk_item[i].Items.Clear();
            trpk_item[i].Items.AddRange(itemlist);

            trpk_lvl[i].Items.Clear();
            for (int z = 0; z <= 100; z++)
                trpk_lvl[i].Items.Add(z.ToString());

            trpk_IV[i].Items.Clear();
            for (int z = 0; z < 256; z++)
                trpk_IV[i].Items.Add(z.ToString());

            trpk_gender[i].Items.Clear();
            trpk_gender[i].Items.Add("- / Genderless/Random");
            trpk_gender[i].Items.Add("♂ / Male");
            trpk_gender[i].Items.Add("♀ / Female");

            trpk_form[i].Items.Add("");

            trpk_pkm[i].SelectedIndex = 0;
        }
        CB_Item_1.Items.Clear();
        CB_Item_2.Items.Clear();
        CB_Item_3.Items.Clear();
        CB_Item_4.Items.Clear();
        CB_Prize.Items.Clear();
        foreach (string s in itemlist)
        {
            CB_Item_1.Items.Add(s);
            CB_Item_2.Items.Add(s);
            CB_Item_3.Items.Add(s);
            CB_Item_4.Items.Add(s);
            CB_Prize.Items.Add(s);
        }

        CB_AI.Items.Clear(); CB_Money.Items.Clear();
        for (int i = 0; i < 256; i++)
        { CB_AI.Items.Add(i.ToString()); CB_Money.Items.Add(i.ToString()); }

        CB_Battle_Type.Items.Clear();
        CB_Battle_Type.Items.Add("Single");
        CB_Battle_Type.Items.Add("Double");
        CB_Battle_Type.Items.Add("Triple");
        CB_Battle_Type.Items.Add("Rotation");
        if (Main.Config.ORAS) CB_Battle_Type.Items.Add("Horde");
        megaEvos = Main.Config.ORAS
            ? [15, 18, 80, 208, 254, 260, 302, 319, 323, 334, 362, 373, 376, 380, 381, 428, 475, 531, 719, 3, 6, 9, 65, 94, 115, 127, 130, 142, 150, 181, 212, 214, 229, 248, 257, 282, 303, 306, 308, 310, 354, 359, 445, 448, 460,
            ]
            : [3, 6, 9, 65, 94, 115, 127, 130, 142, 150, 181, 212, 214, 229, 248, 257, 282, 303, 306, 308, 310, 354, 359, 445, 448, 460,
            ];

        CB_TrainerID.SelectedIndex = 1;
        start = false;
        ReadFile();
    }

    public static bool rPKM, rSmart, rProgressiveBST, rLevel, rMove, rMetronome, rNoMove, rForceHighPower, rAbility, rDiffAI,
    rDiffIV, rClass, rGift, rItem, rDoRand, rRandomMegas, rGymE4Only,
    rTypeTheme, rTypeGymTrainers, rOnlySingles, rDMG, rSTAB, r6PKM, rForceFullyEvolved, rBetterMovesets;

    public static bool rNoFixedDamage;
    public static bool rBanBadTrainerItems;
    public static bool rItemClause;
    public static bool rSmartTrainerItems; // legacy/global disabled in v4.9.4
    public static int rSmartTrainerItemMode = 1;
    public static TrainerHeldItemTemplate rTrainerHeldItemTemplate;
    public static TrainerBetterMovesetTemplate rTrainerBetterMovesetTemplate;
    public static bool rUseLevelCaps, rLevelCapsApplyPrevious, rLevelCapsGuaranteeMega;
    public static bool rRandomDoubleBattles;
    public static int rRandomDoubleBattleChance;
    public static int rLevelCapPreviousGap;
    public static decimal rLevelCapCurvePower;
    public static List<ProgressiveBSTRule> rProgressiveBSTRules = [];
    public static List<TrainerLevelCapRule> rLevelCapRules = [];
    public static List<TrainerMoveRule> rTrainerMoveRules = [];
    internal static bool[] rThemedClasses = [];
    private static string[] rTags;
    private static int[] megaEvos;
    public static int[] rIgnoreClass, rEnsureMEvo;
    public static int rDMGCount, rSTABCount;
    private int[] mEvoTypes;
    private static int[] rModelRestricted;
    public static int[] rFinalEvo;
    private string[] rImportant;
    // Universal Pokemon Randomizer ZX trainer classification for Gen 6.
    // Trainer.isImportant(): RIVAL*, FRIEND*, or tags ending in STRONG.
    // Trainer.isBoss(): ELITE*, CHAMPION*, UBER*, or tags ending in LEADER.
    //
    // These lists are used only by Trainer Level Caps / Trainer Move Rules.
    // Existing pk3DS trainer tags remain untouched for the rest of the randomizer.
    private static readonly int[] UPRZXImportantTrainers_XY =
    [
        130, 131, 132, 137, 138, 139,
        174, 175, 184, 185, 186, 188,
        304,
        321, 322, 323, 324, 325, 327, 328,
        329, 330, 331, 332, 333, 334, 335, 336, 337,
        338, 339, 340, 341, 342, 343,
        344, 345, 346, 347, 348, 349, 350, 351,
        435, 436, 437, 438, 439,
        470, 471, 472, 473, 474, 475, 476, 477, 478, 479,
        519, 520, 521,
        573,
        575, 576, 577, 578, 579, 580, 581, 582, 583,
        584, 585, 586, 587, 588, 589, 590, 591, 592,
        593, 594, 595, 596, 597, 598, 599, 600, 601,
        604, 605, 606, 607, 608, 609,
    ];

    private static readonly int[] UPRZXBossTrainers_XY =
    [
        006, 021, 022, 023, 024, 025, 026, 076,
        187, 269, 270, 271, 276,
        303, 525, 526,
    ];

    private static readonly int[] UPRZXImportantTrainers_ORAS =
    [
        001, 002, 003, 004, 005, 006,
        289, 290, 291, 292, 293, 294,
        295, 296, 297, 298, 299, 300,
        518,
        527, 528, 529, 530, 531, 532,
        583,
        674, 675, 676, 677, 678, 679,
        683, 684, 685, 686, 687,
        688, 689, 690,
        691, 692, 693,
        694, 695, 696, 697, 698,
        699, 700, 701,
        906, 907, 908,
        944, 946,
    ];

    private static readonly int[] UPRZXBossTrainers_ORAS =
    [
        178, 231, 235, 236, 266, 271,
        552, 553, 554, 555, 556, 557,
        561, 563, 567, 569, 570, 571, 572,
        680,
        909, 910, 911, 912, 913,
        942, 943,
    ];
    private readonly List<string> Tags = [];
    private readonly Dictionary<string, int> TagTypes = [];
    public static int[] sL; // Random Species List
    public static decimal rGiftPercent, rLevelMultiplier, rMinPKM, rMaxPKM, rForceFullyEvolvedLevel, rForceHighPowerLevel;

    private List<TrainerLevelCapRule> GetLevelCapCandidates()
    {
        int[] importantIds = Main.Config.ORAS
            ? UPRZXImportantTrainers_ORAS
            : UPRZXImportantTrainers_XY;

        int[] bossIds = Main.Config.ORAS
            ? UPRZXBossTrainers_ORAS
            : UPRZXBossTrainers_XY;

        var bossSet = bossIds.ToHashSet();
        var candidates = new List<TrainerLevelCapRule>();

        foreach (int id in importantIds
            .Concat(bossIds)
            .Distinct()
            .Where(id => id > 0 && id < trdata.Length && id < trpoke.Length))
        {
            if (trpoke[id].Length == 0)
                continue;

            var trainer = new TrainerData6(trdata[id], trpoke[id], Main.Config.ORAS);
            if (trainer.Team.Length == 0)
                continue;

            candidates.Add(new TrainerLevelCapRule
            {
                Enabled = true,
                TrainerID = id,
                Group = bossSet.Contains(id) ? "Boss" : "Important",
                Trainer = GetTrainerDisplayName(id, trainer.Class),
                CurrentAceLevel = GetAceLevel(trainer),
                LevelCap = 0,
            });
        }

        return candidates
            .OrderBy(r => r.CurrentAceLevel)
            .ThenBy(r => r.TrainerID)
            .ToList();
    }
    private string GetTrainerDisplayName(int trainerID, int trainerClass)
    {
        string name = trainerID < trName.Length ? trName[trainerID] : string.Empty;
        string cls = trainerClass < trClass.Length ? trClass[trainerClass] : string.Empty;

        if (string.IsNullOrWhiteSpace(name))
            name = "UNKNOWN";
        if (string.IsNullOrWhiteSpace(cls))
            return name;

        return $"{cls} {name}";
    }

    private void B_Randomize_Click(object sender, EventArgs e)
    {
        rPKM = rMove = rMetronome = rAbility = rDiffAI = rDiffIV = rClass = rGift = rItem = rDoRand = false;
        rSmart = rProgressiveBST = false;
        rUseLevelCaps = rLevelCapsApplyPrevious = rLevelCapsGuaranteeMega = false;
        rBanBadTrainerItems = false;
        rItemClause = false;
        rBetterMovesets = false;
        rSmartTrainerItems = false;
        rSmartTrainerItemMode = 1;
        rTrainerHeldItemTemplate = null;
        rTrainerBetterMovesetTemplate = null;
        rLevelCapPreviousGap = 2;
        rLevelCapCurvePower = 1.6m;
        rProgressiveBSTRules = [];
        rLevelCapRules = [];
        rTrainerMoveRules = [];

        rGiftPercent = 0;
        rForceFullyEvolvedLevel = 0;

        new TrainerRand(GetLevelCapCandidates()).ShowDialog();

        if (rDoRand)
            Randomize();
    }

    private void Randomize()
    {
        List<int> banned = [165, 621]; // Struggle, Hyperspace Fury
        if (rNoFixedDamage)
            banned.AddRange(MoveRandomizer.FixedDamageMoves);
        var move = new MoveRandomizer(Main.Config)
        {
            rDMG = rDMG,
            rSTAB = rSTAB,
            rSTABCount = rSTABCount,
            rDMGCount = rDMGCount,
            BannedMoves = banned,
        };

        rImportant = new string[CB_TrainerID.Items.Count];
        rTags = Main.Config.ORAS ? GetTagsORAS() : GetTagsXY();
        mEvoTypes = GetMegaEvolvableTypes();
        List<int> GymE4Types = [];

        // Fetch Move Stats for more difficult randomization

        bool guaranteeAnyMega = rEnsureMEvo.Length > 0
            || (rUseLevelCaps && rLevelCapRules.Any(r => r.Enabled && r.GuaranteeMega));
        if (guaranteeAnyMega)
        {
            if (mEvoTypes.Length < 13 && rTypeTheme)
            {
                WinFormsUtil.Alert("There are insufficient Types with at least one mega evolution to Guarantee story Mega Evos while keeping Type theming.",
                    "Re-Randomize Personal or don't choose both options."); return;
            }
            GymE4Types.AddRange(mEvoTypes);
        }
        else
        {
            GymE4Types.AddRange(Enumerable.Range(0, types.Length).ToArray());
        }

        foreach (int t1 in rEnsureMEvo.Where(t1 => rTags[t1].Length != 0 && !TagTypes.ContainsKey(rTags[t1])))
        {
            int t;
            if (rTags[t1].Contains("GYM") || rTags[t1].Contains("ELITE") || rTags[t1].Contains("CHAMPION"))
            {
                t = GymE4Types[(int)(Rand() % GymE4Types.Count)];
                GymE4Types.Remove(t);
            }
            else
            {
                t = mEvoTypes[Rand() % mEvoTypes.Length];
            }

            TagTypes[rTags[t1]] = t;
        }
        foreach (string t1 in Tags)
        {
            if (!TagTypes.ContainsKey(t1) && t1.Length != 0)
            {
                int t;
                if (t1.Contains("GYM") || t1.Contains("ELITE") || t1.Contains("CHAMPION"))
                {
                    t = GymE4Types[(int)(Rand() % GymE4Types.Count)];
                    GymE4Types.Remove(t);
                }
                else
                {
                    t = (int)(Rand() % types.Length);
                }

                TagTypes[t1] = t;
            }
            Console.WriteLine(t1 + ": " + types[TagTypes[t1]]);
        }

        CB_TrainerID.SelectedIndex = 0; // fake a writeback
        ushort[] itemvals = Main.Config.ORAS ? Legal.Pouch_Items_AO : Legal.Pouch_Items_XY;
        itemvals = [.. itemvals, .. Legal.Pouch_Berry_XY];

        if (rBanBadTrainerItems)
        {
            ushort[] cleanItemPool = SmartTrainerItemPicker
                .GetBanBadItemPool(itemvals.Select(z => (int)z))
                .Select(z => (ushort)z)
                .ToArray();

            if (cleanItemPool.Length > 0)
                itemvals = cleanItemPool;
        }

        itemvals = SmartTrainerItemPicker
            .AddSmartTrainerItemPoolExtras(itemvals.Select(z => (int)z))
            .Select(z => (ushort)z)
            .ToArray();

        rSmartTrainerItems = CHK_SmartHeldItems?.Checked ?? false;
        rSmartTrainerItemMode = CB_SmartHeldItemMode?.SelectedIndex ?? 1;
        rTrainerHeldItemTemplate = rItem
            ? TrainerHeldItemTemplate.LoadOrCreateDefault(itemlist, itemvals.Select(z => (int)z))
            : null;

        if (rTrainerHeldItemTemplate is not null && rTrainerHeldItemTemplate.Warnings.Count > 0)
        {
            WinFormsUtil.Alert(
                string.Join(Environment.NewLine, rTrainerHeldItemTemplate.Warnings.Take(8)),
                $"Held item template loaded with {rTrainerHeldItemTemplate.Warnings.Count} warning(s)."
            );
        }

        rTrainerBetterMovesetTemplate = rBetterMovesets
            ? TrainerBetterMovesetTemplate.LoadOrCreateDefault()
            : null;

        if (rTrainerBetterMovesetTemplate is not null && rTrainerBetterMovesetTemplate.Warnings.Count > 0)
        {
            WinFormsUtil.Alert(
                string.Join(Environment.NewLine, rTrainerBetterMovesetTemplate.Warnings.Take(8)),
                $"Better moveset template loaded with {rTrainerBetterMovesetTemplate.Warnings.Count} warning(s)."
            );
        }

        var levelCapStages = BuildLevelCapStages();

        int progressTotal = Math.Max(1, CB_TrainerID.Items.Count - 1);
        using var progress = TrainerRandomizeProgress.Show(this, "Randomizing Trainers", progressTotal);

        for (int i = 1; i < CB_TrainerID.Items.Count; i++)
        {
            // Trainer Type/Mega Evo
            int type = GetRandomType(i);
            bool isImportantClass = rImportant[i] != null && (rImportant[i].Contains("GYM") || rImportant[i].Contains("ELITE") || rImportant[i].Contains("CHAMPION"));
            bool typerand = (rTypeTheme && !rGymE4Only) || (rTypeTheme && isImportantClass);

            rSpeciesRand.rType = typerand;

            byte[] trd = trdata[i];
            byte[] trp = trpoke[i];
            var t = new TrainerData6(trd, trp, Main.Config.ORAS)
            {
                Moves = rMove || (!rNoMove && checkBox_Moves.Checked),
                Item = rItem || checkBox_Item.Checked,
            };

            progress.Report(i, progressTotal, GetTrainerDisplayName(i, t.Class));

            int trainerAce = GetAceLevel(t);
            bool mevo = ShouldGuaranteeMega(i, trainerAce, levelCapStages);
            int? targetLevel = GetTrainerTargetLevel(i, trainerAce, levelCapStages, out bool forceExactLevel);
            var moveRule = GetTrainerMoveRule(i);

            SetMinMaxPKM(t);
            SetFullParties(t, rImportant[i] == null);
            ApplyRandomDoubleBattle(t, trainerAce);
            RandomizeTrainerAIClass(t, trClass);
            RandomizeTrainerPrizeItem(t);
            string trainerGroup = i < rImportant.Length ? rImportant[i] ?? string.Empty : string.Empty;
            bool isImportantTrainer = !string.IsNullOrWhiteSpace(trainerGroup);

            RandomizeTeam(t, move, learn, itemvals, type, mevo, typerand, targetLevel, forceExactLevel, trainerAce, moveRule, i, trainerGroup, isImportantTrainer);

            // Final per-trainer Smart Items pass.
            // Runs once after moves are finalized so held items match the final moveset.
            ApplySmartTrainerItemsToTrainer(t, itemvals, mevo, moveRule);

            trdata[i] = t.Write();
            trpoke[i] = t.WriteTeam();
        }
        CB_TrainerID.SelectedIndex = 1;
        RandomizationSessionState.MarkAction("trainers.randomize");
        WinFormsUtil.Alert("Randomized all Trainers according to specification!", "Smart Items were applied only to Use-checked Move Rules trainers. Ban Bad Items applies to the regular random item pool when enabled.");
    }


    private static void ApplySmartTrainerItemsToTrainer(TrainerData6 trainer, ushort[] itemvals, bool mevo, TrainerMoveRule moveRule)
    {
        if (rTrainerHeldItemTemplate is not null)
            return;

        // v4.9.5 overload for per-trainer Smart Items from Trainer Move Rules.
        // Trainers need Use checked and Smart Items checked.
        if (!rItem || moveRule is null || !moveRule.Enabled || !moveRule.SmartItems || trainer?.Team is null || trainer.Team.Length == 0)
            return;

        trainer.Item = true;

        int last = trainer.Team.Length - 1;
        var smartPool = mevo ? RemoveMegaStonesFromItemPool(itemvals) : itemvals;
        if (smartPool.Length == 0)
            smartPool = itemvals;

        var usedItems = rItemClause
            ? trainer.Team.Select(pk => (int)pk.Item).Where(item => item > 0).ToHashSet()
            : null;

        for (int p = 0; p < trainer.Team.Length; p++)
        {
            // Preserve the forced Mega stone.
            if (mevo && p == last)
                continue;

            var pk = trainer.Team[p];
            if (pk is null || pk.Species <= 0)
                continue;

            if (usedItems is not null)
                usedItems.Remove(pk.Item);

            int[] slotPool = ApplyItemClauseToPool(smartPool.Select(z => (int)z), usedItems);
            int smartItem = SmartTrainerItemPicker.Pick(
                pk.Species,
                pk.Form,
                pk.Level,
                pk.Moves.Select(z => (int)z),
                slotPool,
                pk.Ability,
                rFinalEvo != null && rFinalEvo.Contains(pk.Species),
                2
            );

            pk.Item = (ushort)Math.Max(0, smartItem);
            TrackItemClause(pk.Item, usedItems);
        }
    }
    private static void ApplySmartTrainerItemsToTrainer(TrainerData6 trainer, ushort[] itemvals, bool mevo)
    {
        if (rTrainerHeldItemTemplate is not null)
            return;

        if (!rItem || trainer?.Team is null || trainer.Team.Length == 0)
            return;

        trainer.Item = true;

        int last = trainer.Team.Length - 1;
        var smartPool = mevo ? RemoveMegaStonesFromItemPool(itemvals) : itemvals;
        if (smartPool.Length == 0)
            smartPool = itemvals;

        var usedItems = rItemClause
            ? trainer.Team.Select(pk => (int)pk.Item).Where(item => item > 0).ToHashSet()
            : null;

        for (int p = 0; p < trainer.Team.Length; p++)
        {
            // Preserve the forced Mega stone.
            if (mevo && p == last)
                continue;

            var pk = trainer.Team[p];
            if (pk is null || pk.Species <= 0)
                continue;

            if (usedItems is not null)
                usedItems.Remove(pk.Item);

            int[] slotPool = ApplyItemClauseToPool(smartPool.Select(z => (int)z), usedItems);
            int smartItem = SmartTrainerItemPicker.Pick(
                pk.Species,
                pk.Form,
                pk.Level,
                pk.Moves.Select(z => (int)z),
                slotPool,
                pk.Ability,
                rFinalEvo != null && rFinalEvo.Contains(pk.Species),
                rSmartTrainerItemMode
            );

            pk.Item = (ushort)Math.Max(0, smartItem);
            TrackItemClause(pk.Item, usedItems);
        }
    }
    private List<TrainerLevelCapStage> BuildLevelCapStages()
    {
        if (!rUseLevelCaps || rLevelCapRules.Count == 0)
            return [];

        var stages = new List<TrainerLevelCapStage>();
        foreach (var rule in rLevelCapRules.Where(r => r.Enabled))
        {
            if (rule.TrainerID <= 0 || rule.TrainerID >= trdata.Length || trpoke[rule.TrainerID].Length == 0)
                continue;

            var trainer = new TrainerData6(trdata[rule.TrainerID], trpoke[rule.TrainerID], Main.Config.ORAS);
            int ace = GetAceLevel(trainer);
            int cap = rule.LevelCap == 0 ? ace : rule.LevelCap;

            stages.Add(new TrainerLevelCapStage
            {
                TrainerID = rule.TrainerID,
                OriginalAceLevel = ace,
                LevelCap = ClampLevel(cap),
                GuaranteeMega = rule.GuaranteeMega,
                MinMovePower = ClampMovePower(rule.MinMovePower),
            });
        }

        return stages
            .OrderBy(s => s.OriginalAceLevel)
            .ThenBy(s => s.TrainerID)
            .ToList();
    }

    private static int GetAceLevel(TrainerData6 trainer)
    {
        return trainer.Team.Length == 0 ? 1 : trainer.Team.Max(pk => pk.Level);
    }

    private static int? GetTrainerTargetLevel(int trainerID, int trainerAce, List<TrainerLevelCapStage> stages, out bool forceExactLevel)
    {
        forceExactLevel = false;

        if (!rUseLevelCaps || stages.Count == 0)
            return null;

        var exact = stages.FirstOrDefault(s => s.TrainerID == trainerID);
        if (exact is not null)
        {
            forceExactLevel = true;
            return exact.LevelCap;
        }

        if (!rLevelCapsApplyPrevious)
            return null;

        var next = stages.FirstOrDefault(s => trainerAce <= s.OriginalAceLevel);
        if (next is null)
            return null;

        int nextIndex = stages.IndexOf(next);
        TrainerLevelCapStage previous = nextIndex > 0 ? stages[nextIndex - 1] : null;

        // Preserve the game's original trainer curve instead of flattening everyone far below the cap.
        // If the next important trainer moved by +N levels, regular trainers before it also move by +N.
        int endTarget = ClampLevel(next.LevelCap - rLevelCapPreviousGap);
        int deltaToNextCap = endTarget - next.OriginalAceLevel;
        int target = ClampLevel(trainerAce + deltaToNextCap);

        // Do not let trainers after a previous cap fall below that previous cap when caps were raised.
        if (previous is not null && trainerAce > previous.OriginalAceLevel)
            target = Math.Max(target, previous.LevelCap);

        return ClampLevel(target);
    }

    private static bool ShouldGuaranteeMega(int trainerID, int trainerAce, List<TrainerLevelCapStage> stages)
    {
        if (rEnsureMEvo.Contains(trainerID))
            return true;

        return stages.Any(s => s.TrainerID == trainerID && s.GuaranteeMega);
    }

    private static TrainerMoveRule GetTrainerMoveRule(int trainerID)
    {
        var rule = rTrainerMoveRules?.FirstOrDefault(r => r.Enabled && r.TrainerID == trainerID);
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

    private static ushort ApplyLevelTarget(ushort currentLevel, int? targetLevel, bool forceExactLevel, int trainerAce)
    {
        if (targetLevel is null)
            return currentLevel;

        int target = ClampLevel(targetLevel.Value);
        if (forceExactLevel)
            return (ushort)target;

        int delta = target - trainerAce;
        return (ushort)ClampLevel(currentLevel + delta);
    }

    private static ProgressiveBSTRule GetProgressiveBSTRule(int level)
    {
        var rule = rProgressiveBSTRules.FirstOrDefault(r =>
            level >= r.MinLevel && level <= r.MaxLevel);

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

    private static int GetProgressiveRandomSpecies(int oldSpecies, int type, int level)
    {
        var rule = GetProgressiveBSTRule(level);

        if (rule.FullRandom)
        {
            return type == -1
                ? rSpeciesRand.GetRandomSpecies(oldSpecies)
                : rSpeciesRand.GetRandomSpeciesType(oldSpecies, type);
        }

        return rSpeciesRand.GetRandomSpeciesProgressiveBST(
            oldSpecies,
            type,
            rule.MinBST,
            rule.MaxBST
        );
    }
    private static void RandomizeTeam(TrainerData6 t, MoveRandomizer move, LearnsetRandomizer learn, ushort[] itemvals, int type, bool mevo, bool typerand, int? targetLevel, bool forceExactLevel, int trainerAce, TrainerMoveRule moveRule, int trainerID, string trainerGroup, bool isImportantTrainer)
    {
        int last = t.Team.Length - 1;
        var randomItemPool = mevo ? RemoveMegaStonesFromItemPool(itemvals) : itemvals;
        if (randomItemPool.Length == 0)
            randomItemPool = itemvals;
        var usedHeldItems = rItemClause ? new HashSet<int>() : null;
        for (int p = 0; p < t.Team.Length; p++)
        {
            var pk = t.Team[p];
            int[] stones = null;
            int[] slotItemPool = ApplyItemClauseToPool(randomItemPool.Select(z => (int)z), usedHeldItems);

            if (targetLevel is not null)
                pk.Level = ApplyLevelTarget(pk.Level, targetLevel, forceExactLevel, trainerAce);
            else if (rLevel)
                pk.Level = (ushort)Randomizer.GetModifiedLevel(pk.Level, rLevelMultiplier);

            if (rPKM)
            {
                // randomize pokemon
                int species;
                if (typerand)
                {
                    if (rProgressiveBST)
                    {
                        species = GetProgressiveRandomSpecies(pk.Species, -1, pk.Level);
                    }
                    else
                    {
                        species = rSpeciesRand.GetRandomSpeciesType(pk.Species, type);
                    }

                    if (p == last && mevo)
                    {
                        int tries = 0;
                        do { stones = GetRandomMega(out species); }
                        while (Main.Config.Personal[species].Types.All(z => z != type) && tries++ < 100);
                    }
                }
                //
                else if (p == last && mevo)
                {
                    stones = GetRandomMega(out species);
                }
                else
                {
                    if (rProgressiveBST)
                    {
                        var range = SpeciesRandomizer.GetProgressiveBSTRange(pk.Level);

                        species = GetProgressiveRandomSpecies(pk.Species, -1, pk.Level);
                    }
                    else
                    {
                        species = rSpeciesRand.GetRandomSpecies(pk.Species);
                    }
                }
//
                pk.Species = (ushort)species;
                pk.Gender = 0; // Set Gender to Random
                bool mega = rRandomMegas && !(mevo && p == last); // except if mega evolution is forced for the last slot
                pk.Form = (ushort)Randomizer.GetRandomForme(pk.Species, mega, true, Main.SpeciesStat);
            }

            if (mevo && p == last && stones == null)
            {
                stones = GetRandomMegaForType(type, typerand, out int species);
                pk.Species = (ushort)species;
                pk.Form = 0; // force the held stone to trigger a natural Mega Evolution
            }

            if (rAbility)
                pk.Ability = (int)(1 + (Rand() % 3));
            if (rDiffIV)
                pk.IVs = 255;

            if (mevo && p == last && stones != null)
                pk.Item = (ushort)stones[Rand() % stones.Length];
            else if (rItem && rTrainerHeldItemTemplate is null && !rSmartTrainerItems)
                pk.Item = (ushort)slotItemPool[Rand() % slotItemPool.Length];

            if (rForceFullyEvolved && pk.Level >= rForceFullyEvolvedLevel && !rFinalEvo.Contains(pk.Species))
            {
                static int randFinalEvo() => (int)(Util.Random32() % rFinalEvo.Length);
                pk.Species = (ushort)rFinalEvo[randFinalEvo()];
                pk.Form = (ushort)Randomizer.GetRandomForme(pk.Species, rRandomMegas, true, Main.SpeciesStat);
            }

            // random
            if (rMove)
            {
                var pkMoves = move.GetRandomMoveset(pk.Species, 4);
                for (int m = 0; m < 4; m++)
                    pk.Moves[m] = (ushort)pkMoves[m];
            }

            // levelup
            if (rNoMove)
            {
                t.Moves = true;
                var pkMoves = learn.GetCurrentMoves(pk.Species, pk.Form, pk.Level, 4);
                for (int m = 0; m < 4; m++)
                    pk.Moves[m] = (ushort)pkMoves[m];
            }

            if (rMetronome)
            {
                t.Moves = true;
                var pkMoves = new[] { 118, 0, 0, 0 };
                for (int m = 0; m < 4; m++)
                    pk.Moves[m] = (ushort)pkMoves[m];
            }

            // high-power attacks
            if (rForceHighPower && pk.Level >= rForceHighPowerLevel)
            {
                var pkMoves = learn.GetHighPoweredMoves(pk.Species, pk.Form, 4);
                for (int m = 0; m < 4; m++)
                    pk.Moves[m] = (ushort)pkMoves[m];
            }

            if (ShouldUseBetterMoveset(moveRule, trainerID, isImportantTrainer, trainerGroup, pk.Level) && !rMetronome)
            {
                t.Moves = true;
                int teamWeatherMask = GetTeamWeatherSupportMask(t);
                var pkMoves = SmartTrainerMovePicker.PickBetterMoveset(
                    pk.Species,
                    pk.Form,
                    pk.Level,
                    pk.Moves.Select(z => (int)z),
                    move,
                    learn,
                    moveRule,
                    move.rDMG ? move.rDMGCount : 0,
                    pk.Ability,
                    6,
                    teamWeatherMask
                );
                for (int m = 0; m < 4; m++)
                    pk.Moves[m] = (ushort)pkMoves[m];
            }

            if (ShouldApplyMoveRule(moveRule) && !rMetronome)
            {
                t.Moves = true;
                var pkMoves = ApplyTrainerMoveRule(pk.Moves.Select(z => (int)z).ToArray(), pk.Species, moveRule, move, move.rDMG ? move.rDMGCount : 0);
                for (int m = 0; m < 4; m++)
                    pk.Moves[m] = (ushort)pkMoves[m];
            }
            // v4.8.8 final Smart Held Items pass.
            // This must run after moves are finalized so items can match the final moveset.
            if (rItem && rTrainerHeldItemTemplate is null && rSmartTrainerItems && !(mevo && p == last))
            {
                int smartItem = SmartTrainerItemPicker.Pick(
                    pk.Species,
                    pk.Form,
                    pk.Level,
                    pk.Moves.Select(z => (int)z),
                    slotItemPool,
                    pk.Ability,
                    rFinalEvo is not null && rFinalEvo.Contains(pk.Species),
                    rSmartTrainerItemMode
                );

                pk.Item = (ushort)Math.Max(0, smartItem);
            }

            if (rItem && rTrainerHeldItemTemplate is not null && !(mevo && p == last))
            {
                int templateItem = rTrainerHeldItemTemplate.PickItem(
                    trainerID,
                    isImportantTrainer,
                    trainerGroup,
                    pk.Item,
                    pk.Species,
                    pk.Form,
                    pk.Level,
                    pk.Moves.Select(z => (int)z),
                    slotItemPool,
                    pk.Ability,
                    rFinalEvo is not null && rFinalEvo.Contains(pk.Species),
                    rSmartTrainerItemMode,
                    usedHeldItems
                );

                pk.Item = (ushort)Math.Max(0, templateItem);
            }

            if (!(mevo && p == last))
                TrackItemClause(pk.Item, usedHeldItems);
        }
    }

    private static int GetTeamWeatherSupportMask(TrainerData6 t)
    {
        int mask = 0;
        foreach (var ally in t.Team)
            mask |= SmartTrainerMovePicker.GetWeatherAbilityMask(ally.Species, ally.Ability);
        return mask;
    }

    private static int[] ApplyItemClauseToPool(IEnumerable<int> itemPool, HashSet<int> usedItems)
    {
        int[] pool = itemPool.Where(i => i > 0).Distinct().ToArray();
        if (!rItemClause || usedItems is null || usedItems.Count == 0)
            return pool;

        int[] filtered = pool.Where(item => !usedItems.Contains(item)).ToArray();
        return filtered.Length > 0 ? filtered : pool;
    }

    private static void TrackItemClause(int item, HashSet<int> usedItems)
    {
        if (!rItemClause || usedItems is null || item <= 0)
            return;

        usedItems.Add(item);
    }

    private static bool ShouldUseBetterMoveset(TrainerMoveRule rule, int trainerID, bool isImportantTrainer, string trainerGroup, int level)
    {
        if (rule != null && rule.Enabled && rule.BetterMovesets)
            return true;

        if (!rBetterMovesets)
            return false;

        return rTrainerBetterMovesetTemplate?.ShouldApply(trainerID, isImportantTrainer, trainerGroup, level) ?? true;
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
            return stab[(int)(Rand() % stab.Count)];

        return eligible[(int)(Rand() % eligible.Count)];
    }


    private static void ApplyTrainerEVOverride(object pk, TrainerMoveRule rule)
    {
        if (pk is null || rule is null || rule.OverrideEVs < 0)
            return;
        int ev = Math.Clamp(rule.OverrideEVs, 0, 252);
        var type = pk.GetType();
        var arrayProp = type.GetProperty("EVs");
        if (arrayProp is not null && arrayProp.CanWrite && arrayProp.PropertyType == typeof(int[]))
        {
            arrayProp.SetValue(pk, Enumerable.Repeat(ev, 6).ToArray());
            return;
        }
        var scalarProp = type.GetProperty("EV") ?? type.GetProperty("EVs");
        if (scalarProp is not null && scalarProp.CanWrite && scalarProp.PropertyType != typeof(int[]))
        {
            scalarProp.SetValue(pk, Convert.ChangeType(ev, scalarProp.PropertyType));
            return;
        }
        string[] names = ["EV_HP", "EV_ATK", "EV_DEF", "EV_SPA", "EV_SPE", "EV_SPD"];
        foreach (string name in names)
        {
            var prop = type.GetProperty(name);
            if (prop is not null && prop.CanWrite)
                prop.SetValue(pk, Convert.ChangeType(ev, prop.PropertyType));
        }
    }
    private static void SetMinMaxPKM(TrainerData6 t)
    {
        int lastPKM = Math.Max(t.NumPokemon - 1, 0); // 0,1-6 => 0-5 (never is 0)
        var avgBST = (int)t.Team.Average(pk => Main.SpeciesStat[pk.Species].BST);
        int avgLevel = (int)t.Team.Average(pk => pk.Level);
        var pinfo = Main.SpeciesStat.OrderBy(pk => Math.Abs(avgBST - pk.BST)).First();
        int avgSpec = Array.IndexOf(Main.SpeciesStat, pinfo);

        // set minimum pkm, don't modify hordes
        if (t.NumPokemon < rMinPKM && t.BattleType != 4)
        {
            t.NumPokemon = (byte)rMinPKM;
            for (int f = lastPKM + 1; f < t.NumPokemon; f++)
            {
                Array.Resize(ref t.Team, (int)rMinPKM);
                t.Team[f] = // clone last pkm, keeping an average level for all new pkm
                    new TrainerData6.Pokemon(t.Team[lastPKM].Write(t.Item, t.Moves), t.Item, t.Moves)
                    {
                        Species = (ushort)(rProgressiveBST
                            ? GetProgressiveRandomSpecies(avgSpec, -1, avgLevel)
                            : rSpeciesRand.GetRandomSpecies(avgSpec)),
                    };
            }
        }

        // set maximum pkm, don't modify hordes
        if (t.NumPokemon > rMaxPKM && t.BattleType != 4)
        {
            Array.Resize(ref t.Team, (int)rMaxPKM);
            t.NumPokemon = (byte)rMaxPKM;
        }
    }

    private static void SetFullParties(TrainerData6 t, bool important)
    {
        int lastPKM = Math.Max(t.NumPokemon - 1, 0); // 0,1-6 => 0-5 (never is 0)
        var avgBST = (int)t.Team.Average(pk => Main.SpeciesStat[pk.Species].BST);
        int avgLevel = (int)t.Team.Average(pk => pk.Level);
        var pinfo = Main.SpeciesStat.OrderBy(pk => Math.Abs(avgBST - pk.BST)).First();
        int avgSpec = Array.IndexOf(Main.SpeciesStat, pinfo);

        // 6 pkm for important trainers, skip the first rival battles
        if (!r6PKM || important)
            return;

        t.NumPokemon = 6;
        for (int f = lastPKM + 1; f < t.NumPokemon; f++)
        {
            Array.Resize(ref t.Team, t.NumPokemon);
            t.Team[f] = // clone last pkm, keeping an average level for all new pkm
                new TrainerData6.Pokemon(t.Team[lastPKM].Write(t.Item, t.Moves), t.Item, t.Moves)
                {
                    Species = (ushort)(rProgressiveBST
                        ? GetProgressiveRandomSpecies(avgSpec, -1, avgLevel)
                        : rSpeciesRand.GetRandomSpecies(avgSpec)),
                    Level = (ushort)avgLevel,
                };
        }
    }


    private static void ApplyRandomDoubleBattle(TrainerData6 t, int trainerAce)
    {
        if (!rRandomDoubleBattles || rRandomDoubleBattleChance <= 0)
            return;

        // Only convert regular single battles. Do not touch existing Double/Triple/Rotation/Horde battles.
        if (t.BattleType != 0)
            return;

        if (Rand() % 100 >= rRandomDoubleBattleChance)
            return;

        EnsureAtLeastTwoPokemon(t);
        t.BattleType = 1;
    }

    private static void EnsureAtLeastTwoPokemon(TrainerData6 t)
    {
        if (t.NumPokemon >= 2 || t.Team.Length >= 2)
        {
            t.NumPokemon = Math.Max(t.NumPokemon, (byte)Math.Min(t.Team.Length, 6));
            return;
        }

        if (t.Team.Length == 0)
            return;

        Array.Resize(ref t.Team, 2);
        t.Team[1] = new TrainerData6.Pokemon(t.Team[0].Write(t.Item, t.Moves), t.Item, t.Moves)
        {
            Species = t.Team[0].Species,
            Level = t.Team[0].Level,
            Form = t.Team[0].Form,
            Gender = t.Team[0].Gender,
            Ability = t.Team[0].Ability,
            Item = t.Team[0].Item,
            IVs = t.Team[0].IVs,
        };
        t.NumPokemon = 2;
    }

    private static void RandomizeTrainerAIClass(TrainerData6 t, string[] trClass)
    {
        if (rDiffAI)
            t.AI |= 7; // Set first 3 bits, keep any other flag if present

        if (rClass && rModelRestricted.Contains(t.Class) && !rIgnoreClass.Contains(t.Class)) // shuffle classes with 3D models
        {
            int randClass() => (int)(Rand() % rModelRestricted.Length);
            t.Class = rModelRestricted[randClass()];
        }
        else
            if (
                rClass // Classes selected to be randomized
                && (!rOnlySingles || t.BattleType == 0) //  Nonsingles only get changed if rOnlySingles
                && !rIgnoreClass.Contains(t.Class) // Current class isn't a special class
            )
            {
                int randClass() => (int)(Rand() % trClass.Length);
                int rv; do { rv = randClass(); }
                while (rIgnoreClass.Contains(rv) || trClass[rv].StartsWith("[~") || (Main.Config.ORAS && rv is >= 0 and <= 63) || rv is >= 68 and <= 126); // don't allow disallowed classes
                t.Class = rv;
            }
    }


    private static T[] RemoveMegaStonesFromItemPool<T>(IEnumerable<T> items)
    {
        if (MegaDictionary == null || MegaDictionary.Count == 0)
            return items.ToArray();

        var megaStones = MegaDictionary.Values.SelectMany(z => z).ToHashSet();
        return items.Where(item => !megaStones.Contains(Convert.ToInt32(item))).ToArray();
    }
    private static void RandomizeTrainerPrizeItem(TrainerData6 t)
    {
        if (rGift && Rand() % 100 < rGiftPercent)
        {
            ushort[] items;
            uint rnd = Rand() % 10;
            if (rnd < 2) // held item
                items = Main.Config.ORAS ? Legal.Pouch_Items_AO : Legal.Pouch_Items_XY;
            else if (rnd < 5) // medicine
                items = Main.Config.ORAS ? Legal.Pouch_Medicine_AO : Legal.Pouch_Medicine_XY;
            else // berry
                items = Legal.Pouch_Berry_XY;
            t.Prize = items[Rand() % items.Length];
        }
        else if (rGift)
        {
            t.Prize = 0;
        }
    }

    private string[] GetTagsORAS()
    {
        string[] tags = Enumerable.Repeat("", trdata.Length).ToArray();
        ImportantTrainers = true;

        // Rival Battles
        TagTrainer(tags, "RIVAL1", 289, 292, 295, 298, 527, 530, 674, 677, 699, 906); // Rival w/ Grass Starter
        TagTrainer(tags, "RIVAL2", 290, 293, 296, 299, 528, 531, 675, 678, 700, 907); // Rival w/ Fire Starter
        TagTrainer(tags, "RIVAL3", 291, 294, 297, 300, 529, 532, 676, 679, 701, 908); // Rival w/ Water Starter

        // Aqua Admins
        TagTrainer(tags, "AQUA1", 178, 231, 266);           // Archie
        TagTrainer(tags, "AQUA2", 683, 684, 685, 686, 687); // Matt
        TagTrainer(tags, "AQUA3", 688, 689, 690);           // Shelly

        // Magma Admins
        TagTrainer(tags, "MAGMA1", 235, 236, 271);           // Maxie
        TagTrainer(tags, "MAGMA2", 694, 695, 696, 697, 698); // Courtney
        TagTrainer(tags, "MAGMA3", 691, 692, 693);           // Tabitha

        // Gym Leaders
        TagTrainer(tags, "GYM1", 561);      // Roxanne
        TagTrainer(tags, "GYM2", 563);      // Brawly
        TagTrainer(tags, "GYM3", 567);      // Wattson
        TagTrainer(tags, "GYM4", 569);      // Flannery
        TagTrainer(tags, "GYM5", 570);      // Norman
        TagTrainer(tags, "GYM6", 571);      // Winona
        TagTrainer(tags, "GYM7", 552);      // Liza & Tate
        TagTrainer(tags, "GYM8", 572, 943); // Wallace

        // Elite Four
        TagTrainer(tags, "ELITE1", 553, 909);             // Sidney
        TagTrainer(tags, "ELITE2", 554, 910);             // Phoebe
        TagTrainer(tags, "ELITE3", 555, 911);             // Glacia
        TagTrainer(tags, "ELITE4", 556, 912);             // Drake
        TagTrainer(tags, "CHAMPION", 557, 680, 913, 942); // Champion Steven

        // Wally
        TagTrainer(tags, "WALLY", 518, 583, 944, 945, 946, 947);

        // Zinnia
        TagTrainer(tags, "LOREKEEPER", 713, 898);

        // Aarune
        TagTrainer(tags, "EXPERT", 856, 857);

        ImportantTrainers = false;
        TagTrainer(tags, "RIVAL1", 1, 4); // Rival w/ Grass Starter
        TagTrainer(tags, "RIVAL2", 2, 5); // Rival w/ Fire Starter
        TagTrainer(tags, "RIVAL3", 3, 6); // Rival w/ Water Starter

        // Gym Trainers (Tagged in order of appearance on Bulbapedia's lists)
        if (rTypeGymTrainers)
        {
            TagTrainer(tags, "GYM1", 562, 22, 667);
            TagTrainer(tags, "GYM2", 60, 56, 59);
            TagTrainer(tags, "GYM3", 34, 568, 614, 35);
            TagTrainer(tags, "GYM4", 81, 824, 83, 615, 823, 613, 85);
            TagTrainer(tags, "GYM5", 63, 67, 64, 68, 65, 69, 66);
            TagTrainer(tags, "GYM6", 115, 517, 516, 118, 730);
            TagTrainer(tags, "GYM7", 157, 226, 320, 159, 225, 158);
            TagTrainer(tags, "GYM8", 647, 342, 594, 646, 338, 339, 340, 341);
        }
        return tags;
    }

    private string[] GetTagsXY()
    {
        string[] tags = Enumerable.Repeat("", trdata.Length).ToArray();
        ImportantTrainers = true;

        // Rival Battles
        TagTrainer(tags, "RIVAL1", 130, 184, 329, 332, 335, 338, 341, 435, 519, 604, 575, 578, 581, 584, 587, 590, 593, 596, 599, 607); // Rival w/ Fire Starter
        TagTrainer(tags, "RIVAL2", 131, 185, 330, 333, 336, 339, 342, 436, 520, 605, 576, 579, 582, 585, 588, 591, 594, 597, 600, 608); // Rival w/ Water Starter
        TagTrainer(tags, "RIVAL3", 132, 186, 331, 334, 337, 340, 343, 437, 521, 606, 577, 580, 583, 586, 589, 592, 595, 598, 601, 609); // Rival w/ Grass Starter

        // Team Flare Admins
        TagTrainer(tags, "FLAREBOSS", 303, 525, 526); // Lysandre
        TagTrainer(tags, "FLARE1", 175, 344);         // Aliana
        TagTrainer(tags, "FLARE2", 350, 351);         // Bryony
        TagTrainer(tags, "FLARE3", 348, 349);         // Celosia
        TagTrainer(tags, "FLARE4", 346, 347);         // Mable
        TagTrainer(tags, "FLARE5", 345);              // Xerosic

        // Gym Leaders
        TagTrainer(tags, "GYM1", 6, 254, 262);       // Viola
        TagTrainer(tags, "GYM2", 76, 261, 279);      // Grant
        TagTrainer(tags, "GYM3", 21, 255, 263, 613); // Korrina
        TagTrainer(tags, "GYM4", 22, 256, 264);      // Ramos
        TagTrainer(tags, "GYM5", 23, 257, 265);      // Clemont
        TagTrainer(tags, "GYM6", 24, 258, 266);      // Valerie
        TagTrainer(tags, "GYM7", 25, 259, 267);      // Olympia
        TagTrainer(tags, "GYM8", 26, 260, 268);      // Wulfric

        // Elite Four
        TagTrainer(tags, "ELITE1", 269, 273, 507); // Malva
        TagTrainer(tags, "ELITE2", 271, 275);      // Siebold
        TagTrainer(tags, "ELITE3", 187, 272);      // Wikstrom
        TagTrainer(tags, "ELITE4", 270, 274);      // Drasna
        TagTrainer(tags, "CHAMPION", 276, 277);    // Champion Diantha

        // Friends
        TagTrainer(tags, "SHAUNA", 321, 322, 323);
        TagTrainer(tags, "TREVOR", 325, 439);
        TagTrainer(tags, "TIERNO", 324, 438, 573);

        // Professor
        TagTrainer(tags, "PROFESSOR", 327, 328);

        // Suspicious Trainer ???
        TagTrainer(tags, "ESSENTIA", 503, 504, 505, 511, 512, 513, 514, 515); // Emma

        // AZ
        TagTrainer(tags, "AZ", 602);

        // Battle Chatelaines
        TagTrainer(tags, "CHATELAINE1", 559); // Nita
        TagTrainer(tags, "CHATELAINE2", 560); // Evelyn
        TagTrainer(tags, "CHATELAINE3", 561); // Dana
        TagTrainer(tags, "CHATELAINE4", 562); // Morgan

        ImportantTrainers = false;
        TagTrainer(tags, "SHAUNA", 137, 138, 139);
        TagTrainer(tags, "GYM3", 188);

        // Gym Trainers (Tagged in order of appearance on Bulbapedia's lists)
        if (rTypeGymTrainers)
        {
            TagTrainer(tags, "GYM1", 39, 40, 48);
            TagTrainer(tags, "GYM2", 64, 63, 106, 105);
            TagTrainer(tags, "GYM3", 83, 147, 84, 146);
            TagTrainer(tags, "GYM4", 123, 121, 124, 122);
            TagTrainer(tags, "GYM5", 461, 462, 463, 464, 465, 466, 28, 29, 30, 467, 468, 469);
            TagTrainer(tags, "GYM6", 245, 250, 248, 243);
            TagTrainer(tags, "GYM7", 170, 171, 172, 365, 366);
            TagTrainer(tags, "GYM8", 169, 32, 168, 31);
        }
        return tags;
    }


    private bool ImportantTrainers;
    public static SpeciesRandomizer rSpeciesRand;

    // Theme Methods
    private void TagTrainer(string[] trTags, string tag, params int[] ids)
    {
        foreach (int id in ids.Where(id => id >= 0 && id < trTags.Length))
            trTags[id] = tag;

        if (!Tags.Contains(tag))
            Tags.Add(tag);

        if (!ImportantTrainers) return;
        foreach (int id in ids.Where(id => id >= 0 && id < rImportant.Length))
            rImportant[id] = tag;
    }

    private static Dictionary<int, int[]> MegaDictionary;

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

    private int GetRandomType(int trainer)
    {
        if (rTags[trainer].Length != 0)
            return TagTypes[rTags[trainer]];
        if (!rEnsureMEvo.Contains(trainer))
            return (int)(Rand() % types.Length);
        return mEvoTypes[Rand() % mEvoTypes.Length];
    }

    private static int[] GetMegaEvolvableTypes()
    {
        List<int> MEvoTypes = [];
        foreach (int spec in megaEvos)
        {
            if (!MEvoTypes.Contains(Main.SpeciesStat[spec].Types[0]))
                MEvoTypes.Add(Main.SpeciesStat[spec].Types[0]);
            if (!MEvoTypes.Contains(Main.SpeciesStat[spec].Types[1]))
                MEvoTypes.Add(Main.SpeciesStat[spec].Types[1]);
        }
        MEvoTypes.Sort();
        Console.WriteLine("There are " + MEvoTypes.Count + " Types capable of Mega Evolution.");
        return [.. MEvoTypes];
    }

    private void IsFormClosing(object sender, FormClosingEventArgs e)
    {
        WriteFile();
    }

    private void GotoParty(object sender, EventArgs e)
    {
        // When sprite is clicked, jump to that Pokémon.
        tabControl1.SelectedIndex = 1 + Array.IndexOf([PB_Team1, PB_Team2, PB_Team3, PB_Team4, PB_Team5, PB_Team6], sender as PictureBox);
    }
}
