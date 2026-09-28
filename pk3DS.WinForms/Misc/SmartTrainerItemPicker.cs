using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

using pk3DS.Core;

namespace pk3DS.WinForms;

internal static class SmartTrainerItemPicker
{
    private sealed record Candidate(int Item, int Score);


    public static int[] GetBanBadItemPool(IEnumerable<int> itemPool)
    {
        string[] itemNames = Main.Config.GetText(TextName.ItemNames);

        return itemPool
            .Select(Convert.ToInt32)
            .Where(i => i > 0 && i < itemNames.Length)
            .Where(i => !string.IsNullOrWhiteSpace(itemNames[i]))
            .Where(i => !IsNeverUsefulTrainerItem(Normalize(itemNames[i])))
            .Distinct()
            .ToArray();
    }

    public static int[] AddSmartTrainerItemPoolExtras(IEnumerable<int> itemPool)
    {
        string[] itemNames = Main.Config.GetText(TextName.ItemNames);
        var pool = itemPool
            .Select(Convert.ToInt32)
            .Where(i => i > 0 && i < itemNames.Length)
            .Where(i => !string.IsNullOrWhiteSpace(itemNames[i]))
            .ToHashSet();

        AddItemsByKey(pool, itemNames,
            "powerherb", "hierbaunica",
            "whiteherb", "hierbablanca",
            "mentalherb", "hierbamental",
            "leftovers", "restos",
            "lifeorb", "vidasfera",
            "focussash", "bandafocus",
            "choiceband", "cintaelegida",
            "choicespecs", "gafaselegidas",
            "choicescarf", "panuelolegido", "panueloselegido",
            "expertbelt", "cintoexperto",
            "muscleband", "cintafuerte",
            "wiseglasses", "gafasespeciales",
            "assaultvest", "chalecoasalto",
            "eviolite", "mineralevol",
            "blacksludge", "lodonegro",
            "heatrock", "rocacalor",
            "damprock", "rocalluvia", "rocahumeda",
            "smoothrock", "rocasuave",
            "icyrock", "rocahelada"
        );

        return pool.OrderBy(i => i).ToArray();
    }
    public static int PickNormalRelevant(
        int species,
        int form,
        int level,
        IEnumerable<int> moveIDs,
        IEnumerable<int> itemPool,
        int abilitySlot,
        bool isFinalEvolution)
    {
        string[] itemNames = Main.Config.GetText(TextName.ItemNames);
        string[] moveNames = Main.Config.GetText(TextName.MoveNames);
        string[] abilityNames = Main.Config.GetText(TextName.AbilityNames);

        int maxItemID = itemNames.Length;

        if (!Main.Config.ORAS && !Main.Config.SM && !Main.Config.USUM)
            maxItemID = Math.Min(maxItemID, 718);

        int[] pool = itemPool
            .Select(Convert.ToInt32)
            .Where(i => i > 0 && i < maxItemID && i < itemNames.Length)
            .Where(i => !string.IsNullOrWhiteSpace(itemNames[i]))
            .Where(i => !IsNeverUsefulTrainerItem(Normalize(itemNames[i])))
            .Distinct()
            .ToArray();

        if (pool.Length == 0)
            return 0;

        var moveInfo = BuildMoveInfo(species, moveIDs, moveNames);
        var abilityKeys = GetAbilityKeys(species, abilitySlot, abilityNames);
        int[] speciesTypes = GetSpeciesTypes(species);
        string speciesKey = GetSpeciesKey(species);

        var relevant = new List<int>();

        foreach (int item in pool)
        {
            string itemKey = Normalize(itemNames[item]);

            int score = ScoreItem(
                item,
                itemKey,
                species,
                speciesKey,
                speciesTypes,
                isFinalEvolution,
                moveInfo,
                abilityKeys,
                0,
                level);

            if (score > 0)
                relevant.Add(item);
        }

        if (relevant.Count == 0)
            return PickSafeFallback(pool, itemNames);

        return relevant[(int)(Util.Random32() % relevant.Count)];
    }
    public static int Pick(
        int species,
        int form,
        int level,
        IEnumerable<int> moveIDs,
        IEnumerable<int> itemPool,
        int abilitySlot,
        bool isFinalEvolution,
        int qualityMode)
    {
        string[] itemNames = Main.Config.GetText(TextName.ItemNames);
        string[] moveNames = Main.Config.GetText(TextName.MoveNames);
        string[] abilityNames = Main.Config.GetText(TextName.AbilityNames);

        int maxItemID = itemNames.Length;

        // XY has fewer valid item IDs than ORAS.
        if (!Main.Config.ORAS && !Main.Config.SM && !Main.Config.USUM)
            maxItemID = Math.Min(maxItemID, 718);

        int[] pool = itemPool
            .Select(Convert.ToInt32)
            .Where(i => i > 0 && i < maxItemID && i < itemNames.Length)
            .Where(i => !string.IsNullOrWhiteSpace(itemNames[i]))
            .Distinct()
            .ToArray();

        if (pool.Length == 0)
        {
            pool = Enumerable.Range(1, Math.Max(0, maxItemID - 1))
                .Where(i => i < itemNames.Length)
                .Where(i => !string.IsNullOrWhiteSpace(itemNames[i]))
                .Where(i => !IsNeverUsefulTrainerItem(Normalize(itemNames[i])))
                .ToArray();
        }

        var moveInfo = BuildMoveInfo(species, moveIDs, moveNames);
        var abilityKeys = GetAbilityKeys(species, abilitySlot, abilityNames);
        int[] speciesTypes = GetSpeciesTypes(species);
        string speciesKey = GetSpeciesKey(species);

        var candidates = new List<Candidate>();

        foreach (int item in pool)
        {
            string itemKey = Normalize(itemNames[item]);
            if (IsNeverUsefulTrainerItem(itemKey))
                continue;

            int score = ScoreItem(item, itemKey, species, speciesKey, speciesTypes, isFinalEvolution, moveInfo, abilityKeys, qualityMode, level);
            if (score > 0)
                candidates.Add(new Candidate(item, score));
        }

        if (candidates.Count == 0)
            return PickSafeFallback(pool, itemNames);

        int best = candidates.Max(c => c.Score);
        int floor = Math.Max(1, best - 10);

        var top = candidates
            .Where(c => c.Score >= floor)
            .OrderByDescending(c => c.Score)
            .Take(6)
            .ToArray();

        int total = top.Sum(c => Math.Max(1, c.Score));
        int roll = (int)(Util.Random32() % total);

        foreach (var c in top)
        {
            roll -= Math.Max(1, c.Score);
            if (roll < 0)
                return c.Item;
        }

        return top[0].Item;
    }

