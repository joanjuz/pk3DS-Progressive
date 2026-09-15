using pk3DS.Core;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using pk3DS.Core.Structures;

namespace pk3DS.WinForms;

public partial class MoveEditor7 : Form
{
    public MoveEditor7(byte[][] infiles)
    {
        files = infiles;
        movelist[0] = "";
        InitializeComponent();
        Setup();

        AddBalanceMovesButton();
        FixMoveEditorOptionsLayout();
        FixMoveFlagsListLayout();

        RandSettings.GetFormSettings(this, groupBox1.Controls);
    }

    private readonly byte[][] files;
    private Button B_BalanceMoves;
    private readonly string[] types = Main.Config.GetText(TextName.Types);
    private readonly string[] moveflavor = Main.Config.GetText(TextName.MoveFlavor);
    private readonly string[] movelist = Main.Config.GetText(TextName.MoveNames);
    private readonly string[] MoveCategories = ["Status", "Physical", "Special"];
    private readonly string[] StatCategories = ["None", "Attack", "Defense", "Special Attack", "Special Defense", "Speed", "Accuracy", "Evasion", "All",
    ];

    private void AddBalanceMovesButton()
    {
        const int gap = 6;

        B_BalanceMoves = new Button
        {
            Location = new System.Drawing.Point(B_Metronome.Left, B_Metronome.Bottom + gap),
            Name = "B_BalanceMoves",
            Size = new System.Drawing.Size(B_Metronome.Width, 23),
            TabIndex = 999,
            Text = "Balance moves",
            UseVisualStyleBackColor = true,
        };

        B_BalanceMoves.Click += B_BalanceMoves_Click;

        Controls.Add(B_BalanceMoves);
        B_BalanceMoves.BringToFront();

        // Mueve el grupo Options un poco hacia abajo para que no se solape con el nuevo botón.
        int requiredTop = B_BalanceMoves.Bottom + gap;

        if (groupBox1.Top < requiredTop)
            groupBox1.Top = requiredTop;
    }

    private void FixMoveEditorOptionsLayout()
    {
        const int gap = 6;

        B_Table.Location = new System.Drawing.Point(
            B_Table.Left,
            B_Table.Top + 22
        );

        int requiredHeight = B_Table.Bottom + 10;

        if (groupBox1.Height < requiredHeight)
            groupBox1.Height = requiredHeight;
    }
    private void B_BalanceMoves_Click(object sender, EventArgs e)
    {
        if (DialogResult.Yes != WinFormsUtil.Prompt(
            MessageBoxButtons.YesNo,
            "Balance moves?",
            "This will apply move changes from custom_balance_templates. If the template is missing, an example file will be created and the built-in defaults will be used."))
        {
            return;
        }

        SetEntry();

        int changed = ApplyBalancedMoves();
        RandomizationSessionState.MarkAction("moves.balance");

        GetEntry();

        WinFormsUtil.Alert(
            "Moves balanced!",
            $"{changed} moves were updated.");
    }
    private sealed class MoveBalancePatch
    {
        public int Move { get; init; }
        public string Type { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public string Quality { get; init; } = string.Empty;
        public int? Power { get; init; }
        public int? Accuracy { get; init; }
        public int? PP { get; init; }
        public int? Priority { get; init; }
        public int? HitMin { get; init; }
        public int? HitMax { get; init; }
        public int? CriticalStage { get; init; }
        public int? Flinch { get; init; }
        public int? Effect { get; init; }
        public int? Param0x0B { get; init; }
        public int? Heal { get; init; }
        public int? Recoil { get; init; }
        public int? TurnMin { get; init; }
        public int? TurnMax { get; init; }
        public string Targeting { get; init; } = string.Empty;
        public int? Inflict { get; init; }
        public string InflictToken { get; init; } = string.Empty;
        public int? InflictChance { get; init; }
        public bool ClearStatEffects { get; init; }
        public string UserStat { get; init; } = string.Empty;
        public int? UserStatChange { get; init; }
        public int? UserStatChance { get; init; }
        public string TargetStat { get; init; } = string.Empty;
        public int? TargetStatChange { get; init; }
        public int? TargetStatChance { get; init; }
        public string Stat1 { get; init; } = string.Empty;
        public int? Stat1Change { get; init; }
        public int? Stat1Chance { get; init; }
        public string Stat2 { get; init; } = string.Empty;
        public int? Stat2Change { get; init; }
        public int? Stat2Chance { get; init; }
        public string Stat3 { get; init; } = string.Empty;
        public int? Stat3Change { get; init; }
        public int? Stat3Chance { get; init; }
        public bool ClearFlags { get; init; }
        public string SetFlags { get; init; } = string.Empty;
        public string UnsetFlags { get; init; } = string.Empty;
        public bool KingShieldAttackMinusOne { get; init; }

        public int? ZEffect { get; init; }
        public string BattlePatch { get; init; } = string.Empty;
        public string InGameDescription { get; init; } = string.Empty;
    }
    private MoveBalancePatch[] GetBalancedMovePatchesFromTemplate()
    {
        CustomBalanceTemplates.WriteExampleTemplatesIfMissing();

        var templatePatches = CustomBalanceTemplates.LoadMovePatches(7, movelist);
        if (templatePatches.Length == 0)
            return GetBalancedMovePatches();

        return templatePatches.Select(z => new MoveBalancePatch
        {
            Move = z.Move,
            Type = z.Type,
            Category = z.Category,
            Quality = z.Quality,
            Power = z.Power,
            Accuracy = z.Accuracy,
            PP = z.PP,
            Priority = z.Priority,
            HitMin = z.HitMin,
            HitMax = z.HitMax,
            CriticalStage = z.CriticalStage,
            Flinch = z.Flinch,
            Effect = z.Effect,
            Param0x0B = z.Param0x0B,
            Heal = z.Heal,
            Recoil = z.Recoil,
            TurnMin = z.TurnMin,
            TurnMax = z.TurnMax,
            Targeting = z.Targeting,
            Inflict = z.Inflict,
            InflictToken = z.InflictToken,
            InflictChance = z.InflictChance,
            ClearStatEffects = z.ClearStatEffects,
            UserStat = z.UserStat,
            UserStatChange = z.UserStatChange,
            UserStatChance = z.UserStatChance,
            TargetStat = z.TargetStat,
            TargetStatChange = z.TargetStatChange,
            TargetStatChance = z.TargetStatChance,
            Stat1 = z.Stat1,
            Stat1Change = z.Stat1Change,
            Stat1Chance = z.Stat1Chance,
            Stat2 = z.Stat2,
            Stat2Change = z.Stat2Change,
            Stat2Chance = z.Stat2Chance,
            Stat3 = z.Stat3,
            Stat3Change = z.Stat3Change,
            Stat3Chance = z.Stat3Chance,
            ClearFlags = z.ClearFlags,
            SetFlags = z.SetFlags,
            UnsetFlags = z.UnsetFlags,
            KingShieldAttackMinusOne = z.KingShieldAttackMinusOne,
            BattlePatch = z.BattlePatch,
            InGameDescription = z.InGameDescription,

            ZEffect = z.ZEffect,
        }).ToArray();
    }
    private int ApplyBalancedMoves()
    {
        int changed = 0;
        var battlePatchRequests = new List<CustomBattleEffectPatcher.BattlePatchRequest>();
        var appliedPatches = new List<MoveBalancePatch>();

        foreach (var patch in GetBalancedMovePatchesFromTemplate())
        {
            if (patch.Move <= 0 || patch.Move >= files.Length)
                continue;

            byte[] data = files[patch.Move];

            if (data.Length < 0x1E)
                continue;

            ApplyCoreMovePatch(data, patch);
            ApplyGen7ZMovePatch(data, patch);
            ApplyStatPatches(data, patch);
            ApplyFlagPatches(data, patch);
            CustomBattleEffectPatcher.ApplyMoveDataSpecials(7, patch.Move, data, patch.KingShieldAttackMinusOne, patch.BattlePatch);

            bool needsWaterMudSportBattlePatch =
                patch.Param0x0B.HasValue && patch.Move is 300 or 346;

            if (patch.KingShieldAttackMinusOne || !string.IsNullOrWhiteSpace(patch.BattlePatch) || needsWaterMudSportBattlePatch)
            {
                battlePatchRequests.Add(new CustomBattleEffectPatcher.BattlePatchRequest
                {
                    Move = patch.Move,
                    KingShieldAttackMinusOne = patch.KingShieldAttackMinusOne,
                    BattlePatch = patch.BattlePatch,
                    Param0x0B = patch.Param0x0B,
                });
            }

            files[patch.Move] = data;
            appliedPatches.Add(patch);
            changed++;
        }

        int externalPatchResult = CustomBattleEffectPatcher.ApplyExternalPatches(
            7,
            battlePatchRequests,
            (title, message) => WinFormsUtil.Alert(title, message));

        if (externalPatchResult < 0)
        {
            WinFormsUtil.Alert(
                "Move descriptions skipped",
                "At least one Battle.cro patch failed, so in-game move descriptions were not changed. This prevents the game text from describing behavior that was not applied.");
        }
        else
        {
            ApplyInGameMoveDescriptions(appliedPatches);
        }

        return changed;
    }

