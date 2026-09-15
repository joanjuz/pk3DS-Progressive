using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Windows.Forms;

using pk3DS.Core;
using pk3DS.Core.Randomizers;
using pk3DS.Core.Structures;

namespace pk3DS.WinForms;

public partial class EvolutionEditor7 : Form
{
    public EvolutionEditor7(byte[][] infiles)
    {
        files = infiles;
        InitializeComponent();
        AddNormalizeEvolutionsButton();

        specieslist[0] = movelist[0] = itemlist[0] = "";
        Array.Resize(ref specieslist, Main.Config.MaxSpeciesID + 1);
        string[][] AltForms = Main.Config.Personal.GetFormList(specieslist, Main.Config.MaxSpeciesID);
        specieslist = Main.Config.Personal.GetPersonalEntryList(AltForms, specieslist, Main.Config.MaxSpeciesID, out baseForms, out formVal);

        string[] evolutionMethods =
        [
            "",
            "Level Up with Friendship",
            "Level Up at Morning with Friendship",
            "Level Up at Night with Friendship",
            "Level Up",
            "Trade",
            "Trade with Held Item",
            $"Trade for opposite {specieslist[588]}/{specieslist[616]}", // Shelmet&Karrablast
            "Used Item",
            "Level Up (Attack > Defense)",
            "Level Up (Attack = Defense)",
            "Level Up (Attack < Defense)",
            "Level Up (Random < 5)",
            "Level Up (Random > 5)",
            $"Level Up ({specieslist[291]})", // Ninjask
            $"Level Up ({specieslist[292]})", // Shedinja
            "Level Up (Beauty)",
            "Used Item (Male)", // Kirlia->Gallade
            "Used Item (Female)", // Snorunt->Froslass
            "Level Up with Held Item (Day)",
            "Level Up with Held Item (Night)",
            "Level Up with Move",
            "Level Up with Party",
            "Level Up Male",
            "Level Up Female",
            "Level Up at Electric",
            "Level Up at Forest",
            "Level Up at Cold",
            "Level Up with 3DS Upside Down",
            "Level Up with 50 Affection + MoveType",
            $"{typelist[16]} Type in Party",
            "Overworld Rain",
            "Level Up (@) at Morning",
            "Level Up (@) at Night",
            "Level Up Female (SetForm 1)",
            "UNUSED",
            "Level Up Any Time on Version",
            "Level Up Daytime on Version",
            "Level Up Nighttime on Version",
            "Level Up Summit",
        ];

        var evos = new List<string>(evolutionMethods);
        if (Main.Config.USUM)
        {
            evos.AddRange([
                "Level Up (@) Dusk", // 40
                "Level Up (Wormhole)", // 41
                "Used Item (Wormhole)", // 42
            ]);
        }

        mb = [CB_M1, CB_M2, CB_M3, CB_M4, CB_M5, CB_M6, CB_M7, CB_M8];
        pb = [CB_P1, CB_P2, CB_P3, CB_P4, CB_P5, CB_P6, CB_P7, CB_P8];
        rb = [CB_I1, CB_I2, CB_I3, CB_I4, CB_I5, CB_I6, CB_I7, CB_I8];
        fb = [NUD_F1, NUD_F2, NUD_F3, NUD_F4, NUD_F5, NUD_F6, NUD_F7, NUD_F8];
        lb = [NUD_L1, NUD_L2, NUD_L3, NUD_L4, NUD_L5, NUD_L6, NUD_L7, NUD_L8];
        pic = [PB_1, PB_2, PB_3, PB_4, PB_5, PB_6, PB_7, PB_8];

        maxEvoMethod = evos.Count;
        foreach (ComboBox cb in mb) { cb.Items.AddRange(evos.ToArray()); }
        foreach (ComboBox cb in rb) { cb.Items.AddRange(specieslist.Take(Main.Config.MaxSpeciesID + 1).ToArray()); }

        CB_Species.Items.Clear();
        CB_Species.Items.AddRange(specieslist);

        CB_Species.SelectedIndex = 1;
        RandSettings.GetFormSettings(this, GB_Randomizer.Controls);
    }
    private void AddNormalizeEvolutionsButton()
    {
        const int gap = 6;

        B_NormalizeEvolutions = new Button
        {
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Location = new System.Drawing.Point(
                B_EveryLevel.Left,
                B_RandAll.Top - B_RandAll.Height - gap
            ),
            Name = "B_NormalizeEvolutions",
            Size = new System.Drawing.Size(B_EveryLevel.Width, B_RandAll.Height),
            TabIndex = 999,
            Text = "Normalize evolutions",
            UseVisualStyleBackColor = true,
        };

        B_NormalizeEvolutions.Click += B_NormalizeEvolutions_Click;

        Controls.Add(B_NormalizeEvolutions);
        B_NormalizeEvolutions.BringToFront();
    }
    private const int EvoNone = 0;
    private const int EvoFriendship = 1;
    private const int EvoLevelUp = 4;
    private const int EvoUsedItem = 8;
    private const int EvoLevelUpMale = 23;
    private const int EvoLevelUpFemale = 24;

