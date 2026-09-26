using System;
using System.Collections.Generic;
using System.Linq;

namespace pk3DS.Core.Modding.Research;

public enum LevelCapConditionKind : byte
{
    EventFlagSet = 0,
    EventWorkAtLeast = 1,
}

public sealed record LevelCapEntry(
    string Label,
    LevelCapConditionKind Kind,
    ushort FlagOffset,
    ushort FlagBit,
    byte Cap)
{
    public LevelCapEntry(
        string label,
        ushort flagOffset,
        ushort flagBit,
        byte cap)
        : this(
            label,
            LevelCapConditionKind.EventFlagSet,
            flagOffset,
            flagBit,
            cap)
    {
    }

    public byte[] ToBytes() =>
    [
        (byte)Kind,
        Cap,
        (byte)(FlagOffset & 0xFF),
        (byte)(FlagOffset >> 8),
        (byte)(FlagBit & 0xFF),
        (byte)(FlagBit >> 8),
    ];

    public override string ToString() =>
        Kind switch
        {
            LevelCapConditionKind.EventFlagSet =>
                $"{Label} - Lv {Cap} (flag byte 0x{FlagOffset:X4}, mask 0x{FlagBit:X2})",

            LevelCapConditionKind.EventWorkAtLeast =>
                $"{Label} - Lv {Cap} (event work 0x{FlagOffset:X4} >= {FlagBit})",

            _ =>
                $"{Label} - Lv {Cap} (unknown condition {(byte)Kind})",
        };
}

public sealed record StoryFlag(
    string Label,
    LevelCapConditionKind Kind,
    ushort Offset,
    ushort Bit)
{
    public StoryFlag(
        string label,
        ushort offset,
        ushort bit)
        : this(
            label,
            LevelCapConditionKind.EventFlagSet,
            offset,
            bit)
    {
    }

    public override string ToString() =>
        Kind switch
        {
            LevelCapConditionKind.EventFlagSet =>
                $"{Label}  (flag 0x{Offset:X4}/0x{Bit:X2})",

            LevelCapConditionKind.EventWorkAtLeast =>
                $"{Label}  (work 0x{Offset:X4} >= {Bit})",

            _ =>
                Label,
        };
}

/// <summary>
/// Save-validated USUM player level-cap progression.
///
/// Runtime entry layout (6 bytes):
///   u8  condition kind
///   u8  level cap
///   u16 argument 0
///   u16 argument 1
///
/// EventFlagSet:
///   argument 0 = global Event Flags byte offset from SaveFlagBase
///   argument 1 = bit mask
///
/// EventWorkAtLeast:
///   argument 0 = Event Work index
///   argument 1 = minimum ushort value
///
/// The runtime advances while the current checkpoint condition is complete.
/// The first incomplete checkpoint supplies the active cap. After every
/// checkpoint is complete, the terminator supplies HardCeiling (Lv100).
/// </summary>
public sealed class LevelCapTable
{
    public const int EntrySize = 6;
    public const int MaxEntries = 128;
    public const byte ResearchFinalCap = 70;
    public const byte HardCeiling = 100;

    public const ushort EventFlagByteCount = 4960 / 8;
    public const ushort EventWorkCount = 1000;

    public static readonly StoryFlag[] KnownFlags =
    [
        new("Clear Normal Trial - Ilima (1dom)", 0x0010, 0x0008),
        new("Defeat Kahuna Hala", 0x0011, 0x0008),
        new("Clear Water Trial - Lana (2dom)", 0x0010, 0x0010),
        new("Clear Fire Trial - Kiawe (3dom)", 0x0010, 0x0040),
        new("Clear Grass Trial - Mallow (4dom)", 0x0010, 0x0020),
        new("Defeat Olivia - Mayla", 0x0011, 0x0010),
        new("Clear Electric Trial - Sophocles (5dom)", 0x0010, 0x0080),
        new("Clear Ghost Trial - Acerola (6dom)", 0x0011, 0x0001),
        new("Defeat Guzma", 0x0198, 0x0080),
        new("Defeat Nanu - Denio", 0x0011, 0x0020),
        new("Defeat Lusamine", 0x018B, 0x0080),
        new("Clear Dragon Trial - Poni (7dom)", 0x0011, 0x0002),

        new(
            "Defeat Ultra Necrozma",
            LevelCapConditionKind.EventWorkAtLeast,
            0x0044,
            1796),

        new("Clear Fairy Trial - Mina (8dom)", 0x0013, 0x0020),
        new("Defeat Hapu - Hela", 0x0011, 0x0040),
        new(
            "Defeat Gladion - Mount Lanakila",
            LevelCapConditionKind.EventWorkAtLeast,
            0x0044,
            1850),
        new("Elite Four - Kahili", 0x018F, 0x0001),
        new("Elite Four - Molayne (Lario)", 0x01B8, 0x0020),
        new("Elite Four - Olivia (Mayla)", 0x018E, 0x0020),
        new("Elite Four - Acerola (Zarala)", 0x018E, 0x0002),
        new(
            "Defeat Champion Hau (Tilo)",
            LevelCapConditionKind.EventWorkAtLeast,
            0x0044,
            2000),
    ];

    public List<LevelCapEntry> Entries { get; init; } = [];