    private void ApplyInGameMoveDescriptions(IEnumerable<MoveBalancePatch> patches)
    {
        // The CSV descriptions in this project are Spanish. GameText is language-specific;
        // in USUM the Spanish GameText GARC is language index 6 (a/0/3/6).
        const int spanishLanguage = 6;

        var descriptionPatches = patches
            .Where(z => !string.IsNullOrWhiteSpace(z.InGameDescription))
            .Where(z => z.Move > 0)
            .GroupBy(z => z.Move)
            .Select(z => z.Last())
            .ToArray();

        if (descriptionPatches.Length == 0)
            return;

        int originalLanguage = Main.Config.Language;
        bool switchedLanguage = originalLanguage != spanishLanguage;
        string gameTextPath = string.Empty;
        int verifiedCount = 0;
        Exception failure = null;

        try
        {
            // Save the description into the Spanish bank regardless of the language
            // currently selected in pk3DS.
            if (switchedLanguage)
            {
                Main.Config.Language = spanishLanguage;
                Main.Config.InitializeGameText();
            }

            gameTextPath = Path.Combine(
                Main.Config.RomFS,
                Main.Config.GetGARCFileName("gametext"));

            string[] spanishMoveFlavor = Main.Config.GetText(TextName.MoveFlavor);
            bool changed = false;

            foreach (var patch in descriptionPatches)
            {
                if (patch.Move >= spanishMoveFlavor.Length)
                    continue;

                string description = patch.InGameDescription.Trim()
                    .Replace("\\r\\n", "\n")
                    .Replace("\\n", "\n");
                if (spanishMoveFlavor[patch.Move] == description)
                    continue;

                spanishMoveFlavor[patch.Move] = description;
                changed = true;
            }

            if (changed)
            {
                Main.Config.SetText(TextName.MoveFlavor, spanishMoveFlavor);
                Main.SaveGameText();
            }

            // Re-open the GARC from disk and verify the exact text. This distinguishes
            // a memory-only edit from a real RomFS write.
            Main.Config.InitializeGameText();
            string[] persistedMoveFlavor = Main.Config.GetText(TextName.MoveFlavor);

            foreach (var patch in descriptionPatches)
            {
                if (patch.Move >= persistedMoveFlavor.Length)
                    continue;

                string expected = patch.InGameDescription.Trim();
                if (!string.Equals(persistedMoveFlavor[patch.Move], expected, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Move {patch.Move} description was not persisted to the Spanish GameText bank.");
                }

                verifiedCount++;
            }

            // If the editor itself was opened in Spanish, keep its local display cache
            // synchronized with the text that was just verified on disk.
            if (!switchedLanguage)
            {
                foreach (var patch in descriptionPatches)
                {
                    if (patch.Move > 0 && patch.Move < moveflavor.Length)
                        moveflavor[patch.Move] = patch.InGameDescription.Trim();
                }
            }
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            try
            {
                if (switchedLanguage)
                {
                    Main.Config.Language = originalLanguage;
                    Main.Config.InitializeGameText();
                }
                else if (failure is not null)
                {
                    // Discard any unsaved/in-memory text after an error.
                    Main.Config.InitializeGameText();
                }
            }
            catch (Exception restoreEx)
            {
                failure ??= restoreEx;
            }
        }

        if (failure is not null)
        {
            WinFormsUtil.Alert(
                "Move descriptions could not be saved",
                failure.Message);
            return;
        }

        WinFormsUtil.Alert(
            "Move descriptions saved",
            $"Spanish GameText verified on disk for {verifiedCount} move(s).{Environment.NewLine}{gameTextPath}");
    }

    private void ApplyCoreMovePatch(byte[] data, MoveBalancePatch patch)
    {
        SetOptionalByte(data, 0x00, ResolveType(patch.Type));
        SetOptionalByte(data, 0x01, ResolveQuality(patch.Quality));
        SetOptionalByte(data, 0x02, ResolveCategory(patch.Category));

        if (patch.Power.HasValue)
            data[0x03] = (byte)patch.Power.Value;

        if (patch.Accuracy.HasValue)
            data[0x04] = (byte)patch.Accuracy.Value;

        if (patch.PP.HasValue)
            data[0x05] = (byte)patch.PP.Value;

        if (patch.Priority.HasValue)
            data[0x06] = unchecked((byte)(sbyte)patch.Priority.Value);

        if (patch.HitMin.HasValue || patch.HitMax.HasValue)
        {
            int min = patch.HitMin ?? (data[0x07] & 0xF);
            int max = patch.HitMax ?? (data[0x07] >> 4);
            data[0x07] = (byte)((min & 0xF) | ((max & 0xF) << 4));
        }

        int inflict = patch.Inflict ?? ResolveInflict(patch.InflictToken);
        if (inflict >= 0)
            Array.Copy(BitConverter.GetBytes((short)inflict), 0, data, 0x08, 2);

        if (patch.InflictChance.HasValue)
            data[0x0A] = (byte)patch.InflictChance.Value;

        if (patch.Param0x0B.HasValue)
            data[0x0B] = (byte)patch.Param0x0B.Value;

        if (patch.TurnMin.HasValue)
            data[0x0C] = (byte)patch.TurnMin.Value;

        if (patch.TurnMax.HasValue)
            data[0x0D] = (byte)patch.TurnMax.Value;

        if (patch.CriticalStage.HasValue)
            data[0x0E] = (byte)patch.CriticalStage.Value;

        if (patch.Flinch.HasValue)
            data[0x0F] = (byte)patch.Flinch.Value;

        if (patch.Effect.HasValue)
            Array.Copy(BitConverter.GetBytes((ushort)patch.Effect.Value), 0, data, 0x10, 2);

        if (patch.Recoil.HasValue)
            data[0x12] = unchecked((byte)(sbyte)patch.Recoil.Value);

        if (patch.Heal.HasValue)
            data[0x13] = (byte)patch.Heal.Value;

        SetOptionalByte(data, 0x14, ResolveTargeting(patch.Targeting));
    }

    private void ApplyStatPatches(byte[] data, MoveBalancePatch patch)
    {
        if (patch.ClearStatEffects || global::MoveBalanceTemplateSanitizer.ShouldClearStatEffectsBeforeApply(patch))
            ClearMoveStatEffects(data);

        if (!string.IsNullOrWhiteSpace(patch.UserStat))
            ApplyNextStatSlot(data, patch.UserStat, patch.UserStatChange ?? 1, patch.UserStatChance ?? 100);

        if (!string.IsNullOrWhiteSpace(patch.TargetStat))
            ApplyNextStatSlot(data, patch.TargetStat, patch.TargetStatChange ?? -1, patch.TargetStatChance ?? 100);

        ApplyExplicitStatSlot(data, 0, patch.Stat1, patch.Stat1Change, patch.Stat1Chance);
        ApplyExplicitStatSlot(data, 1, patch.Stat2, patch.Stat2Change, patch.Stat2Chance);
        ApplyExplicitStatSlot(data, 2, patch.Stat3, patch.Stat3Change, patch.Stat3Chance);
    }

    private void ApplyFlagPatches(byte[] data, MoveBalancePatch patch)
    {
        if (!patch.ClearFlags && string.IsNullOrWhiteSpace(patch.SetFlags) && string.IsNullOrWhiteSpace(patch.UnsetFlags))
            return;

        var move = new Move7(data);
        uint flags = patch.ClearFlags ? 0 : (uint)move.Flags;
        flags |= ResolveFlags(patch.SetFlags, typeof(MoveFlag7));
        flags &= ~ResolveFlags(patch.UnsetFlags, typeof(MoveFlag7));
        move.Flags = (MoveFlag7)flags;
    }

    private static uint ResolveFlags(string value, Type flagType)
    {
        uint result = 0;
        if (string.IsNullOrWhiteSpace(value))
            return result;

        foreach (string token in CustomBalanceTemplates.SplitTokens(value))
        {
            if (uint.TryParse(token, out uint numeric))
            {
                result |= numeric;
                continue;
            }

            string normalized = CustomBalanceTemplates.NormalizeToken(token);
            foreach (string name in Enum.GetNames(flagType))
            {
                if (CustomBalanceTemplates.NormalizeToken(name) != normalized)
                    continue;

                object parsed = Enum.Parse(flagType, name);
                result |= Convert.ToUInt32(parsed);
                break;
            }
        }

        return result;
    }

    private void ApplyNextStatSlot(byte[] data, string statToken, int change, int chance)
    {
        int stat = ResolveStat(statToken);
        if (stat <= 0)
            return;

        for (int slot = 0; slot < 3; slot++)
        {
            int statOffset = 0x15 + slot;
            if (data[statOffset] != 0)
                continue;

            ApplyStatSlot(data, slot, stat, change, chance);
            return;
        }

        ApplyStatSlot(data, 2, stat, change, chance);
    }

    private void ApplyExplicitStatSlot(byte[] data, int slot, string statToken, int? change, int? chance)
    {
        int stat = ResolveStat(statToken);
        if (stat <= 0)
            return;

        ApplyStatSlot(data, slot, stat, change ?? 0, chance ?? 100);
    }

    private static void ApplyStatSlot(byte[] data, int slot, int stat, int change, int chance)
    {
        data[0x15 + slot] = (byte)stat;
        data[0x18 + slot] = unchecked((byte)(sbyte)change);
        data[0x1B + slot] = (byte)chance;
    }

    private static void SetOptionalByte(byte[] data, int offset, int value)
    {
        if (value >= 0)
            data[offset] = (byte)value;
    }

    private int ResolveType(string value)
        => CustomBalanceTemplates.ResolveToken(value, types, TypeAliases);

    private int ResolveCategory(string value)
        => CustomBalanceTemplates.ResolveToken(value, MoveCategories, CategoryAliases);

    private int ResolveQuality(string value)
        => CustomBalanceTemplates.ResolveToken(value, MoveQualities);

    private int ResolveTargeting(string value)
        => CustomBalanceTemplates.ResolveToken(value, TargetingTypes, TargetingAliases);

    private int ResolveInflict(string value)
        => CustomBalanceTemplates.ResolveToken(value, InflictionTypes, InflictAliases);

    private int ResolveStat(string value)
        => CustomBalanceTemplates.ResolveToken(value, StatCategories, StatAliases);

    private static readonly Dictionary<string, int> TypeAliases = new()
    {
        ["normal"] = 0,
        ["fighting"] = 1,
        ["lucha"] = 1,
        ["flying"] = 2,
        ["volador"] = 2,
        ["poison"] = 3,
        ["veneno"] = 3,
        ["ground"] = 4,
        ["tierra"] = 4,
        ["rock"] = 5,
        ["roca"] = 5,
        ["bug"] = 6,
        ["bicho"] = 6,
        ["ghost"] = 7,
        ["fantasma"] = 7,
        ["steel"] = 8,
        ["acero"] = 8,
        ["fire"] = 9,
        ["fuego"] = 9,
        ["water"] = 10,
        ["agua"] = 10,
        ["grass"] = 11,
        ["planta"] = 11,
        ["electric"] = 12,
        ["electrico"] = 12,
        ["psychic"] = 13,
        ["psiquico"] = 13,
        ["ice"] = 14,
        ["hielo"] = 14,
        ["dragon"] = 15,
        ["dark"] = 16,
        ["siniestro"] = 16,
        ["fairy"] = 17,
        ["hada"] = 17,
    };

    private static readonly Dictionary<string, int> CategoryAliases = new()
    {
        ["status"] = 0,
        ["estado"] = 0,
        ["physical"] = 1,
        ["fisico"] = 1,
        ["special"] = 2,
        ["especial"] = 2,
    };

    private static readonly Dictionary<string, int> StatAliases = new()
    {
        ["none"] = 0,
        ["ninguno"] = 0,
        ["attack"] = 1,
        ["atk"] = 1,
        ["ataque"] = 1,
        ["defense"] = 2,
        ["def"] = 2,
        ["defensa"] = 2,
        ["specialattack"] = 3,
        ["spatk"] = 3,
        ["specialatk"] = 3,
        ["ataqueespecial"] = 3,
        ["atakespecial"] = 3,
        ["specialdefense"] = 4,
        ["spdef"] = 4,
        ["specialdef"] = 4,
        ["defensaespecial"] = 4,
        ["speed"] = 5,
        ["spe"] = 5,
        ["velocidad"] = 5,
        ["accuracy"] = 6,
        ["precision"] = 6,
        ["evasion"] = 7,
        ["all"] = 8,
        ["todo"] = 8,
        ["todos"] = 8,
    };

    private static readonly Dictionary<string, int> InflictAliases = new()
    {
        ["none"] = 0,
        ["ninguno"] = 0,
        ["paralyze"] = 1,
        ["paralysis"] = 1,
        ["paralisis"] = 1,
        ["paralizar"] = 1,
        ["sleep"] = 2,
        ["dormir"] = 2,
        ["sueno"] = 2,
        ["freeze"] = 3,
        ["congelar"] = 3,
        ["congelacion"] = 3,
        ["burn"] = 4,
        ["quemar"] = 4,
        ["quemadura"] = 4,
        ["poison"] = 5,
        ["veneno"] = 5,
        ["envenenar"] = 5,
        ["confusion"] = 6,
        ["confundir"] = 6,
        ["attract"] = 7,
        ["atraccion"] = 7,
        ["nightmare"] = 9,
        ["pesadilla"] = 9,
        ["curse"] = 10,
        ["maldicion"] = 10,
        ["taunt"] = 11,
        ["mofa"] = 11,
        ["torment"] = 12,
        ["tormento"] = 12,
        ["disable"] = 13,
        ["anulacion"] = 13,
        ["yawn"] = 14,
        ["bostezo"] = 14,
        ["healblock"] = 15,
        ["anticura"] = 15,
        ["detect"] = 17,
        ["proteccion"] = 17,
        ["leechseed"] = 18,
        ["drenadoras"] = 18,
        ["embargo"] = 19,
        ["perishsong"] = 20,
        ["cantoperish"] = 20,
        ["cantomortal"] = 20,
        ["ingrain"] = 21,
        ["arraigo"] = 21,
    };

    private static readonly Dictionary<string, int> TargetingAliases = new()
    {
        ["single"] = 0,
        ["selected"] = 0,
        ["target"] = 0,
        ["adjacent"] = 0,
        ["seleccionado"] = 0,
        ["ally"] = 1,
        ["companero"] = 1,
        ["adjacentally"] = 2,
        ["singlefoe"] = 3,
        ["foe"] = 3,
        ["rival"] = 3,
        ["opponent"] = 3,
        ["everyonebutuser"] = 4,
        ["allfoes"] = 5,
        ["allenemies"] = 5,
        ["enemigos"] = 5,
        ["rivales"] = 5,
        ["allallies"] = 6,
        ["aliados"] = 6,
        ["self"] = 7,
        ["user"] = 7,
        ["usuario"] = 7,
        ["allpokemon"] = 8,
        ["allfield"] = 8,
        ["todos"] = 8,
        ["entirefield"] = 10,
        ["field"] = 10,
        ["campo"] = 10,
        ["opponentfield"] = 11,
        ["foefield"] = 11,
        ["camporival"] = 11,
        ["userfield"] = 12,
        ["allyfield"] = 12,
        ["campousuario"] = 12,
    };

    private static void ApplyGen7ZMovePatch(byte[] data, MoveBalancePatch patch)
    {
        if (!patch.ZEffect.HasValue)
            return;

        // ZEffect is stored separately from the normal move Effect.
        // Move7 writes the Gen7 Z-Move metadata into the same backing byte[].
        Move7 move = new(data);
        move.ZEffect = patch.ZEffect.Value;
    }

    private static void ClearMoveStatEffects(byte[] data)
    {
        // Stat categories: None, Attack, Defense, Sp. Atk, Sp. Def, Speed, Accuracy, Evasion, All.
        data[0x15] = 0;
        data[0x16] = 0;
        data[0x17] = 0;

        // Stat stages.
        data[0x18] = 0;
        data[0x19] = 0;
        data[0x1A] = 0;

        // Stat effect chances.
        data[0x1B] = 0;
        data[0x1C] = 0;
        data[0x1D] = 0;
    }

    private static int PatchKingShieldAttackDropInDllBattle(byte[] cro)
    {
        byte[] oldPattern =
        {
        0x01, 0x20, 0xA0, 0xE3,
        0xFE, 0x30, 0xA0, 0xE3
    };

        byte[] alreadyPatched =
        {
        0x01, 0x20, 0xA0, 0xE3,
        0xFF, 0x30, 0xA0, 0xE3
    };

        int offset = FindBytes(cro, oldPattern);

        if (offset >= 0)
        {
            cro[offset + 4] = 0xFF;
            return 1;
        }

        if (FindBytes(cro, alreadyPatched) >= 0)
            return 0;

        return 0; // Gen7: Gen6 DLLBattle pattern not found; skip CRO patch.
    }

    private static int FindBytes(byte[] data, byte[] pattern)
    {
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;

            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
                return i;
        }

        return -1;
    }
    private static MoveBalancePatch[] GetBalancedMovePatches()
    {
        const int Confusion = 6;

        return
        [
            // Página 1 del PDF
            // Corte: 70 power, 100 accuracy, 15 PP, critical stage 1.
            new MoveBalancePatch { Move = 15, Power = 70, Accuracy = 100, PP = 15, CriticalStage = 1 },

        // Golpe Roca
        new MoveBalancePatch { Move = 249, Power = 60, Accuracy = 100 },

        // Vuelo
        new MoveBalancePatch { Move = 19, Power = 100, Accuracy = 100 },

        // Espora
        new MoveBalancePatch { Move = 147, Accuracy = 85 },

        // Deslumbrar
        new MoveBalancePatch { Move = 137, Accuracy = 90, PP = 15 },

        // Torm. Diamantes / Diamond Storm: remover boost secundario.
        new MoveBalancePatch { Move = 591, Power = 95, Accuracy = 95, PP = 10, ClearStatEffects = true },

        // Ala Mortífera / Oblivion Wing
        new MoveBalancePatch { Move = 613, Power = 80, Accuracy = 100, Heal = 50 },

        // Cháchara / Chatter
        new MoveBalancePatch { Move = 448, Power = 80, Accuracy = 100, PP = 15, Inflict = Confusion, InflictChance = 15 },

        // Chupavidas / Leech Life
        new MoveBalancePatch { Move = 141, Power = 70, Accuracy = 100, PP = 15 },

        // Bomba Fango / Sludge Bomb
        new MoveBalancePatch { Move = 188, Power = 60, Accuracy = 100 },

        // Danza Llama / Fire Spin
        new MoveBalancePatch { Move = 552, Power = 70, Accuracy = 100 },

        // Shuriken de Agua / Water Shuriken: 20 por golpe.
        new MoveBalancePatch { Move = 594, Power = 20, Accuracy = 100 },

        // PP 7
        new MoveBalancePatch { Move = 182, PP = 7 }, // Protección / Protect
        new MoveBalancePatch { Move = 197, PP = 7 }, // Detección / Detect
        new MoveBalancePatch { Move = 596, PP = 7 }, // Barrera Espinosa / Spiky Shield
        new MoveBalancePatch { Move = 588, PP = 7, KingShieldAttackMinusOne = true }, // Escudo Real / King's Shield
        new MoveBalancePatch { Move = 476, PP = 7 }, // Polvo Ira / Rage Powder
        new MoveBalancePatch { Move = 266, PP = 7 }, // Señuelo / Follow Me
        new MoveBalancePatch { Move = 502, PP = 7 }, // Cambio Banda / Ally Switch
        new MoveBalancePatch { Move = 73,  PP = 7 }, // Drenadoras / Leech Seed

        // Página 2 del PDF
        new MoveBalancePatch { Move = 281, PP = 7 }, // Bostezo / Yawn

        // PP 5
        new MoveBalancePatch { Move = 270, PP = 5 }, // Refuerzo / Helping Hand
        new MoveBalancePatch { Move = 105, PP = 5 }, // Recuperación / Recover
        new MoveBalancePatch { Move = 234, PP = 5 }, // Sol Matinal / Morning Sun
        new MoveBalancePatch { Move = 236, PP = 5 }, // Luz Lunar / Moonlight
        new MoveBalancePatch { Move = 208, PP = 5 }, // Batido / Milk Drink
        new MoveBalancePatch { Move = 303, PP = 5 }, // Relajo / Slack Off
        new MoveBalancePatch { Move = 235, PP = 5 }, // Síntesis / Synthesis
        new MoveBalancePatch { Move = 456, PP = 5 }, // Auxilio / Heal Order
        new MoveBalancePatch { Move = 135, PP = 5 }, // Amortiguador / Soft-Boiled
        new MoveBalancePatch { Move = 355, PP = 5 }, // Respiro / Roost
        new MoveBalancePatch { Move = 505, PP = 5 }, // Pulso Cura / Heal Pulse
    ];
    }
    private void FixMoveFlagsListLayout()
    {
        const int gap = 6;

        CheckedListBox flagsList = null;

        foreach (Control control in Controls)
        {
            if (control is CheckedListBox checkedListBox)
            {
                flagsList = checkedListBox;
                break;
            }
        }

        if (flagsList is null)
            return;

        // Poner la lista debajo del botón Export Table.
        flagsList.Top = B_Table.Bottom + gap;

        // Reducir la altura, manteniendo scroll.
        int bottomMargin = 8;
        int newHeight = ClientSize.Height - flagsList.Top - bottomMargin;

        if (newHeight < 120)
            newHeight = 120;

        flagsList.Height = newHeight;
    }

