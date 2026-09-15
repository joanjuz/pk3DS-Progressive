using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using pk3DS.Core.Structures.PersonalInfo;

namespace pk3DS.WinForms;

internal static class CustomBalanceTemplates
{
    internal const string TemplateRoot = "custom_balance_templates";

    internal sealed class MovePatchRow
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

    internal sealed class EvolutionPatchRow
    {
        public int Source { get; init; }
        public int Target { get; init; }
        public string Method { get; init; } = string.Empty;
        public int Level { get; init; }
        public int Argument { get; init; }
        public sbyte Form { get; init; } = -1;
        public string ItemName { get; init; } = string.Empty;
        public string AltItemName { get; init; } = string.Empty;
    }

    internal sealed class PokemonStatPatchRow
    {
        public int Entry { get; init; }
        public string Name { get; init; } = string.Empty;
        public int? HP { get; init; }
        public int? ATK { get; init; }
        public int? DEF { get; init; }
        public int? SPA { get; init; }
        public int? SPD { get; init; }
        public int? SPE { get; init; }
        public string Notes { get; init; } = string.Empty;
    }

    internal static string GetMoveTemplatePath(int generation)
        => Path.Combine(GetTemplateRoot(), $"moves_gen{generation}.csv");

    internal static string GetEvolutionTemplatePath(int generation)
        => Path.Combine(GetTemplateRoot(), $"evolutions_gen{generation}.csv");

    internal static string GetPokemonStatsTemplatePath(int generation)
        => Path.Combine(GetTemplateRoot(), $"pokemon_stats_gen{generation}.csv");

    internal static string GetTemplateDirectory()
        => GetTemplateRoot();