    private readonly record struct EvolutionPatch(
    int Source,
    int Target,
    int Method,
    int Level = 0,
    int Argument = 0,
    sbyte Form = -1,
    string ItemName = "",
    string AltItemName = ""
);

    private static EvolutionPatch L(int source, int target, int level)
    => new(source, target, EvoLevelUp, level);

    private static EvolutionPatch F(int source, int target)
        => new(source, target, EvoFriendship);

    private static EvolutionPatch FL(int source, int target, int level)
        => new(source, target, EvoFriendship, level);

    private static EvolutionPatch M(int source, int target, int level)
        => new(source, target, EvoLevelUpMale, level);

    private static EvolutionPatch H(int source, int target, int level)
        => new(source, target, EvoLevelUpFemale, level);

    private static EvolutionPatch U(int source, int target, string itemName, string altItemName = "")
        => new(source, target, EvoUsedItem, 0, 0, -1, itemName, altItemName);

    private static EvolutionPatch LF(int source, int target, int level, sbyte form)
        => new(source, target, EvoLevelUp, level, 0, form);

    private EvolutionPatch ResolveEvolutionPatchArgument(EvolutionPatch patch)
    {
        if (string.IsNullOrWhiteSpace(patch.ItemName))
            return patch;

        int item = GetItemID(patch.ItemName, patch.AltItemName);

        if (item <= 0)
        {
            WinFormsUtil.Alert(
                "Evolution item not found.",
                $"Could not find item: {patch.ItemName}" +
                (string.IsNullOrWhiteSpace(patch.AltItemName) ? "" : $" / {patch.AltItemName}")
            );

            return patch with { Argument = 0 };
        }

        return patch with { Argument = item };
    }

    private int GetItemID(string englishName, string spanishName = "")
    {
        int item = Array.FindIndex(itemlist, z =>
            string.Equals(z, englishName, StringComparison.OrdinalIgnoreCase));

        if (item > 0)
            return item;

        if (!string.IsNullOrWhiteSpace(spanishName))
        {
            item = Array.FindIndex(itemlist, z =>
                string.Equals(z, spanishName, StringComparison.OrdinalIgnoreCase));

            if (item > 0)
                return item;
        }

        return -1;
    }

    private EvolutionPatch[] GetNormalizedEvolutionPatchesFromTemplate()
    {
        CustomBalanceTemplates.WriteExampleTemplatesIfMissing();

        var templatePatches = CustomBalanceTemplates.LoadEvolutionPatches(7, specieslist);
        if (templatePatches.Length == 0)
            return GetNormalizedEvolutionPatches();

        return templatePatches.Select(ConvertEvolutionPatchRow).ToArray();
    }

    private static EvolutionPatch ConvertEvolutionPatchRow(CustomBalanceTemplates.EvolutionPatchRow row)
    {
        int method = ResolveEvolutionMethod(row.Method);
        return new EvolutionPatch(row.Source, row.Target, method, row.Level, row.Argument, row.Form, row.ItemName, row.AltItemName);
    }

    private static int ResolveEvolutionMethod(string value)
    {
        value = (value ?? string.Empty).Trim();

        if (int.TryParse(value, out int method))
            return method;

        return value.ToLowerInvariant() switch
        {
            "level" or "levelup" or "l" => EvoLevelUp,
            "friendship" or "friend" or "f" => EvoFriendship,
            "malelevel" or "levelmale" or "male" or "m" => EvoLevelUpMale,
            "femalelevel" or "levelfemale" or "female" or "h" => EvoLevelUpFemale,
            "useditem" or "item" or "stone" or "u" => EvoUsedItem,
            _ => EvoLevelUp,
        };
    }