    private static readonly string[] TargetingTypes =
    [
        "Single Adjacent Ally/Foe",
        "Any Ally", "Any Adjacent Ally", "Single Adjacent Foe", "Everyone but User", "All Foes",
        "All Allies", "Self", "All Pokémon on Field", "Single Adjacent Foe (2)", "Entire Field",
        "Opponent's Field", "User's Field", "Self",
    ];

    private static readonly string[] InflictionTypes =
    [
        "None",
        "Paralyze", "Sleep", "Freeze", "Burn", "Poison",
        "Confusion", "Attract", "Capture", "Nightmare", "Curse",
        "Taunt", "Torment", "Disable", "Yawn", "Heal Block",
        "?", "Detect", "Leech Seed", "Embargo", "Perish Song",
        "Ingrain", "??? 0x16", "??? 0x17", "Mute",
    ];

    private static readonly string[] MoveQualities =
    [
        "Only DMG",
        "No DMG -> Inflict Status", "No DMG -> -Target/+User Stat", "No DMG | Heal User", "DMG | Inflict Status", "No DMG | STATUS | +Target Stat",
        "DMG | -Target Stat", "DMG | +User Stat", "DMG | Absorbs DMG", "One-Hit KO", "Affects Whole Field",
        "Affect One Side of the Field", "Forces Target to Switch", "Unique Effect",
    ];