    private sealed record MoveInfo(
        int DamagingCount,
        int PhysicalCount,
        int SpecialCount,
        int StatusCount,
        HashSet<int> DamagingTypes,
        HashSet<string> MoveNames
    );

    private static MoveInfo BuildMoveInfo(int species, IEnumerable<int> moveIDs, string[] moveNames)
    {
        int damaging = 0;
        int physical = 0;
        int special = 0;
        int status = 0;
        var damagingTypes = new HashSet<int>();
        var names = new HashSet<string>();

        foreach (int moveID in moveIDs.Select(Convert.ToInt32).Where(m => m > 0 && m < Main.Config.Moves.Length).Take(4))
        {
            if (moveID < moveNames.Length)
                names.Add(Normalize(moveNames[moveID]));

            var data = Main.Config.Moves[moveID];

            if (data.Category == 0 || data.Power <= 0)
            {
                status++;
                continue;
            }

            damaging++;
            damagingTypes.Add(data.Type);

            if (data.Category == 1)
                physical++;
            else if (data.Category == 2)
                special++;
        }

        return new MoveInfo(damaging, physical, special, status, damagingTypes, names);
    }

    private static int ScoreItem(
        int itemID,
        string itemKey,
        int species,
        string speciesKey,
        int[] speciesTypes,
        bool isFinalEvolution,
        MoveInfo moves,
        HashSet<string> abilities,
        int qualityMode,
        int level)
    {
        // EV-reducing berries are never useful as trainer held items.
        if (ContainsAny(itemKey, "pomeg", "grana", "kelpsy", "algama", "qualot", "ispero", "hondew", "meluce", "grepa", "uvav", "tamato", "tamate"))
            return 0;

        int speciesScore = ScoreSpeciesSpecificItem(itemKey, speciesKey);
        if (speciesScore > 0)
            return speciesScore;

        bool bulky = IsBulkySpecies(species);
        bool veryBulky = IsVeryBulkySpecies(species);
        bool offensive = IsOffensiveSpecies(species);
        bool hasChoiceTrick = HasMoveContaining(moves, "trick", "truco", "switcheroo", "trapicheo");
        bool hasLastResort = HasMoveContaining(moves, "lastresort", "ultimabaza");
        bool hasPrimordialSea = HasAbility(abilities, "primordialsea", "mardelalbor");
        bool hasDesolateLand = HasAbility(abilities, "desolateland", "tierradeldesaliento");
        bool hasDeltaStream = HasAbility(abilities, "deltastream", "rafagadelta");

        bool setsRain = !hasPrimordialSea && (HasMoveContaining(moves, "raindance", "danzalluvia") || HasAbility(abilities, "drizzle", "llovizna"));
        bool setsSun = !hasDesolateLand && (HasMoveContaining(moves, "sunnyday", "diasoleado") || HasAbility(abilities, "drought", "sequia", "sequía"));
        bool setsSand = HasMoveContaining(moves, "sandstorm", "tormentaarena", "tormentadearena") || HasAbility(abilities, "sandstream", "chorroarena");
        bool setsHail = HasMoveContaining(moves, "hail", "granizo", "snowscape", "paisajenevado") || HasAbility(abilities, "snowwarning", "nevada");
        bool hasPermanentWeather = hasPrimordialSea || hasDesolateLand || hasDeltaStream;

        // Orb items only when the ability makes the status useful.
        if (ContainsAny(itemKey, "toxicorb", "toxicoesfera", "esferatoxica"))
        {
            if (HasAbility(abilities, "poisonheal", "antidoto"))
                return 98;
            if (HasAbility(abilities, "guts", "agallas", "quickfeet", "piesrapidos", "marvelscale", "escamamarina"))
                return 76;
            return 0;
        }

        if (ContainsAny(itemKey, "flameorb", "llamaesfera", "esferallama"))
        {
            if (HasAbility(abilities, "guts", "agallas", "quickfeet", "piesrapidos", "marvelscale", "escamamarina", "magicguard", "muromagico"))
                return 78;
            return 0;
        }

        int boostType = GetBoostedTypeFromItem(itemKey);
        if (boostType >= 0)
        {
            if (!moves.DamagingTypes.Contains(boostType))
                return 0;

            int score = IsZCrystal(itemKey) ? 74 : IsConsumableTypeBooster(itemKey) ? 66 : 56;
            if (speciesTypes.Contains(boostType))
                score += 16;
            if (moves.DamagingTypes.Count >= 3)
                score += 4;

            return AdjustByQuality(score, qualityMode, IsZCrystal(itemKey) || IsConsumableTypeBooster(itemKey));
        }

        int resistType = GetResistBerryType(itemKey);
        if (resistType >= 0)
            return IsWeakTo(resistType, speciesTypes) ? 66 : 0;

        if (IsBerry(itemKey))
        {
            // Early-game healing berries are useful and not random nonsense.
            if (ContainsAny(itemKey, "oran", "aranja"))
                return level <= 25 ? 74 : 38;

            if (ContainsAny(itemKey, "sitrus", "zidra"))
                return 66 + (bulky ? 22 : 0) + (veryBulky ? 8 : 0);

            if (ContainsAny(itemKey, "figy", "wiki", "mago", "aguav", "iapapa"))
                return 62 + (bulky ? 18 : 0);

            if (ContainsAny(itemKey, "lum", "zreza"))
                return 54;

            if (ContainsAny(itemKey, "chesto", "ataniya"))
                return HasMoveContaining(moves, "rest", "descanso") ? 78 : 36;

            if (ContainsAny(itemKey, "persim", "caquic"))
                return 32;

            // Pinch/stat berries are not generally smart.
            // They are only allowed in Competitive mode at higher levels and with sets that can intentionally activate them.
            if (ContainsAny(itemKey, "liechi", "ganlon", "salac", "petaya", "apicot", "lansat", "starf", "custap"))
            {
                if (qualityMode >= 2 && level >= 30 && HasMoveContaining(moves, "substitute", "sustituto", "endure", "aguante", "bellydrum", "tambor", "flail", "azote", "reversal", "inversion"))
                    return 62;

                return 0;
            }

            // Damage-reflect berries are too niche for random trainer sets.
            if (ContainsAny(itemKey, "jaboca", "rowap"))
                return 0;

            // Unknown berries are ignored instead of being treated as generally useful.
            return 0;
        }

        if (ContainsAny(itemKey, "lifeorb", "vidasfera"))
        {
            if (moves.DamagingCount < 2)
                return 0;

            int score = 74 + moves.DamagingCount * 3;
            if (HasAbility(abilities, "sheerforce", "potenciabruta", "magicguard", "muromagico"))
                score += 18;
            if (bulky && !offensive)
                score -= 10;

            return AdjustByQuality(score, qualityMode, true);
        }

        if (ContainsAny(itemKey, "choiceband", "cintaelegida"))
        {
            if (hasLastResort)
                return 0;
            if (moves.PhysicalCount >= 3 && moves.StatusCount == 0)
                return AdjustByQuality(86, qualityMode, true);
            if (hasChoiceTrick && moves.DamagingCount >= 2)
                return AdjustByQuality(moves.PhysicalCount >= 2 ? 82 : 70, qualityMode, true);
            return 0;
        }

        if (ContainsAny(itemKey, "choicespecs", "gafaselegidas"))
        {
            if (hasLastResort)
                return 0;
            if (moves.SpecialCount >= 3 && moves.StatusCount == 0)
                return AdjustByQuality(86, qualityMode, true);
            if (hasChoiceTrick && moves.DamagingCount >= 2)
                return AdjustByQuality(moves.SpecialCount >= 2 ? 82 : 70, qualityMode, true);
            return 0;
        }

        if (ContainsAny(itemKey, "choicescarf", "panuelolegido", "panueloscel"))
        {
            if (hasLastResort)
                return 0;
            if (moves.DamagingCount >= 3 && moves.StatusCount == 0)
                return AdjustByQuality(78, qualityMode, true);
            if (hasChoiceTrick && moves.DamagingCount >= 2)
                return AdjustByQuality(84, qualityMode, true);
            return 0;
        }

        if (ContainsAny(itemKey, "expertbelt", "cintoexperto"))
            return moves.DamagingCount >= 2 && moves.DamagingTypes.Count >= 2 ? 72 : 0;

        if (ContainsAny(itemKey, "muscleband", "cintafuerte"))
            return moves.PhysicalCount > 0 ? 48 + moves.PhysicalCount * 7 : 0;

        if (ContainsAny(itemKey, "wiseglasses", "gafasespeciales"))
            return moves.SpecialCount > 0 ? 48 + moves.SpecialCount * 7 : 0;

        if (ContainsAny(itemKey, "leftovers", "restos"))
            return 66 + (bulky ? 24 : 0) + (veryBulky ? 8 : 0);

        if (ContainsAny(itemKey, "blacksludge", "lodonegro"))
            return speciesTypes.Contains(3) ? 72 + (bulky ? 24 : 0) : 0;

        if (ContainsAny(itemKey, "eviolite", "mineralevol"))
            return !isFinalEvolution ? 82 : 0;

        if (ContainsAny(itemKey, "focussash", "bandafocus"))
        {
            int score = level <= 35 ? 58 : 70;
            if (bulky)
                score -= 24;
            if (offensive)
                score += 8;
            return AdjustByQuality(Math.Max(1, score), qualityMode, true);
        }

        if (ContainsAny(itemKey, "focusband", "cintafocus"))
            return 42;

        if (ContainsAny(itemKey, "rockyhelmet", "cascodentado"))
            return 55 + (bulky ? 20 : 0) + (veryBulky ? 6 : 0);

        if (ContainsAny(itemKey, "assaultvest", "chalecoasalto"))
        {
            if (moves.DamagingCount != 4 || moves.StatusCount != 0)
                return 0;

            int score = 78 + (bulky ? 16 : 0) + (veryBulky ? 6 : 0);
            return AdjustByQuality(score, qualityMode, true);
        }

        if (ContainsAny(itemKey, "airballoon", "globohelio"))
            return IsWeakTo(4, speciesTypes) ? 68 : 36;

        if (ContainsAny(itemKey, "weaknesspolicy", "segurodebilidad"))
            return AdjustByQuality(CountWeaknesses(speciesTypes) >= 3 ? 70 : 44, qualityMode, true);

        if (ContainsAny(itemKey, "quickclaw", "garrarapida"))
            return 44;

        if (ContainsAny(itemKey, "kingsrock", "rocarey", "razorfang", "colmilloagudo"))
            return moves.DamagingCount >= 3 ? 42 : 0;

        if (ContainsAny(itemKey, "scopelens", "periscopio", "razorclaw", "garraafilada"))
            return moves.DamagingCount >= 2 ? 44 : 0;

        if (ContainsAny(itemKey, "widelens", "lupa", "zoomlens", "telescopio"))
            return moves.DamagingCount >= 2 ? 46 : 0;

        if (ContainsAny(itemKey, "shellbell", "campanaconcha"))
            return moves.DamagingCount >= 2 ? 46 : 0;

        if (ContainsAny(itemKey, "bigroot", "raizgrande"))
            return HasMoveContaining(moves, "drain", "absorb", "giga", "megadrain", "drenadoras", "absorber") ? 58 : 0;

        if (ContainsAny(itemKey, "lightclay", "refleluz"))
            return HasMoveContaining(moves, "reflect", "lightscreen", "auroraveil", "reflejo", "pantalla", "veloaurora") ? 76 : 0;

        if (ContainsAny(itemKey, "heatrock", "rocacalor"))
            return !hasPermanentWeather && setsSun ? 114 : 0;

        if (ContainsAny(itemKey, "damprock", "rocalluvia", "rocahumeda"))
            return !hasPermanentWeather && setsRain ? 116 : 0;

        if (ContainsAny(itemKey, "smoothrock", "rocasuave"))
            return !hasPermanentWeather && setsSand ? 112 : 0;

        if (ContainsAny(itemKey, "icyrock", "rocahelada"))
            return !hasPermanentWeather && setsHail ? 112 : 0;

        if (ContainsAny(itemKey, "whiteherb", "hierbablanca"))
            return HasWhiteHerbMove(moves) ? 76 : 0;

        if (ContainsAny(itemKey, "powerherb", "hierbaunica"))
            return HasPowerHerbMove(moves) ? 76 : 0;

        if (ContainsAny(itemKey, "mentalherb", "hierbamental"))
            return 30;

        if (ContainsAny(itemKey, "redcard", "tarjetaroja", "ejectbutton", "botonescape"))
            return 32;

        if (ContainsAny(itemKey, "brightpowder", "polvobrillo", "laxincense", "inciensosuave", "safetygoggles", "gafasprotectoras"))
            return 34;

        return 0;
    }

