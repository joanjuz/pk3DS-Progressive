using System.CodeDom;
using pk3DS.Core.Structures;

namespace pk3DS.WinForms;

internal static class EconomyFixer
{
    internal const string ActionId = "items.fix-economy";

    // Item IDs are stable in Gen 6/7 item data.
    private const int UltraBall = 2;
    private const int GreatBall = 3;
    private const int PokeBall = 4;
    private const int SuperRepel = 76;
    private const int MaxRepel = 77;
    private const int Repel = 79;

    private const int PlumaVigor = 565;
    private const int PlumaMusculo = 566;
    private const int PlumaAguante = 567;
    private const int PlumaIntelecto = 568;
    private const int PlumaMente = 569;
    private const int PlumaImpetu = 570;
    private const int EscamaCorazon = 93;


    internal static int Apply(byte[][] files)
    {
        int changed = 0;

        // Technical Machines / Hidden Machines used by pk3DS as the protected TM/HM set.
        foreach (int itemID in MartEditor7.BannedItems)
        changed += SetBuyPrice(files, itemID, 1000);

        changed += SetBuyPrice(files, PokeBall, 100);
        changed += SetBuyPrice(files, GreatBall, 150);
        changed += SetBuyPrice(files, UltraBall, 200);

        changed += SetBuyPrice(files, Repel, 50);
        changed += SetBuyPrice(files, SuperRepel, 50);
        changed += SetBuyPrice(files, MaxRepel, 50);

        changed += SetBuyPrice(files, PlumaAguante, 10);
        changed += SetBuyPrice(files, PlumaImpetu, 10);
        changed += SetBuyPrice(files, EscamaCorazon, 7500);
        changed += SetBuyPrice(files, PlumaVigor, 10);
        changed += SetBuyPrice(files, PlumaMusculo, 10);
        changed += SetBuyPrice(files, PlumaMente, 10);
        changed += SetBuyPrice(files, PlumaIntelecto, 10);

        

        return changed;
    }

    private static int SetBuyPrice(byte[][] files, int itemID, int buyPrice)
    {
        if ((uint)itemID >= (uint)files.Length)
            return 0;

        if (files[itemID] is not { Length: > 0 })
            return 0;

        var item = new Item(files[itemID]);

        if (item.BuyPrice == buyPrice)
            return 0;

        item.BuyPrice = buyPrice;
        files[itemID] = item.Write();
        return 1;
    }
}
