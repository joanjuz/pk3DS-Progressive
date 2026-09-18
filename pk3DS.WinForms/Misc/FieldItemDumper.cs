using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using pk3DS.Core;
using pk3DS.Core.CTR;
using pk3DS.Core.Structures;

namespace pk3DS.WinForms;

/// <summary>
/// Diagnostic field item dumper and safe field item shuffler based on the field item locations used by the Universal Pokémon Randomizer ZX.
/// The randomizer intentionally starts in a conservative shuffle mode: it only swaps detected field item IDs between known locations.
/// </summary>
internal static class FieldItemDumper
{
    private const string Gen6OrasHiddenPattern = "A100A200A300A400A5001400010053004A0084000900";
    private const int OrasSuspiciousVisiblePotionTailOffset = 0x16A4;

    internal static FieldItemDumpResult DumpCsv()
    {
        var entries = Main.Config.Generation switch
        {
            6 => DumpGen6(),
            7 => DumpGen7(),
            _ => throw new NotSupportedException("Field item dumping is only implemented for Gen 6 and Gen 7."),
        };

        string csv = BuildCsv(entries);
        return new FieldItemDumpResult(entries.Count, csv);
    }

    internal static FieldItemRandomizeResult RandomizeDefault()
    {
        string[] itemNames = GetItemNames();
        byte[][] itemData = TryGetItemData();
        var template = FieldItemRandomizerTemplate.Load(itemNames, itemData);
        var options = template.Options;
        var entries = Main.Config.Generation switch
        {
            6 => DumpGen6(),
            7 => DumpGen7(),
            _ => throw new NotSupportedException("Field item randomization is only implemented for Gen 6 and Gen 7."),
        };

        var candidates = entries
            .Where(e => IsRandomizable(e, options, itemNames, itemData))
            .OrderBy(e => e.Index)
            .ToList();

        if (candidates.Count < 2)
            return new FieldItemRandomizeResult(entries.Count, candidates.Count, 0, "Not enough safe field items were found to randomize.");

        Random random = Util.Rand;
        int changed = template.Mode == FieldItemRandomizeMode.RandomPool
            ? ApplyRandomPool(candidates, template, options, itemNames, itemData, random)
            : ApplyShuffle(candidates, random);

        switch (Main.Config.Generation)
        {
            case 6:
                ApplyGen6(entries.Where(e => e.NewItemID.HasValue));
                break;
            case 7:
                ApplyGen7(entries.Where(e => e.NewItemID.HasValue));
                break;
        }

        string summary = BuildRandomizeSummary(entries, candidates, changed, options, itemNames);
        return new FieldItemRandomizeResult(entries.Count, candidates.Count, changed, summary);
    }

