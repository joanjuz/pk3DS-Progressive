using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using pk3DS.Core.Structures;

namespace pk3DS.WinForms;

internal static class EconomyFixer
{
    internal const string ActionId = "items.fix-economy";
    private const string PricesParameter = "prices";
    private const string SchemaParameter = "schema";
    private const string BasisParameter = "basis";
    private const string SchemaVersion = "1";
    private const string BasisName = "UPR-ZX Gen7 balancedItemPrices + pk3DS-Progressive";

    // Universal Pokemon Randomizer ZX, Gen7Constants.balancedItemPrices.
    // UPR-ZX stores these values divided by 10. The values below are the actual
    // Gen 7 BuyPrice values, indexed directly by item ID. Index 0 is unused.
    // IDs 1..959 are the complete UPR-ZX Gen 7 table.
    private static readonly int[] UprZxGen7BuyPrices =
    [
        0, 3000, 800, 600, 200, 500, 1000, 1000, 1000, 1000, 1000, 1000, 200, 1000, 300, 1000, 200, 200, 200, 300, 100, 100, 300, 3000,
        2500, 1500, 700, 400, 2000, 4000, 200, 300, 400, 600, 500, 1200, 300, 2800, 3000, 4500, 15000, 18000, 350, 200, 5000, 10000, 10000, 10000,
        10000, 10000, 10000, 10000, 10000, 25000, 350, 1500, 1000, 1000, 2000, 1000, 1000, 1000, 2000, 100, 100, 20, 20, 20, 20, 20, 20, 20,
        1000, 1000, 1000, 1000, 700, 900, 1000, 400, 3000, 3000, 3000, 3000, 3000, 3000, 500, 5000, 2000, 8000, 3000, 12000, 10000, 5000, 300, 200,
        200, 200, 200, 7000, 7000, 7000, 7000, 10000, 7000, 7000, 5000, 3000, 3000, 3000, 2000, 2100, 10000, 0, 0, 0, 1000, 1000, 1000, 1000,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 150, 10000, 10000, 50, 50, 50, 50, 50, 50, 50,
        50, 50, 50, 50, 50, 200, 250, 100, 250, 250, 3000, 50, 200, 500, 500, 100, 100, 100, 100, 100, 500, 500, 500, 500,
        500, 500, 500, 500, 500, 500, 500, 500, 500, 500, 500, 500, 500, 500, 500, 500, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000,
        1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 4000, 4000, 3000,
        0, 4500, 1000, 1000, 10000, 5000, 2000, 15000, 1000, 200, 3000, 3000, 4000, 3000, 3000, 10000, 5000, 3000, 10000, 3000, 1000, 2000, 2000, 2000,
        2000, 2000, 2000, 2000, 2000, 2000, 2000, 2000, 2000, 2000, 2000, 2000, 3000, 6000, 2000, 3000, 1000, 1000, 1000, 1000, 100, 100, 100, 100,
        100, 1500, 2000, 2000, 6000, 1500, 10000, 1000, 1500, 1500, 1000, 2000, 1500, 3000, 1000, 1000, 1500, 5000, 200, 200, 200, 200, 1500, 10000,
        1500, 3000, 3000, 3000, 3000, 3000, 3000, 500, 1500, 10000, 2000, 2000, 2000, 2000, 2000, 2000, 2000, 2000, 2000, 2000, 2000, 2000, 2000, 2000,
        2000, 2000, 2000, 2000, 1000, 2000, 2000, 15000, 1000, 3000, 3000, 3000, 3000, 3000, 5000, 5000, 10000, 10000, 10000, 10000, 10000, 10000, 20000, 10000,
        10000, 10000, 20000, 10000, 10000, 20000, 20000, 10000, 10000, 20000, 10000, 10000, 10000, 10000, 10000, 10000, 20000, 10000, 10000, 20000, 10000, 10000, 10000, 10000,
        10000, 10000, 10000, 10000, 20000, 20000, 10000, 10000, 10000, 10000, 10000, 10000, 10000, 10000, 10000, 10000, 10000, 20000, 10000, 20000, 10000, 10000, 10000, 10000,
        10000, 10000, 20000, 10000, 10000, 10000, 10000, 10000, 10000, 10000, 10000, 20000, 10000, 20000, 20000, 10000, 5000, 10000, 10000, 10000, 10000, 10000, 10000, 10000,
        10000, 10000, 10000, 10000, 10000, 10000, 10000, 10000, 10000, 10000, 10000, 10000, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 20, 20, 20, 20, 20, 20, 20, 300, 300, 300, 300, 300, 300, 300, 300, 0, 0, 0, 0,
        350, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 3000, 10000, 1000, 6000, 1000, 1000, 1000, 2000, 1000, 1000, 1000, 1000, 1000, 1000, 1000,
        1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 300, 300, 300, 300, 300, 300, 1000, 7000, 7000, 0, 200,
        1000, 100, 0, 0, 15000, 40000, 30000, 60000, 0, 0, 0, 0, 0, 0, 0, 350, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 20000, 20000, 10000, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2000, 6000, 0, 0, 0, 2000, 5000, 3000, 3000,
        200, 200, 3000, 0, 200, 200, 200, 200, 10000, 10000, 10000, 10000, 10000, 10000, 20000, 20000, 10000, 5000, 10000, 10000, 5000, 20000, 10000, 10000,
        10000, 10000, 5000, 5000, 10000, 5000, 10000, 10000, 10000, 3000, 5000, 20000, 20000, 20000, 1000, 1000, 1000, 0, 10000, 10000, 20000, 10000, 5000, 0,
        0, 0, 0, 20, 0, 0, 0, 0, 3000, 0, 0, 0, 350, 350, 7000, 7000, 0, 0, 0, 1000, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 10000, 10000, 3000, 5000, 10000, 5000, 20000, 5000, 5000, 10000, 5000, 5000, 20000, 0, 0, 5000,
        5000, 20000, 3000, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 5000, 10000, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 10, 0, 300, 0, 0, 3000, 0, 300, 350, 300, 300, 300, 300, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4000, 3000, 1000, 1000, 1000, 1000, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000,
        1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
    ];

