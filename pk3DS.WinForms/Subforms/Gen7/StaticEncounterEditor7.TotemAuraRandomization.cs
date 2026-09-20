using System;
using System.Linq;

using pk3DS.Core;
using pk3DS.Core.Structures;

namespace pk3DS.WinForms;

public partial class StaticEncounterEditor7
{
    private static readonly int[] TotemAuraSingleStatBases =
    [
        1,  // Attack
        4,  // Defense
        7,  // Sp. Attack
        10, // Sp. Defense
        13, // Speed
    ];

    private void RandomizeTotemAuraStat(
        int encounterIndex,
        EncounterStatic7 encounter)
    {
        if (!IsTotemAuraRandomizationTarget(
                encounterIndex,
                encounter))
        {
            return;
        }

        int aura = encounter.Aura;

        // 16-18 are All Stats +1/+2/+3. Keep those intact because
        // converting them into one individual stat would substantially
        // change the original Totem's strength rather than only changing
        // which stat receives the vanilla boost.
        if (aura is >= 16 and <= 18)
            return;

        if (aura is < 1 or > 15)
            return;

        int boost = ((aura - 1) % 3) + 1;
        int currentBase = aura - boost + 1;

        int[] alternatives = TotemAuraSingleStatBases
            .Where(statBase => statBase != currentBase)
            .ToArray();

        int newBase = alternatives[
            Util.Rand.Next(alternatives.Length)];

        encounter.Aura = newBase + boost - 1;
    }

    private bool IsTotemAuraRandomizationTarget(
        int encounterIndex,
        EncounterStatic7 encounter)
    {
        if (Main.Config.USUM)
        {
            // USUM's static table also contains old SM Totems and SOS allies.
            // Use the known boss Entry IDs so the randomizer targets the actual
            // Totem battles even after their species have been randomized.
            return GetUSUMTotemBossIndices()
                .Contains(encounterIndex);
        }

        // Preserve the existing SM behavior until its Totem Entry IDs are
        // mapped explicitly.
        return encounter.Aura != 0;
    }
}
