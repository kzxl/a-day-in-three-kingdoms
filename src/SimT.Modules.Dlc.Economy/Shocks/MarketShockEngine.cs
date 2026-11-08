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
public sealed partial class MarketShockEngine : IEventRumorSource
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

    private bool _hasRumorTable;

    /// <summary>
    /// Arbitrage Radar: Scans all connected citadels and identifies top trade routes by profit margin.
    /// </summary>
    public IReadOnlyList<ArbitrageOpportunity> ScanArbitrageCorridors(
        IWorldMap map,
        IItemCatalog items,
        IMarketTrade trade,
        int maxResults = 5,
        GameDate? today = null)
    {
        var citadels = map.Citadels;
        var goods = items.All;
        var cityCount = citadels.Count;
        var itemCount = goods.Count;
        var buyPrices = new long[cityCount, itemCount];
        var sellPrices = new long[cityCount, itemCount];

        if (trade is MarketService && today is { } currentDate)
        {
            PopulateBulkQuotes(citadels, goods, currentDate, buyPrices, sellPrices);
        }
        else
        {
            var isDefaultMarket = trade is MarketService;
            for (var c = 0; c < cityCount; c++)
            {
                var citadelId = citadels[c].Id;
                for (var i = 0; i < itemCount; i++)
                {
                    var itemId = goods[i].Id;
                    var buy = trade.QuoteBuy(citadelId, itemId);
                    buyPrices[c, i] = buy;
                    sellPrices[c, i] = isDefaultMarket
                        ? MarketPricing.SellQuote(buy)
                        : trade.QuoteSell(citadelId, itemId);
                }
            }
        }

        var list = new List<ArbitrageOpportunity>();

        for (var s = 0; s < cityCount; s++)
        {
            var source = citadels[s];
            for (var t = 0; t < cityCount; t++)
            {
                if (s == t) continue;
                var target = citadels[t];

                for (var i = 0; i < itemCount; i++)
                {
                    var buyAtSource = buyPrices[s, i];
                    var sellAtTarget = sellPrices[t, i];
                    var profit = sellAtTarget - buyAtSource;

                    if (buyAtSource > 0 && profit > 4)
                    {
                        var item = goods[i];
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

    private void PopulateBulkQuotes(
        IReadOnlyList<CitadelDef> citadels,
        IReadOnlyList<ItemDef> goods,
        GameDate today,
        long[,] buyPrices,
        long[,] sellPrices)
    {
        var listings = new Dictionary<(long CityId, long ItemId), (int Stock, int Baseline)>();
        var listingRows = _db.Query(
            "SELECT citadel_id, item_id, COALESCE(stock, 0), COALESCE(baseline_stock, 40) FROM mk_stall_listing",
            r => (City: r.GetInt64(0), Item: r.GetInt64(1), Stock: r.GetInt32(2), Baseline: r.GetInt32(3)));
        foreach (var row in listingRows)
            listings[(row.City, row.Item)] = (row.Stock, row.Baseline);

        Dictionary<(long CityId, long ItemId), int>? rumors = null;
        if (_hasRumorTable || _db.ScalarLong("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='cv_market_rumor'") > 0)
        {
            _hasRumorTable = true;
            var rumorRows = _db.Query(
                "SELECT citadel_id, item_id, price_mult_permille FROM cv_market_rumor WHERE is_active = 1 AND start_day <= @day AND end_day >= @day ORDER BY id ASC",
                r => (City: r.GetInt64(0), Item: r.GetInt64(1), Mult: r.GetInt32(2)),
                DbArgs.One("@day", today.DayIndex));
            if (rumorRows.Count > 0)
            {
                rumors = new Dictionary<(long CityId, long ItemId), int>();
                foreach (var r in rumorRows)
                {
                    if (r.Mult > 0)
                        rumors[(r.City, r.Item)] = r.Mult;
                }
            }
        }

        var month = today.Month;
        for (var c = 0; c < citadels.Count; c++)
        {
            var citadel = citadels[c];
            for (var i = 0; i < goods.Count; i++)
            {
                var item = goods[i];
                var (stock, baseline) = listings.TryGetValue((citadel.Id, item.Id), out var sb) ? sb : (0, 40);
                var baseQuote = MarketPricing.Quote(item, citadel, month, stock, baseline);
                var factor = rumors is not null && rumors.TryGetValue((citadel.Id, item.Id), out var f) ? f : 1000;
                var buy = factor == 1000 ? baseQuote : Math.Max(1, baseQuote * factor / 1000);
                buyPrices[c, i] = buy;
                sellPrices[c, i] = MarketPricing.SellQuote(buy);
            }
        }
    }
}