    private static readonly HashSet<int> LegacyProgressiveIds =
        BuildLegacyProgressiveIds();

    internal static SortedDictionary<int, int> GetDefaultPrices(int itemCount)
    {
        var result =
            new SortedDictionary<int, int>();

        int end =
            Math.Min(
                itemCount - 1,
                UprZxGen7BuyPrices.Length - 1);

        for (int itemID = 1;
             itemID <= end;
             itemID++)
        {
            result[itemID] =
                UprZxGen7BuyPrices[itemID];
        }

        // Keep historical pk3DS-Progressive targets represented even if a
        // future UPR-ZX table ever stops before one of these IDs.
        foreach ((int itemID, int price) in
                 GetLegacyProgressiveDefaults())
        {
            if ((uint)itemID < (uint)itemCount &&
                !result.ContainsKey(itemID))
            {
                result[itemID] =
                    price;
            }
        }

        return result;
    }

    internal static SortedDictionary<int, int> GetEditorInitialPrices(
        int itemCount)
    {
        GlobalRandomizationAction existing =
            RandomizationSessionState
                .ExportActions()
                .FirstOrDefault(z =>
                    string.Equals(
                        z?.Id,
                        ActionId,
                        StringComparison.OrdinalIgnoreCase));

        if (existing?.Parameters?.ContainsKey(PricesParameter) == true)
        {
            if (!TryParsePrices(
                    existing,
                    out SortedDictionary<int, int> parsed,
                    out string error))
            {
                throw new InvalidOperationException(
                    error);
            }

            return parsed;
        }

        return GetDefaultPrices(
            itemCount);
    }