    private static int ApplyShuffle(IReadOnlyList<FieldItemDumpEntry> candidates, Random random)
    {
        int[] shuffled = candidates.Select(e => e.ItemID).ToArray();
        Shuffle(shuffled, random);

        int changed = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].ItemID == shuffled[i] && candidates.Count > 1)
            {
                int swap = (i + 1) % candidates.Count;
                (shuffled[i], shuffled[swap]) = (shuffled[swap], shuffled[i]);
            }

            candidates[i].NewItemID = shuffled[i];
            if (candidates[i].ItemID != candidates[i].NewItemID)
                changed++;
        }

        return changed;
    }

    private static int ApplyRandomPool(IReadOnlyList<FieldItemDumpEntry> candidates, FieldItemRandomizerTemplate template, FieldItemRandomizeOptions options, string[] itemNames, byte[][] itemData, Random random)
    {
        int changed = 0;
        var poolCache = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in candidates)
        {
            string cacheKey = GetPoolCacheKey(entry, options);
            if (!poolCache.TryGetValue(cacheKey, out int[] pool))
            {
                pool = template.GetPoolFor(entry, itemNames, itemData)
                    .Where(item => item > 0)
                    .Where(item => !options.ExcludeMachines || !IsMachine(item, GetItemName(itemNames, item)))
                    .Where(item => !options.ExcludeKeyItems || !IsLikelyKeyItem(item, GetItemName(itemNames, item), itemData))
                    .Where(item => options.IncludeMegaStones || !FieldItemDumperCategory.IsMegaStoneName(Normalize(GetItemName(itemNames, item))))
                    .Where(item => !template.IsBlacklisted(item, itemNames, itemData))
                    .Distinct()
                    .ToArray();
                poolCache[cacheKey] = pool;
            }

            if (pool.Length == 0)
            {
                if (options.FallbackToShuffle)
                    return ApplyShuffle(candidates, random);

                continue;
            }

            int item = pool[random.Next(pool.Length)];
            if (pool.Length > 1)
            {
                for (int tries = 0; tries < 8 && item == entry.ItemID; tries++)
                    item = pool[random.Next(pool.Length)];
            }

            entry.NewItemID = item;
            if (entry.ItemID != item)
                changed++;
        }

        return changed;
    }

    private static string GetPoolCacheKey(FieldItemDumpEntry entry, FieldItemRandomizeOptions options)
    {
        if (!options.KeepCategory)
            return "All";

        // KeepCategory depends on the original pickup category. The exact category
        // resolution lives inside the template, so this conservative key still avoids
        // rebuilding the same pool for identical source/item combinations while keeping
        // behavior correct for items that belong to different categories.
        return $"{entry.Source}:{entry.ItemID}";
    }
    private static bool IsRandomizable(FieldItemDumpEntry entry, FieldItemRandomizeOptions options, string[] itemNames, byte[][] itemData)
    {
        if (entry.ItemID <= 0)
            return false;

        if (!options.IncludeVisible && entry.Source.Contains("Visible", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!options.IncludeHidden && (entry.Source.Contains("Hidden", StringComparison.OrdinalIgnoreCase) || entry.Source is "EI" or "EB"))
            return false;
        if (!options.IncludeMegaStones && entry.Source.Contains("MegaStone", StringComparison.OrdinalIgnoreCase))
            return false;

        // ORAS script 39 contains a long tail of repeated Potion values after the real pickup table.
        // The dumper lists them for transparency, but safe randomization leaves that suspicious tail untouched.
        if (options.SafeMode && Main.Config.ORAS && entry.Source == "VisibleScript" && entry.Offset >= OrasSuspiciousVisiblePotionTailOffset)
            return false;

        if (options.ExcludeMachines && IsMachine(entry.ItemID, entry.ItemName))
            return false;
        if (options.ExcludeKeyItems && IsLikelyKeyItem(entry.ItemID, entry.ItemName, itemData))
            return false;

        return true;
    }

    private static bool IsMachine(int itemID, string itemName)
    {
        string key = Normalize(itemName);
        if (key.StartsWith("tm", StringComparison.Ordinal) || key.StartsWith("hm", StringComparison.Ordinal) ||
            key.StartsWith("mt", StringComparison.Ordinal) || key.StartsWith("mo", StringComparison.Ordinal))
            return true;

        // Gen 6 / Gen 7 machines sit in this area; the name check above is still the main protection for localized text.
        return itemID is >= 328 and <= 434;
    }

    private static bool IsLikelyKeyItem(int itemID, string itemName, byte[][] itemData)
    {
        string key = Normalize(itemName);
        if (key.Contains("keyitem") || key.Contains("key item") || key.Contains("clave") || key.Contains("llave"))
            return true;

        // Keep important progression items out of the shuffle pool when they are present in scripts.
        string[] suspiciousNames =
        [
            "bike", "bicycle", "bicicleta", "ticket", "pass", "pase", "card", "tarjeta", "letter", "carta",
            "devon", "scanner", "scope", "fossil", "fosil", "mega bracelet", "mega ring", "z ring", "z-ring"
        ];
        if (suspiciousNames.Any(key.Contains))
            return true;

        if (itemID > 0 && itemID < itemData.Length && itemData[itemID]?.Length > 0)
        {
            try
            {
                var item = new Item(itemData[itemID]);
                // In Gen 6/7 item data, high field pockets are generally not normal consumable/held-item field pickups.
                // This is intentionally conservative; Mega Stones stay allowed by name/source unless their pocket is clearly key-item-like.
                if (item.PocketField >= 7 && !key.Contains("ite") && !key.Contains("mega"))
                    return true;
            }
            catch { }
        }

        return false;
    }

    private static byte[][] TryGetItemData()
    {
        try { return Main.Config.GetGARCData("item").Files; }
        catch { return []; }
    }

    private static void ApplyGen6(IEnumerable<FieldItemDumpEntry> updatedEntries)
    {
        var updates = updatedEntries.ToList();
        if (updates.Count == 0)
            return;

        string scriptsPath = GetGen6ScriptsPath();
        if (!File.Exists(scriptsPath))
            throw new FileNotFoundException("Could not find the Gen 6 Scripts GARC.", scriptsPath);

        var scriptUpdates = updates.Where(e => e.Container == "Scripts").GroupBy(e => e.FileIndex).ToList();
        if (scriptUpdates.Count > 0)
        {
            var scripts = new GARC.MemGARC(File.ReadAllBytes(scriptsPath));
            byte[][] files = scripts.Files;
            foreach (var group in scriptUpdates)
            {
                int fileIndex = group.Key;
                if (fileIndex < 0 || fileIndex >= files.Length)
                    continue;

                byte[] raw = files[fileIndex];
                var script = new Script(raw);
                byte[] data = DecompressScript(raw);
                foreach (var entry in group)
                    WriteUInt16(data, entry.Offset, entry.NewItemID!.Value);

                files[fileIndex] = RebuildScript(script, data);
            }
            scripts.Files = files;
            File.WriteAllBytes(scriptsPath, GetMemGarcData(scripts));
        }

        var codeUpdates = updates.Where(e => e.Container == ".code.bin").ToList();
        if (codeUpdates.Count > 0)
        {
            string codePath = TryGetCodeBinPath();
            if (string.IsNullOrWhiteSpace(codePath) || !File.Exists(codePath))
                throw new FileNotFoundException("Could not find .code.bin for hidden field items.", codePath ?? string.Empty);

            byte[] code = File.ReadAllBytes(codePath);
            foreach (var entry in codeUpdates)
                WriteUInt16(code, entry.Offset, entry.NewItemID!.Value);
            File.WriteAllBytes(codePath, code);
        }
    }

    private static byte[] GetMemGarcData(GARC.MemGARC garc)
    {
        var field = typeof(GARC.MemGARC).GetField("Data", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field?.GetValue(garc) is byte[] data)
            return data;

        throw new InvalidOperationException("Could not read rebuilt MemGARC data.");
    }

    private static void ApplyGen7(IEnumerable<FieldItemDumpEntry> updatedEntries)
    {
        var updates = updatedEntries.ToList();
        if (updates.Count == 0)
            return;

        // Important: do NOT use worldData.Files here.
        // LazyGARCFile.Files marks every file as saved, which forces the entire
        // encdata GARC to be rebuilt/recompressed and can make pk3DS look stuck.
        // Touch only the area files that actually contain randomized field items.
        var worldData = Main.Config.GetlzGARCData("encdata");
        int changedAreaFiles = 0;

        foreach (var areaGroup in updates.GroupBy(e => e.AreaIndex).OrderBy(g => g.Key))
        {
            int area = areaGroup.Key;
            int fileIndex = area * 11;
            if (area < 0 || fileIndex < 0 || fileIndex >= worldData.FileCount)
                continue;

            byte[] file = worldData[fileIndex];
            byte[][] ed = Mini.UnpackMini(file, "ED");
            if (ed == null || ed.Length <= 11)
                continue;

            bool changed = false;

            var eiUpdates = areaGroup.Where(e => e.Source.Equals("EI", StringComparison.OrdinalIgnoreCase)).ToList();
            if (eiUpdates.Count > 0)
            {
                byte[][] ei = Mini.UnpackMini(ed[10], "EI");
                if (ei != null)
                {
                    foreach (var entry in eiUpdates)
                    {
                        if ((uint)entry.SubFile >= ei.Length)
                            continue;
                        if (entry.Offset < 0 || entry.Offset + 1 >= ei[entry.SubFile].Length)
                            continue;

                        WriteUInt16(ei[entry.SubFile], entry.Offset, entry.NewItemID!.Value);
                        changed = true;
                    }

                    if (changed)
                        ed[10] = Mini.PackMini(ei, "EI");
                }
            }

            var ebUpdates = areaGroup.Where(e => e.Source.Equals("EB", StringComparison.OrdinalIgnoreCase)).ToList();
            if (ebUpdates.Count > 0)
            {
                byte[][] eb = Mini.UnpackMini(ed[11], "EB");
                if (eb != null)
                {
                    bool ebChanged = false;
                    foreach (var entry in ebUpdates)
                    {
                        if ((uint)entry.SubFile >= eb.Length)
                            continue;
                        if (entry.Offset < 0 || entry.Offset + 1 >= eb[entry.SubFile].Length)
                            continue;

                        WriteUInt16(eb[entry.SubFile], entry.Offset, entry.NewItemID!.Value);
                        changed = true;
                        ebChanged = true;
                    }

                    if (ebChanged)
                        ed[11] = Mini.PackMini(eb, "EB");
                }
            }

            if (!changed)
                continue;

            worldData[fileIndex] = Mini.PackMini(ed, "ED");
            changedAreaFiles++;
        }

        if (changedAreaFiles > 0)
            worldData.Save();
    }
    private static string BuildRandomizeSummary(IReadOnlyList<FieldItemDumpEntry> entries, IReadOnlyList<FieldItemDumpEntry> candidates, int changed, FieldItemRandomizeOptions options, string[] itemNames)
    {
        int skipped = entries.Count - candidates.Count;
        int tms = entries.Count(e => IsMachine(e.ItemID, e.ItemName));
        int safeTail = entries.Count(e => Main.Config.ORAS && e.Source == "VisibleScript" && e.Offset >= OrasSuspiciousVisiblePotionTailOffset);
        int megas = candidates.Count(e => e.Source.Contains("MegaStone", StringComparison.OrdinalIgnoreCase) || Normalize(GetItemName(itemNames, e.ItemID)).Contains("ite"));

        var sb = new StringBuilder();
        sb.AppendLine($"Detected field item entries: {entries.Count}");
        sb.AppendLine($"Randomized candidates: {candidates.Count}");
        sb.AppendLine($"Changed item slots: {changed}");
        sb.AppendLine($"Skipped entries: {skipped}");
        sb.AppendLine();
        sb.AppendLine("Template mode used:");
        sb.AppendLine($"- Mode: {options.Mode}");
        sb.AppendLine($"- Pool mode: {options.PoolMode}");
        sb.AppendLine($"- Keep categories: {(options.KeepCategory ? "enabled" : "disabled")}");
        sb.AppendLine($"- Visible items: {(options.IncludeVisible ? "enabled" : "disabled")}");
        sb.AppendLine($"- Hidden items: {(options.IncludeHidden ? "enabled" : "disabled")}");
        sb.AppendLine($"- Mega Stones: {(options.IncludeMegaStones ? "included in the same pool" : "excluded")}");
        sb.AppendLine($"- TMs/HMs: {(options.ExcludeMachines ? "excluded" : "included")}");
        sb.AppendLine($"- Key/story-like items: {(options.ExcludeKeyItems ? "excluded" : "included")}");
        sb.AppendLine($"- ORAS suspicious repeated-Potion tail: {(options.SafeMode ? "skipped" : "included")}");
        if (options.Mode == FieldItemRandomizeMode.RandomPool && options.PoolMode == FieldItemPoolMode.ExcludeListed)
        {
            sb.AppendLine($"- Mail items: {(options.BanMails ? "blacklisted" : "allowed")}");
            sb.AppendLine($"- Low-value berries: {(options.BanBadBerries ? "blacklisted" : "allowed")}");
            sb.AppendLine($"- Flavor/vendor items: {(options.BanFlavorItems ? "blacklisted" : "allowed")}");
            sb.AppendLine($"- Battle-only filler: {(options.BanBattleJunk ? "blacklisted" : "allowed")}");
        }
        sb.AppendLine();
        sb.AppendLine($"Detected machines excluded: {tms}");
        sb.AppendLine($"Safe-tail entries skipped: {(options.SafeMode ? safeTail : 0)}");
        sb.AppendLine($"Mega-like items in shuffle candidates: {megas}");
        return sb.ToString();
    }

    private static List<FieldItemDumpEntry> DumpGen6()
    {
        var entries = new List<FieldItemDumpEntry>();
        string scriptsPath = GetGen6ScriptsPath();
        if (!File.Exists(scriptsPath))
            throw new FileNotFoundException("Could not find the Gen 6 Scripts GARC.", scriptsPath);

        var scripts = new GARC.MemGARC(File.ReadAllBytes(scriptsPath));
        string[] itemNames = GetItemNames();
        string game = Main.Config.ORAS ? "ORAS" : "XY";

        int visibleScript = Main.Config.ORAS ? 39 : 17;
        int visibleOffset = Main.Config.ORAS ? 0xB64 : 0xB04;
        DumpGen6ScriptItems(entries, scripts, visibleScript, visibleOffset, "VisibleScript", game, itemNames);

        if (Main.Config.XY)
        {
            DumpGen6ScriptItems(entries, scripts, 26, 0xB18, "HiddenScript", game, itemNames);
        }
        else if (Main.Config.ORAS)
        {
            DumpGen6OrasHiddenCodeItems(entries, itemNames);
            DumpGen6OrasMegaStoneScriptItems(entries, scripts, itemNames);
        }

        Renumber(entries);
        return entries;
    }

    private static void DumpGen6ScriptItems(List<FieldItemDumpEntry> entries, GARC.MemGARC scripts, int scriptIndex, int startOffset, string source, string game, string[] itemNames)
    {
        if (scriptIndex < 0 || scriptIndex >= scripts.FileCount)
            return;

        byte[] raw = scripts.GetFile(scriptIndex);
        byte[] data = DecompressScript(raw);
        for (int offset = startOffset, slot = 0; offset + 1 < data.Length; offset += 12, slot++)
        {
            int item = ReadUInt16(data, offset);
            if (item <= 0)
                continue;

            entries.Add(new FieldItemDumpEntry
            {
                Generation = 6,
                Game = game,
                Source = source,
                Container = "Scripts",
                FileIndex = scriptIndex,
                SubFile = 0,
                EntryIndex = slot,
                Slot = 0,
                Offset = offset,
                ItemID = item,
                ItemName = GetItemName(itemNames, item),
            });
        }
    }

    private static void DumpGen6OrasHiddenCodeItems(List<FieldItemDumpEntry> entries, string[] itemNames)
    {
        byte[] code = TryReadCodeBin();
        if (code.Length == 0)
            return;

        int patternOffset = FindHexPattern(code, Gen6OrasHiddenPattern);
        if (patternOffset < 0)
            return;

        int start = patternOffset + (Gen6OrasHiddenPattern.Length / 2);
        for (int i = 0; i < 170; i++)
        {
            int offset = start + (i * 14) + 2;
            if (offset + 1 >= code.Length)
                break;

            int item = ReadUInt16(code, offset);
            if (item <= 0)
                continue;

            entries.Add(new FieldItemDumpEntry
            {
                Generation = 6,
                Game = "ORAS",
                Source = "HiddenCode",
                Container = ".code.bin",
                FileIndex = -1,
                SubFile = 0,
                EntryIndex = i,
                Slot = 0,
                Offset = offset,
                ItemID = item,
                ItemName = GetItemName(itemNames, item),
            });
        }
    }

    private static void DumpGen6OrasMegaStoneScriptItems(List<FieldItemDumpEntry> entries, GARC.MemGARC scripts, string[] itemNames)
    {
        const int megaStoneScript = 57;
        if (megaStoneScript >= scripts.FileCount)
            return;

        byte[] data = DecompressScript(scripts.GetFile(megaStoneScript));
        for (int i = 0; i < 27; i++)
        {
            int offset = 2746 + (i * 32);
            if (offset + 1 >= data.Length)
                break;

            int item = ReadUInt16(data, offset);
            if (item <= 0)
                continue;

            entries.Add(new FieldItemDumpEntry
            {
                Generation = 6,
                Game = "ORAS",
                Source = "MegaStoneScript",
                Container = "Scripts",
                FileIndex = megaStoneScript,
                SubFile = 0,
                EntryIndex = i,
                Slot = 0,
                Offset = offset,
                ItemID = item,
                ItemName = GetItemName(itemNames, item),
            });
        }
    }

    private static List<FieldItemDumpEntry> DumpGen7()
    {
        var entries = new List<FieldItemDumpEntry>();
        string[] itemNames = GetItemNames();
        string game = Main.Config.USUM ? "USUM" : "SM";

        var worldData = Main.Config.GetlzGARCData("encdata");
        int areaCount = worldData.FileCount / 11;
        for (int area = 0; area < areaCount; area++)
        {
            byte[] file = worldData[area * 11];
            byte[][] ed = Mini.UnpackMini(file, "ED");
            if (ed == null || ed.Length <= 11)
                continue;

            DumpGen7EiItems(entries, ed[10], area, game, itemNames);
            DumpGen7EbItems(entries, ed[11], area, game, itemNames);
        }

        Renumber(entries);
        return entries;
    }

    private static void DumpGen7EiItems(List<FieldItemDumpEntry> entries, byte[] packed, int area, string game, string[] itemNames)
    {
        byte[][] files = Mini.UnpackMini(packed, "EI");
        if (files == null)
            return;

        for (int sub = 0; sub < files.Length; sub++)
        {
            byte[] data = files[sub];
            if (data.Length == 0)
                continue;

            int count = data[0];
            for (int i = 0; i < count; i++)
            {
                int offset = (i * 64) + 52;
                if (offset + 1 >= data.Length)
                    break;

                int item = ReadUInt16(data, offset);
                if (item <= 0)
                    continue;

                entries.Add(new FieldItemDumpEntry
                {
                    Generation = 7,
                    Game = game,
                    Source = "EI",
                    Container = "encdata",
                    FileIndex = area * 11,
                    SubFile = sub,
                    AreaIndex = area,
                    EntryIndex = i,
                    Slot = 0,
                    Offset = offset,
                    ItemID = item,
                    ItemName = GetItemName(itemNames, item),
                });
            }
        }
    }

    private static void DumpGen7EbItems(List<FieldItemDumpEntry> entries, byte[] packed, int area, string game, string[] itemNames)
    {
        byte[][] files = Mini.UnpackMini(packed, "EB");
        if (files == null)
            return;

        for (int sub = 0; sub < files.Length; sub++)
        {
            byte[] data = files[sub];
            if (data.Length == 0)
                continue;

            int count = data[0];
            for (int i = 0; i < count; i++)
            {
                for (int slot = 0; slot < 7; slot++)
                {
                    int offset = 4 + (i * 68) + 54 + (slot * 2);
                    if (offset + 1 >= data.Length)
                        break;

                    int item = ReadUInt16(data, offset);
                    if (item <= 0)
                        continue;

                    entries.Add(new FieldItemDumpEntry
                    {
                        Generation = 7,
                        Game = game,
                        Source = "EB",
                        Container = "encdata",
                        FileIndex = area * 11,
                        SubFile = sub,
                        AreaIndex = area,
                        EntryIndex = i,
                        Slot = slot,
                        Offset = offset,
                        ItemID = item,
                        ItemName = GetItemName(itemNames, item),
                    });
                }
            }
        }
    }

    private static string BuildCsv(IReadOnlyList<FieldItemDumpEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Index,Generation,Game,Source,Container,FileIndex,SubFile,AreaIndex,EntryIndex,Slot,OffsetHex,ItemID,ItemName");
        foreach (var entry in entries)
        {
            sb.Append(entry.Index.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(entry.Generation.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(Csv(entry.Game)).Append(',')
                .Append(Csv(entry.Source)).Append(',')
                .Append(Csv(entry.Container)).Append(',')
                .Append(entry.FileIndex.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(entry.SubFile.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(entry.AreaIndex.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(entry.EntryIndex.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(entry.Slot.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append("0x").Append(entry.Offset.ToString("X", CultureInfo.InvariantCulture)).Append(',')
                .Append(entry.ItemID.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(Csv(entry.ItemName))
                .AppendLine();
        }
        return sb.ToString();
    }

    private static byte[] DecompressScript(byte[] raw)
    {
        var script = new Script(raw);
        var instructions = script.DecompressedInstructions;
        var bytes = new byte[instructions.Length * 4];
        for (int i = 0; i < instructions.Length; i++)
            Array.Copy(BitConverter.GetBytes(instructions[i]), 0, bytes, i * 4, 4);
        return bytes;
    }

    private static byte[] RebuildScript(Script script, byte[] decompressedBytes)
    {
        byte[] compressed = CompressScript(decompressedBytes);
        byte[] rebuilt = new byte[script.ScriptInstructionStart + compressed.Length];
        Buffer.BlockCopy(script.Raw, 0, rebuilt, 0, script.ScriptInstructionStart);
        Buffer.BlockCopy(compressed, 0, rebuilt, script.ScriptInstructionStart, compressed.Length);
        Array.Copy(BitConverter.GetBytes(rebuilt.Length), 0, rebuilt, 0x00, 4);
        return rebuilt;
    }

    private static byte[] CompressScript(byte[] data)
    {
        if (data == null || data.Length % 4 != 0)
            return [];

        using var ms = new MemoryStream();
        for (int pos = 0; pos < data.Length; pos += 4)
            ms.Write(CompressInstruction(data.AsSpan(pos, 4)));
        return ms.ToArray();
    }

    private static byte[] CompressInstruction(ReadOnlySpan<byte> db)
    {
        // Encode the 32-bit decompressed script instruction using the same signed
        // 7-bit variable-length format expected by Scripts.QuickDecompress.
        // The older branchy encoder could return an empty byte array for some
        // negative instructions, which shortened the script stream and made
        // future dumps crash with IndexOutOfRangeException while decompressing.
        uint value = BitConverter.ToUInt32(db);
        long signed = unchecked((int)value);

        int groupCount = 5;
        for (int n = 1; n <= 5; n++)
        {
            int bits = n * 7;
            long min = -(1L << (bits - 1));
            long max = (1L << (bits - 1)) - 1;
            if (signed >= min && signed <= max)
            {
                groupCount = n;
                break;
            }
        }

        long mask = (1L << (groupCount * 7)) - 1;
        long encoded = signed & mask;
        var output = new byte[groupCount];
        for (int i = 0; i < groupCount; i++)
        {
            int shift = (groupCount - 1 - i) * 7;
            int chunk = (int)((encoded >> shift) & 0x7F);
            if (i < groupCount - 1)
                chunk |= 0x80;
            output[i] = (byte)chunk;
        }

        return output;
    }

    private static string GetGen6ScriptsPath()
    {
        string relative = Main.Config.ORAS
            ? Path.Combine("a", "0", "2", "9")
            : Path.Combine("a", "0", "3", "1");
        return Path.Combine(Main.Config.RomFS, relative);
    }

    private static byte[] TryReadCodeBin()
    {
        string path = TryGetCodeBinPath();
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? File.ReadAllBytes(path) : [];
    }

    private static string TryGetCodeBinPath()
    {
        string path = Main.ExeFSPath ?? Main.Config.ExeFS;
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return null;

        return Directory.GetFiles(path)
            .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Contains("code", StringComparison.OrdinalIgnoreCase));
    }

    private static string[] GetItemNames()
    {
        try { return Main.Config.GetText(TextName.ItemNames); }
        catch { return []; }
    }

    private static string GetItemName(string[] itemNames, int item)
    {
        if ((uint)item < itemNames.Length && !string.IsNullOrWhiteSpace(itemNames[item]))
            return itemNames[item];
        return $"Item {item}";
    }

    private static int ReadUInt16(byte[] data, int offset) => BitConverter.ToUInt16(data, offset);

    private static void WriteUInt16(byte[] data, int offset, int value)
    {
        if (offset < 0 || offset + 1 >= data.Length)
            return;
        Array.Copy(BitConverter.GetBytes((ushort)value), 0, data, offset, 2);
    }

    private static int FindHexPattern(byte[] data, string hex)
    {
        byte[] pattern = HexToBytes(hex);
        if (pattern.Length == 0 || data.Length < pattern.Length)
            return -1;

        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] == pattern[j])
                    continue;
                match = false;
                break;
            }
            if (match)
                return i;
        }
        return -1;
    }

    private static byte[] HexToBytes(string hex)
    {
        var clean = new string(hex.Where(Uri.IsHexDigit).ToArray());
        if (clean.Length % 2 != 0)
            clean = "0" + clean;

        var bytes = new byte[clean.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(clean.Substring(i * 2, 2), 16);
        return bytes;
    }

    private static void Renumber(IReadOnlyList<FieldItemDumpEntry> entries)
    {
        for (int i = 0; i < entries.Count; i++)
            entries[i].Index = i + 1;
    }

    private static void Shuffle<T>(IList<T> values, Random random)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var sb = new StringBuilder(value.Length);
        foreach (char c in value.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
        }
        return sb.ToString();
    }

    private static string Csv(string value)
    {
        value ??= string.Empty;
        return value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? '"' + value.Replace("\"", "\"\"") + '"'
            : value;
    }
}

internal sealed class FieldItemRandomizeOptions
{
    public FieldItemRandomizeMode Mode { get; init; } = FieldItemRandomizeMode.Shuffle;
    public bool IncludeVisible { get; init; } = true;
    public bool IncludeHidden { get; init; } = true;
    public bool IncludeMegaStones { get; init; } = true;
    public bool ExcludeMachines { get; init; } = true;
    public bool ExcludeKeyItems { get; init; } = true;
    public bool SafeMode { get; init; } = true;
    public bool KeepCategory { get; init; } = false;
    public bool FallbackToShuffle { get; init; } = true;
    public FieldItemPoolMode PoolMode { get; init; } = FieldItemPoolMode.IncludeOnly;
    public bool BanMails { get; init; } = true;
    public bool BanBadBerries { get; init; } = true;
    public bool BanFlavorItems { get; init; } = true;
    public bool BanBattleJunk { get; init; } = true;

    public static FieldItemRandomizeOptions Default { get; } = new();
}

internal enum FieldItemRandomizeMode
{
    Shuffle,
    RandomPool,
}

internal enum FieldItemPoolMode
{
    IncludeOnly,
    ExcludeListed,
}

internal sealed class FieldItemRandomizerTemplate
{
    private const string TemplateFileName = "field_items.txt";
    internal static string DefaultPath => GetTemplatePath();
    private readonly Dictionary<string, int[]> Pools;
    private readonly Dictionary<string, int[]> Blacklists;

    public FieldItemRandomizeOptions Options { get; }
    public FieldItemRandomizeMode Mode => Options.Mode;

    private FieldItemRandomizerTemplate(FieldItemRandomizeOptions options, Dictionary<string, int[]> pools, Dictionary<string, int[]> blacklists)
    {
        Options = options;
        Pools = pools;
        Blacklists = blacklists;
    }

    public static FieldItemRandomizerTemplate Load(string[] itemNames, byte[][] itemData)
    {
        string path = GetTemplatePath();
        EnsureDefaultTemplate(path);

        var options = FieldItemRandomizeOptions.Default;
        var poolsRaw = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var blacklistsRaw = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (string rawLine in File.ReadAllLines(path))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                continue;

            int eq = line.IndexOf('=');
            if (eq <= 0)
                continue;

            string key = line[..eq].Trim();
            string value = line[(eq + 1)..].Trim();
            switch (NormalizeKey(key))
            {
                case "mode":
                    options = options.WithMode(ParseMode(value));
                    break;
                case "includevisible":
                    options = options.WithIncludeVisible(ParseBool(value, options.IncludeVisible));
                    break;
                case "includehidden":
                    options = options.WithIncludeHidden(ParseBool(value, options.IncludeHidden));
                    break;
                case "includemegastones":
                    options = options.WithIncludeMegaStones(ParseBool(value, options.IncludeMegaStones));
                    break;
                case "excludemachines":
                case "excludetms":
                    options = options.WithExcludeMachines(ParseBool(value, options.ExcludeMachines));
                    break;
                case "excludekeyitems":
                    options = options.WithExcludeKeyItems(ParseBool(value, options.ExcludeKeyItems));
                    break;
                case "safemode":
                    options = options.WithSafeMode(ParseBool(value, options.SafeMode));
                    break;
                case "keepcategory":
                    options = options.WithKeepCategory(ParseBool(value, options.KeepCategory));
                    break;
                case "fallbacktoshuffle":
                    options = options.WithFallbackToShuffle(ParseBool(value, options.FallbackToShuffle));
                    break;
                case "poolmode":
                    options = options.WithPoolMode(ParsePoolMode(value));
                    break;
                case "banmails":
                    options = options.WithBanMails(ParseBool(value, options.BanMails));
                    break;
                case "banbadberries":
                case "banlowvalueberries":
                    options = options.WithBanBadBerries(ParseBool(value, options.BanBadBerries));
                    break;
                case "banflavoritems":
                case "banvendoritems":
                    options = options.WithBanFlavorItems(ParseBool(value, options.BanFlavorItems));
                    break;
                case "banbattlejunk":
                case "banbattlefiller":
                    options = options.WithBanBattleJunk(ParseBool(value, options.BanBattleJunk));
                    break;
                default:
                    if (key.StartsWith("Pool", StringComparison.OrdinalIgnoreCase))
                    {
                        string poolName = GetNamedSection(key, "All");
                        if (!poolsRaw.TryGetValue(poolName, out var list))
                            poolsRaw[poolName] = list = [];

                        list.AddRange(SplitItems(value));
                    }
                    else if (key.StartsWith("Blacklist", StringComparison.OrdinalIgnoreCase))
                    {
                        string blacklistName = GetNamedSection(key, "All");
                        if (!blacklistsRaw.TryGetValue(blacklistName, out var list))
                            blacklistsRaw[blacklistName] = list = [];

                        list.AddRange(SplitItems(value));
                    }
                    break;
            }
        }

        var pools = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in poolsRaw)
        {
            int[] ids = kvp.Value
                .Select(name => ResolveItem(name, itemNames))
                .Where(id => id > 0)
                .Distinct()
                .ToArray();
            if (ids.Length > 0)
                pools[kvp.Key] = ids;
        }

        var blacklists = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in blacklistsRaw)
        {
            int[] ids = kvp.Value
                .Select(name => ResolveItem(name, itemNames))
                .Where(id => id > 0)
                .Distinct()
                .ToArray();
            if (ids.Length > 0)
                blacklists[kvp.Key] = ids;
        }

        return new FieldItemRandomizerTemplate(options, pools, blacklists);
    }

    public int[] GetPoolFor(FieldItemDumpEntry entry, string[] itemNames, byte[][] itemData)
    {
        if (Options.Mode == FieldItemRandomizeMode.Shuffle)
            return [];

        if (Options.PoolMode == FieldItemPoolMode.ExcludeListed)
            return BuildExcludedListedPool(entry, itemNames, itemData);

        if (Options.KeepCategory)
        {
            string category = GetCategory(entry, itemNames, itemData);
            if (Pools.TryGetValue(category, out int[] categoryPool) && categoryPool.Length > 0)
                return categoryPool;
        }

        return Pools.TryGetValue("All", out int[] all) ? all : [];
    }

    public bool IsBlacklisted(int itemID, string[] itemNames, byte[][] itemData)
    {
        string itemName = FieldItemDumperCategory.GetItemNamePublic(itemNames, itemID);
        string key = FieldItemDumperCategory.NormalizePublic(itemName);
        string category = GetCategoryByItem(itemID, itemNames, itemData);

        if (ContainsBlacklistedItem("All", itemID) || ContainsBlacklistedItem(category, itemID))
            return true;

        return IsAutoBlacklisted(key, Options);
    }

    private int[] BuildExcludedListedPool(FieldItemDumpEntry entry, string[] itemNames, byte[][] itemData)
    {
        string requestedCategory = Options.KeepCategory ? GetCategory(entry, itemNames, itemData) : string.Empty;
        var result = new List<int>();

        for (int item = 1; item < itemNames.Length; item++)
        {
            string name = FieldItemDumperCategory.GetItemNamePublic(itemNames, item);
            if (string.IsNullOrWhiteSpace(name) || name.StartsWith("Item ", StringComparison.OrdinalIgnoreCase))
                continue;

            if (Options.KeepCategory && !string.Equals(GetCategoryByItem(item, itemNames, itemData), requestedCategory, StringComparison.OrdinalIgnoreCase))
                continue;

            if (IsBlacklisted(item, itemNames, itemData))
                continue;

            result.Add(item);
        }

        return result.ToArray();
    }

    private bool ContainsBlacklistedItem(string category, int itemID) =>
        Blacklists.TryGetValue(category, out int[] ids) && Array.IndexOf(ids, itemID) >= 0;

    private static string GetCategoryByItem(int itemID, string[] itemNames, byte[][] itemData)
    {
        var entry = new FieldItemDumpEntry
        {
            ItemID = itemID,
            ItemName = FieldItemDumperCategory.GetItemNamePublic(itemNames, itemID),
            Source = string.Empty,
        };
        return GetCategory(entry, itemNames, itemData);
    }

    private static bool IsAutoBlacklisted(string key, FieldItemRandomizeOptions options)
    {
        if (options.BanMails && FieldItemDumperCategory.IsMail(key))
            return true;
        if (options.BanBadBerries && FieldItemDumperCategory.IsLowValueBerry(key))
            return true;
        if (options.BanFlavorItems && FieldItemDumperCategory.IsFlavorItem(key))
            return true;
        if (options.BanBattleJunk && FieldItemDumperCategory.IsBattleJunk(key))
            return true;

        return false;
    }

    private static string GetTemplatePath()
    {
        string dir = CustomBalanceTemplates.GetTemplateDirectory();
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, TemplateFileName);

        // Preserve an existing field-item template from older builds. The first
        // time this version runs, copy it into custom_balance_templates so the
        // global ROM template and future batch builder use one canonical file.
        if (!File.Exists(path))
        {
            string legacyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", TemplateFileName);
            if (File.Exists(legacyPath))
                File.Copy(legacyPath, path, overwrite: false);
        }

        return path;
    }

    private static void EnsureDefaultTemplate(string path)
    {
        if (File.Exists(path))
            return;

        File.WriteAllText(path, DefaultTemplateText, Encoding.UTF8);
    }

    private const string DefaultTemplateText = """
# pk3DS Field Item Randomizer Template
#
# Mode: Shuffle or RandomPool
# - Shuffle swaps existing field item IDs between safe detected locations.
# - RandomPool picks new items from the pool mode below.
#
# PoolMode:
# - IncludeOnly: only items listed in Pool.* can appear.
# - ExcludeListed: all legal items can appear except filters/Blacklist.* below.
#
# KeepCategory only matters in RandomPool mode. If enabled, the result keeps the original pickup category.
# Categories currently include: Medicine, Ball, Berry, EVWing, EVResetBerry, Money, MegaStone, Held, Other.
#
# Use item IDs or exact item names. Numeric IDs are the safest option across languages.

Mode=Shuffle
PoolMode=ExcludeListed
IncludeVisible=true
IncludeHidden=true
IncludeMegaStones=true
ExcludeMachines=true
ExcludeKeyItems=true
SafeMode=true
KeepCategory=false
FallbackToShuffle=true

# Automatic blacklist filters. These are mainly for PoolMode=ExcludeListed.
BanMails=true
BanBadBerries=true
BanFlavorItems=true
BanBattleJunk=true

# Manual blacklists. Add anything you do not want to appear.
# These are applied after the automatic filters.
Blacklist.All=Pretty Wing, Honey, Tiny Mushroom, Big Mushroom, Shoal Salt, Shoal Shell
Blacklist.All=Orange Mail, Harbor Mail, Glitter Mail, Mech Mail, Wood Mail, Wave Mail, Bead Mail, Shadow Mail, Tropic Mail, Dream Mail, Fab Mail, Retro Mail
Blacklist.Berry=Cheri Berry, Pecha Berry, Rawst Berry, Aspear Berry, Persim Berry, Razz Berry, Bluk Berry, Nanab Berry, Wepear Berry, Pinap Berry

# Example IncludeOnly setup. Change PoolMode=IncludeOnly and Mode=RandomPool to use these.
# Pool.All=Rare Candy, PP Up, PP Max, Nugget, Big Nugget, Star Piece, Heart Scale
# Pool.EVWing=Health Wing, Muscle Wing, Resist Wing, Genius Wing, Clever Wing, Swift Wing
# Pool.EVResetBerry=Pomeg Berry, Kelpsy Berry, Qualot Berry, Hondew Berry, Grepa Berry, Tamato Berry
# Pool.MegaStone=Gengarite, Venusaurite, Charizardite X, Charizardite Y, Blastoisinite, Beedrillite
""";

    private static string GetNamedSection(string key, string fallback)
    {
        int dot = key.IndexOf('.');
        return dot >= 0 && dot + 1 < key.Length ? key[(dot + 1)..].Trim() : fallback;
    }

    private static IEnumerable<string> SplitItems(string value) =>
        value.Split([',', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static int ResolveItem(string value, string[] itemNames)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
            return id;

        string key = NormalizeName(value);
        for (int i = 0; i < itemNames.Length; i++)
        {
            if (NormalizeName(itemNames[i]) == key)
                return i;
        }

        return -1;
    }

    private static string GetCategory(FieldItemDumpEntry entry, string[] itemNames, byte[][] itemData)
    {
        string name = FieldItemDumperCategory.NormalizePublic(FieldItemDumperCategory.GetItemNamePublic(itemNames, entry.ItemID));
        if (entry.Source.Contains("MegaStone", StringComparison.OrdinalIgnoreCase) || FieldItemDumperCategory.IsMegaStoneName(name))
            return "MegaStone";

        if (FieldItemDumperCategory.IsEVWing(name))
            return "EVWing";
        if (FieldItemDumperCategory.IsEVResetBerry(name))
            return "EVResetBerry";
        if (name.Contains("berry") || name.Contains("baya"))
            return "Berry";
        if (FieldItemDumperCategory.IsBall(name))
            return "Ball";
        if (FieldItemDumperCategory.IsMoneyItem(name))
            return "Money";
        if (FieldItemDumperCategory.IsMedicine(name))
            return "Medicine";

        if (entry.ItemID > 0 && entry.ItemID < itemData.Length && itemData[entry.ItemID]?.Length > 0)
        {
            try
            {
                var item = new Item(itemData[entry.ItemID]);
                if (item.PocketBattle > 0 || item.PocketField == 5)
                    return "Held";
            }
            catch { }
        }

        return "Other";
    }

    private static FieldItemRandomizeMode ParseMode(string value) =>
        value.Equals("RandomPool", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("Pool", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("Random", StringComparison.OrdinalIgnoreCase)
            ? FieldItemRandomizeMode.RandomPool
            : FieldItemRandomizeMode.Shuffle;

    private static FieldItemPoolMode ParsePoolMode(string value) =>
        value.Equals("ExcludeListed", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("Blacklist", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("BlackList", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("AllExcept", StringComparison.OrdinalIgnoreCase)
            ? FieldItemPoolMode.ExcludeListed
            : FieldItemPoolMode.IncludeOnly;

    private static bool ParseBool(string value, bool fallback)
    {
        if (bool.TryParse(value, out bool b))
            return b;
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
            return i != 0;
        return fallback;
    }

    private static string NormalizeKey(string value) => NormalizeName(value);

    private static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var sb = new StringBuilder(value.Length);
        foreach (char c in value.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
        }
        return sb.ToString();
    }
}

internal static class FieldItemDumperCategory
{
    public static string GetItemNamePublic(string[] itemNames, int item) =>
        (uint)item < itemNames.Length && !string.IsNullOrWhiteSpace(itemNames[item]) ? itemNames[item] : $"Item {item}";

    public static string NormalizePublic(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var sb = new StringBuilder(value.Length);
        foreach (char c in value.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
        }
        return sb.ToString();
    }

    public static bool IsMegaStoneName(string key)
    {
        if (key is "eviolite" or "mineralviolite")
            return false;

        return key.EndsWith("ite", StringComparison.Ordinal) || key.EndsWith("ita", StringComparison.Ordinal) ||
            key.Contains("mewtwonite") || key.Contains("charizardite") || key.Contains("blastoisinite");
    }

    public static bool IsEVWing(string key) =>
        key.Contains("wing") || key.Contains("feather") || key.Contains("pluma");

    public static bool IsEVResetBerry(string key) =>
        key.Contains("pomeg") || key.Contains("kelpsy") || key.Contains("qualot") ||
        key.Contains("hondew") || key.Contains("grepa") || key.Contains("tamato") ||
        key.Contains("grana") || key.Contains("algama") || key.Contains("ispero") ||
        key.Contains("meluce") || key.Contains("uvav") || key.Contains("tamate");

    public static bool IsBall(string key) =>
        key.Contains("ball") || key.Contains("bola") || key.Contains("ball");

    public static bool IsMoneyItem(string key) =>
        key.Contains("nugget") || key.Contains("pearl") || key.Contains("star") || key.Contains("piece") ||
        key.Contains("pepita") || key.Contains("perla") || key.Contains("estrella") || key.Contains("seta") || key.Contains("mushroom");

    public static bool IsMedicine(string key) =>
        key.Contains("potion") || key.Contains("restore") || key.Contains("revive") || key.Contains("heal") ||
        key.Contains("ether") || key.Contains("elixir") || key.Contains("repel") ||
        key.Contains("pocion") || key.Contains("restaurar") || key.Contains("revivir") || key.Contains("cura") ||
        key.Contains("antidote") || key.Contains("paralyze") || key.Contains("awakening") || key.Contains("iceheal") ||
        key.Contains("antidoto") || key.Contains("despertar") || key.Contains("antihielo");

    public static bool IsMail(string key) =>
        key.Contains("mail") || key.Contains("correo") || key.Contains("carta");

    public static bool IsLowValueBerry(string key)
    {
        if (!key.Contains("berry") && !key.Contains("baya"))
            return false;

        if (IsEVResetBerry(key))
            return false;

        string[] allowed =
        [
            "sitrus", "zidra", "lum", "ziuela", "chesto", "atanya",
            "liechi", "ganlon", "salac", "petaya", "apicot", "lansat", "starf",
            "enigma", "micle", "custap", "jaboca", "rowap"
        ];
        return !allowed.Any(key.Contains);
    }

    public static bool IsFlavorItem(string key) =>
        key.Contains("mulch") || key.Contains("abono") || key.Contains("honey") || key.Contains("miel") ||
        key.Contains("prettywing") || key.Contains("alabonita") || key.Contains("plumabonita") ||
        key.Contains("shoalsalt") || key.Contains("shoalshell") || key.Contains("salcardumen") ||
        key.Contains("conchacardumen") || key.Contains("tinymushroom") || key.Contains("bigmushroom") ||
        key.Contains("setapequena") || key.Contains("setagrande");

    public static bool IsBattleJunk(string key) =>
        key.Contains("xattack") || key.Contains("xdefense") || key.Contains("xspatk") || key.Contains("xspdef") ||
        key.Contains("xspeed") || key.Contains("xaccuracy") || key.Contains("direhit") || key.Contains("guardspec") ||
        key.Contains("ataquex") || key.Contains("defensax") || key.Contains("velocidadx") ||
        key.Contains("precisionx") || key.Contains("directo") || key.Contains("proteccionespecial") ||
        key.Contains("flute") || key.Contains("flauta");
}

internal static class FieldItemRandomizeOptionsExtensions
{
    public static FieldItemRandomizeOptions WithMode(this FieldItemRandomizeOptions options, FieldItemRandomizeMode mode) => Copy(options, mode: mode);

    public static FieldItemRandomizeOptions WithIncludeVisible(this FieldItemRandomizeOptions options, bool value) => Copy(options, includeVisible: value);
    public static FieldItemRandomizeOptions WithIncludeHidden(this FieldItemRandomizeOptions options, bool value) => Copy(options, includeHidden: value);
    public static FieldItemRandomizeOptions WithIncludeMegaStones(this FieldItemRandomizeOptions options, bool value) => Copy(options, includeMegaStones: value);
    public static FieldItemRandomizeOptions WithExcludeMachines(this FieldItemRandomizeOptions options, bool value) => Copy(options, excludeMachines: value);
    public static FieldItemRandomizeOptions WithExcludeKeyItems(this FieldItemRandomizeOptions options, bool value) => Copy(options, excludeKeyItems: value);
    public static FieldItemRandomizeOptions WithSafeMode(this FieldItemRandomizeOptions options, bool value) => Copy(options, safeMode: value);
    public static FieldItemRandomizeOptions WithKeepCategory(this FieldItemRandomizeOptions options, bool value) => Copy(options, keepCategory: value);
    public static FieldItemRandomizeOptions WithFallbackToShuffle(this FieldItemRandomizeOptions options, bool value) => Copy(options, fallbackToShuffle: value);
    public static FieldItemRandomizeOptions WithPoolMode(this FieldItemRandomizeOptions options, FieldItemPoolMode value) => Copy(options, poolMode: value);
    public static FieldItemRandomizeOptions WithBanMails(this FieldItemRandomizeOptions options, bool value) => Copy(options, banMails: value);
    public static FieldItemRandomizeOptions WithBanBadBerries(this FieldItemRandomizeOptions options, bool value) => Copy(options, banBadBerries: value);
    public static FieldItemRandomizeOptions WithBanFlavorItems(this FieldItemRandomizeOptions options, bool value) => Copy(options, banFlavorItems: value);
    public static FieldItemRandomizeOptions WithBanBattleJunk(this FieldItemRandomizeOptions options, bool value) => Copy(options, banBattleJunk: value);

    private static FieldItemRandomizeOptions Copy(FieldItemRandomizeOptions options, FieldItemRandomizeMode? mode = null, bool? includeVisible = null, bool? includeHidden = null, bool? includeMegaStones = null, bool? excludeMachines = null, bool? excludeKeyItems = null, bool? safeMode = null, bool? keepCategory = null, bool? fallbackToShuffle = null, FieldItemPoolMode? poolMode = null, bool? banMails = null, bool? banBadBerries = null, bool? banFlavorItems = null, bool? banBattleJunk = null) => new()
    {
        Mode = mode ?? options.Mode,
        IncludeVisible = includeVisible ?? options.IncludeVisible,
        IncludeHidden = includeHidden ?? options.IncludeHidden,
        IncludeMegaStones = includeMegaStones ?? options.IncludeMegaStones,
        ExcludeMachines = excludeMachines ?? options.ExcludeMachines,
        ExcludeKeyItems = excludeKeyItems ?? options.ExcludeKeyItems,
        SafeMode = safeMode ?? options.SafeMode,
        KeepCategory = keepCategory ?? options.KeepCategory,
        FallbackToShuffle = fallbackToShuffle ?? options.FallbackToShuffle,
        PoolMode = poolMode ?? options.PoolMode,
        BanMails = banMails ?? options.BanMails,
        BanBadBerries = banBadBerries ?? options.BanBadBerries,
        BanFlavorItems = banFlavorItems ?? options.BanFlavorItems,
        BanBattleJunk = banBattleJunk ?? options.BanBattleJunk,
    };
}

internal sealed class FieldItemRandomizeResult(int totalEntries, int randomizedEntries, int changedEntries, string summary)
{
    public int TotalEntries { get; } = totalEntries;
    public int RandomizedEntries { get; } = randomizedEntries;
    public int ChangedEntries { get; } = changedEntries;
    public string Summary { get; } = summary;
}

internal sealed class FieldItemDumpResult(int count, string csv)
{
    public int Count { get; } = count;
    public string Csv { get; } = csv;
}

internal sealed class FieldItemDumpEntry
{
    public int Index { get; set; }
    public int Generation { get; set; }
    public string Game { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Container { get; set; } = string.Empty;
    public int FileIndex { get; set; }
    public int SubFile { get; set; }
    public int AreaIndex { get; set; } = -1;
    public int EntryIndex { get; set; }
    public int Slot { get; set; }
    public int Offset { get; set; }
    public int ItemID { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int? NewItemID { get; set; }
}