    private static bool AddOrUpdateEvolution(EvolutionSet7 set, EvolutionPatch patch)
    {
        // Si ya existe exactamente esta evolución alternativa, la actualiza.
        for (int i = 0; i < set.PossibleEvolutions.Length; i++)
        {
            var evo = set.PossibleEvolutions[i];

            if (evo.Species != patch.Target)
                continue;

            if (evo.Method != patch.Method)
                continue;

            // Si es evolución por objeto, no queremos sobrescribir otra piedra distinta.
            if (patch.Method == EvoUsedItem && evo.Argument != patch.Argument)
                continue;

            set.PossibleEvolutions[i].Species = patch.Target;
            set.PossibleEvolutions[i].Method = patch.Method;
            set.PossibleEvolutions[i].Argument = patch.Argument;
            set.PossibleEvolutions[i].Level = patch.Level;
            set.PossibleEvolutions[i].Form = patch.Form;

            return true;
        }

        // Busca un espacio vacío y agrega la evolución alternativa.
        for (int i = 0; i < set.PossibleEvolutions.Length; i++)
        {
            var evo = set.PossibleEvolutions[i];

            if (evo.Species != 0 || evo.Method != EvoNone)
                continue;

            set.PossibleEvolutions[i].Species = patch.Target;
            set.PossibleEvolutions[i].Method = patch.Method;
            set.PossibleEvolutions[i].Argument = patch.Argument;
            set.PossibleEvolutions[i].Level = patch.Level;
            set.PossibleEvolutions[i].Form = patch.Form;

            return true;
        }

        return false;
    }

    private static void CleanupLegacyNormalizeMistakes(EvolutionSet7 set, int source)
    {
        for (int i = 0; i < set.PossibleEvolutions.Length; i++)
        {
            var evo = set.PossibleEvolutions[i];

            bool remove =
                // Versión vieja incorrecta: Dusclops nivel 36 -> Dusknoir.
                source == 356 && evo.Species == 477 && evo.Method == EvoLevelUp ||

                // Versión vieja incorrecta: Luvdisc amistad -> Alomomola.
                source == 370 && evo.Species == 594 && evo.Method == EvoFriendship;

            if (!remove)
                continue;

            ClearEvolutionSlot(set, i);
        }
    }

    private static void ClearEvolutionSlot(EvolutionSet7 set, int slot)
    {
        set.PossibleEvolutions[slot].Species = 0;
        set.PossibleEvolutions[slot].Method = EvoNone;
        set.PossibleEvolutions[slot].Argument = 0;
        set.PossibleEvolutions[slot].Level = 0;
        set.PossibleEvolutions[slot].Form = -1;
    }
    private void B_NormalizeEvolutions_Click(object sender, EventArgs e)
    {
        if (DialogResult.Yes != WinFormsUtil.Prompt(
            MessageBoxButtons.YesNo,
            "Normalize evolutions?",
            "This will apply evolution changes from custom_balance_templates. If the template is missing, an example file will be created and the built-in defaults will be used."))
        {
            return;
        }

        SetList();

        int changed = ApplyNormalizedEvolutions();
        RandomizationSessionState.MarkAction("evolutions.normalize");

        GetList();

        WinFormsUtil.Alert(
            "Evolutions normalized!",
            $"{changed} Pokémon evolution entries were updated.");
    }
    private int ApplyNormalizedEvolutions()
    {
        var patches = GetNormalizedEvolutionPatchesFromTemplate();

        int changed = 0;

        foreach (var group in patches.GroupBy(z => z.Source))
        {
            int source = group.Key;

            if (source <= 0 || source >= files.Length)
                continue;

            if (files[source].Length != EvolutionSet7.SIZE)
                continue;

            var set = new EvolutionSet7(files[source]);

            // Limpia errores de la versión anterior del botón, pero NO borra evoluciones originales.
            CleanupLegacyNormalizeMistakes(set, source);

            bool sourceChanged = false;

            foreach (var patch in group)
            {
                var resolvedPatch = ResolveEvolutionPatchArgument(patch);

                if (resolvedPatch.Method == EvoUsedItem && resolvedPatch.Argument <= 0)
                    continue;

                sourceChanged |= AddOrUpdateEvolution(set, resolvedPatch);
            }

            if (!sourceChanged)
                continue;

            files[source] = set.Write();
            changed++;
        }

        return changed;
    }

