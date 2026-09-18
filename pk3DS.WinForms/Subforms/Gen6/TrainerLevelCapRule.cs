namespace pk3DS.WinForms;

public sealed class TrainerLevelCapRule
{
    public bool Enabled { get; set; } = true;
    public int TrainerID { get; set; }
    public string Group { get; set; } = string.Empty;
    public string Trainer { get; set; } = string.Empty;
    public int CurrentAceLevel { get; set; }

    // 0 means: use the trainer's current highest-level Pokémon as the cap.
    public int LevelCap { get; set; }

    // Per-trainer option: force the selected important battle to have a Mega-capable ace.
    public bool GuaranteeMega { get; set; }

    // Gen 7 only: force one Pokémon in this important battle to hold a compatible Z-Crystal.
    public bool GuaranteeZMove { get; set; }

    // Per-trainer option: force randomized trainer moves to have at least this power.
    // 0 means disabled.
    public int MinMovePower { get; set; }

    public TrainerLevelCapRule Clone()
    {
        return new TrainerLevelCapRule
        {
            Enabled = Enabled,
            TrainerID = TrainerID,
            Group = Group,
            Trainer = Trainer,
            CurrentAceLevel = CurrentAceLevel,
            LevelCap = LevelCap,
            GuaranteeMega = GuaranteeMega,
            GuaranteeZMove = GuaranteeZMove,
            MinMovePower = MinMovePower,
        };
    }
}

public sealed class TrainerLevelCapStage
{
    public int TrainerID { get; set; }
    public int OriginalAceLevel { get; set; }
    public int LevelCap { get; set; }
    public bool GuaranteeMega { get; set; }

    // Gen 7 only: force one Pokémon in this important battle to hold a compatible Z-Crystal.
    public bool GuaranteeZMove { get; set; }
    public int MinMovePower { get; set; }
}