    internal static MovePatchRow[] LoadMovePatches(int generation, string[] moveNames)
    {
        string path = GetMoveTemplatePath(generation);
        if (!File.Exists(path))
            return [];

        var rows = new List<MovePatchRow>();
        Dictionary<string, int> header = null;

        foreach (var fields in ReadCsv(path))
        {
            if (fields.Length == 0)
                continue;

            string first = Get(fields, 0);
            if (first.Length == 0 || first.StartsWith('#'))
                continue;

            if (IsHeader(first, "Move"))
            {
                header = BuildHeaderMap(fields);
                continue;
            }

            string moveToken = GetField(fields, header, 0, "Move");
            int move = ResolveId(moveToken, moveNames);
            if (move <= 0)
                continue;

            string inflict = GetField(fields, header, 6, "Inflict", "InflictStatus", "Status");

            rows.Add(new MovePatchRow
            {
                Move = move,
                Type = GetField(fields, header, -1, "Type"),
                Category = GetField(fields, header, -1, "Category", "DamageCategory"),
                Quality = GetField(fields, header, -1, "Quality", "MoveQuality"),
                Power = ParseNullableInt(GetField(fields, header, 1, "Power", "BasePower")),
                Accuracy = ParseNullableInt(GetField(fields, header, 2, "Accuracy", "Acc")),
                PP = ParseNullableInt(GetField(fields, header, 3, "PP")),
                Priority = ParseNullableInt(GetField(fields, header, -1, "Priority")),
                HitMin = ParseNullableInt(GetField(fields, header, -1, "HitMin", "MinHits")),
                HitMax = ParseNullableInt(GetField(fields, header, -1, "HitMax", "MaxHits")),
                CriticalStage = ParseNullableInt(GetField(fields, header, 4, "CriticalStage", "CritStage", "Critical", "Crit")),
                Flinch = ParseNullableInt(GetField(fields, header, -1, "Flinch", "FlinchChance")),
                Effect = ParseNullableInt(GetField(fields, header, -1, "Effect", "EffectID")),
                Param0x0B = ParseNullableInt(GetField(fields, header, -1, "Param0x0B", "0x0B", "Byte0B", "Extra0x0B", "Raw0x0B")),
                Heal = ParseNullableInt(GetField(fields, header, 5, "Heal", "Drain")),
                Recoil = ParseNullableInt(GetField(fields, header, -1, "Recoil")),
                TurnMin = ParseNullableInt(GetField(fields, header, -1, "TurnMin", "MinTurns")),
                TurnMax = ParseNullableInt(GetField(fields, header, -1, "TurnMax", "MaxTurns")),
                Targeting = GetField(fields, header, -1, "Targeting", "Target", "Targets"),
                Inflict = ParseNullableInt(inflict),
                InflictToken = int.TryParse(inflict, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ? string.Empty : inflict,
                InflictChance = ParseNullableInt(GetField(fields, header, 7, "InflictChance", "StatusChance", "EffectChance", "SecondaryChance")),
                ClearStatEffects = ParseBool(GetField(fields, header, 8, "ClearStatEffects", "ClearStats")),
                UserStat = GetField(fields, header, -1, "UserStat", "SelfStat"),
                UserStatChange = ParseNullableInt(GetField(fields, header, -1, "UserStatChange", "SelfStatChange")),
                UserStatChance = ParseNullableInt(GetField(fields, header, -1, "UserStatChance", "SelfStatChance")),
                TargetStat = GetField(fields, header, -1, "TargetStat", "FoeStat"),
                TargetStatChange = ParseNullableInt(GetField(fields, header, -1, "TargetStatChange", "FoeStatChange")),
                TargetStatChance = ParseNullableInt(GetField(fields, header, -1, "TargetStatChance", "FoeStatChance")),
                Stat1 = GetField(fields, header, -1, "Stat1"),
                Stat1Change = ParseNullableInt(GetField(fields, header, -1, "Stat1Change")),
                Stat1Chance = ParseNullableInt(GetField(fields, header, -1, "Stat1Chance")),
                Stat2 = GetField(fields, header, -1, "Stat2"),
                Stat2Change = ParseNullableInt(GetField(fields, header, -1, "Stat2Change")),
                Stat2Chance = ParseNullableInt(GetField(fields, header, -1, "Stat2Chance")),
                Stat3 = GetField(fields, header, -1, "Stat3"),
                Stat3Change = ParseNullableInt(GetField(fields, header, -1, "Stat3Change")),
                Stat3Chance = ParseNullableInt(GetField(fields, header, -1, "Stat3Chance")),
                ClearFlags = ParseBool(GetField(fields, header, -1, "ClearFlags")),
                SetFlags = GetField(fields, header, -1, "SetFlags", "Flags", "AddFlags"),
                UnsetFlags = GetField(fields, header, -1, "UnsetFlags", "RemoveFlags"),
                KingShieldAttackMinusOne = ParseBool(GetField(fields, header, 9, "KingShieldAttackMinusOne")),

                ZEffect = ParseNullableInt(GetField(fields, header, -1, "ZEffect", "ZMoveEffect", "ZStatusEffect")),
                BattlePatch = GetField(fields, header, -1, "BattlePatch", "SpecialPatch", "BattleEffectPatch"),
                InGameDescription = GetField(fields, header, -1, "InGameDescription", "MoveDescription", "MoveFlavor", "FlavorText"),
            });
        }

        return [.. rows];
    }

    private static string GetTemplateRoot()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;

        string[] candidates =
        [
            Path.Combine(baseDir, TemplateRoot),
            Path.Combine(Environment.CurrentDirectory, TemplateRoot),
            Path.Combine(AppContext.BaseDirectory, TemplateRoot),
        ];

        foreach (string candidate in candidates)
        {
            if (Directory.Exists(candidate))
                return candidate;
        }

        return Path.Combine(baseDir, TemplateRoot);
    }