    private static void ClearEvolutionSet(EvolutionSet7 set)
    {
        for (int i = 0; i < set.PossibleEvolutions.Length; i++)
        {
            set.PossibleEvolutions[i].Species = 0;
            set.PossibleEvolutions[i].Method = EvoNone;
            set.PossibleEvolutions[i].Argument = 0;
            set.PossibleEvolutions[i].Level = 0;
            set.PossibleEvolutions[i].Form = -1;
        }
    }
    private static EvolutionPatch[] GetNormalizedEvolutionPatches()
    {
        return
        [
            // Primera generación
            L(25, 26, 30),
        L(30, 31, 30),
        L(33, 34, 30),
        L(35, 36, 30),
        L(37, 38, 30),
        L(39, 40, 30),
        L(44, 45, 30),
        F(44, 182),
        L(46, 47, 15),
        L(48, 49, 24),
        L(54, 55, 24),
        L(58, 59, 36),
        F(61, 62),
        L(61, 186, 34),
        L(64, 65, 36),
        L(66, 67, 24),
        L(67, 68, 36),
        L(70, 71, 30),
        L(74, 75, 22),
        L(75, 76, 36),
        L(77, 78, 24),
        L(79, 80, 30),
        F(79, 199),
        L(81, 82, 24),
        L(82, 462, 36),
        L(84, 85, 24),
        L(86, 87, 26),
        L(88, 89, 30),
        L(90, 91, 30),
        L(93, 94, 36),
        L(95, 208, 34),
        L(102, 103, 30),
        L(108, 463, 30),
        L(109, 110, 28),
        L(111, 112, 28),
        L(112, 464, 36),
        L(440, 113, 26),
        L(113, 242, 36),
        L(114, 465, 36),
        L(116, 117, 24),
        L(117, 230, 36),
        L(118, 119, 24),
        L(120, 121, 30),
        L(123, 212, 34),
        L(238, 124, 24),
        L(239, 125, 26),
        F(239, 125),
        L(125, 466, 36),
        L(240, 126, 26),
        F(240, 126),
        L(126, 467, 36),
        L(137, 233, 34),
        L(233, 474, 36),
        L(138, 139, 30),
        L(140, 141, 30),
        L(147, 148, 24),
        L(148, 149, 45),

        // Segunda generación
        L(170, 171, 24),
        L(176, 468, 36),
        L(190, 424, 30),
        L(191, 192, 20),
        L(193, 469, 30),
        L(198, 430, 30),
        L(200, 429, 30),
        L(204, 205, 20),
        L(207, 472, 36),
        L(215, 461, 34),
        L(218, 219, 25),
        L(220, 221, 25),
        L(221, 473, 36),
        L(246, 247, 24),
        L(247, 248, 45),

        // Tercera generación
        L(271, 272, 30),
        L(274, 275, 30),
        F(281, 475),
        L(287, 288, 24),
        L(288, 289, 45),
        L(294, 295, 32),
        L(299, 476, 36),
        L(300, 301, 30),
        L(304, 305, 26),
        L(307, 308, 28),
        L(315, 407, 30),
        L(318, 319, 26),
        L(320, 321, 28),
        L(322, 323, 26),
        L(325, 326, 24),
        L(328, 329, 22),
        L(329, 330, 36),
        L(331, 332, 26),
        L(333, 334, 26),
        L(343, 344, 28),
        L(345, 346, 30),
        L(347, 348, 30),
        L(349, 350, 36),
        L(353, 354, 26),
        L(355, 356, 32),
        U(356, 477, "Dusk Stone", "Piedra Noche"),
        L(360, 202, 24),
        M(361, 362, 30),
        H(361, 478, 30),
        L(363, 364, 26),
        L(364, 365, 36),
        M(366, 367, 28),
        H(366, 368, 28),
        U(370, 594, "Water Stone", "Piedra Agua"),
        L(371, 372, 24),
        L(372, 373, 45),
        L(374, 375, 24),
        L(375, 376, 45),

        // Cuarta generación
        L(406, 315, 15),
        F(406, 315),
        L(408, 409, 30),
        L(410, 411, 30),
        L(415, 416, 21),
        L(431, 432, 21),
        L(433, 358, 24),
        F(433, 358),
        L(434, 435, 26),
        L(436, 437, 26),
        L(438, 185, 20),
        F(438, 185),
        L(439, 122, 24),
        F(439, 122),
        L(443, 444, 24),
        L(444, 445, 45),
        L(446, 143, 35),
        L(447, 448, 32),
        L(451, 452, 30),
        L(453, 454, 26),
        L(458, 226, 24),
        L(456, 457, 26),
        L(459, 460, 30),

        // Quinta generación
        L(511, 512, 30),
        L(513, 514, 30),
        L(515, 516, 30),
        L(517, 518, 30),
        L(532, 533, 24),
        L(533, 534, 36),
        L(546, 547, 30),
        L(548, 549, 30),
        L(551, 552, 20),
        L(552, 553, 32),
        L(557, 558, 28),
        L(559, 560, 30),
        L(564, 565, 30),
        L(566, 567, 37),
        L(568, 569, 30),
        L(572, 573, 30),
        L(574, 575, 24),
        L(575, 576, 36),
        L(577, 578, 26),
        L(578, 579, 36),
        L(580, 581, 24),
        L(582, 583, 24),
        L(583, 584, 36),
        L(585, 586, 24),
        L(588, 589, 30),
        L(592, 593, 30),
        L(599, 600, 24),
        L(600, 601, 36),
        L(602, 603, 24),
        L(603, 604, 36),
        L(605, 606, 30),
        L(607, 608, 24),
        L(608, 609, 36),
        L(610, 611, 28),
        L(611, 612, 38),
        L(613, 614, 30),
        L(616, 617, 30),
        L(619, 620, 32),
        L(622, 623, 28),
        L(624, 625, 36),
        L(627, 628, 36),
        L(629, 630, 36),
        L(633, 634, 24),
        L(634, 635, 45),
        L(636, 637, 38),

        // Sexta generación
        L(667, 668, 28),
        L(670, 671, 30),
        L(674, 675, 32),
        L(680, 681, 30),
        F(682, 683),
        F(684, 685),
        L(686, 687, 30),
        L(688, 689, 28),
        L(690, 691, 36),
        L(692, 693, 28),
        L(694, 695, 30),
        L(696, 697, 30),
        L(698, 699, 30),
        L(704, 705, 24),
        L(705, 706, 45),
        L(708, 709, 30),
        L(710, 711, 30),
        L(714, 715, 36),

        // Séptima generación
        // Nota: Alolan Sandshrew y Alolan Vulpix usan Form = 1 en el resultado.
        // Si en tu ROM los Alola forms están como entradas separadas, habría que ajustar la fuente.
        LF(27, 28, 22, 1),
        LF(37, 38, 30, 1),
        L(737, 738, 36),
        L(739, 740, 30),
        L(753, 754, 30),
        L(757, 758, 28),
        L(762, 763, 34),
        L(769, 770, 30),
        L(782, 783, 24),
        L(783, 784, 45),
    ];
    }
    private readonly byte[][] files;
    private readonly ComboBox[] pb, mb, rb;
    private Button B_NormalizeEvolutions;
    private readonly NumericUpDown[] fb, lb;
    private readonly PictureBox[] pic;
    private int entry = -1;
    private readonly string[] specieslist = Main.Config.GetText(TextName.SpeciesNames);
    private readonly string[] movelist = Main.Config.GetText(TextName.MoveNames);
    private readonly string[] itemlist = Main.Config.GetText(TextName.ItemNames);
    private readonly string[] typelist = Main.Config.GetText(TextName.Types);
    private bool dumping, loading;
    private readonly int[] baseForms, formVal;
    private EvolutionSet7 evo = new(new byte[EvolutionSet7.SIZE]);
    private readonly int maxEvoMethod;