    private static readonly string[] ZMoveEffects =
    [
        "None",
        "+1 Attack",
        "+2 Attack",
        "+3 Attack",
        "+1 Defense",
        "+2 Defense",
        "+3 Defense",
        "+1 Special Attack",
        "+2 Special Attack",
        "+3 Special Attack",
        "+1 Special Defense",
        "+2 Special Defense",
        "+3 Special Defense",
        "+1 Speed",
        "+2 Speed",
        "+3 Speed",
        "+1 Accuracy",
        "+2 Accuracy",
        "+3 Accuracy",
        "+1 Evasiveness",
        "+2 Evasiveness",
        "+3 Evasiveness",
        "+1 to all (except Accuracy or Evasiveness)",
        "+2 to all (except Accuracy or Evasiveness)",
        "+3 to all (except Accuracy or Evasiveness)",
        "raises critical-hit ratio two stages",
        "resets lowered stats of the user",
        "recovers all of user's HP",
        "recovers all Hp of the Pokémon switching-in (Memento and Parting Shot)",
        "makes the user the center of attention",
        "only on Curse: recovers all HP if the user's a Ghost type, +1 Attack otherwise",
    ];

    private void Setup()
    {
        char[] ps = ['P', 'S']; // Distinguish Physical/Special Z-Moves
        for (int i = 622; i < 658; i++)
            movelist[i] += $" ({ps[i % 2]})";
        CB_Move.Items.AddRange(movelist);
        CB_Type.Items.AddRange(types);
        CB_Category.Items.AddRange(MoveCategories);
        CB_Stat1.Items.AddRange(StatCategories);
        CB_Stat2.Items.AddRange(StatCategories);
        CB_Stat3.Items.AddRange(StatCategories);
        CB_Targeting.Items.AddRange(TargetingTypes);
        CB_Quality.Items.AddRange(MoveQualities);
        CB_Inflict.Items.AddRange(InflictionTypes);
        CB_ZMove.Items.AddRange(movelist);
        var flagnames = Enum.GetNames(typeof(MoveFlag7)).Skip(1).ToArray();
        CLB_Flags.Items.AddRange(flagnames);
        CB_ZEffect.Items.AddRange(ZMoveEffects);
        CB_Inflict.Items.Add("Special");
        var refreshtypes = Enum.GetNames(typeof(RefreshType));
        CB_AfflictRefresh.Items.AddRange(refreshtypes);

        CB_Move.Items.RemoveAt(0);
        CB_Move.SelectedIndex = 0;
    }

