using System;
using System.Collections.Generic;
using System.Linq;

namespace pk3DS.Core.Modding.Research;

public sealed record LevelCapEntry(string Label, byte FlagOffset, byte FlagBit, byte Cap)
{
    public byte[] ToBytes() => [FlagOffset, FlagBit, Cap];

    public override string ToString() =>
        $"{Label} - Lv {Cap} (flag 0x{FlagOffset:X2} bit 0x{FlagBit:X2})";
}

public sealed record StoryFlag(string Label, byte Offset, byte Bit)
{
    public override string ToString() => $"{Label}  (0x{Offset:X2}/0x{Bit:X2})";
}

/// <summary>
/// Story-flag driven player level-cap progression researched for USUM.
/// Each entry is three bytes in the injected runtime table: flag offset, flag bit, level cap.
/// </summary>
public sealed class LevelCapTable
{
    public const int EntrySize = 3;
    public const int MaxEntries = 128;
    public const byte ResearchFinalCap = 98;
    public const byte HardCeiling = 100;

    public static readonly StoryFlag[] KnownFlags =
    [
        new("Ride Pager", 0x01, 0x02),
        new("Tauros Charge", 0x01, 0x04),
        new("Stoutland Search", 0x01, 0x08),
        new("Machamp Shove", 0x01, 0x10),
        new("Mudsdale Ride", 0x01, 0x20),
        new("Lapras Surf", 0x01, 0x40),
        new("Sharpedo Jet", 0x01, 0x80),
        new("Charizard Fly", 0x02, 0x01),
        new("Fly - Pokemon League", 0x10, 0x20),
        new("Fly - Ruins of Abundance", 0x10, 0x40),
        new("Fly - Paniola Ranch", 0x10, 0x80),
        new("Fly - Route 1", 0x11, 0x01),
        new("Fly - Hau'oli City", 0x11, 0x02),
        new("Fly - Route 2", 0x11, 0x04),
        new("Fly - Iki Town", 0x11, 0x08),
        new("Fly - Your House", 0x11, 0x10),
        new("Fly - Ten Carat Hill", 0x11, 0x20),
        new("Fly - Melemele Meadow", 0x11, 0x40),
        new("Fly - Verdant Cave", 0x11, 0x80),
        new("Fly - Ruins of Conflict", 0x12, 0x01),
        new("Fly - Hau'oli Cemetery", 0x12, 0x02),
        new("Fly - Heahea City", 0x12, 0x04),
        new("Fly - Royal Avenue", 0x12, 0x08),
        new("Fly - Route 8", 0x12, 0x10),
        new("Fly - Konikoni City", 0x12, 0x20),
        new("Fly - Paniola Town", 0x12, 0x40),
        new("Fly - Battle Royale Dome", 0x12, 0x80),
        new("Fly - Hano Grand Resort", 0x13, 0x01),
        new("Fly - Route 5/Brooklet Hill", 0x13, 0x02),
        new("Fly - Lush Jungle", 0x13, 0x04),
        new("Fly - Route 7/Wela Volcano Park", 0x13, 0x08),
        new("Fly - Ruins of Life", 0x13, 0x10),
        new("Fly - Route 9/Memorial Hill", 0x13, 0x20),
        new("Fly - Malie City", 0x13, 0x40),
        new("Fly - Tapu Village", 0x14, 0x01),
        new("Fly - Route 16", 0x14, 0x02),
        new("Fly - Mount Hokulani", 0x14, 0x04),
        new("Fly - Blush Mountain", 0x14, 0x08),
        new("Fly - Seafolk Village", 0x14, 0x20),
        new("Fly - Battle Tree", 0x14, 0x40),
        new("Fly - Vast Poni Canyon", 0x14, 0x80),
        new("Fly - Ruins of Hope", 0x15, 0x01),
        new("Fly - Poni Meadow", 0x15, 0x02),
        new("Fly - Exeggutor Island", 0x15, 0x04),
        new("Fly - Aether Paradise", 0x15, 0x08),
        new("Fly - Po Town", 0x2D, 0x40),
        new("Fly - Lake of the Moone", 0x2E, 0x02),
        new("Fly - Altar of the Sunne", 0x2E, 0x10),
        new("Fly - Heahea Beach", 0x35, 0x02),
        new("Fly - Circle Controls", 0x35, 0x04),
        new("Fly - Big Wave Beach", 0x35, 0x08),
        new("Fly - Hau'oli Photo Club", 0x35, 0x20),
        new("Fly - Konikoni Photo Club", 0x35, 0x40),
        new("Fly - Ula'ula Beach", 0x35, 0x80),
        new("Fly - Poni Beach", 0x36, 0x01),
        new("Lanturn 360", 0x3C, 0x02),
        new("Primarina Twist", 0x3C, 0x04),
        new("Starmie 720", 0x3C, 0x08),
        new("Over the Gyarados", 0x3C, 0x10),
    ];