    private void GetList()
    {
        entry = Array.IndexOf(specieslist, CB_Species.Text);
        byte[] input = files[entry];
        if (input.Length != EvolutionSet7.SIZE) return; // error
        evo = new EvolutionSet7(input);

        for (int i = 0; i < evo.PossibleEvolutions.Length; i++)
        {
            if (evo.PossibleEvolutions[i].Method > maxEvoMethod)
                return; // Invalid!

            loading = true;
            fb[i].Value = evo.PossibleEvolutions[i].Form;
            lb[i].Value = evo.PossibleEvolutions[i].Level;
            mb[i].SelectedIndex = evo.PossibleEvolutions[i].Method; // Which will trigger the params cb to reload the valid params list
            pb[i].SelectedIndex = evo.PossibleEvolutions[i].Argument;
            rb[i].SelectedIndex = evo.PossibleEvolutions[i].Species; // Triggers sprite to reload
            loading = false;
            ChangeInto(rb[i], null); // refresh sprite
        }
    }

    private void SetList()
    {
        if (entry < 1 || dumping) return;

        for (int i = 0; i < 8; i++)
        {
            evo.PossibleEvolutions[i].Species = rb[i].SelectedIndex;
            evo.PossibleEvolutions[i].Method = mb[i].SelectedIndex;
            evo.PossibleEvolutions[i].Argument = pb[i].SelectedIndex;
            evo.PossibleEvolutions[i].Form = (sbyte)fb[i].Value;
            evo.PossibleEvolutions[i].Level = (int)lb[i].Value;
        }
        files[entry] = evo.Write();
    }