    internal static string GetSourceLabel(
        int itemID,
        int targetPrice,
        int itemCount)
    {
        SortedDictionary<int, int> defaults =
            GetDefaultPrices(
                itemCount);

        if (!defaults.TryGetValue(
                itemID,
                out int defaultPrice) ||
            defaultPrice != targetPrice)
        {
            return "Custom";
        }

        return LegacyProgressiveIds.Contains(
            itemID)
                ? "UPR-ZX + Progressive"
                : "UPR-ZX";
    }

    internal static void MarkSessionAction(
        IReadOnlyDictionary<int, int> prices)
    {
        RandomizationSessionState.MarkAction(
            ActionId,
            (SchemaParameter, SchemaVersion),
            (BasisParameter, BasisName),
            (PricesParameter, SerializePrices(prices)));
    }

    // Compatibility overload for Gen 6 ItemEditor6.
    // The editable UPR-ZX table introduced by this feature is Gen 7-specific;
    // XY/ORAS keep the historical pk3DS-Progressive Fix Economy preset.
    internal static int Apply(byte[][] files) =>
        ApplyLegacy(files);

    internal static int Apply(
        byte[][] files,
        IReadOnlyDictionary<int, int> prices)
    {
        if (files is null)
            throw new ArgumentNullException(nameof(files));
        if (prices is null)
            throw new ArgumentNullException(nameof(prices));

        int changed =
            0;

        foreach ((int itemID, int buyPrice) in
                 prices.OrderBy(z => z.Key))
        {
            ValidatePriceEntry(
                itemID,
                buyPrice);

            if ((uint)itemID >= (uint)files.Length ||
                files[itemID] is not { Length: > 0 })
            {
                throw new InvalidOperationException(
                    $"Fix Economy item ID {itemID} is not available in the loaded item table.");
            }

            changed +=
                SetBuyPrice(
                    files,
                    itemID,
                    buyPrice);
        }

        return changed;
    }

    internal static int ApplyAction(
        byte[][] files,
        GlobalRandomizationAction action,
        out string mode)
    {
        if (action?.Parameters?.ContainsKey(PricesParameter) == true)
        {
            if (!TryParsePrices(
                    action,
                    out SortedDictionary<int, int> prices,
                    out string error))
            {
                throw new InvalidOperationException(
                    error);
            }

            mode =
                $"custom table, {prices.Count} item(s)";

            return Apply(
                files,
                prices);
        }

        // Backward compatibility: templates created before this feature stored
        // items.fix-economy with no parameters. Replaying them must preserve
        // the old pk3DS-Progressive prices instead of silently changing them.
        mode =
            "legacy pk3DS-Progressive preset";

        return ApplyLegacy(
            files);
    }

    internal static void ValidateAction(
        GlobalRandomizationAction action)
    {
        if (action?.Parameters?.ContainsKey(PricesParameter) != true)
            return;

        if (!TryParsePrices(
                action,
                out _,
                out string error))
        {
            throw new InvalidOperationException(
                error);
        }
    }

    internal static string SerializePrices(
        IReadOnlyDictionary<int, int> prices)
    {
        if (prices is null)
            return string.Empty;

        return string.Join(
            ";",
            prices
                .OrderBy(z => z.Key)
                .Select(z =>
                    $"{z.Key.ToString(CultureInfo.InvariantCulture)}:{z.Value.ToString(CultureInfo.InvariantCulture)}"));
    }