    private int entry = -1;

    private void ChangeEntry(object sender, EventArgs e)
    {
        SetEntry();
        entry = Array.IndexOf(movelist, CB_Move.Text);
        GetEntry();
    }

    private void GetEntry()
    {
        if (entry < 1) return;
        byte[] data = files[entry];
        {
            RTB.Text = moveflavor[entry].Replace("\\n", Environment.NewLine);

            CB_Type.SelectedIndex = data[0x00];
            CB_Quality.SelectedIndex = data[0x01];
            CB_Category.SelectedIndex = data[0x02];
            NUD_Power.Value = data[0x3];
            NUD_Accuracy.Value = data[0x4];
            NUD_PP.Value = data[0x05];
            NUD_Priority.Value = (sbyte)data[0x06];
            NUD_HitMin.Value = data[0x7] & 0xF;
            NUD_HitMax.Value = data[0x7] >> 4;
            short inflictVal = BitConverter.ToInt16(data, 0x08);
            CB_Inflict.SelectedIndex = inflictVal < 0 ? CB_Inflict.Items.Count - 1 : inflictVal;
            NUD_Inflict.Value = data[0xA];
            NUD_0xB.Value = data[0xB]; // 0xB ~ Something to deal with skipImmunity
            NUD_TurnMin.Value = data[0xC];
            NUD_TurnMax.Value = data[0xD];
            NUD_CritStage.Value = data[0xE];
            NUD_Flinch.Value = data[0xF];
            NUD_Effect.Value = BitConverter.ToUInt16(data, 0x10);
            NUD_Recoil.Value = (sbyte)data[0x12];
            NUD_Heal.Value = data[0x13];

            CB_Targeting.SelectedIndex = data[0x14];
            CB_Stat1.SelectedIndex = data[0x15];
            CB_Stat2.SelectedIndex = data[0x16];
            CB_Stat3.SelectedIndex = data[0x17];
            NUD_Stat1.Value = (sbyte)data[0x18];
            NUD_Stat2.Value = (sbyte)data[0x19];
            NUD_Stat3.Value = (sbyte)data[0x1A];
            NUD_StatP1.Value = data[0x1B];
            NUD_StatP2.Value = data[0x1C];
            NUD_StatP3.Value = data[0x1D];

            var move = new Move7(data);
            CB_ZMove.SelectedIndex = move.ZMove;
            NUD_ZPower.Value = move.ZPower;
            CB_ZEffect.SelectedIndex = move.ZEffect;
            CB_AfflictRefresh.SelectedIndex = (int)move.RefreshAfflictType;
            NUD_RefreshAfflictPercent.Value = move.RefreshAfflictPercent;

            var flags = (uint)move.Flags;
            for (int i = 0; i < CLB_Flags.Items.Count; i++)
                CLB_Flags.SetItemChecked(i, ((flags >> i) & 1) == 1);
        }
    }

