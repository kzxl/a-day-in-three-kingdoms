namespace SimT.Contracts.Playing;

public sealed record CorneredMarketView(
    long Id,
    long CitadelId,
    string CitadelNameVi,
    long ItemId,
    string ItemNameVi,
    string Category,
    int CorneredDay,
    int HoardedQuantity,
    int Severity,
    string Status,
    int PriceSurgePercent,
    int UnrestPenalty,
    int DefensePenalty,
    string StrategicImpactVi);

public sealed record SmuggleRouteOpportunity(
    long OriginCitadelId,
    string OriginCitadelNameVi,
    long DestCitadelId,
    string DestCitadelNameVi,
    long ItemId,
    string ItemNameVi,
    long UnitBasePrice,
    long SmuggleBuyPrice,
    long SmuggleSellPrice,
    int ProfitMarginPercent,
    int InterceptionRiskPercent,
    string RiskLevelVi,
    string StrategicReasonVi);

public readonly record struct SmuggleResult(
    bool Ok,
    bool Intercepted,
    int Quantity,
    long TotalRevenue,
    long FineOrLoss,
    int MeritsEarned,
    string MessageVi)
{
    public static SmuggleResult Fail(string message) => new(false, false, 0, 0, 0, 0, message);
}

public sealed record SmuggleRunLogView(
    long Id,
    int DayIndex,
    long OriginCitadelId,
    string OriginCitadelNameVi,
    long DestCitadelId,
    string DestCitadelNameVi,
    long ItemId,
    string ItemNameVi,
    int Quantity,
    long Revenue,
    int MeritsEarned,
    bool IsIntercepted,
    string NarrativeVi);

/// <summary>
/// Capability surface for economic warfare: cornering markets to collapse enemy defenses,
/// and smuggling contraband through contested military frontlines.
/// Provided by <c>dlc.economy</c> as <c>economy.warfare</c>.
/// </summary>
public interface IEconomicWarfare
{
    IReadOnlyList<CorneredMarketView> ActiveCorneredMarkets { get; }

    IReadOnlyList<SmuggleRouteOpportunity> AvailableSmuggleOpportunities { get; }

    IReadOnlyList<SmuggleRunLogView> RecentSmuggleRuns(int limit = 6);

    bool IsMarketCornered(long citadelId, long itemId);

    int GetCorneringPriceFactorPercent(long citadelId, long itemId);

    TradeResult CornerMarket(long citadelId, long itemId, int quantity);

    SmuggleResult ExecuteSmuggleRun(long originCitadelId, long destCitadelId, long itemId, int quantity);

    void SimulateEconomicWarfarePulse(SimT.Contracts.Time.GameDate date);
}