    /// <summary>
    /// Canonical table contains only checkpoints proven by the LevelCaps.zip
    /// before/after save pairs. Earlier/unlisted bosses are intentionally not
    /// inferred. The validated progression now begins at Lv14 until 1dom.
    /// </summary>
    public static LevelCapTable Default() => new()
    {
        Entries =
        [
            new("Clear Normal Trial - Ilima (1dom)", 0x0010, 0x0008, 14),
            new("Defeat Kahuna Hala", 0x0011, 0x0008, 19),
            new("Clear Water Trial - Lana (2dom)", 0x0010, 0x0010, 24),
            new("Clear Fire Trial - Kiawe (3dom)", 0x0010, 0x0040, 26),
            new("Clear Grass Trial - Mallow (4dom)", 0x0010, 0x0020, 29),
            new("Defeat Olivia - Mayla", 0x0011, 0x0010, 34),
            new("Clear Electric Trial - Sophocles (5dom)", 0x0010, 0x0080, 40),
            new("Clear Ghost Trial - Acerola (6dom)", 0x0011, 0x0001, 42),
            new("Defeat Guzma", 0x0198, 0x0080, 42),
            new("Defeat Nanu - Denio", 0x0011, 0x0020, 53),
            new("Defeat Lusamine", 0x018B, 0x0080, 53),
            new("Clear Dragon Trial - Poni (7dom)", 0x0011, 0x0002, 59),

            new(
                "Defeat Ultra Necrozma",
                LevelCapConditionKind.EventWorkAtLeast,
                0x0044,
                1796,
                60),

            new("Clear Fairy Trial - Mina (8dom)", 0x0013, 0x0020, 66),
            new("Defeat Hapu - Hela", 0x0011, 0x0040, 67),
            new(
                "Defeat Gladion - Mount Lanakila",
                LevelCapConditionKind.EventWorkAtLeast,
                0x0044,
                1850,
                67),
            new("Elite Four - Kahili", 0x018F, 0x0001, 68),
            new("Elite Four - Molayne (Lario)", 0x01B8, 0x0020, 68),
            new("Elite Four - Olivia (Mayla)", 0x018E, 0x0020, 68),
            new("Elite Four - Acerola (Zarala)", 0x018E, 0x0002, 68),
            new(
                "Defeat Champion Hau (Tilo)",
                LevelCapConditionKind.EventWorkAtLeast,
                0x0044,
                2000,
                ResearchFinalCap),
        ],
    };

    public byte[] ToBytes(bool terminate = true)
    {
        var bytes =
            new List<byte>(
                (Entries.Count * EntrySize) +
                EntrySize);

        foreach (LevelCapEntry entry in Entries)
            bytes.AddRange(entry.ToBytes());

        if (terminate)
        {
            bytes.AddRange(
            [
                0xFF,
                HardCeiling,
                0,
                0,
                0,
                0,
            ]);
        }

        return [.. bytes];
    }

    public List<string> Validate()
    {
        var problems =
            new List<string>();

        if (Entries.Count == 0)
        {
            problems.Add(
                "the table is empty; no cap would ever apply");
            return problems;
        }

        if (Entries.Count > MaxEntries)
        {
            problems.Add(
                $"the table has more than {MaxEntries} checkpoints; " +
                $"the runtime table limit is {MaxEntries}");
        }

        foreach (LevelCapEntry entry in Entries)
        {
            if (entry.Cap is 0 or > HardCeiling)
            {
                problems.Add(
                    $"'{entry.Label}': cap {entry.Cap} is outside 1-100");
            }

            switch (entry.Kind)
            {
                case LevelCapConditionKind.EventFlagSet:
                    if (entry.FlagOffset >= EventFlagByteCount)
                    {
                        problems.Add(
                            $"'{entry.Label}': Event Flags byte offset " +
                            $"0x{entry.FlagOffset:X4} is outside the USUM bitfield");
                    }

                    if (entry.FlagBit is 0 or > 0x00FF)
                    {
                        problems.Add(
                            $"'{entry.Label}': event-flag mask " +
                            $"0x{entry.FlagBit:X4} must fit in one byte and be non-zero");
                    }
                    break;

                case LevelCapConditionKind.EventWorkAtLeast:
                    if (entry.FlagOffset >= EventWorkCount)
                    {
                        problems.Add(
                            $"'{entry.Label}': Event Work index " +
                            $"0x{entry.FlagOffset:X4} is outside the USUM work array");
                    }

                    if (entry.FlagBit == 0)
                    {
                        problems.Add(
                            $"'{entry.Label}': Event Work threshold cannot be zero");
                    }
                    break;

                default:
                    problems.Add(
                        $"'{entry.Label}': unsupported condition kind {(byte)entry.Kind}");
                    break;
            }
        }

        foreach (var group in Entries
                     .GroupBy(z => (z.Kind, z.FlagOffset, z.FlagBit))
                     .Where(z => z.Count() > 1))
        {
            problems.Add(
                $"condition {group.Key.Kind} " +
                $"0x{group.Key.FlagOffset:X4}/0x{group.Key.FlagBit:X4} " +
                "is used by " +
                string.Join(
                    " and ",
                    group.Select(z =>
                        $"'{z.Label}'")));
        }

        for (int i = 1; i < Entries.Count; i++)
        {
            if (Entries[i].Cap < Entries[i - 1].Cap)
            {
                problems.Add(
                    $"'{Entries[i].Label}': cap {Entries[i].Cap} is lower than " +
                    $"the previous checkpoint cap {Entries[i - 1].Cap}; " +
                    "checkpoint order must never go backwards");
            }
        }

        return problems;
    }

    public LevelCapTable Clone() => new()
    {
        Entries = [.. Entries],
    };
}
