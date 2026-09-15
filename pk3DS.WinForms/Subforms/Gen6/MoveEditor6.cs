using pk3DS.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using pk3DS.Core.Structures;

namespace pk3DS.WinForms;

public partial class MoveEditor6 : Form
{
    public MoveEditor6(byte[][] infiles)
    {
        files = infiles;
        movelist[0] = "";

        InitializeComponent();
        Setup();

        AddBalanceMovesButton();
        FixMoveEditor6Layout();

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

        int requiredTop = B_BalanceMoves.Bottom + gap;

        if (groupBox1.Top < requiredTop)
            groupBox1.Top = requiredTop;
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
        public string BattlePatch { get; init; } = string.Empty;
    }
    private void FixMoveEditor6Layout()
    {
        const int gap = 6;

        groupBox1.Top = B_BalanceMoves.Bottom + gap;

        B_Table.Left = B_RandAll.Left;
        B_Table.Top = groupBox1.Bottom + gap;

        CLB_Flags.Left = B_RandAll.Left - 5;
        CLB_Flags.Top = B_Table.Bottom + gap;

        int bottomMargin = 8;
        int newHeight = ClientSize.Height - CLB_Flags.Top - bottomMargin;

        if (newHeight < 120)
            newHeight = 120;

        CLB_Flags.Height = newHeight;
    }
    private MoveBalancePatch[] GetBalancedMovePatchesFromTemplate()
    {
        CustomBalanceTemplates.WriteExampleTemplatesIfMissing();

        var templatePatches = CustomBalanceTemplates.LoadMovePatches(6, movelist);
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
        }).ToArray();
    }
    private int ApplyBalancedMoves()
    {
        int changed = 0;
        var battlePatchRequests = new List<CustomBattleEffectPatcher.BattlePatchRequest>();

        foreach (var patch in GetBalancedMovePatchesFromTemplate())
        {
            if (patch.Move <= 0 || patch.Move >= files.Length)
                continue;

            byte[] data = files[patch.Move];

            if (data.Length < 0x1E)
                continue;

            ApplyCoreMovePatch(data, patch);
            ApplyStatPatches(data, patch);
            ApplyFlagPatches(data, patch);
            CustomBattleEffectPatcher.ApplyMoveDataSpecials(6, patch.Move, data, patch.KingShieldAttackMinusOne, patch.BattlePatch);

            if (patch.KingShieldAttackMinusOne || !string.IsNullOrWhiteSpace(patch.BattlePatch))
            {
                battlePatchRequests.Add(new CustomBattleEffectPatcher.BattlePatchRequest
                {
                    Move = patch.Move,
                    KingShieldAttackMinusOne = patch.KingShieldAttackMinusOne,
                    BattlePatch = patch.BattlePatch,
                });
            }

            files[patch.Move] = data;
            changed++;
        }

        CustomBattleEffectPatcher.ApplyExternalPatches(6, battlePatchRequests, (title, message) => WinFormsUtil.Alert(title, message));

        return changed;
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

        var move = new Move6(data);
        uint flags = patch.ClearFlags ? 0 : (uint)move.Flags;
        flags |= ResolveFlags(patch.SetFlags, typeof(MoveFlag6));
        flags &= ~ResolveFlags(patch.UnsetFlags, typeof(MoveFlag6));
        move.Flags = (MoveFlag6)flags;
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

    private static void ClearMoveStatEffects(byte[] data)
    {
        data[0x15] = 0;
        data[0x16] = 0;
        data[0x17] = 0;

        data[0x18] = 0;
        data[0x19] = 0;
        data[0x1A] = 0;

        data[0x1B] = 0;
        data[0x1C] = 0;
        data[0x1D] = 0;
    }

    private static int SetKingShieldAttackDrop(byte[] cro)
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

        throw new InvalidOperationException("Could not find King's Shield attack drop pattern in DLLBattle.cro.");
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
            new MoveBalancePatch { Move = 15, Power = 70, Accuracy = 100, PP = 15, CriticalStage = 1 }, // Cut
        new MoveBalancePatch { Move = 249, Power = 60, Accuracy = 100 }, // Rock Smash
        new MoveBalancePatch { Move = 19, Power = 100, Accuracy = 100 }, // Fly
        new MoveBalancePatch { Move = 147, Accuracy = 85 }, // Spore
        new MoveBalancePatch { Move = 137, Accuracy = 90, PP = 15 }, // Glare
        new MoveBalancePatch { Move = 591, Power = 95, Accuracy = 95, PP = 10, ClearStatEffects = true }, // Diamond Storm
        new MoveBalancePatch { Move = 613, Power = 80, Accuracy = 100, Heal = 50 }, // Oblivion Wing
        new MoveBalancePatch { Move = 448, Power = 80, Accuracy = 100, PP = 15, Inflict = Confusion, InflictChance = 15 }, // Chatter
        new MoveBalancePatch { Move = 141, Power = 70, Accuracy = 100, PP = 15 }, // Leech Life
        new MoveBalancePatch { Move = 188, Power = 60, Accuracy = 100 }, // Sludge Bomb
        new MoveBalancePatch { Move = 552, Power = 70, Accuracy = 100 }, // Fiery Dance
        new MoveBalancePatch { Move = 594, Power = 20, Accuracy = 100 }, // Water Shuriken

        new MoveBalancePatch { Move = 182, PP = 7 }, // Protect
        new MoveBalancePatch { Move = 197, PP = 7 }, // Detect
        new MoveBalancePatch { Move = 596, PP = 7 }, // Spiky Shield
        new MoveBalancePatch { Move = 588, PP = 7, KingShieldAttackMinusOne = true }, // King's Shield
        new MoveBalancePatch { Move = 476, PP = 7 }, // Rage Powder
        new MoveBalancePatch { Move = 266, PP = 7 }, // Follow Me
        new MoveBalancePatch { Move = 502, PP = 7 }, // Ally Switch
        new MoveBalancePatch { Move = 73, PP = 7 }, // Leech Seed
        new MoveBalancePatch { Move = 281, PP = 7 }, // Yawn

        new MoveBalancePatch { Move = 270, PP = 5 }, // Helping Hand
        new MoveBalancePatch { Move = 105, PP = 5 }, // Recover
        new MoveBalancePatch { Move = 234, PP = 5 }, // Morning Sun
        new MoveBalancePatch { Move = 236, PP = 5 }, // Moonlight
        new MoveBalancePatch { Move = 208, PP = 5 }, // Milk Drink
        new MoveBalancePatch { Move = 303, PP = 5 }, // Slack Off
        new MoveBalancePatch { Move = 235, PP = 5 }, // Synthesis
        new MoveBalancePatch { Move = 456, PP = 5 }, // Heal Order
        new MoveBalancePatch { Move = 135, PP = 5 }, // Soft-Boiled
        new MoveBalancePatch { Move = 355, PP = 5 }, // Roost
        new MoveBalancePatch { Move = 505, PP = 5 }, // Heal Pulse
    ];
    }

    private readonly string[] TargetingTypes =
    [
        "Single Adjacent Ally/Foe",
        "Any Ally", "Any Adjacent Ally", "Single Adjacent Foe", "Everyone but User", "All Foes",
        "All Allies", "Self", "All Pokémon on Field", "Single Adjacent Foe (2)", "Entire Field",
        "Opponent's Field", "User's Field", "Self",
    ];

    private readonly string[] InflictionTypes =
    [
        "None",
        "Paralyze", "Sleep", "Freeze", "Burn", "Poison",
        "Confusion", "Attract", "Capture", "Nightmare", "Curse",
        "Taunt", "Torment", "Disable", "Yawn", "Heal Block",
        "?", "Detect", "Leech Seed", "Embargo", "Perish Song",
        "Ingrain",
    ];

    private readonly string[] MoveQualities =
    [
        "Only DMG",
        "No DMG -> Inflict Status", "No DMG -> -Target/+User Stat", "No DMG | Heal User", "DMG | Inflict Status", "No DMG | STATUS | +Target Stat",
        "DMG | -Target Stat", "DMG | +User Stat", "DMG | Absorbs DMG", "One-Hit KO", "Affects Whole Field",
        "Affect One Side of the Field", "Forces Target to Switch", "Unique Effect",
    ];

    private void Setup()
    {
        CB_Move.Items.AddRange(movelist);
        CB_Type.Items.AddRange(types);
        CB_Category.Items.AddRange(MoveCategories);
        CB_Stat1.Items.AddRange(StatCategories);
        CB_Stat2.Items.AddRange(StatCategories);
        CB_Stat3.Items.AddRange(StatCategories);
        CB_Targeting.Items.AddRange(TargetingTypes);
        CB_Quality.Items.AddRange(MoveQualities);
        CB_Inflict.Items.AddRange(InflictionTypes);
        foreach (var s in Enum.GetNames(typeof(MoveFlag6)).Skip(1)) CLB_Flags.Items.Add(s);
        CB_Inflict.Items.Add("Special");

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

            var move = new Move6(data);
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

            uint flagval = 0;
            for (int i = 0; i < CLB_Flags.Items.Count; i++)
                flagval |= CLB_Flags.GetItemChecked(i) ? 1u << i : 0;
            BitConverter.GetBytes(flagval).CopyTo(data, 0x1E);
        }
        files[entry] = data;
    }

    private void B_Table_Click(object sender, EventArgs e)
    {
        var items = files.Select(z => new Move6(z));
        Clipboard.SetText(TableUtil.GetTable(items, movelist));
        System.Media.SystemSounds.Asterisk.Play();
    }

    private void CloseForm(object sender, FormClosingEventArgs e)
    {
        SetEntry();
        RandSettings.SetFormSettings(this, groupBox1.Controls);
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
}