    private void SetEntry()
    {
        if (entry < 1) return;
        byte[] data = files[entry];
        {
            data[0x00] = (byte)CB_Type.SelectedIndex;
            data[0x01] = (byte)CB_Quality.SelectedIndex;
            data[0x02] = (byte)CB_Category.SelectedIndex;
            data[0x03] = (byte)NUD_Power.Value;
            data[0x04] = (byte)NUD_Accuracy.Value;
            data[0x05] = (byte)NUD_PP.Value;
            data[0x06] = (byte)(int)NUD_Priority.Value;
            data[0x07] = (byte)((byte)NUD_HitMin.Value | ((byte)NUD_HitMax.Value << 4));
            int inflictval = CB_Inflict.SelectedIndex; if (inflictval == CB_Inflict.Items.Count) inflictval = -1;
            Array.Copy(BitConverter.GetBytes((short)inflictval), 0, data, 0x08, 2);
            data[0x0A] = (byte)NUD_Inflict.Value;
            data[0x0B] = (byte)NUD_0xB.Value;
            data[0x0C] = (byte)NUD_TurnMin.Value;
            data[0x0D] = (byte)NUD_TurnMax.Value;
            data[0x0E] = (byte)NUD_CritStage.Value;
            data[0x0F] = (byte)NUD_Flinch.Value;
            Array.Copy(BitConverter.GetBytes((ushort)NUD_Effect.Value), 0, data, 0x10, 2);
            data[0x12] = (byte)(int)NUD_Recoil.Value;
            data[0x13] = (byte)NUD_Heal.Value;
            data[0x14] = (byte)CB_Targeting.SelectedIndex;
            data[0x15] = (byte)CB_Stat1.SelectedIndex;
            data[0x16] = (byte)CB_Stat2.SelectedIndex;
            data[0x17] = (byte)CB_Stat3.SelectedIndex;
            data[0x18] = (byte)(int)NUD_Stat1.Value;
            data[0x19] = (byte)(int)NUD_Stat2.Value;
            data[0x1A] = (byte)(int)NUD_Stat3.Value;
            data[0x1B] = (byte)NUD_StatP1.Value;
            data[0x1C] = (byte)NUD_StatP2.Value;
            data[0x1D] = (byte)NUD_StatP3.Value;

            var move = new Move7(data)
            {
                ZMove = CB_ZMove.SelectedIndex,
                ZPower = (int)NUD_ZPower.Value,
                ZEffect = CB_ZEffect.SelectedIndex,
                RefreshAfflictPercent = (int)NUD_RefreshAfflictPercent.Value,
                RefreshAfflictType = (RefreshType)CB_AfflictRefresh.SelectedIndex,
            };

            uint flagval = 0;
            for (int i = 0; i < CLB_Flags.Items.Count; i++)
                flagval |= CLB_Flags.GetItemChecked(i) ? 1u << i : 0;
            move.Flags = (MoveFlag7)flagval;
        }
        files[entry] = data;
    }