    internal static EvolutionPatchRow[] LoadEvolutionPatches(int generation, string[] speciesNames)
    {
        string path = GetEvolutionTemplatePath(generation);
        if (!File.Exists(path))
            return [];

        var rows = new List<EvolutionPatchRow>();

        foreach (var fields in ReadCsv(path))
        {
            if (fields.Length == 0)
                continue;

            string first = Get(fields, 0);
            if (first.Length == 0 || first.StartsWith('#'))
                continue;

            if (IsHeader(first, "Source"))
                continue;

            int source = ResolveId(first, speciesNames);
            int target = ResolveId(Get(fields, 1), speciesNames);
            if (source <= 0 || target <= 0)
                continue;

            rows.Add(new EvolutionPatchRow
            {
                Source = source,
                Target = target,
                Method = Get(fields, 2),
                Level = ParseInt(Get(fields, 3)),
                Argument = ParseInt(Get(fields, 4)),
                Form = (sbyte)ParseInt(Get(fields, 5), -1),
                ItemName = Get(fields, 6),
                AltItemName = Get(fields, 7),
            });
        }

        return [.. rows];
    }

    internal static PokemonStatPatchRow[] LoadPokemonStatPatches(int generation, string[] entryNames)
    {
        string path = GetPokemonStatsTemplatePath(generation);
        if (!File.Exists(path))
            return [];

        var rows = new List<PokemonStatPatchRow>();
        Dictionary<string, int> header = null;

        foreach (var fields in ReadCsv(path))
        {
            if (fields.Length == 0)
                continue;

            string first = Get(fields, 0);
            
            if (first.StartsWith('#'))
                continue;

            if (IsHeader(first, "Entry") || IsHeader(first, "Pokemon") || IsHeader(first, "Species"))
            {
                header = BuildHeaderMap(fields);
                continue;
            }

            string entryToken = GetField(fields, header, 0, "Entry", "PersonalEntry", "ID");
            string pokemonToken = GetField(fields, header, 1, "Pokemon", "Species", "Name");

            int entry = ResolvePokemonStatEntry(entryToken, pokemonToken, entryNames);
            if (entry <= 0 || entry >= entryNames.Length)
                continue;

            rows.Add(new PokemonStatPatchRow
            {
                Entry = entry,
                Name = entryNames[entry],
                HP = ParseNullableInt(GetField(fields, header, 2, "HP")),
                ATK = ParseNullableInt(GetField(fields, header, 3, "ATK", "Attack")),
                DEF = ParseNullableInt(GetField(fields, header, 4, "DEF", "Defense")),
                SPA = ParseNullableInt(GetField(fields, header, 5, "SPA", "SpA", "SpecialAttack")),
                SPD = ParseNullableInt(GetField(fields, header, 6, "SPD", "SpD", "SpecialDefense")),
                SPE = ParseNullableInt(GetField(fields, header, 7, "SPE", "Speed")),
                Notes = GetField(fields, header, 8, "Notes", "Note"),
            });
        }

        return [.. rows];
    }

    internal static int ApplyPokemonStatPatches(int generation, PersonalInfo[] personal, string[] entryNames)
    {
        if (personal is null)
            throw new ArgumentNullException(nameof(personal));
        if (entryNames is null)
            throw new ArgumentNullException(nameof(entryNames));

        var rows = LoadPokemonStatPatches(generation, entryNames);
        int applied = 0;

        foreach (var row in rows)
        {
            if (row.Entry <= 0 || row.Entry >= personal.Length)
                continue;

            var pkm = personal[row.Entry];
            pkm.HP = ResolveBaseStat(pkm.HP, row.HP, row, "HP");
            pkm.ATK = ResolveBaseStat(pkm.ATK, row.ATK, row, "ATK");
            pkm.DEF = ResolveBaseStat(pkm.DEF, row.DEF, row, "DEF");
            pkm.SPA = ResolveBaseStat(pkm.SPA, row.SPA, row, "SPA");
            pkm.SPD = ResolveBaseStat(pkm.SPD, row.SPD, row, "SPD");
            pkm.SPE = ResolveBaseStat(pkm.SPE, row.SPE, row, "SPE");
            applied++;
        }

        return applied;
    }

