using System.Collections.Generic;
using System.Linq;
namespace pk3DS.WinForms;

public sealed class TrainerMoveRule
{
    public bool Enabled { get; set; } = true;
    public int TrainerID { get; set; }
    public string Group { get; set; } = string.Empty;
    public string Trainer { get; set; } = string.Empty;
    public int CurrentAceLevel { get; set; }

    // 0 means disabled.
    public int MinMovePower { get; set; }

    // If enabled, damaging moves are filtered by the Pokémon's stronger attacking stat.
    public bool UseStrongestAttackStat { get; set; }

    // If Attack and Sp. Attack differ by this value or less, the Pokémon is treated as mixed.
    public int MixedTolerance { get; set; } = 15;

    // Enabled by default. If disabled, status moves are filtered out.
    public bool AllowStatusMoves { get; set; } = true;

    // Force Better Movesets for this trainer even when the global Better Movesets checkbox is disabled.
    public bool BetterMovesets { get; set; } = true;

    // Give this trainer competitive/smart held items after its final moveset is generated.
    public bool SmartItems { get; set; } = true;
    // -1 means disabled. If set, all EV stats for every Pokémon in this trainer battle use this value when supported.
    public int OverrideEVs { get; set; } = -1;

    public TrainerMoveRule Clone()
    {
        return new TrainerMoveRule
        {
            Enabled = Enabled,
            TrainerID = TrainerID,
            Group = Group,
            Trainer = Trainer,
            CurrentAceLevel = CurrentAceLevel,
            MinMovePower = MinMovePower,
            UseStrongestAttackStat = UseStrongestAttackStat,
            MixedTolerance = MixedTolerance,
            AllowStatusMoves = AllowStatusMoves,
            BetterMovesets = BetterMovesets,
            SmartItems = SmartItems,
            OverrideEVs = OverrideEVs,
        };
    }

    public static List<TrainerMoveRule> FromLevelCapRules(IEnumerable<TrainerLevelCapRule> rules)
    {
        return rules.Select(r => new TrainerMoveRule
        {
            Enabled = true,
            TrainerID = r.TrainerID,
            Group = r.Group,
            Trainer = r.Trainer,
            CurrentAceLevel = r.CurrentAceLevel,
            MinMovePower = 0,
            UseStrongestAttackStat = false,
            MixedTolerance = 15,
            AllowStatusMoves = true,
            BetterMovesets = true,
            SmartItems = true,
            OverrideEVs = -1,
        })
        .OrderBy(r => r.CurrentAceLevel)
        .ThenBy(r => r.TrainerID)
        .ToList();
    }
}
