using System.Collections.Generic;
using System.Linq;

using pk3DS.Core;
using pk3DS.Core.Randomizers;

namespace pk3DS.WinForms;

public class Wild7Randomizer
{
    public SpeciesRandomizer RandSpec { private get; set; }
    public FormRandomizer RandForm { private get; set; }

    public int TableRandomizationOption { private get; set; }
    public decimal LevelAmplifier { private get; set; }
    public bool ModifyLevel { private get; set; }
    public bool AllCanCallAllies { private get; set; }

    private void RandomizeTable7(EncounterTable Table, int slotStart, int slotStop)
    {
        int end = slotStop < 0 ? Table.Encounter7s.Length : slotStop;
        for (int s = slotStart; s < end; s++)
        {
            var EncounterSet = Table.Encounter7s[s];
            foreach (var enc in EncounterSet.Where(enc => enc.Species != 0))
            {
                enc.Species = (uint)RandSpec.GetRandomSpecies((int)enc.Species);
                enc.Forme = (uint)RandForm.GetRandomForme((int)enc.Species);
            }
        }
    }

    private void FillEmptySOSSlots(EncounterTable table)
    {
        var regular = table.Encounter7s[0];

        // Encounter7s[8] is AdditionalSOS/weather. Keep those slots exactly
        // as authored and only complete the seven normal SOS rows.
        for (int s = 1; s < table.Encounter7s.Length - 1; s++)
        {
            var sos = table.Encounter7s[s];
            for (int i = 0; i < sos.Length && i < regular.Length; i++)
            {
                if (sos[i].Species != 0 || regular[i].Species == 0)
                    continue;

                int species = RandSpec.GetRandomSpecies((int)regular[i].Species);
                sos[i].Species = (uint)species;
                sos[i].Forme = (uint)RandForm.GetRandomForme(species);
            }
        }
    }

    public void Execute(IEnumerable<Area7> Areas, LazyGARCFile encdata)
    {
        GetTableRandSettings((RandOption)TableRandomizationOption, out int slotStart, out int slotStop, out bool copy);

        foreach (var Map in Areas)
        {
            foreach (var Table in Map.Tables)
            {
                if (ModifyLevel)
                {
                    Table.MinLevel = Randomizer.GetModifiedLevel(Table.MinLevel, LevelAmplifier);
                    Table.MaxLevel = Randomizer.GetModifiedLevel(Table.MaxLevel, LevelAmplifier);
                }

                RandomizeTable7(Table, slotStart, slotStop);
                if (copy) // copy row 0 to rest
                    Table.CopySlotsToSOS();

                if (AllCanCallAllies)
                    FillEmptySOSSlots(Table);

                Table.Write();
            }
            encdata[Map.FileNumber] = Area7.GetDayNightTableBinary(Map.Tables);
        }
    }

    private static void GetTableRandSettings(RandOption option, out int slotStart, out int slotStop, out bool copy)
    {
        copy = false;
        switch (option)
        {
            default: // All
                slotStart = 0;
                slotStop = -1;
                break;
            case RandOption.Regular_Only:
                slotStart = 0;
                slotStop = 1;
                break;
            case RandOption.SOS_Only:
                slotStart = 1;
                slotStop = -1;
                break;
            case RandOption.Regular_CopySOS:
                slotStart = 0;
                slotStop = 1;
                copy = true;
                break;
        }
    }

    private enum RandOption
    {
        All = 0,
        Regular_Only = 1,
        SOS_Only = 2,
        Regular_CopySOS = 3,
    }
}