    private static int ResolvePokemonStatEntry(string entryToken, string pokemonToken, string[] entryNames)
    {
        if (!string.IsNullOrWhiteSpace(entryToken))
        {
            if (int.TryParse(entryToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
                return id;

            int byEntryName = ResolveToken(entryToken, entryNames);
            if (byEntryName >= 0)
                return byEntryName;
        }

        return string.IsNullOrWhiteSpace(pokemonToken) ? -1 : ResolveToken(pokemonToken, entryNames);
    }

    private static int ResolveBaseStat(int current, int? value, PokemonStatPatchRow row, string stat)
    {
        if (!value.HasValue)
            return current;

        int v = value.Value;
        if (v is < 1 or > 255)
            throw new InvalidDataException($"Invalid {stat} value {v} for Pokémon entry {row.Entry} ({row.Name}). Base stats must be between 1 and 255.");

        return v;
    }

    internal static void WriteExampleTemplatesIfMissing()
    {
        string root = GetTemplateRoot();
        Directory.CreateDirectory(root);

        string gen7Moves = Path.Combine(root, "moves_gen7.csv");

        WriteIfMissing(Path.Combine(root, "moves_gen6.csv"), ExampleMoves());
        WriteIfMissing(gen7Moves, ExampleMoves());
        EnsureGen7InGameDescriptionColumn(gen7Moves);
        EnsureGen7NightmareSleepV76TemplateRow(gen7Moves);
        EnsureGen7WishPivotTemplateRow(gen7Moves);
        Gen7MeditateV73Patcher.EnsureTemplateRow(gen7Moves);
        Gen7MistOwnerActivePatcher.EnsureTemplateRow(gen7Moves);
        Gen7FairyLockV11Patcher.EnsureTemplateRow(gen7Moves);
        WriteIfMissing(Path.Combine(root, "evolutions_gen6.csv"), ExampleEvolutions());
        WriteIfMissing(Path.Combine(root, "evolutions_gen7.csv"), ExampleEvolutions());
        WriteIfMissing(Path.Combine(root, "pokemon_stats_gen6.csv"), ExamplePokemonStats());
        WriteIfMissing(Path.Combine(root, "pokemon_stats_gen7.csv"), ExamplePokemonStats());
    
        Gen7LuckyChantCritPatcher.EnsureTemplateRow(
            Path.Combine(root, "moves_gen7.csv"));}

    private static string ExampleMoves() =>
        "Move,Type,Category,Quality,Power,Accuracy,PP,Priority,HitMin,HitMax,CriticalStage,Flinch,Effect,Param0x0B,Inflict,InflictChance,Heal,Recoil,TurnMin,TurnMax,Targeting,ClearStatEffects,UserStat,UserStatChange,UserStatChance,TargetStat,TargetStatChange,TargetStatChance,Stat1,Stat1Change,Stat1Chance,Stat2,Stat2Change,Stat2Chance,Stat3,Stat3Change,Stat3Chance,ClearFlags,SetFlags,UnsetFlags,KingShieldAttackMinusOne,BattlePatch,Notes,ZEffect,InGameDescription" + Environment.NewLine +
        "# Move can be an ID or exact move name. Empty cells keep the current value." + Environment.NewLine +
        "# Type/Category/Targeting/Inflict/Stats accept IDs or names. Flags accept enum names, numeric masks, or tokens separated by ; | ," + Environment.NewLine +
        "15,Grass,Physical,,70,100,15,,,,1,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,Cut / Corte" + Environment.NewLine +
        "249,Fighting,Physical,,60,100,,,,,,,,,,,,,,,,,,,Defense,-1,100,,,,,,,,,,,,Rock Smash / Golpe Roca" + Environment.NewLine +
        "19,Flying,Physical,,130,100,,,,,,,,,,,,33,,,,,,,,,,,,,,,,,,,,,Fly / Vuelo" + Environment.NewLine +
        "147,,Status,,,80,,,,,,,,Sleep,100,,,,,,,,,,,,,,,,,,,,,,,,Spore / Espora" + Environment.NewLine +
        "79,,Status,,,70,,,,,,,,Sleep,100,,,,,,,,,,,,,,,,,,,,,,,,Sleep Powder / Somnifero" + Environment.NewLine +
        "601,,Status,,,,,-1,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,Geomancy / Geocontrol priority -1" + Environment.NewLine +
        "6,,Physical,,25,,40,,,,,40,,,,,,,,,,,,,,,,,,,,,,,,,,,,,Pay Day / Dia de Pago" + Environment.NewLine +
        "594,Water,Special,,30,100,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,Water Shuriken / Shuriken de Agua" + Environment.NewLine +
        "273,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,Gen7WishPivot,Deseo / Wish: delayed heal + immediate pivot switch" + Environment.NewLine +
        "588,,Status,,,5,,,,,,,,,,,,,,,,true,,,,,,,,,,,,,,,,,,true,King's Shield PP 5 and -1 Attack" + Environment.NewLine;

    private static string ExampleEvolutions() =>
        "Source,Target,Method,Level,Argument,Form,ItemName,AltItemName" + Environment.NewLine +
        "# Methods: Level, Friendship, MaleLevel, FemaleLevel, UsedItem, or a numeric method ID." + Environment.NewLine +
        "25,26,Level,30,,, ," + Environment.NewLine +
        "44,182,Friendship,,,,," + Environment.NewLine +
        "356,477,UsedItem,,,,Reaper Cloth,Tela Terrible" + Environment.NewLine;

    private static string ExamplePokemonStats() =>
        "Entry,Pokemon,HP,ATK,DEF,SPA,SPD,SPE,Notes" + Environment.NewLine +
        "# Change only the Pokémon listed here. Empty stat cells keep the current value." + Environment.NewLine +
        "# Entry is the Personal entry ID and is the safest option for alternate forms. Pokemon can be used instead when Entry is blank." + Environment.NewLine +
        "# Example rows are commented out so a new template never changes the ROM by accident." + Environment.NewLine +
        "# 25,Pikachu,35,60,40,50,50,100,Example: raise Attack and Speed" + Environment.NewLine +
        "# ,Butterfree,,45,,,,,Example: change only Attack; all blank stats stay unchanged" + Environment.NewLine;

    private static void EnsureGen7WishPivotTemplateRow(string path)
    {
        if (!File.Exists(path))
            return;

        foreach (string line in File.ReadLines(path))
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;

            string[] fields = SplitCsvLine(line);
            if (fields.Length == 0)
                continue;

            if (int.TryParse(Get(fields, 0), NumberStyles.Integer, CultureInfo.InvariantCulture, out int move) && move == 273)
                return;
        }

        const string row = "273,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,Gen7WishPivot,Deseo / Wish: delayed heal + immediate pivot switch";

        string existing = File.ReadAllText(path);
        string separator = existing.Length == 0 || existing.EndsWith('\n') ? string.Empty : Environment.NewLine;
        File.AppendAllText(path, separator + row + Environment.NewLine);
    }

    private static void EnsureGen7InGameDescriptionColumn(string path)
    {
        if (!File.Exists(path))
            return;

        string[] lines = File.ReadAllLines(path);

        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;

            string[] fields = SplitCsvLine(lines[i]);
            if (fields.Length == 0 || NormalizeToken(Get(fields, 0)) != "move")
                continue;

            if (fields.Any(z => NormalizeToken(z) == "ingamedescription"))
                return;

            Array.Resize(ref fields, fields.Length + 1);
            fields[^1] = "InGameDescription";
            lines[i] = NightmareV76JoinCsvLine(fields);
            File.WriteAllLines(path, lines);
            return;
        }
    }

