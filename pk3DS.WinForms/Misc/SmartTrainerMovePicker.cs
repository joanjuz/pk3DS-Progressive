using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using pk3DS.Core;
using pk3DS.Core.Randomizers;
using pk3DS.Core.Structures;

namespace pk3DS.WinForms;

internal static class SmartTrainerMovePicker
{
    private const int WeatherRain = 1;
    private const int WeatherSun = 2;
    private const int WeatherSand = 4;
    private const int WeatherHail = 8;

    private sealed class Candidate
    {
        public int Move { get; init; }
        public int Score { get; init; }
        public int Type { get; init; }
        public int Category { get; init; }
        public bool Damaging { get; init; }
        public bool RequiresStatBoostPartner { get; init; }
        public bool BoostsOwnStats { get; init; }
        public bool SelfSupportStatus { get; init; }
        public bool OpponentUtilityStatus { get; init; }
        public bool RegeneratorPivot { get; init; }
        public bool WeatherAbuser { get; init; }
        public bool Status => Category == 0;
    }

    private static readonly Dictionary<int, int[]> EggMoveCache = [];
    private static Dictionary<int, int[]> PreEvolutionCache6;
    private static Dictionary<int, int[]> PreEvolutionCache7;

    private static readonly int[] Gen6BasicTutors = [520, 519, 518, 338, 307, 308, 434, 620];
    private static readonly int[] Gen6ORASTutors =
    [
        450, 343, 162, 530, 324, 442, 402, 529, 340, 067, 441, 253, 009, 007, 008,
        277, 335, 414, 492, 356, 393, 334, 387, 276, 527, 196, 401, 399, 428, 406, 304, 231,
        020, 173, 282, 235, 257, 272, 215, 366, 143, 220, 202, 409, 355, 264, 351, 352,
        380, 388, 180, 495, 270, 271, 478, 472, 283, 200, 278, 289, 446, 214, 285,
    ];

    private static readonly int[] Gen7BasicTutors = [520, 519, 518, 338, 307, 308, 434, 620];
    private static readonly int[] Gen7USUMTutors =
    [
        450, 343, 162, 530, 324, 442, 402, 529, 340, 067, 441, 253, 009, 007, 008,
        277, 335, 414, 492, 356, 393, 334, 387, 276, 527, 196, 401, 428, 406, 304, 231,
        020, 173, 282, 235, 257, 272, 215, 366, 143, 220, 202, 409, 264, 351, 352,
        380, 388, 180, 495, 270, 271, 478, 472, 283, 200, 278, 289, 446, 285,
        477, 502, 432, 710, 707, 675, 673,
    ];

    public static int[] PickBetterMoveset(
        int species,
        int form,
        int level,
        IEnumerable<int> currentMoves,
        MoveRandomizer move,
        LearnsetRandomizer learn,
        TrainerMoveRule rule,
        int minimumDamagingMoves,
        int abilitySlot,
        int generation,
        int teamWeatherMask = 0,
        bool includeTMs = false)
    {
        // Only confirmed weather should enable weather-dependent attacks.
        // Do not trust the incoming currentMoves here: those moves are only a pre-Better-Moveset seed
        // and may contain Rain Dance/Sunny Day/etc. that will not survive the final selection.
        teamWeatherMask |= GetWeatherAbilityMask(species, abilitySlot);

        var pool = BuildMovePool(
            species,
            form,
            level,
            move,
            learn,
            generation,
            includeTMs);
        if (pool.Count == 0)
            return currentMoves?.Take(4).Concat(Enumerable.Repeat(0, 4)).Take(4).ToArray() ?? new int[4];

        var candidates = pool
            .Select(m => BuildCandidate(m, species, rule, move, abilitySlot, teamWeatherMask))
            .Where(c => c is not null && c.Score > 0)
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.Move)
            .ToList();

        if (candidates.Count == 0)
            return currentMoves?.Take(4).Concat(Enumerable.Repeat(0, 4)).Take(4).ToArray() ?? new int[4];

        var selected = new List<Candidate>(4);
        int desiredStatusMoves = GetDesiredStatusMoveCount(species, rule, abilitySlot);
        int requiredDamage = Math.Clamp(Math.Max(minimumDamagingMoves <= 0 ? 2 : minimumDamagingMoves, 4 - desiredStatusMoves), 0, 4);
        var abilityKeys = GetAbilityKeys(species, abilitySlot);
        bool pranksterStyle = HasAbility(abilityKeys, "prankster", "bromista");
        bool regenerator = HasAbility(abilityKeys, "regenerator", "regeneracion", "regeneración");
        if (pranksterStyle)
            requiredDamage = Math.Min(requiredDamage, 4 - desiredStatusMoves);

        // Regenerator Pokémon should try to carry a pivot move; bulky ones especially prefer Teleport.
        if (regenerator && selected.Count < 4)
            AddBest(selected, candidates.Where(c => c.RegeneratorPivot), false);

        // If the team already has weather support, grab a high-value weather abuser if available.
        // Example: Drought/Sunny Day enables Solar Beam/Solar Blade; Rain enables Thunder/Hurricane.
        if (teamWeatherMask != 0 && selected.Count < 4)
            AddBest(selected, candidates.Where(c => c.WeatherAbuser), true);

        // Try to give dual-type Pokémon one damaging STAB move for each type before filling coverage.
        // This keeps STAB priority without letting a single type consume the whole moveset.
        foreach (int stabType in GetSpeciesTypes(species))
        {
            if (selected.Count >= 4 || selected.Count(c => c.Damaging) >= requiredDamage)
                break;

            AddBest(selected, candidates.Where(c => c.Damaging && c.Type == stabType), false);
        }

        while (selected.Count(c => c.Damaging) < requiredDamage && selected.Count < 4)
        {
            if (!AddBest(selected, candidates.Where(c => c.Damaging), true))
                break;
        }

