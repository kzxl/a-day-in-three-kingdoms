using SimT.Contracts.Playing;

namespace SimT.Modules.Dlc.Market;

/// <summary>
/// Price model in integer arithmetic so a replay is bit-identical. No decimal, no double, no
/// allocation: this is the hot path the settlement loop and the balance simulator call.
/// </summary>
internal static class MarketPricing
{
    private const int BaselinePermille = 1000;
    private const long PermilleScale = 1_000_000L;
    private const int SellRatePercent = 60;

    /// <summary>Seasonal factor applied on top of the regional factor.</summary>
    internal static int SeasonPermille(int month) => month switch
    {
        <= 3 => 1000,
        <= 6 => 1050,
        <= 9 => 950,
        _ => 1100
    };

    /// <summary>Buy price: base price scaled by region and season, rounded half up.</summary>
    internal static long Quote(ItemDef item, CitadelDef citadel, int month)
    {
        var permille = (long)citadel.PriceFactorPermille * SeasonPermille(month);
        return ((item.BasePrice * permille) + (PermilleScale / 2)) / PermilleScale;
    }

    /// <summary>Sell price: the stall buys back at a fixed fraction of its own ask.</summary>
    internal static long SellQuote(long buyPrice) => buyPrice * SellRatePercent / 100;

    internal static long Total(long unitPrice, int quantity) => unitPrice * quantity;
}