    private static int PickSafeFallback(int[] pool, string[] itemNames)
    {
        var safe = pool
            .Where(i => i < itemNames.Length)
            .Where(i =>
            {
                string key = Normalize(itemNames[i]);
                return ContainsAny(key,
                    "leftovers", "restos",
                    "sitrus", "zidra",
                    "lum", "zreza",
                    "focussash", "bandafocus",
                    "oran", "aranja"
                );
            })
            .ToArray();

        if (safe.Length > 0)
            return safe[(int)(Util.Random32() % safe.Length)];

        return 0;
    }

    private static int ScoreSpeciesSpecificItem(string itemKey, string speciesKey)
    {
        if (ContainsAny(itemKey, "lightball", "bolaluminosa") && speciesKey.Contains("pikachu"))
            return 100;

        if (ContainsAny(itemKey, "thickclub", "huesogrueso") && (speciesKey.Contains("cubone") || speciesKey.Contains("marowak")))
            return 120;

        if (ContainsAny(itemKey, "deepseatooth", "dientemarin") && speciesKey.Contains("clamperl"))
            return 100;

        if (ContainsAny(itemKey, "deepseascale", "escamamarin") && speciesKey.Contains("clamperl"))
            return 96;

        if (ContainsAny(itemKey, "souldew", "rociobondad") && (speciesKey.Contains("latias") || speciesKey.Contains("latios")))
            return 120;

        if (ContainsAny(itemKey, "luckypunch", "punosuerte") && speciesKey.Contains("chansey"))
            return 96;

        if (ContainsAny(itemKey, "stick", "leek", "palo") && speciesKey.Contains("farfetch"))
            return 92;

        if (ContainsAny(itemKey, "adamantorb", "diamansfera") && speciesKey.Contains("dialga"))
            return 120;

        if (ContainsAny(itemKey, "lustrousorb", "lustresfera") && speciesKey.Contains("palkia"))
            return 120;

        if (ContainsAny(itemKey, "griseousorb", "griseosfera") && speciesKey.Contains("giratina"))
            return 120;

        return 0;
    }

