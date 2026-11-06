using SimT.Contracts.Logging;
using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Modules.Dlc.Economy.ValueChain;

namespace SimT.Modules.Dlc.Economy.Shocks;

public static class MarketShockTypes
{
    public const string Normal = "NORMAL";
    public const string Siege = "SIEGE";
    public const string Drought = "DROUGHT";
    public const string Epidemic = "EPIDEMIC";
    public const string Prosperity = "PROSPERITY";
}

public sealed record CityShockDef(
    long Id,
    long CitadelId,
    string ShockType,
    int StartDay,
    int EndDay,
    int SeverityPermille,
    string NoteVi);

public sealed record ArbitrageOpportunity(
    string ItemCode,
    string ItemNameVi,
    long SourceCitadelId,
    string SourceCitadelName,
    long TargetCitadelId,
    string TargetCitadelName,
    long BuyPriceAtSource,
    long SellPriceAtTarget,
    long ProfitPerUnit,
    int ProfitMarginPercent,
    string StrategicAdvice);

/// <summary>
/// Geopolitical Shocks & Arbitrage Intelligence Engine.
/// Simulates siege shortages, drought famines, epidemics, and computes optimal trade corridors.
/// </summary>
public sealed class MarketShockEngine
{
    private readonly IDbSession _db;

    public MarketShockEngine(IDbSession db)
    {
        _db = db;
    }

    public CityShockDef? GetActiveShock(long citadelId, int currentDayIndex)
    {
        return _db.Query(
            """
            SELECT id, citadel_id, shock_type, start_day, end_day, severity_permille, note_vi
            FROM mk_city_shock
            WHERE citadel_id = @city AND @day BETWEEN start_day AND end_day
            ORDER BY id DESC LIMIT 1
            """,
            row => new CityShockDef(
                row.GetInt64(0),
                row.GetInt64(1),
                row.GetString(2),
                row.GetInt32(3),
                row.GetInt32(4),
                row.GetInt32(5),
                row.GetString(6)),
            new DbArgs().Add("@city", citadelId).Add("@day", currentDayIndex)).FirstOrDefault();
    }

    public void TriggerShock(long citadelId, string shockType, int startDay, int durationDays, int severityPermille, string noteVi)
    {
        _db.Execute(
            """
            INSERT INTO mk_city_shock(citadel_id, shock_type, start_day, end_day, severity_permille, note_vi)
            VALUES(@city, @type, @start, @end, @sev, @note)
            """,
            new DbArgs()
                .Add("@city", citadelId)
                .Add("@type", shockType)
                .Add("@start", startDay)
                .Add("@end", startDay + durationDays)
                .Add("@sev", severityPermille)
                .Add("@note", noteVi));
    }

    /// <summary>
    /// Computes shock multiplier in permille (1000 = normal baseline).
    /// </summary>
    public int GetShockMultiplierPermille(long citadelId, string itemCode, int currentDayIndex)
    {
        var activeShock = GetActiveShock(citadelId, currentDayIndex);
        if (activeShock is null)
        {
            return 1000;
        }

        var tier = ItemValueChain.GetTier(itemCode);
        return activeShock.ShockType switch
        {
            MarketShockTypes.Siege => itemCode switch
            {
                "gao" or "lua_mi" or "ca_kho" => Math.Max(2500, activeShock.SeverityPermille * 2), // Lương thực tăng vọt
                "sat" or "cuoc_nong" or "liem_gat" => 2000,                                         // Khí cụ, sắt tăng
                _ when tier == EconomicValueTier.Tier4_Luxury_EndSink => 450,                      // Xa xỉ phẩm giảm mạnh
                _ => 1200
            },
            MarketShockTypes.Drought => itemCode switch
            {
                "gao" or "lua_mi" or "hat_giong_lua" => 2000,                                      // Hạn hán thiếu thóc
                "phan_bon" or "binh_tuoi" => 1800,
                _ => 1000
            },
            MarketShockTypes.Epidemic => itemCode switch
            {
                "thuoc" or "sam" or "hat_giong_duoc_thao" => 3000,                                 // Dược liệu tăng gấp 3
                _ => 1000
            },
            MarketShockTypes.Prosperity => itemCode switch
            {
                "gao" or "lua_mi" => 800,                                                          // Được mùa thóc rẻ
                _ when tier == EconomicValueTier.Tier4_Luxury_EndSink => 1400,                      // Dân giàu chuộng xa xỉ
                _ => 1050
            },
            _ => 1000
        };
    }

    /// <summary>
    /// Arbitrage Radar: Scans all connected citadels and identifies top trade routes by profit margin.
    /// </summary>
    public IReadOnlyList<ArbitrageOpportunity> ScanArbitrageCorridors(
        IWorldMap map,
        IItemCatalog items,
        IMarketTrade trade,
        int maxResults = 5)
    {
        var citadels = map.Citadels;
        var goods = items.All;
        var list = new List<ArbitrageOpportunity>();

        foreach (var source in citadels)
        {
            foreach (var target in citadels)
            {
                if (source.Id == target.Id) continue;

                foreach (var item in goods)
                {
                    var buyAtSource = trade.QuoteBuy(source.Id, item.Id);
                    var sellAtTarget = trade.QuoteSell(target.Id, item.Id);
                    var profit = sellAtTarget - buyAtSource;

                    if (buyAtSource > 0 && profit > 4)
                    {
                        var margin = (int)((profit * 100) / buyAtSource);
                        var advice = margin >= 50
                            ? "🔥 Cơ hội ngàn vàng: Biên lợi nhuận béo bở!"
                            : "📦 Tuyến buôn ổn định, lợi nhuận khá.";

                        list.Add(new ArbitrageOpportunity(
                            item.Code,
                            item.NameVi,
                            source.Id,
                            source.NameVi,
                            target.Id,
                            target.NameVi,
                            buyAtSource,
                            sellAtTarget,
                            profit,
                            margin,
                            advice));
                    }
                }
            }
        }

        return list.OrderByDescending(x => x.ProfitMarginPercent)
                   .ThenByDescending(x => x.ProfitPerUnit)
                   .Take(maxResults)
                   .ToList();
    }
}