        if (pranksterStyle && desiredStatusMoves >= 2 && selected.Count < 4)
            AddBest(selected, candidates.Where(c => c.Status && c.SelfSupportStatus), false);

        if (pranksterStyle && desiredStatusMoves >= 2 && selected.Count < 4)
            AddBest(selected, candidates.Where(c => c.Status && c.OpponentUtilityStatus), false);

        while (selected.Count(c => c.Status) < desiredStatusMoves && selected.Count < 4)
        {
            if (!AddBest(selected, candidates.Where(c => c.Status), false))
                break;
        }

        while (selected.Count < 4)
        {
            bool canAddStatus = selected.Count(c => c.Status) < desiredStatusMoves;
            var filler = canAddStatus ? candidates : candidates.Where(c => !c.Status);
            if (!AddBest(selected, filler, true))
                break;
        }

        return selected
            .Select(c => c.Move)
            .Concat(Enumerable.Repeat(0, 4))
            .Take(4)
            .ToArray();
    }

    private static HashSet<int> BuildMovePool(
        int species,
        int form,
        int level,
        MoveRandomizer move,
        LearnsetRandomizer learn,
        int generation,
        bool includeTMs)
    {
        var pool = new HashSet<int>();

        // Base Better Movesets is intentionally progression-safe:
        // only level-up moves the CURRENT species/form can learn at or before
        // its final trainer level are eligible. Do not pull future level-up
        // moves, egg moves, tutor moves, pre-evolution-only moves, or arbitrary
        // random moves into the candidate pool.
        AddMoves(pool, SafeGetMovesUpToLevel(learn, species, form, level));

        // Optional expansion: compatible TMs/HMs from the CURRENT ROM.
        // This respects randomized TM lists and the species' current TMHM flags.
        if (includeTMs)
            AddMoves(pool, GetTMMoves(species, form, generation));

        pool.RemoveWhere(m => m <= 0 || m >= Main.Config.Moves.Length || move.BannedMoves.Contains(m));
        return pool;
    }

    private static Candidate BuildCandidate(int moveID, int species, TrainerMoveRule rule, MoveRandomizer move, int abilitySlot, int teamWeatherMask)
    {
        if (moveID <= 0 || moveID >= Main.Config.Moves.Length || move.BannedMoves.Contains(moveID))
            return null;

        var data = Main.Config.Moves[moveID];
        int category = data.Category;
        int power = data.Power;
        bool damaging = category != 0 && power > 0;

        if (rule is not null)
        {
            if (category == 0 && !rule.AllowStatusMoves)
                return null;

            if (damaging && rule.MinMovePower > 0 && power < Math.Clamp(rule.MinMovePower, 0, 250))
                return null;

            if (damaging && rule.UseStrongestAttackStat)
            {
                int preferred = GetPreferredMoveCategory(species, rule.MixedTolerance);
                if (preferred != 0 && category != preferred)
                    return null;
            }
        }

        string moveKey = Normalize(Main.Config.GetText(TextName.MoveNames).ElementAtOrDefault(moveID) ?? string.Empty);
        int score = ScoreMove(moveID, species, rule, abilitySlot, teamWeatherMask);
        return new Candidate
        {
            Move = moveID,
            Score = score,
            Type = data.Type,
            Category = category,
            Damaging = damaging,
            RequiresStatBoostPartner = IsStoredPowerStyleMove(moveID, moveKey),
            BoostsOwnStats = BoostsOwnStats(moveID, moveKey, category, power, species, abilitySlot),
            SelfSupportStatus = IsSelfSupportStatusMove(moveKey),
            OpponentUtilityStatus = IsOpponentUtilityStatusMove(moveKey),
            RegeneratorPivot = IsRegeneratorPivotMove(moveKey),
            WeatherAbuser = IsWeatherAbuserMove(moveKey, data.Type, category, power, teamWeatherMask),
        };
    }

    private static int ScoreMove(int moveID, int species, TrainerMoveRule rule, int abilitySlot, int teamWeatherMask)
    {
        var data = Main.Config.Moves[moveID];
        string name = Normalize(Main.Config.GetText(TextName.MoveNames).ElementAtOrDefault(moveID) ?? string.Empty);
        int category = data.Category;
        int power = data.Power;
        int type = data.Type;
        int score = 0;
        var abilityKeys = GetAbilityKeys(species, abilitySlot);
        var coverageTypes = GetCoverageTypesForWeaknesses(species);
        int effectiveWeatherMask = teamWeatherMask | GetWeatherMaskFromAbility(abilityKeys);

        // Weather-dependent moves should not be selected unless the team/pokemon has confirmed weather support.
        // This prevents sets like Hurricane + Solar Beam with no Rain/Sun setter on the team.
        if (!HasRequiredWeatherSupport(name, category, power, effectiveWeatherMask, abilityKeys, data.Accuracy))
            return 0;

        // Avoid selecting Dream Eater as a generic strong Psychic move.
        // It only works on sleeping targets, and this picker does not currently build sleep combos reliably.
        if (ContainsAny(name, "dreameater", "comesuenos"))
            return 0;

        // Last Resort is almost never usable on trainer AI sets and becomes especially bad with Choice items.
        // Block it from Better Movesets instead of giving the item picker a set it cannot rescue reliably.
        if (ContainsAny(name, "lastresort", "ultimabaza"))
            return 0;

        if (category != 0 && power > 0)
        {
            score = Math.Min(92, 28 + power);
            if (IsSTAB(species, type))
                score += 22;
            if (IsPreferredCategory(species, category))
                score += 14;
            else if (GetPreferredMoveCategory(species, 12) == 0)
                score += 6;

            if (power >= 90)
                score += 8;
            if (power <= 40 && !ContainsAny(name, "priority", "quickattack", "aquajet", "iceshard", "bulletpunch", "machpunch", "shadowsneak", "suckerpunch"))
                score -= 18;

            if (coverageTypes.Contains(type))
                score += IsSTAB(species, type) ? 8 : 24;

            score += GetWeatherMoveBonus(name, type, category, power, effectiveWeatherMask);
        }
        else
        {
            int preferredCategory = GetPreferredMoveCategory(species, 12);
            bool physicalSetup = IsPhysicalSetupMove(name);
            bool specialSetup = IsSpecialSetupMove(name);
            bool mixedSetup = IsMixedSetupMove(name);

            if (physicalSetup)
                score = preferredCategory == 2 ? 0 : preferredCategory == 1 ? 88 : 68;
            else if (specialSetup)
                score = preferredCategory == 1 ? 0 : preferredCategory == 2 ? 88 : 68;
            else if (mixedSetup)
                score = 76;
            else if (TryScoreWeatherMove(name, species, abilityKeys, out int weatherScore))
                score = weatherScore;
            else if (ContainsAny(name, "roost", "recover", "softboiled", "milkdrink", "slackoff", "synthesis", "morningsun", "moonlight"))
                score = 72;
            else if (ContainsAny(name, "willowisp", "toxic", "thunderwave", "spore", "sleeppowder", "stunspore", "glare", "nuzzle"))
                score = 68;
            else if (ContainsAny(name, "stealthrock", "spikes", "toxicspikes", "stickyweb"))
                score = 62;
            else if (ContainsAny(name, "reflect", "lightscreen", "auroraveil", "tailwind"))
                score = 58;
            else if (ContainsAny(name, "protect", "detect", "spikyshield", "kingsshield", "banefulbunker", "bunker", "proteccion", "deteccion", "escudoreal"))
                score = HasQuadWeakness(species) ? 90 : 52;
            else if (ContainsAny(name, "substitute", "taunt", "encore", "trickroom"))
                score = 52;
            else
                score = 28;
        }

        score += GetAbilityMoveBonus(name, category, power, data.Accuracy, type, species, abilityKeys);

        return Math.Max(0, score);
    }

    private static int GetDesiredStatusMoveCount(int species, TrainerMoveRule rule, int abilitySlot)
    {
        if (rule is not null && !rule.AllowStatusMoves)
            return 0;

        if (species <= 0 || species >= Main.SpeciesStat.Length)
            return 0;

        var p = Main.SpeciesStat[species];
        int bulk = p.HP + p.DEF + p.SPD;
        int bestOffense = Math.Max(p.ATK, p.SPA);
        int offenseProfile = bestOffense + p.SPE;
        var abilityKeys = GetAbilityKeys(species, abilitySlot);

        // Prankster is strongest when it has more than one utility option: one self-support tool and one disruption move.
        if (HasAbility(abilityKeys, "prankster", "bromista"))
            return 2;

        // Triage still wants one support/recovery slot, but not a full support set by default.
        if (HasAbility(abilityKeys, "triage", "primerauxilio"))
            return 1;

        // Bulky Regenerator Pokémon are good slow-pivot candidates, especially with Teleport.
        if (HasAbility(abilityKeys, "regenerator", "regeneracion", "regeneración") && bulk >= offenseProfile - 10)
            return 1;

        // Bulky Pokémon make better use of recovery, status, hazards, screens, etc.
        if (bulk >= 285 && bulk >= offenseProfile - 10)
            return 1;

        // Very defensive but slow Pokémon can still run a support move even with average offense.
        if (bulk >= 330 && p.SPE <= 75)
            return 1;

        // Fast or glass-cannon Pokémon should normally keep attacking moves.
        if (offenseProfile >= bulk + 35 || (bestOffense >= 100 && p.SPE >= 90))
            return 0;

        return 0;
    }

    private static bool IsStoredPowerStyleMove(int moveID, string moveKey)
        => moveID is 500 or 681 || ContainsAny(moveKey, "storedpower", "poderreserva", "powertrip", "chuleria", "chulería");

    private static bool BoostsOwnStats(int moveID, string moveKey, int category, int power, int species, int abilitySlot)
    {
        if (IsReliableSetupMove(moveKey))
            return true;

        if (category != 0 && power > 0)
        {
            if (ContainsAny(moveKey,
                "flamecharge", "nitrocarga",
                "poweruppunch", "incrementopuno",
                "chargebeam", "rayocarga",
                "fellstinger", "aguijonletal",
                "aurawheel", "ruedaaural",
                "trailblaze", "abrecaminos"))
                return true;

            var abilityKeys = GetAbilityKeys(species, abilitySlot);
            if (HasAbility(abilityKeys, "contrary", "respondon") && IsContraryBoostMove(moveKey))
                return true;
        }

        return false;
    }

    private static bool IsReliableSetupMove(string moveKey)
        => IsPhysicalSetupMove(moveKey) || IsSpecialSetupMove(moveKey) || IsMixedSetupMove(moveKey) || ContainsAny(moveKey,
            "amnesia", "amnesia",
            "irondefense", "defensaferrea",
            "cosmicpower", "podercosmico",
            "acidarmor", "armaduraacida",
            "agility", "agilidad",
            "rockpolish", "pulimento",
            "autotomize", "aligerar",
            "doubleteam", "dobleequipo",
            "minimize", "reduccion",
            "stockpile", "reserva",
            "curse", "maldicion",
            "acupressure", "acupresion");

    private static bool IsPhysicalSetupMove(string moveKey) => ContainsAny(moveKey,
        "swordsdance", "danzaespada",
        "dragondance", "danzadragon",
        "bulkup", "corpulen",
        "coil", "enrosque",
        "honeclaws", "afilagarras",
        "bellydrum", "tambor");

    private static bool IsSpecialSetupMove(string moveKey) => ContainsAny(moveKey,
        "nastyplot", "maquinacion",
        "calmmind", "pazmental",
        "quiverdance", "danzaaleteo",
        "tailglow", "rafaga",
        "geomancy", "geocontrol");

    private static bool IsMixedSetupMove(string moveKey) => ContainsAny(moveKey,
        "shellsmash", "rompecoraza", "abrecoraza",
        "workup", "avivar",
        "growth", "desarrollo");

    private static bool IsSelfSupportStatusMove(string moveKey)
        => IsReliableSetupMove(moveKey) || ContainsAny(moveKey,
            "recover", "roost", "softboiled", "milkdrink", "slackoff", "synthesis", "morningsun", "moonlight",
            "reflect", "lightscreen", "auroraveil", "tailwind",
            "substitute", "protect", "detect", "spikyshield", "kingsshield", "banefulbunker");

    private static bool IsOpponentUtilityStatusMove(string moveKey) => ContainsAny(moveKey,
        "willowisp", "fuegofatuo",
        "toxic", "toxico",
        "thunderwave", "ondatrueno",
        "spore", "espora",
        "sleeppowder", "somnifero",
        "stunspore", "paralizador",
        "glare", "deslumbrar",
        "nuzzle", "mofleteestatico",
        "taunt", "mofa",
        "encore", "otra vez", "otravez",
        "leechseed", "drenadoras",
        "confuseray", "rayoconfuso",
        "trick", "truco",
        "switcheroo", "trapicheo");

    private static bool IsContraryBoostMove(string moveKey) => ContainsAny(moveKey,
        "leafstorm", "lluevehojas",
        "dracometeor", "cometadraco",
        "overheat", "sofoco",
        "superpower", "fuerzabruta",
        "closecombat", "abocajarro",
        "vcreate",
        "psychoboost", "psicoataque",
        "dragonascent", "ascensodraco",
        "fleurcannon", "canonfloral",
        "clangingscales", "escamadragon",
        "icehammer", "martillohielo");

    private static bool TryScoreWeatherMove(string moveKey, int species, HashSet<string> abilityKeys, out int score)
    {
        score = 0;

        if (ContainsAny(moveKey, "sandstorm", "tormentadearena"))
        {
            if (HasAbility(abilityKeys, "sandrush", "impulsoarena", "sandforce", "poderarena", "sandveil", "veloarena"))
                score = 84;
            else if (HasType(species, 5)) // Rock gets the Sp. Def boost in sand.
                score = 72;
            return true;
        }

        if (ContainsAny(moveKey, "raindance", "danzalluvia"))
        {
            if (HasAbility(abilityKeys, "swiftswim", "nado rapido", "nadorapido", "raindish", "curalluvia", "hydration", "hidratacion", "dryskin", "pielseca"))
                score = 84;
            else if (HasType(species, 10)) // Water STAB benefits from rain.
                score = 70;
            return true;
        }

        if (ContainsAny(moveKey, "sunnyday", "diasoleado"))
        {
            if (HasAbility(abilityKeys, "chlorophyll", "clorofila", "solarpower", "poder solar", "podersolar", "harvest", "cosecha", "leafguard", "defensahoja"))
                score = 84;
            else if (HasType(species, 9) || HasType(species, 11)) // Fire boost or Grass solar move support.
                score = 70;
            return true;
        }

        if (ContainsAny(moveKey, "hail", "granizo"))
        {
            if (HasAbility(abilityKeys, "slushrush", "quitaneves", "icebody", "gélido", "gelido", "snowcloak", "mantoniveo"))
                score = 84;
            else if (HasType(species, 14))
                score = 70;
            return true;
        }

        return false;
    }

    public static int GetWeatherSupportMask(int species, int abilitySlot, IEnumerable<int> moveIDs)
    {
        int mask = GetWeatherMaskFromAbility(GetAbilityKeys(species, abilitySlot));
        string[] moveNames = Main.Config.GetText(TextName.MoveNames);
        foreach (int moveID in moveIDs ?? Array.Empty<int>())
        {
            if (moveID <= 0 || moveID >= moveNames.Length)
                continue;
            mask |= GetWeatherMaskFromMoveKey(Normalize(moveNames[moveID]));
        }
        return mask;
    }

    public static int GetWeatherAbilityMask(int species, int abilitySlot)
        => GetWeatherMaskFromAbility(GetAbilityKeys(species, abilitySlot));

    private static int GetWeatherMaskFromAbility(HashSet<string> abilityKeys)
    {
        int mask = 0;
        if (HasAbility(abilityKeys, "drizzle", "llovizna"))
            mask |= WeatherRain;
        if (HasAbility(abilityKeys, "drought", "sequia", "sequía"))
            mask |= WeatherSun;
        if (HasAbility(abilityKeys, "sandstream", "chorroarena", "tormentadearena"))
            mask |= WeatherSand;
        if (HasAbility(abilityKeys, "snowwarning", "nevada", "granizo"))
            mask |= WeatherHail;
        return mask;
    }

    private static int GetWeatherMaskFromMoveKey(string moveKey)
    {
        if (ContainsAny(moveKey, "raindance", "danzalluvia"))
            return WeatherRain;
        if (ContainsAny(moveKey, "sunnyday", "diasoleado"))
            return WeatherSun;
        if (ContainsAny(moveKey, "sandstorm", "tormentadearena"))
            return WeatherSand;
        if (ContainsAny(moveKey, "hail", "granizo", "snowscape", "paisajenevado"))
            return WeatherHail;
        return 0;
    }

    private static bool IsRegeneratorPivotMove(string moveKey) => ContainsAny(moveKey,
        "teleport", "teletransporte",
        "uturn", "idavuelta",
        "voltswitch", "voltiocambio",
        "flipturn", "viraje",
        "partingshot", "ultimapalabra",
        "batonpass", "relevo");

    private static bool HasRequiredWeatherSupport(string moveKey, int category, int power, int weatherMask, HashSet<string> abilityKeys, int accuracy)
    {
        // Exact matches are intentional. ContainsAny("thunder") would also catch Thunderbolt/Thunder Wave.
        bool accuracyAbility = HasAbility(abilityKeys, "noguard", "indefenso", "compoundeyes", "ojocompuesto");

        if (MatchesAny(moveKey, "weatherball", "meteorobola"))
            return weatherMask != 0;

        if (MatchesAny(moveKey, "solarbeam", "rayosolar", "solarblade", "cuchillasolar"))
            return (weatherMask & WeatherSun) != 0;

        if (MatchesAny(moveKey, "thunder", "trueno", "hurricane", "vendaval"))
            return (weatherMask & WeatherRain) != 0 || accuracyAbility;

        if (MatchesAny(moveKey, "blizzard", "ventisca"))
            return (weatherMask & WeatherHail) != 0 || accuracyAbility;

        return true;
    }

    private static bool IsWeatherAbuserMove(string moveKey, int type, int category, int power, int weatherMask)
        => GetWeatherMoveBonus(moveKey, type, category, power, weatherMask) >= 50;

    private static int GetWeatherMoveBonus(string moveKey, int type, int category, int power, int weatherMask)
    {
        if (weatherMask == 0)
            return 0;

        bool damaging = category != 0 && power > 0;
        int bonus = 0;

        if ((weatherMask & WeatherSun) != 0)
        {
            if (MatchesAny(moveKey, "solarbeam", "rayosolar", "solarblade", "cuchillasolar"))
                bonus += 76;
            if (damaging && type == 9) // Fire
                bonus += 14;
            if (ContainsAny(moveKey, "weatherball", "meteorobola", "growth", "desarrollo", "synthesis", "sintesis", "morningsun", "solmatinal"))
                bonus += 34;
        }

        if ((weatherMask & WeatherRain) != 0)
        {
            if (MatchesAny(moveKey, "thunder", "trueno", "hurricane", "vendaval"))
                bonus += 70;
            if (damaging && type == 10) // Water
                bonus += 14;
            if (ContainsAny(moveKey, "weatherball", "meteorobola"))
                bonus += 34;
        }

        if ((weatherMask & WeatherSand) != 0)
        {
            if (ContainsAny(moveKey, "shoreup", "recogearena", "weatherball", "meteorobola"))
                bonus += 52;
        }

        if ((weatherMask & WeatherHail) != 0)
        {
            if (MatchesAny(moveKey, "blizzard", "ventisca", "auroraveil", "veloaurora", "weatherball", "meteorobola"))
                bonus += 62;
        }

        return bonus;
    }

    private static int GetAbilityMoveBonus(string moveKey, int category, int power, int accuracy, int type, int species, HashSet<string> abilityKeys)
    {
        if (abilityKeys.Count == 0)
            return 0;

        bool damaging = category != 0 && power > 0;
        int bonus = 0;

        if (HasAbility(abilityKeys, "contrary", "respondon"))
        {
            if (ContainsAny(moveKey, "shellsmash", "rompecoraza", "abrecoraza"))
                return -120;
            if (ContainsAny(moveKey,
                "leafstorm", "lluevehojas",
                "dracometeor", "cometadraco",
                "overheat", "sofoco",
                "superpower", "fuerzabruta",
                "closecombat", "abocajarro",
                "vcreate", "victini",
                "psychoboost", "psicoataque",
                "dragonascent", "ascensodraco",
                "fleurcannon", "canonfloral",
                "clangingscales", "escamadragon",
                "icehammer", "martillohielo"))
                bonus += 48;
        }

        if (damaging && HasAbility(abilityKeys, "technician", "experto") && power > 0 && power <= 60)
            bonus += 28;

        if (damaging && HasAbility(abilityKeys, "skilllink", "encadenado") && ContainsAny(moveKey,
            "bulletseed", "pinmissile", "rockblast", "iciclespear", "tailslap", "armthrust", "cometpunch", "furyattack", "furyswipes", "doubleslap", "bonerush"))
            bonus += 42;

        if (damaging && HasAbility(abilityKeys, "sheerforce", "potenciabruta") && ContainsAny(moveKey,
            "flamethrower", "fireblast", "icebeam", "blizzard", "thunderbolt", "thunder", "sludgebomb", "earthpower", "psychic", "shadowball", "crunch", "ironhead", "rockslide", "waterpulse", "darkpulse", "dragonpulse", "moongeistbeam"))
            bonus += 24;

        if (damaging && HasAbility(abilityKeys, "serenegrace", "dichoso") && ContainsAny(moveKey,
            "airslash", "ironhead", "bodyslam", "thunder", "thunderbolt", "icebeam", "flamethrower", "scald", "shadowball", "psychic", "rockslide"))
            bonus += 20;

        if (damaging && HasAbility(abilityKeys, "noguard", "indefenso") && accuracy > 0 && accuracy < 90)
            bonus += 30;
        else if (damaging && HasAbility(abilityKeys, "compoundeyes", "ojocompuesto") && accuracy > 0 && accuracy < 90)
            bonus += 18;

        if (damaging && HasAbility(abilityKeys, "rockhead", "cabezadura") && ContainsAny(moveKey,
            "doubleedge", "headsmash", "flareblitz", "woodhammer", "bravebird", "wildcharge", "volttackle", "taketdown", "submision", "submission"))
            bonus += 36;
        if (damaging && HasAbility(abilityKeys, "reckless", "audaz") && ContainsAny(moveKey,
            "doubleedge", "headsmash", "flareblitz", "woodhammer", "bravebird", "wildcharge", "volttackle", "highjumpkick", "jumpkick", "taketdown", "submission"))
            bonus += 28;

        if (damaging && HasAbility(abilityKeys, "ironfist", "punoferrico", "punohierro") && ContainsAny(moveKey, "punch", "puno"))
            bonus += 28;
        if (damaging && HasAbility(abilityKeys, "strongjaw", "mandibulafuerte") && ContainsAny(moveKey, "fang", "colmillo", "bite", "mordisco", "crunch", "triturar"))
            bonus += 28;
        if (damaging && HasAbility(abilityKeys, "megalauncher", "megadisparador") && ContainsAny(moveKey, "pulse", "pulso", "aurasphere", "esferaaural"))
            bonus += 28;
        if (damaging && HasAbility(abilityKeys, "toughclaws", "garrafuerte") && ContainsAny(moveKey,
            "claw", "garra", "punch", "puno", "fang", "colmillo", "slash", "cuchillada", "contact"))
            bonus += 18;

        if (damaging && HasAbility(abilityKeys, "adaptability", "adaptable") && IsSTAB(species, type))
            bonus += 22;

        if (!damaging && HasAbility(abilityKeys, "prankster", "bromista") && ContainsAny(moveKey,
            "willowisp", "toxic", "thunderwave", "spore", "sleeppowder", "taunt", "encore", "reflect", "lightscreen", "recover", "roost", "substitute", "tailwind"))
            bonus += 22;
        if (!damaging && HasAbility(abilityKeys, "triage", "primerauxilio") && ContainsAny(moveKey,
            "recover", "roost", "synthesis", "morningsun", "moonlight", "drainingkiss", "gigadrain", "drainpunch", "hornleech"))
            bonus += 22;

        if (HasAbility(abilityKeys, "regenerator", "regeneracion", "regeneración") && IsRegeneratorPivotMove(moveKey))
        {
            bonus += ContainsAny(moveKey, "teleport", "teletransporte") ? 74 : 46;
        }

        return bonus;
    }

    private static bool HasAbility(HashSet<string> abilityKeys, params string[] tokens)
        => abilityKeys.Any(a => tokens.Any(a.Contains));

    private static bool AddBest(List<Candidate> selected, IEnumerable<Candidate> source, bool preferDifferentType)
    {
        var used = selected.Select(c => c.Move).ToHashSet();
        var usedTypes = selected.Where(c => c.Damaging).Select(c => c.Type).ToHashSet();
        var usable = source
            .Where(c => !used.Contains(c.Move))
            .Where(c => HasRequiredPartners(c, selected))
            .Where(c => !preferDifferentType || !c.Damaging || !usedTypes.Contains(c.Type))
            .OrderByDescending(c => c.Score)
            .Take(6)
            .ToList();

        if (usable.Count == 0 && preferDifferentType)
            usable = source
                .Where(c => !used.Contains(c.Move))
                .Where(c => HasRequiredPartners(c, selected))
                .OrderByDescending(c => c.Score)
                .Take(6)
                .ToList();
        if (usable.Count == 0)
            return false;

        int total = usable.Sum(c => Math.Max(1, c.Score));
        int roll = (int)(Util.Random32() % total);
        foreach (var c in usable)
        {
            roll -= Math.Max(1, c.Score);
            if (roll < 0)
            {
                selected.Add(c);
                return true;
            }
        }

        selected.Add(usable[0]);
        return true;
    }

    private static bool HasRequiredPartners(Candidate candidate, IEnumerable<Candidate> selected)
    {
        if (candidate.RequiresStatBoostPartner && !selected.Any(c => c.BoostsOwnStats))
            return false;

        return true;
    }

    private static bool IsSTABByAnySelected(List<Candidate> selected, int type) => selected.Any(c => c.Damaging && c.Type == type);

    private static HashSet<int> GetCoverageTypesForWeaknesses(int species)
    {
        var coverage = new HashSet<int>();
        if (species <= 0 || species >= Main.SpeciesStat.Length)
            return coverage;

        int[] defendingTypes = Main.SpeciesStat[species].Types.Where(t => t >= 0).Distinct().ToArray();
        for (int attackingType = 0; attackingType <= 17; attackingType++)
        {
            int scale = 1;
            foreach (int defendingType in defendingTypes)
            {
                int eff = GetTypeEffectiveness(attackingType, defendingType);
                if (eff == 0)
                {
                    scale = 0;
                    break;
                }
                scale *= eff;
            }

            if (scale > 1)
            {
                foreach (int counter in GetCounterCoverageTypes(attackingType))
                    coverage.Add(counter);
            }
        }

        return coverage;
    }

    private static bool HasQuadWeakness(int species)
    {
        if (species <= 0 || species >= Main.SpeciesStat.Length)
            return false;

        int[] defendingTypes = Main.SpeciesStat[species].Types.Where(t => t >= 0).Distinct().ToArray();
        for (int attackingType = 0; attackingType <= 17; attackingType++)
        {
            int scale = 1;
            foreach (int defendingType in defendingTypes)
            {
                int eff = GetTypeEffectiveness(attackingType, defendingType);
                if (eff == 0)
                {
                    scale = 0;
                    break;
                }
                scale *= eff;
            }

            if (scale >= 4)
                return true;
        }

        return false;
    }

    private static IEnumerable<int> GetCounterCoverageTypes(int weaknessType) => weaknessType switch
    {
        1 => new[] { 2, 13, 17 },          // Fighting
        2 => new[] { 12, 5, 14 },          // Flying
        3 => new[] { 4, 13 },              // Poison
        4 => new[] { 10, 11, 14 },         // Ground
        5 => new[] { 10, 11, 1, 4, 8 },    // Rock
        6 => new[] { 9, 2, 5 },            // Bug
        7 => new[] { 7, 16 },              // Ghost
        8 => new[] { 9, 1, 4 },            // Steel
        9 => new[] { 10, 4, 5 },           // Fire
        10 => new[] { 11, 12 },            // Water
        11 => new[] { 9, 14, 3, 2, 6 },    // Grass
        12 => new[] { 4 },                 // Electric
        13 => new[] { 6, 7, 16 },          // Psychic
        14 => new[] { 9, 1, 5, 8 },        // Ice
        15 => new[] { 14, 15, 17 },        // Dragon
        16 => new[] { 1, 6, 17 },          // Dark
        17 => new[] { 3, 8 },              // Fairy
        _ => Array.Empty<int>(),
    };

    // Type ids in pk3DS are the standard Gen6/7 order:
    // Normal, Fighting, Flying, Poison, Ground, Rock, Bug, Ghost, Steel, Fire, Water, Grass, Electric, Psychic, Ice, Dragon, Dark, Fairy.
    private static int GetTypeEffectiveness(int attack, int defend) => (attack, defend) switch
    {
        (_, 0) when attack == 7 => 0,
        (_, 0) when attack == 1 => 2,
        (_, 0) => 1,

        (2, 1) or (13, 1) or (17, 1) => 2,
        (5, 1) or (6, 1) or (16, 1) => 1,
        (1, 1) => 1,

        (12, 2) or (5, 2) or (14, 2) => 2,
        (1, 2) or (6, 2) or (11, 2) => 1,

        (4, 3) or (13, 3) => 2,
        (1, 3) or (3, 3) or (6, 3) or (11, 3) or (17, 3) => 1,

        (10, 4) or (11, 4) or (14, 4) => 2,
        (3, 4) or (5, 4) => 1,
        (12, 4) => 0,

        (10, 5) or (11, 5) or (1, 5) or (4, 5) or (8, 5) => 2,
        (0, 5) or (9, 5) or (3, 5) or (2, 5) => 1,

        (9, 6) or (2, 6) or (5, 6) => 2,
        (1, 6) or (4, 6) or (11, 6) => 1,

        (7, 7) or (16, 7) => 2,
        (3, 7) or (6, 7) => 1,
        (0, 7) or (1, 7) => 0,

        (9, 8) or (1, 8) or (4, 8) => 2,
        (0, 8) or (11, 8) or (14, 8) or (2, 8) or (13, 8) or (6, 8) or (5, 8) or (15, 8) or (8, 8) or (17, 8) => 1,
        (3, 8) => 0,

        (10, 9) or (4, 9) or (5, 9) => 2,
        (9, 9) or (11, 9) or (14, 9) or (6, 9) or (8, 9) or (17, 9) => 1,

        (11, 10) or (12, 10) => 2,
        (9, 10) or (10, 10) or (14, 10) or (8, 10) => 1,

        (9, 11) or (14, 11) or (3, 11) or (2, 11) or (6, 11) => 2,
        (10, 11) or (11, 11) or (12, 11) or (4, 11) => 1,

        (4, 12) => 2,
        (12, 12) or (2, 12) or (8, 12) => 1,

        (6, 13) or (7, 13) or (16, 13) => 2,
        (1, 13) or (13, 13) => 1,

        (9, 14) or (1, 14) or (5, 14) or (8, 14) => 2,
        (14, 14) => 1,

        (14, 15) or (15, 15) or (17, 15) => 2,
        (9, 15) or (10, 15) or (11, 15) or (12, 15) => 1,

        (1, 16) or (6, 16) or (17, 16) => 2,
        (7, 16) or (16, 16) => 1,
        (13, 16) => 0,

        (3, 17) or (8, 17) => 2,
        (1, 17) or (6, 17) or (16, 17) => 1,
        (15, 17) => 0,

        _ => 1,
    };

    private static int[] GetSpeciesTypes(int species)
    {
        if (species <= 0 || species >= Main.SpeciesStat.Length)
            return [];

        return Main.SpeciesStat[species].Types
            .Where(t => t >= 0)
            .Distinct()
            .ToArray();
    }

    private static bool HasType(int species, int type)
    {
        if (species <= 0 || species >= Main.SpeciesStat.Length)
            return false;
        return Main.SpeciesStat[species].Types.Contains(type);
    }

    private static bool IsSTAB(int species, int type)
    {
        if (species <= 0 || species >= Main.SpeciesStat.Length)
            return false;
        return Main.SpeciesStat[species].Types.Contains(type);
    }

    private static bool IsPreferredCategory(int species, int category)
    {
        int preferred = GetPreferredMoveCategory(species, 12);
        return preferred == 0 || preferred == category;
    }

    private static int GetPreferredMoveCategory(int species, int tolerance)
    {
        if (species <= 0 || species >= Main.SpeciesStat.Length)
            return 0;

        var p = Main.SpeciesStat[species];
        int delta = p.ATK - p.SPA;
        int tol = Math.Max(0, tolerance);
        if (delta > tol)
            return 1;
        if (-delta > tol)
            return 2;
        return 0;
    }

    private static IEnumerable<int> GetTMMoves(int species, int form, int generation)
    {
        var tmhm = generation == 6 ? GetGen6TMsHMs() : TMEditor7.GetTMHMList().Select(z => (int)z).ToArray();
        if (tmhm.Length == 0)
            yield break;

        foreach (int move in GetMovesFromBooleanFlags(species, form, "TMHM", tmhm))
            yield return move;
    }

    private static int[] GetGen6TMsHMs()
    {
        TMHMEditor6.GetTMHMList(out ushort[] tms, out ushort[] hms);
        return tms.Concat(hms).Select(z => (int)z).ToArray();
    }

    private static IEnumerable<int> GetTutorMoves(int species, int form, int generation)
    {
        int[] basic = generation == 6 ? Gen6BasicTutors : Gen7BasicTutors;
        foreach (int move in GetMovesFromBooleanFlags(species, form, "TypeTutors", basic))
            yield return move;

        int[] special = generation == 6 ? Gen6ORASTutors : Gen7USUMTutors;
        foreach (int move in GetMovesFromNestedBooleanFlags(species, form, "SpecialTutors", special))
            yield return move;
    }

    private static IEnumerable<int> GetMovesFromBooleanFlags(int species, int form, string propertyName, int[] moveList)
    {
        object personal = GetPersonal(species, form);
        if (personal is null)
            yield break;

        var prop = personal.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (prop?.GetValue(personal) is not bool[] flags)
            yield break;

        int count = Math.Min(flags.Length, moveList.Length);
        for (int i = 0; i < count; i++)
        {
            if (flags[i] && moveList[i] > 0)
                yield return moveList[i];
        }
    }

    private static IEnumerable<int> GetMovesFromNestedBooleanFlags(int species, int form, string propertyName, int[] moveList)
    {
        object personal = GetPersonal(species, form);
        if (personal is null)
            yield break;

        var prop = personal.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (prop?.GetValue(personal) is not bool[][] groups)
            yield break;

        int index = 0;
        foreach (bool[] group in groups)
        {
            foreach (bool flag in group)
            {
                if (index >= moveList.Length)
                    yield break;
                if (flag && moveList[index] > 0)
                    yield return moveList[index];
                index++;
            }
        }
    }

    private static object GetPersonal(int species, int form)
    {
        if (species <= 0 || species >= Main.SpeciesStat.Length)
            return null;

        try
        {
            int entry = Main.Config.Personal.GetFormIndex(species, form);
            if (entry > 0 && entry < Main.SpeciesStat.Length)
                return Main.SpeciesStat[entry];
        }
        catch { }

        return Main.SpeciesStat[species];
    }

    private static IEnumerable<int> GetEggMoves(int species, int form, int generation)
    {
        int cacheKey = generation * 10000 + Math.Max(0, species);
        if (EggMoveCache.TryGetValue(cacheKey, out int[] cached))
            return cached;

        try
        {
            var garc = Main.Config.GetGARCData("eggmove");
            byte[][] files = garc.Files;
            if (species <= 0 || species >= files.Length || files[species].Length == 0)
                return EggMoveCache[cacheKey] = [];

            int[] moves;
            if (generation == 6)
                moves = new EggMoves6(files[species]).Moves.Select(z => (int)z).ToArray();
            else
                moves = new EggMoves7(files[species]).Moves.Select(z => (int)z).ToArray();

            return EggMoveCache[cacheKey] = moves.Where(m => m > 0).Distinct().ToArray();
        }
        catch
        {
            return EggMoveCache[cacheKey] = [];
        }
    }

    private static IEnumerable<int> GetPreEvolutions(int species, int generation)
    {
        var map = generation == 6
            ? PreEvolutionCache6 ??= BuildPreEvolutionMap(6)
            : PreEvolutionCache7 ??= BuildPreEvolutionMap(7);

        return map.TryGetValue(species, out int[] prevos) ? prevos : [];
    }

    private static Dictionary<int, int[]> BuildPreEvolutionMap(int generation)
    {
        var direct = new Dictionary<int, List<int>>();
        try
        {
            byte[][] files = Main.Config.GetGARCData("evolution").Files;
            for (int source = 1; source < files.Length; source++)
            {
                if (files[source].Length == 0)
                    continue;

                IEnumerable<int> targets;
                if (generation == 6)
                    targets = new EvolutionSet6(files[source]).PossibleEvolutions.Select(e => (int)e.Species);
                else
                    targets = new EvolutionSet7(files[source]).PossibleEvolutions.Select(e => (int)e.Species);

                foreach (int target in targets.Where(t => t > 0 && t != source))
                {
                    if (!direct.TryGetValue(target, out var list))
                        direct[target] = list = [];
                    if (!list.Contains(source))
                        list.Add(source);
                }
            }
        }
        catch { }

        var result = new Dictionary<int, int[]>();
        foreach (int target in direct.Keys)
        {
            var all = new HashSet<int>();
            AddPreEvolutionsRecursive(target, direct, all);
            result[target] = all.ToArray();
        }

        return result;
    }

    private static void AddPreEvolutionsRecursive(int species, Dictionary<int, List<int>> direct, HashSet<int> result)
    {
        if (!direct.TryGetValue(species, out var prevos))
            return;

        foreach (int prevo in prevos)
        {
            if (result.Add(prevo))
                AddPreEvolutionsRecursive(prevo, direct, result);
        }
    }

    private static int[] SafeGetCurrentMoves(LearnsetRandomizer learn, int species, int form, int level)
    {
        try { return learn.GetCurrentMoves(species, form, level, 4); }
        catch { return []; }
    }

    private static int[] SafeGetMovesUpToLevel(LearnsetRandomizer learn, int species, int form, int level)
    {
        try { return learn.GetMovesUpToLevel(species, form, level); }
        catch { return []; }
    }

    private static int[] SafeGetHighPoweredMoves(LearnsetRandomizer learn, int species, int form)
    {
        try { return learn.GetHighPoweredMoves(species, form, 4); }
        catch { return []; }
    }

    private static int[] SafeGetRandomMoves(MoveRandomizer move, int species)
    {
        try { return move.GetRandomMoveset(species, 4); }
        catch { return []; }
    }

    private static void AddMoves(HashSet<int> pool, IEnumerable<int> moves)
    {
        if (moves is null)
            return;
        foreach (int move in moves)
            if (move > 0)
                pool.Add(move);
    }

    private static HashSet<string> GetAbilityKeys(int species, int abilitySlot)
    {
        var result = new HashSet<string>();
        string[] abilityNames = Main.Config.GetText(TextName.AbilityNames);
        if (species <= 0 || species >= Main.SpeciesStat.Length)
            return result;

        int[] abilities = Main.SpeciesStat[species].Abilities;
        if (abilities.Length == 0)
            return result;

        if (abilitySlot > 0)
        {
            int index = Math.Clamp(abilitySlot - 1, 0, abilities.Length - 1);
            AddAbility(abilities[index]);
        }
        else
        {
            foreach (int ability in abilities)
                AddAbility(ability);
        }

        return result;

        void AddAbility(int ability)
        {
            if (ability <= 0 || ability >= abilityNames.Length)
                return;

            string key = Normalize(abilityNames[ability]);
            if (!string.IsNullOrWhiteSpace(key))
                result.Add(key);
        }
    }

    private static string Normalize(string value)
    {
        var chars = value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray();
        return new string(chars);
    }

    private static bool MatchesAny(string text, params string[] tokens) => tokens.Any(t => text == t);

    private static bool ContainsAny(string text, params string[] tokens) => tokens.Any(text.Contains);
}