    private static HashSet<string> GetAbilityKeys(int species, int abilitySlot, string[] abilityNames)
    {
        var result = new HashSet<string>();

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

    private static int[] GetSpeciesTypes(int species)
    {
        if (species <= 0 || species >= Main.SpeciesStat.Length)
            return [];

        return Main.SpeciesStat[species].Types ?? [];
    }

    private static string GetSpeciesKey(int species)
    {
        try
        {
            string[] names = Main.Config.GetText(TextName.SpeciesNames);
            if (species > 0 && species < names.Length)
                return Normalize(names[species]);
        }
        catch { }

        return species.ToString(CultureInfo.InvariantCulture);
    }

    private static bool IsNeverUsefulTrainerItem(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return true;

        return ContainsAny(key,
            "xspeed", "xattack", "xdefense", "xspatk", "xspdef", "xaccuracy", "xspecial",
            "direhit", "guardspec", "ataquex", "defensax", "velocidadx", "precisionx", "especialx", "directo", "proteccionespecial",
            "potion", "pocion", "superpotion", "hyperpotion", "maxpotion", "antidote", "antidoto",
            "awakening", "despertar", "paralyzeheal", "antiparalisis", "fullheal", "curatotal",
            "revive", "revivir", "repel", "repelente", "escaperope", "cuerdahuida",
            "pokeball", "superball", "ultraball", "masterball",
            "mail", "correo", "fossil", "fosil", "key", "llave", "shard", "parte",
            "nugget", "pepita", "pearl", "perla", "starpiece", "trozoestrella",
            "honey", "miel", "mulch", "abono", "pluma", "wing"
        );
    }

    private static int GetBoostedTypeFromItem(string key)
    {
        foreach (var pair in TypeBoosters)
            if (pair.Value.Any(key.Contains))
                return pair.Key;

        return -1;
    }

    private static int GetResistBerryType(string key)
    {
        foreach (var pair in ResistBerries)
            if (pair.Value.Any(key.Contains))
                return pair.Key;

        return -1;
    }

    private static bool IsConsumableTypeBooster(string key)
        => key.Contains("gem") || key.Contains("gema");

    private static bool IsZCrystal(string key)
        => key.EndsWith("iumz") || key.Contains("cristalz") || key.Contains("zcrystal");

    private static bool IsBerry(string key)
        => key.Contains("berry") || key.Contains("baya") || BerryTokens.Any(key.Contains);

    private static int AdjustByQuality(int score, int qualityMode, bool strong)
    {
        return qualityMode switch
        {
            0 when strong => Math.Max(1, score - 20),
            2 when strong => score + 10,
            _ => score,
        };
    }

    private static bool IsBulkySpecies(int species)
    {
        if (species <= 0 || species >= Main.SpeciesStat.Length)
            return false;

        var p = Main.SpeciesStat[species];
        int bulk = p.HP + p.DEF + p.SPD;
        int bestOffense = Math.Max(p.ATK, p.SPA);
        int offenseProfile = bestOffense + p.SPE;
        return bulk >= 285 && bulk >= offenseProfile - 10;
    }

    private static bool IsVeryBulkySpecies(int species)
    {
        if (species <= 0 || species >= Main.SpeciesStat.Length)
            return false;

        var p = Main.SpeciesStat[species];
        return p.HP + p.DEF + p.SPD >= 330 && p.SPE <= 90;
    }

    private static bool IsOffensiveSpecies(int species)
    {
        if (species <= 0 || species >= Main.SpeciesStat.Length)
            return false;

        var p = Main.SpeciesStat[species];
        int bulk = p.HP + p.DEF + p.SPD;
        int bestOffense = Math.Max(p.ATK, p.SPA);
        return bestOffense + p.SPE >= bulk + 35 || (bestOffense >= 100 && p.SPE >= 90);
    }

    private static int CountWeaknesses(int[] defenderTypes)
    {
        int count = 0;
        for (int type = 0; type < 18; type++)
            if (IsWeakTo(type, defenderTypes))
                count++;
        return count;
    }

    private static bool IsWeakTo(int attackingType, int[] defenderTypes)
    {
        if (attackingType < 0 || attackingType >= 18)
            return false;

        double value = 1;
        foreach (int type in defenderTypes.Distinct())
        {
            if (type >= 0 && type < 18)
                value *= TypeChart[attackingType, type];
        }

        return value > 1;
    }

    private static void AddItemsByKey(HashSet<int> pool, string[] itemNames, params string[] tokens)
    {
        for (int i = 1; i < itemNames.Length; i++)
        {
            string itemName = itemNames[i];
            if (string.IsNullOrWhiteSpace(itemName))
                continue;

            string key = Normalize(itemName);
            if (tokens.Any(t => key.Contains(Normalize(t))))
                pool.Add(i);
        }
    }

    private static bool HasMoveContaining(MoveInfo moves, params string[] tokens)
        => moves.MoveNames.Any(m => tokens.Any(t => m.Contains(Normalize(t))));

    private static bool HasWhiteHerbMove(MoveInfo moves)
        => HasMoveContaining(moves,
            "overheat", "sofoco",
            "leafstorm", "lluevehojas",
            "dracometeor", "cometadraco",
            "closecombat", "abocajarro",
            "shellsmash", "rompecoraza",
            "superpower", "fuerzabruta",
            "hammerarm", "machada",
            "vcreate", "vdefuego",
            "psychoboost", "psicoataque",
            "dragonascent", "ascensodraco",
            "hyperspacefury", "cercodimension",
            "fleurcannon", "canonfloral",
            "clangingscales", "fragorescamas",
            "icehammer", "martillohielo"
        );

    private static bool HasPowerHerbMove(MoveInfo moves)
        => HasMoveContaining(moves,
            "solarbeam", "rayosolar",
            "solarblade", "cuchillasolar",
            "skyattack", "ataqueaereo",
            "geomancy", "geocontrol",
            "razorwind", "vientocortante",
            "skullbash", "cabezazo",
            "freezeshock", "rayogelido",
            "iceburn", "llamagelida",
            "bounce", "bote",
            "fly", "vuelo",
            "dig", "excavar",
            "dive", "buceo",
            "phantomforce", "golpefantasma",
            "shadowforce", "golpeumbrio"
        );

    private static bool HasAbility(HashSet<string> abilities, params string[] tokens)
        => abilities.Any(a => tokens.Any(t => a.Contains(Normalize(t))));

    private static bool ContainsAny(string key, params string[] tokens)
        => tokens.Any(t => key.Contains(Normalize(t)));

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (char c in normalized)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(c))
                sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }

    private static readonly string[] BerryTokens =
    [
        "sitrus", "oran", "lum", "chesto", "persim", "figy", "wiki", "mago", "aguav", "iapapa",
        "liechi", "ganlon", "salac", "petaya", "apicot", "lansat", "starf", "custap", "jaboca", "rowap",
        "occa", "passho", "wacan", "rindo", "yache", "chople", "kebia", "shuca", "coba", "payapa",
        "tanga", "charti", "kasib", "haban", "colbur", "babiri", "chilan", "roseli"
    ];

    private static readonly Dictionary<int, string[]> ResistBerries = new()
    {
        [0] = ["chilan"],
        [1] = ["chople"],
        [2] = ["coba"],
        [3] = ["kebia"],
        [4] = ["shuca"],
        [5] = ["charti"],
        [6] = ["tanga"],
        [7] = ["kasib"],
        [8] = ["babiri"],
        [9] = ["occa"],
        [10] = ["passho"],
        [11] = ["rindo"],
        [12] = ["wacan"],
        [13] = ["payapa"],
        [14] = ["yache"],
        [15] = ["haban"],
        [16] = ["colbur"],
        [17] = ["roseli"],
    };

    private static readonly Dictionary<int, string[]> TypeBoosters = new()
    {
        [0] = ["silkscarf", "panueloseda", "normalgem", "gemanormal", "normaliumz"],
        [1] = ["blackbelt", "cintanegra", "fistplate", "tablapuno", "fightinggem", "gemalucha", "fightiniumz"],
        [2] = ["sharpbeak", "picopuntiagudo", "skyplate", "tablacielo", "flyinggem", "gemavolador", "flyiniumz"],
        [3] = ["poisonbarb", "flehaveneno", "toxicplate", "tablatoxica", "poisongem", "gemaveneno", "poisoniumz"],
        [4] = ["softsand", "arenafina", "earthplate", "tablaterra", "groundgem", "gematierra", "groundiumz"],
        [5] = ["hardstone", "piedradura", "stoneplate", "tablapetre", "rockgem", "gemaroca", "rockiumz"],
        [6] = ["silverpowder", "polvoplata", "insectplate", "tablabicho", "buggem", "gemabicho", "buginiumz"],
        [7] = ["spelltag", "hechizo", "spookyplate", "tablaterror", "ghostgem", "gemafantasma", "ghostiumz"],
        [8] = ["metalcoat", "revestmetalico", "ironplate", "tablahierro", "steelgem", "gemaacero", "steeliumz"],
        [9] = ["charcoal", "carbon", "flameplate", "tablallama", "firegem", "gemafuego", "firiumz"],
        [10] = ["mysticwater", "aguamistica", "splashplate", "tablahidro", "seaincense", "inciensomarino", "waveincense", "inciensooleaje", "watergem", "gemaagua", "wateriumz"],
        [11] = ["miracleseed", "semillamilagro", "meadowplate", "tablapradera", "roseincense", "inciensorosa", "grassgem", "gemaplanta", "grassiumz"],
        [12] = ["magnet", "iman", "zapplate", "tablarayo", "electricgem", "gemaelectrica", "electriumz"],
        [13] = ["twistedspoon", "cucharatorcida", "mindplate", "tablamental", "oddincense", "inciensoraro", "psychicgem", "gemapsiquica", "psychiumz"],
        [14] = ["nevermeltice", "antiderretir", "icicleplate", "tablahelada", "icegem", "gemahielo", "iciumz"],
        [15] = ["dragonfang", "colmillodragon", "dracoplate", "tabladraco", "dragongem", "gemadragon", "dragoniumz"],
        [16] = ["blackglasses", "gafasnegras", "dreadplate", "tablaprurito", "darkgem", "gemasiniestra", "darkiniumz"],
        [17] = ["pixieplate", "tablahada", "fairygem", "gemahada", "fairiumz"],
    };

    // Type order: Normal, Fighting, Flying, Poison, Ground, Rock, Bug, Ghost, Steel, Fire, Water, Grass, Electric, Psychic, Ice, Dragon, Dark, Fairy.
    private static readonly double[,] TypeChart =
    {
        {1,1,1,1,1,.5,1,0,.5,1,1,1,1,1,1,1,1,1},
        {2,1,.5,.5,1,2,.5,0,2,1,1,1,1,.5,2,1,2,.5},
        {1,2,1,1,1,.5,2,1,.5,1,1,2,.5,1,1,1,1,1},
        {1,1,1,.5,.5,.5,1,.5,0,1,1,2,1,1,1,1,1,2},
        {1,1,0,2,1,2,.5,1,2,2,1,.5,2,1,1,1,1,1},
        {1,.5,2,1,.5,1,2,1,.5,2,1,1,1,1,2,1,1,1},
        {1,.5,.5,.5,1,1,1,.5,.5,.5,1,2,1,2,1,1,2,.5},
        {0,1,1,1,1,1,1,2,1,1,1,1,1,2,1,1,.5,1},
        {1,1,1,1,1,2,1,1,.5,.5,.5,1,.5,1,2,1,1,2},
        {1,1,1,1,1,.5,2,1,2,.5,.5,2,1,1,2,.5,1,1},
        {1,1,1,1,2,2,1,1,1,2,.5,.5,1,1,1,.5,1,1},
        {1,1,.5,.5,2,2,.5,1,.5,.5,2,.5,1,1,1,.5,1,1},
        {1,1,2,1,0,1,1,1,1,1,2,.5,.5,1,1,.5,1,1},
        {1,2,1,2,1,1,1,1,.5,1,1,1,1,.5,1,1,0,1},
        {1,1,2,1,2,1,1,1,.5,.5,.5,2,1,1,.5,2,1,1},
        {1,1,1,1,1,1,1,1,.5,1,1,1,1,1,1,2,1,0},
        {1,.5,1,1,1,1,1,2,1,1,1,1,1,2,1,1,.5,.5},
        {1,2,1,.5,1,1,1,1,.5,.5,1,1,1,1,1,2,2,1},
    };
}