    private void ChangeEntry(object sender, EventArgs e)
    {
        SetList();
        GetList();
    }

    private void B_RandAll_Click(object sender, EventArgs e)
    {
        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Randomize all resulting species?", "Evolution methods and parameters will stay the same."))
            return;

        SetList();
        // Set up advanced randomization options
        var evos = files.Select(z => new EvolutionSet7(z)).ToArray();
        var evoRand = new EvolutionRandomizer(Main.Config, evos)
        {
            Randomizer =
            {
                rBST = CHK_BST.Checked,
                rEXP = CHK_Exp.Checked,
                rType = CHK_Type.Checked,
                L = CHK_L.Checked,
                E = CHK_E.Checked,
            },
        };
        evoRand.Randomizer.Initialize();
        evoRand.Execute();
        evos.Select(z => z.Write()).ToArray().CopyTo(files, 0);
        GetList();
        RandomizationSessionState.MarkAction("evolutions.randomize");

        WinFormsUtil.Alert("All Pokémon's Evolutions have been randomized!");
    }

    private void B_Trade_Click(object sender, EventArgs e)
    {
        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Remove all trade evolutions?", "Evolution methods will be altered so that evolutions will be possible with only one game."))
            return;

        SetList();
        var evos = files.Select(z => new EvolutionSet7(z)).ToArray();
        var evoRand = new EvolutionRandomizer(Main.Config, evos);
        evoRand.Randomizer.Initialize();
        evoRand.ExecuteTrade();
        evos.Select(z => z.Write()).ToArray().CopyTo(files, 0);
        GetList();
        RandomizationSessionState.MarkAction("evolutions.remove-trade");

        WinFormsUtil.Alert("All trade evolutions have been removed!", "Trade evolutions will now occur after reaching a certain Level, or after leveling up while holding its appropriate trade item.");
    }

    private void B_EveryLevel_Click(object sender, EventArgs e)
    {
        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Modify evolutions?", "This will make it to where your Pokémon will evolve into something random every time it levels up."))
            return;

        SetList();
        var evos = files.Select(z => new EvolutionSet7(z)).ToArray();
        var evoRand = new EvolutionRandomizer(Main.Config, evos)
        {
            Randomizer =
            {
                rBST = CHK_BST.Checked,
                rEXP = CHK_Exp.Checked,
                rType = CHK_Type.Checked,
                L = CHK_L.Checked,
                E = CHK_E.Checked,
            },
        };
        evoRand.Randomizer.Initialize();
        evoRand.ExecuteEvolveEveryLevel();
        evoRand.Execute(); // randomize right after
        evos.Select(z => z.Write()).ToArray().CopyTo(files, 0);
        GetList();
        RandomizationSessionState.MarkAction("evolutions.every-level");
        SystemSounds.Asterisk.Play();
    }

