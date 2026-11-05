using SimT.Contracts.Playing;

namespace SimT.Modules.Dlc.Economy;

/// <summary>
/// Price model in integer arithmetic so a replay is bit-identical. No decimal, no double, no
/// allocation: this is the hot path the settlement loop and the balance simulator call.
///
/// A price is the item's base value scaled by three independent factors:
/// region (where the stall is), season (when you trade) and scarcity (how much stock is left).
/// </summary>
internal static class MarketPricing
{
    private const long PermilleScale = 1_000_000_000L;
    private const int SellRatePercent = 82;

    /// <summary>Applied when a stall is empty: the last units cost 40% above baseline.</summary>
    internal const int ScarcityCeilingPermille = 1400;

    /// <summary>Applied when a stall holds twice its baseline stock.</summary>
    internal const int ScarcityFloorPermille = 700;

    /// <summary>Seasonal factor applied on top of the regional factor.</summary>
    internal static int SeasonPermille(int month) => month switch
    {
        <= 3 => 1000,
        <= 6 => 1050,
        <= 9 => 950,
        _ => 1100
    };

    /// <summary>
    /// Supply factor: 1400 when the shelf is empty, 1000 at baseline stock, 700 when it holds
    /// twice the baseline. Linear in between, so draining a stall visibly costs more.
    /// </summary>
    internal static int ScarcityPermille(int stock, int baseline)
    {
        if (baseline <= 0)
        {
            return 1000;
        }

        if (stock >= baseline)
        {
            var excess = Math.Min(stock - baseline, baseline);
            return 1000 - (int)((long)(1000 - ScarcityFloorPermille) * excess / baseline);
        }

        var missing = baseline - stock;
        return 1000 + (int)((long)(ScarcityCeilingPermille - 1000) * missing / baseline);
    }

    /// <summary>Buy price: base value scaled by region, season and supply, rounded half up.</summary>
    internal static long Quote(ItemDef item, CitadelDef citadel, int month, int stock, int baseline)
    {
        var permille = (long)citadel.PriceFactorPermille
                       * SeasonPermille(month)
                       * ScarcityPermille(stock, baseline);

        return ((item.BasePrice * permille) + (PermilleScale / 2)) / PermilleScale;
    }

    /// <summary>Sell price: the stall buys back at a fixed fraction of its own ask.</summary>
    internal static long SellQuote(long buyPrice) => buyPrice * SellRatePercent / 100;

    internal static long Total(long unitPrice, int quantity) => unitPrice * quantity;

    /// <summary>How far a stall will stock up before it refuses to buy more of an item.</summary>
    internal static int BuyBackCapacity(int baseline) => baseline * 2;
}