    private static void EnsureGen7NightmareSleepV76TemplateRow(string path)
    {
        if (!File.Exists(path))
            return;

        const string token = "Gen7NightmareSleepV76";
        const string finalNote = "Pesadilla / Nightmare: target awake -> stock Sleep only; target already asleep -> vanilla Nightmare.";
        const string finalDescription = "Si está despierto, duerme al objetivo.\\nSi ya duerme, pierde 1/4 de sus PS máximos por turno";

        string[] lines = File.ReadAllLines(path);
        int headerLine = -1;
        int moveIndex = -1;
        int battlePatchIndex = -1;
        int notesIndex = -1;
        int fieldCount = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;

            string[] fields = SplitCsvLine(lines[i]);
            if (fields.Length == 0 || NormalizeToken(Get(fields, 0)) != "move")
                continue;

            headerLine = i;
            fieldCount = fields.Length;

            for (int j = 0; j < fields.Length; j++)
            {
                string name = NormalizeToken(fields[j]);

                if (name == "move")
                    moveIndex = j;
                else if (name == "battlepatch")
                    battlePatchIndex = j;
                else if (name is "notes" or "note")
                    notesIndex = j;
            }

            break;
        }

        if (headerLine < 0 || moveIndex < 0 || battlePatchIndex < 0 || notesIndex < 0)
            return;