    private void CloseForm(object sender, FormClosingEventArgs e)
    {
        SetEntry();
        RandSettings.SetFormSettings(this, groupBox1.Controls);
    }

    private void B_Table_Click(object sender, EventArgs e)
    {
        var items = files.Select(z => new Move7(z));
        Clipboard.SetText(TableUtil.GetTable(items, movelist));
        System.Media.SystemSounds.Asterisk.Play();
    }

    private void B_RandAll_Click(object sender, EventArgs e)
    {
        if (!CHK_Category.Checked && !CHK_Type.Checked)
        {
            WinFormsUtil.Alert("Cannot randomize Moves.", "Please check any of the options on the right to randomize Moves.");
            return;
        }

        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Randomize Moves? Cannot undo.", "Double check options on the right before continuing.") != DialogResult.Yes) return;
        Random rnd = Util.Rand;
        for (int i = 0; i < CB_Move.Items.Count; i++)
        {
            CB_Move.SelectedIndex = i; // Get new Move
            if (i is 165 or 174) continue; // Don't change Struggle or Curse

            // Change Damage Category if Not Status
            if (CB_Category.SelectedIndex > 0 && CHK_Category.Checked) // Not Status
                CB_Category.SelectedIndex = rnd.Next(1, 3);

            // Change Move Type
            if (CHK_Type.Checked)
                CB_Type.SelectedIndex = rnd.Next(0, 18);
        }
        RandomizationSessionState.MarkAction("moves.randomize");
        WinFormsUtil.Alert("All Moves have been randomized!");
    }

