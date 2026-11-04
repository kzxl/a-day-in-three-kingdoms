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