        for (int i = headerLine + 1; i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;

            string[] fields = SplitCsvLine(lines[i]);

            if (moveIndex >= fields.Length ||
                !int.TryParse(
                    Get(fields, moveIndex),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int move) ||
                move != 171)
            {
                continue;
            }

            int required = Math.Max(
                fieldCount,
                Math.Max(battlePatchIndex, notesIndex) + 1);

            if (fields.Length < required)
                Array.Resize(ref fields, required);

            bool hasToken =
                Get(fields, battlePatchIndex)
                    .Split([',', ';', '|'],
                        StringSplitOptions.RemoveEmptyEntries |
                        StringSplitOptions.TrimEntries)
                    .Any(z => NormalizeToken(z) == "gen7nightmaresleepv76");

            bool changed = false;

            if (!hasToken)
            {
                fields[battlePatchIndex] =
                    string.IsNullOrWhiteSpace(fields[battlePatchIndex])
                        ? token
                        : fields[battlePatchIndex] + ";" + token;

                changed = true;
            }

            string note = Get(fields, notesIndex);

            if (string.IsNullOrWhiteSpace(note) ||
                note.Contains("SPECIAL CHECK", StringComparison.OrdinalIgnoreCase) ||
                note.Contains("Nightmare", StringComparison.OrdinalIgnoreCase) ||
                note.Contains("Pesadilla", StringComparison.OrdinalIgnoreCase))
            {
                if (note != finalNote)
                {
                    fields[notesIndex] = finalNote;
                    changed = true;
                }
            }

            if (changed)
            {
                lines[i] = NightmareV76JoinCsvLine(fields);
                File.WriteAllLines(path, lines);
            }

            return;
        }

        int newCount = Math.Max(
            fieldCount,
            Math.Max(battlePatchIndex, notesIndex) + 1);

        var newFields = new string[newCount];
        newFields[moveIndex] = "171";
        newFields[battlePatchIndex] = token;
        newFields[notesIndex] = finalNote;

        string existing = File.ReadAllText(path);
        string separator =
            existing.Length == 0 || existing.EndsWith('\n')
                ? string.Empty
                : Environment.NewLine;