    private void B_Metronome_Click(object sender, EventArgs e)
    {
        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Play using Metronome Mode?", "This will set the Base PP for every other Move to 0!") != DialogResult.Yes) return;

        for (int i = 0; i < CB_Move.Items.Count; i++)
        {
            CB_Move.SelectedIndex = i;
            if (CB_Move.SelectedIndex is not (117 and 32))
                NUD_PP.Value = 0;
            if (CB_Move.SelectedIndex == 117)
                NUD_PP.Value = 40;
            if (CB_Move.SelectedIndex == 32)
                NUD_PP.Value = 1;
        }
        CB_Move.SelectedIndex = 0;
        RandomizationSessionState.MarkAction("moves.metronome");
        WinFormsUtil.Alert("All Moves have had their Base PP values modified!");
    }

    private static void SetKingShieldAttackDrop(byte[] data)
    {
        if (data.Length <= 0x1D)
            return;

        // Clear the three normal stat-change slots.
        data[0x15] = 0;
        data[0x16] = 0;
        data[0x17] = 0;
        data[0x18] = 0;
        data[0x19] = 0;
        data[0x1A] = 0;
        data[0x1B] = 0;
        data[0x1C] = 0;
        data[0x1D] = 0;

        // Visual move-data side: Attack -1 at 100%.
        // Gen7 still needs Battle.cro patched for the real in-battle behavior.
        data[0x15] = 1; // Attack
        data[0x18] = unchecked((byte)-1);
        data[0x1B] = 100;
    }
    private static int PatchKingShieldAttackDropInGen7BattleCroCandidate2()
    {
        string battleCroPath = FindGen7BattleCroPathForKingShield();
        if (string.IsNullOrWhiteSpace(battleCroPath) || !File.Exists(battleCroPath))
            return -1;

        byte[] data = File.ReadAllBytes(battleCroPath);

        // Candidate 2 verified in-game:
        // change mov r0, #0xFE (-2) to mov r0, #0xFF (-1) in Battle.cro.
        byte[] pattern =
        {
            0x01, 0x20, 0xA0, 0xE3,
            0xFE, 0x00, 0xA0, 0xE3,
            0x04, 0x20, 0xC4, 0xE5,
            0x11, 0x00, 0xC4, 0xE5,
            0x26, 0x00, 0x81, 0xE3,
            0x10, 0x20, 0xC4, 0xE5,
        };

        byte[] alreadyPatched =
        {
            0x01, 0x20, 0xA0, 0xE3,
            0xFF, 0x00, 0xA0, 0xE3,
            0x04, 0x20, 0xC4, 0xE5,
            0x11, 0x00, 0xC4, 0xE5,
            0x26, 0x00, 0x81, 0xE3,
            0x10, 0x20, 0xC4, 0xE5,
        };

        int changed = PatchKingShieldPatternByte(data, pattern, 4, 0xFF);
        if (changed < 0)
            return FindKingShieldBytes(data, alreadyPatched) >= 0 ? 0 : -1;

        string backup = battleCroPath + ".bak_kings_shield_minus_one";
        if (!File.Exists(backup))
            File.Copy(battleCroPath, backup, false);

        File.WriteAllBytes(battleCroPath, data);
        return changed;
    }

    private static string FindGen7BattleCroPathForKingShield()
    {
        if (string.IsNullOrWhiteSpace(Main.RomFSPath))
            return string.Empty;

        string[] directCandidates =
        {
            Path.Combine(Main.RomFSPath, "Battle.cro"),
            Path.Combine(Main.RomFSPath, "ExtractedRomFS", "Battle.cro"),
        };

        foreach (string candidate in directCandidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        try
        {
            return Directory.GetFiles(Main.RomFSPath, "Battle.cro", SearchOption.AllDirectories).FirstOrDefault() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static int PatchKingShieldPatternByte(byte[] data, byte[] pattern, int byteOffset, byte replacement)
    {
        int changed = 0;

        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j])
                {
                    match = false;
                    break;
                }
            }

            if (!match)
                continue;

            data[i + byteOffset] = replacement;
            changed++;
        }

        return changed == 0 ? -1 : changed;
    }

    private static int FindKingShieldBytes(byte[] data, byte[] pattern)
    {
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
                return i;
        }

        return -1;
    }

}