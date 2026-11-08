namespace SimT.Contracts.Playing;

/// <summary>Trade surface provided by <c>dlc.market</c> as the <c>market.trade</c> capability.</summary>
public interface IMarketTrade
{
    long QuoteBuy(long cityId, long itemId);

    long QuoteSell(long cityId, long itemId);

    TradeResult Buy(long cityId, long itemId, int quantity);

    TradeResult Sell(long cityId, long itemId, int quantity);

    int StallStock(long cityId, long itemId);

    /// <summary>Stock level the stall restocks toward; also the reference for the price curve.</summary>
    int StallBaseline(long cityId, long itemId);

    /// <summary>Daily buy prices, oldest first: the data behind a price trend.</summary>
    IReadOnlyList<long> PriceHistory(long cityId, long itemId, int days = 10);

    /// <summary>Adjusts the stall stock directly by delta (positive or negative), clamped at zero.</summary>
    void AdjustStock(long cityId, long itemId, int delta);
}

public readonly record struct TradeResult(bool Ok, long UnitPrice, long Total, string Message)
{
    public static TradeResult Fail(string message) => new(false, 0, 0, message);
}

/// <summary>Warehouse surface provided by <c>dlc.market</c> as the <c>market.warehouse</c> capability.</summary>
public interface IMarketWarehouse
{
    long DefaultCapacity { get; }

    long DefaultMonthlyRent { get; }

    WarehouseInfo? Info(long cityId);

    int StockAt(long cityId, long itemId);

    /// <summary>Every stored line with its age, so the screen can show what is about to spoil.</summary>
    IReadOnlyList<WarehouseStockView> Stock(long cityId);

    bool Store(long cityId, long itemId, int quantity, out string message);

    bool Take(long cityId, long itemId, int quantity, out string message);

    bool Rent(long cityId, out string message);
}

public sealed record WarehouseInfo(long CityId, int Capacity, long MonthlyRent, int Used);

/// <summary>What the player's goods are worth at sell prices — the number the old deck showed.</summary>
public interface IMarketPortfolio
{
    long BagValue(long cityId);

    long InventoryValue();
}

public sealed record WarehouseStockView(long ItemId, int Quantity, int AgeDays);

/// <summary>Player-owned retail stall surface provided by <c>dlc.market</c>.</summary>
public interface IPlayerStall
{
    long DefaultStallRent { get; }

    PlayerStallInfo? StallInfo(long cityId);

    IReadOnlyList<PlayerShelfItem> Shelves(long cityId);

    bool RentStall(long cityId, out string message);

    bool Stock(long cityId, long itemId, int quantity, out string message);

    bool Unstock(long cityId, long itemId, int quantity, out string message);

    bool SetPrice(long cityId, long itemId, long newPrice, out string message);

    bool StockAll(long cityId, out string message);
}

public sealed record PlayerStallInfo(long CityId, long MonthlyRent, int HiredClerks, bool IsOpen);

public sealed record PlayerShelfItem(long ItemId, int Quantity, long SellingPrice);

/// <summary>A specialized industry stall in a citadel (official or NPC owned).</summary>
public sealed record MarketStallDef(
    long Id,
    long CitadelId,
    string Sector,
    string NameVi,
    string OwnerKind,
    long? NpcId,
    int Reputation);

/// <summary>Surplus goods listed by an NPC for sale at a specialized stall.</summary>
public sealed record NpcMarketOffer(
    long Id,
    long StallId,
    long NpcId,
    long ItemId,
    int Quantity,
    int MaxQuantity,
    long UnitPrice,
    int DiscountPermille);

/// <summary>Trade preference of an NPC indicating willingness to buy specific goods.</summary>
public sealed record NpcTradePreference(
    long NpcId,
    string Category,
    int BidMarkupPermille);

/// <summary>NPC market and specialized stall surface provided by <c>dlc.market</c>.</summary>
public interface INpcMarket
{
    IReadOnlyList<MarketStallDef> StallsAt(long cityId);

    IReadOnlyList<NpcMarketOffer> OffersAt(long cityId);

    IReadOnlyList<NpcMarketOffer> OffersByStall(long stallId);

    TradeResult BuyFromNpcOffer(long cityId, long offerId, int quantity);

    long QuoteNpcOfferUnitPrice(long cityId, long npcId, long itemId, int discountPermille = 50);

    long QuoteNpcBuyBidUnitPrice(long cityId, long npcId, long itemId, int bidMarkupPermille = 100);

    int AdjustNpcOfferStock(long npcId, int baseQuantity);

    TradeResult SellToNpcStall(long cityId, long stallId, long itemId, int quantity);
}
/// <summary>Economic value tier classification representing the closed macro-economic supply chain.</summary>
public enum EconomicValueTier
{
    Tier1_Raw = 1,
    Tier2_Processed = 2,
    Tier3_Manufactured = 3,
    Tier4_Luxury_EndSink = 4
}

/// <summary>Detailed value chain profile of a commodity item in the Three Kingdoms economic ecosystem.</summary>
public sealed record ValueChainProfileDef(
    string ItemCode,
    EconomicValueTier Tier,
    string TierNameVi,
    string BadgeVi,
    string CategoryVi,
    string PrimaryConsumerVi,
    string EconomicRoleVi,
    IReadOnlyList<string> Inputs,
    IReadOnlyList<string> Outputs);

/// <summary>Real-time market demand velocity and stock depletion estimates for a citadel commodity.</summary>
public sealed record MarketDemandStatus(
    long CitadelId,
    long ItemId,
    string ItemCode,
    string ItemNameVi,
    int Stock,
    int Baseline,
    int MonthlyDemandEstimate,
    string VelocityLevelVi,
    int DaysUntilDepletion);

/// <summary>Historical resource node (mine, forest, herb valley, fishery, or weaving hub) attached to a citadel.</summary>
public sealed record RegionalResourceNodeDef(
    long CitadelId,
    string NodeCode,
    string NodeNameVi,
    string NodeType,
    long PrimaryItemId,
    string PrimaryItemCode,
    long? SecondaryItemId,
    string? SecondaryItemCode,
    int BaseMonthlyYield,
    string DescriptionVi);

/// <summary>Macro-economic value chain and civic consumption engine provided by <c>dlc.economy</c>.</summary>
public interface IValueChainService
{
    ValueChainProfileDef GetProfile(string itemCode);
    MarketDemandStatus GetDemandStatus(long citadelId, long itemId);
    MarketDemandStatus EstimateDemand(long citadelId, long itemId, string itemCode, string itemNameVi, string category, int stock, int baseline, int npcCount);
    IReadOnlyList<MarketDemandStatus> GetCitadelDemands(long citadelId);
    IReadOnlyList<RegionalResourceNodeDef> GetResourceNodes(long citadelId);
    IReadOnlyList<RegionalResourceNodeDef> AllResourceNodes { get; }
    int SimulateRegionalExtraction(long citadelId);
    void SimulateMonthlyCivicConsumption(SimT.Contracts.Time.GameDate date);
}