        File.AppendAllText(
            path,
            separator +
            NightmareV76JoinCsvLine(newFields) +
            Environment.NewLine);
    }

    private static string NightmareV76JoinCsvLine(IEnumerable<string> fields)
        => string.Join(",", fields.Select(NightmareV76EscapeCsvField));

    private static string NightmareV76EscapeCsvField(string value)
    {
        value ??= string.Empty;

        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0)
            return value;

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
    private static void WriteIfMissing(string path, string text)
    {
        if (File.Exists(path))
            return;

        File.WriteAllText(path, text);
    }

    private static IEnumerable<string[]> ReadCsv(string path)
    {
        foreach (string line in File.ReadLines(path))
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0)
                continue;

            yield return SplitCsvLine(line);
        }
    }

    private static string[] SplitCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        bool quote = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (quote && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    quote = !quote;
                }
                continue;
            }

            if (c == ',' && !quote)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        result.Add(current.ToString().Trim());
        return [.. result];
    }

    private static Dictionary<string, int> BuildHeaderMap(string[] fields)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < fields.Length; i++)
        {
            string key = NormalizeToken(fields[i]);
            if (key.Length != 0 && !result.ContainsKey(key))
                result.Add(key, i);
        }
        return result;
    }

    private static string GetField(string[] fields, Dictionary<string, int> header, int fallbackIndex, params string[] names)
    {
        if (header is not null)
        {
            foreach (string name in names)
            {
                if (header.TryGetValue(NormalizeToken(name), out int index))
                    return Get(fields, index);
            }
            return string.Empty;
        }

        return fallbackIndex >= 0 ? Get(fields, fallbackIndex) : string.Empty;
    }

    private static string Get(string[] fields, int index)
        => index >= 0 && index < fields.Length ? fields[index]?.Trim() ?? string.Empty : string.Empty;

    private static bool IsHeader(string value, string header)
        => string.Equals(value, header, StringComparison.OrdinalIgnoreCase);

    private static int ResolveId(string value, string[] names)
    {
        value = value.Trim();
        if (value.Length == 0)
            return -1;

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
            return id;

        return Array.FindIndex(names, z => string.Equals(z, value, StringComparison.OrdinalIgnoreCase));
    }

    internal static int ResolveToken(string value, string[] names, Dictionary<string, int> aliases = null)
    {
        value = value.Trim();
        if (value.Length == 0)
            return -1;

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
            return id;

        string normalized = NormalizeToken(value);
        if (aliases is not null && aliases.TryGetValue(normalized, out int alias))
            return alias;

        return Array.FindIndex(names, z => NormalizeToken(z) == normalized);
    }

    internal static IEnumerable<string> SplitTokens(string value)
        => value.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    internal static string NormalizeToken(string value)
    {
        if (value is null)
            return string.Empty;

        string lower = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(lower.Length);
        foreach (char c in lower)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(c))
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static int ParseInt(string value, int fallback = 0)
        => TryParseIntegerToken(value, out int result) ? result : fallback;

    private static int? ParseNullableInt(string value)
        => TryParseIntegerToken(value, out int result) ? result : null;

    private static bool TryParseIntegerToken(string value, out int result)
    {
        result = 0;

        value = value?.Trim() ?? string.Empty;
        if (value.Length == 0)
            return false;

        int sign = 1;
        if (value.StartsWith("+", StringComparison.Ordinal))
        {
            value = value[1..].TrimStart();
        }
        else if (value.StartsWith("-", StringComparison.Ordinal))
        {
            sign = -1;
            value = value[1..].TrimStart();
        }

        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hex))
                return false;

            result = sign * hex;
            return true;
        }

        if (value.EndsWith("h", StringComparison.OrdinalIgnoreCase) && value.Length > 1)
        {
            if (!int.TryParse(value[..^1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hex))
                return false;

            result = sign * hex;
            return true;
        }

        if (!int.TryParse((sign < 0 ? "-" : string.Empty) + value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
            return false;

        return true;
    }
    private static bool ParseBool(string value)
    {
        value = value.Trim();
        return value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Equals("y", StringComparison.OrdinalIgnoreCase)
            || value.Equals("si", StringComparison.OrdinalIgnoreCase)
            || value.Equals("sí", StringComparison.OrdinalIgnoreCase);
    }
}