    internal static bool TryParsePrices(
        GlobalRandomizationAction action,
        out SortedDictionary<int, int> prices,
        out string error)
    {
        prices =
            new SortedDictionary<int, int>();

        error =
            string.Empty;

        if (action?.Parameters is null ||
            !action.Parameters.TryGetValue(
                PricesParameter,
                out string raw))
        {
            error =
                "Fix Economy action does not contain an editable price table.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(raw))
            return true;

        foreach (string token in
                 raw.Split(
                     ';',
                     StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries))
        {
            string[] parts =
                token.Split(
                    ':',
                    StringSplitOptions.TrimEntries);

            if (parts.Length != 2 ||
                !int.TryParse(
                    parts[0],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int itemID) ||
                !int.TryParse(
                    parts[1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int price))
            {
                error =
                    $"Invalid Fix Economy price entry '{token}'. Expected itemID:price.";
                prices.Clear();
                return false;
            }

            try
            {
                ValidatePriceEntry(
                    itemID,
                    price);
            }
            catch (Exception ex)
            {
                error =
                    ex.Message;

                prices.Clear();
                return false;
            }

            if (!prices.TryAdd(
                    itemID,
                    price))
            {
                error =
                    $"Fix Economy price table contains duplicate item ID {itemID}.";

                prices.Clear();
                return false;
            }
        }

        return true;
    }

    internal static int GetCurrentBuyPrice(
        byte[][] files,
        int itemID)
    {
        if (files is null ||
            (uint)itemID >= (uint)files.Length ||
            files[itemID] is not { Length: > 0 })
        {
            throw new ArgumentOutOfRangeException(
                nameof(itemID));
        }

        return new Item(
            files[itemID]).BuyPrice;
    }

    internal static int GetSellPriceFromBuy(
        int buyPrice) =>
        buyPrice / 2;

    private static void ValidatePriceEntry(
        int itemID,
        int price)
    {
        if (itemID <= 0)
        {
            throw new InvalidOperationException(
                $"Fix Economy item ID must be positive. Actual: {itemID}.");
        }

        if (price < 0 ||
            price > 655350)
        {
            throw new InvalidOperationException(
                $"Fix Economy price for item {itemID} must be between 0 and 655350. Actual: {price}.");
        }

        if (price % 10 != 0)
        {
            throw new InvalidOperationException(
                $"Fix Economy price for item {itemID} must be a multiple of 10 because Gen 7 stores prices divided by 10. Actual: {price}.");
        }
    }

    private static int ApplyLegacy(
        byte[][] files)
    {
        int changed =
            0;

        foreach (int itemID in
                 MartEditor7.BannedItems)
        {
            changed +=
                SetBuyPrice(
                    files,
                    itemID,
                    1000);
        }

        foreach ((int itemID, int price) in
                 GetLegacyProgressiveDefaults())
        {
            changed +=
                SetBuyPrice(
                    files,
                    itemID,
                    price);
        }

        return changed;
    }

    private static IEnumerable<(int ItemID, int Price)>
        GetLegacyProgressiveDefaults()
    {
        yield return (4, 100);   // Poke Ball
        yield return (3, 150);   // Great Ball
        yield return (2, 200);   // Ultra Ball
        yield return (79, 50);   // Repel
        yield return (76, 50);   // Super Repel
        yield return (77, 50);   // Max Repel
        yield return (567, 10);  // Resist Feather
        yield return (570, 10);  // Swift Feather
        yield return (93, 7500); // Heart Scale
        yield return (565, 10);  // Health Feather
        yield return (566, 10);  // Muscle Feather
        yield return (569, 10);  // Clever Feather
        yield return (568, 10);  // Genius Feather
    }

    private static HashSet<int>
        BuildLegacyProgressiveIds()
    {
        var result =
            new HashSet<int>(
                MartEditor7.BannedItems);

        foreach ((int itemID, _) in
                 GetLegacyProgressiveDefaults())
        {
            result.Add(
                itemID);
        }

        return result;
    }

    private static int SetBuyPrice(
        byte[][] files,
        int itemID,
        int buyPrice)
    {
        if ((uint)itemID >= (uint)files.Length ||
            files[itemID] is not { Length: > 0 })
        {
            return 0;
        }

        var item =
            new Item(
                files[itemID]);

        if (item.BuyPrice ==
            buyPrice)
        {
            return 0;
        }

        item.BuyPrice =
            buyPrice;

        files[itemID] =
            item.Write();

        return 1;
    }
}