    public List<LevelCapEntry> Entries { get; init; } = [];

    public static LevelCapTable Default() => new()
    {
        Entries =
        [
            new("Iki Town (added)", 0x11, 0x08, 10),
            new("Ride Pager (added)", 0x01, 0x02, 12),
            new("Trainer School", 0x11, 0x01, 15),
            new("Captain Ilima", 0x11, 0x02, 17),
            new("Route 2 (added)", 0x11, 0x04, 18),
            new("Totem Gumshoos", 0x11, 0x80, 20),
            new("Kahuna Hala", 0x11, 0x40, 27),
            new("Tauros Charge (added: ride 1)", 0x01, 0x04, 28),
            new("Dexio/Sina Battle", 0x12, 0x04, 29),
            new("Hau, Paniola Town", 0x12, 0x40, 32),
            new("Paniola Ranch (added)", 0x10, 0x80, 34),
            new("Stoutland Search (added: ride 2)", 0x01, 0x08, 35),
            new("Gladion", 0x13, 0x02, 37),
            new("Totem Araquanid", 0x01, 0x40, 40),
            new("Royal Avenue", 0x12, 0x08, 45),
            new("Battle Royale Dome (added)", 0x12, 0x80, 47),
            new("Totem Marowak", 0x13, 0x08, 50),
            new("Charizard Glide (added: ride 4)", 0x02, 0x01, 51),
            new("Lush Jungle (added)", 0x13, 0x04, 52),
            new("Totem Lurantis", 0x12, 0x10, 55),
            new("Konikoni City", 0x12, 0x20, 60),
            new("Kahuna Olivia", 0x13, 0x10, 63),
            new("Malie City Hau Battle", 0x13, 0x40, 65),
            new("Mt. Hokulani Bus (added: Route 16)", 0x14, 0x02, 70),
            new("Totem Togedemaru", 0x14, 0x04, 72),
            new("Ula'ula Beach (Mudsdale Ride)", 0x01, 0x20, 75),
            new("Sharpedo Jet (added: ride 6)", 0x01, 0x80, 77),
            new("Totem Mimikyu", 0x14, 0x01, 80),
            new("Po Town", 0x2D, 0x40, 85),
            new("Kahuna Nanu (added: Ula'ula Beach fly)", 0x35, 0x80, 87),
            new("Aether Branch Chief Faba", 0x15, 0x08, 90),
            new("Arrive Seafolk Village", 0x3C, 0x10, 95),
            new("Vast Poni Canyon (added: after Hapu)", 0x14, 0x80, 96),
            new("Arrive Altar of the Sunne", 0x2E, 0x10, 97),
            new("Elite Four (post-game flag)", 0x2E, 0x02, ResearchFinalCap),
        ],
    };

    public byte[] ToBytes(bool terminate = true)
    {
        var bytes = new List<byte>((Entries.Count * EntrySize) + EntrySize);
        foreach (var e in Entries)
            bytes.AddRange(e.ToBytes());

        if (terminate)
            bytes.AddRange([0, 0, HardCeiling]);

        return [.. bytes];
    }

    public List<string> Validate()
    {
        var problems = new List<string>();
        if (Entries.Count == 0)
        {
            problems.Add("the table is empty; no cap would ever apply");
            return problems;
        }

        if (Entries.Count > MaxEntries)
            problems.Add($"the table has more than {MaxEntries} checkpoints; the runtime table limit is {MaxEntries}");

        foreach (var e in Entries)
        {
            if (e.Cap is 0 or > HardCeiling)
                problems.Add($"'{e.Label}': cap {e.Cap} is outside 1-100");

            if (e.FlagBit == 0 || (e.FlagBit & (e.FlagBit - 1)) != 0)
                problems.Add($"'{e.Label}': bit 0x{e.FlagBit:X2} is not a single bit");
        }

        foreach (var g in Entries.GroupBy(e => (e.FlagOffset, e.FlagBit)).Where(g => g.Count() > 1))
        {
            problems.Add(
                $"flag 0x{g.Key.FlagOffset:X2} bit 0x{g.Key.FlagBit:X2} is used by " +
                string.Join(" and ", g.Select(e => $"'{e.Label}'")));
        }

        for (int i = 1; i < Entries.Count; i++)
        {
            if (Entries[i].Cap < Entries[i - 1].Cap)
            {
                problems.Add(
                    $"'{Entries[i].Label}': cap {Entries[i].Cap} is lower than the previous " +
                    $"checkpoint cap {Entries[i - 1].Cap}; checkpoint order must never go backwards");
            }
        }

        return problems;
    }

    public LevelCapTable Clone() => new() { Entries = [.. Entries] };
}