    private void B_Dump_Click(object sender, EventArgs e)
    {
        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Dump all Evolutions to Text File?"))
            return;

        dumping = true;
        string result = "";
        for (int i = 0; i < CB_Species.Items.Count; i++)
        {
            CB_Species.Text = specieslist[i]; // Get new Species
            result += "======" + Environment.NewLine + entry + " " + CB_Species.Text + Environment.NewLine + "======" + Environment.NewLine;
            for (int j = 0; j < 8; j++)
            {
                int methodval = mb[j].SelectedIndex;
                // int param = pb[j].SelectedIndex;
                int poke = rb[j].SelectedIndex;
                if (poke > 0 && methodval > 0)
                {
                    string species = rb[j].Text;
                    int bf = formVal[entry];
                    string param = pb[j].Visible ? " [" + pb[j].Text + "]" : "";
                    if (lb[j].Value > 0)
                        param += $"@ level {lb[j].Value}";
                    string method = mb[j].Text;
                    int f = fb[j].Value == -1 ? bf : (int)fb[j].Value;
                    string form = f == 0 ? "" : "-" + f;

                    result += $"{method} {param} into {species}{form}".Replace("  ", " ") + Environment.NewLine;
                }
            }

            result += Environment.NewLine;
        }
        var sfd = new SaveFileDialog { FileName = "Evolutions.txt", Filter = "Text File|*.txt" };

        SystemSounds.Asterisk.Play();
        if (sfd.ShowDialog() == DialogResult.OK)
        {
            string path = sfd.FileName;
            File.WriteAllText(path, result, Encoding.Unicode);
        }
        dumping = false;
    }

    private void Form_Closing(object sender, FormClosingEventArgs e)
    {
        SetList();
        RandSettings.SetFormSettings(this, GB_Randomizer.Controls);
    }

    private void ChangeMethod(object sender, EventArgs e)
    {
        int op = Array.IndexOf(mb, sender as ComboBox);
        ushort[] methodCase =
        [
            0,0,0,0,1,0,2,0,2,1,1,1,1,1,1,1,5,2,2,2,2,3,4,1,1,0,0,0, // 27, Past Methods
            // New Methods
            1, // 28 - Dark Type Party
            6, // 29 - Affection + MoveType
            1, // 30 - UpsideDown 3DS
            1, // 31 - Overworld Rain
            1, // 32 - Level @ Day
            1, // 33 - Level @ Night
            1, // 34 - Gender Branch
            1, // 35 - UNUSED
            7, 7, 7, // Version Specific
            1,
            1, // 40 - Level Up with Condition (???)
            1, // 41 - Level Up with Condition (???)
            2, // 42 - Use Item with Condition (???)
        ];

        pb[op].Visible = pic[op].Visible = rb[op].Visible = fb[op].Visible = lb[op].Visible = mb[op].SelectedIndex > 0;

        pb[op].Items.Clear();
        int cv = methodCase[mb[op].SelectedIndex];
        switch (cv)
        {
            case 0: // No Parameter Required
                { pb[op].Visible = false; pb[op].Items.Add(""); break; }
            case 1: // Level
                { pb[op].Visible = false; pb[op].Items.Add(""); break; }
            case 2: // Items
                { pb[op].Items.AddRange(itemlist); break; }
            case 3: // Moves
                { pb[op].Items.AddRange(movelist); break; }
            case 4: // Species
                { pb[op].Items.AddRange(specieslist); break; }
            case 5: // 0-255 (Beauty)
                { for (int i = 0; i <= 255; i++) pb[op].Items.Add(i.ToString()); break; }
            case 6:
                { pb[op].Items.AddRange(typelist); break; }
            case 7: // Version
                { for (int i = 0; i <= 255; i++) pb[op].Items.Add(i.ToString()); break; }
        }
        pb[op].SelectedIndex = 0;
    }

    private void ChangeInto(object sender, EventArgs e)
    {
        if (loading || dumping)
            return;
        int index = sender is ComboBox ? Array.IndexOf(rb, sender) : Array.IndexOf(fb, sender);
        int species = Array.IndexOf(specieslist, rb[index].Text);
        int form = (int)fb[index].Value;
        if (form == -1)
            form = baseForms[species];

        pic[index].Image = WinFormsUtil.GetSprite(species, form, 0, 0, Main.Config);
    }
}