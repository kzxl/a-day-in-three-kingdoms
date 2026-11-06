using SimT.Contracts.Logging;
using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Economy.Caravan;

public sealed record AutoRouteDef(
    long Id,
    string Name,
    long FromCityId,
    long ToCityId,
    long BuyItemId,
    long MaxBuyPrice,
    long SellMinPrice,
    int BatchSize,
    bool Active,
    int LastRunDay);

public sealed record CartelReputationDef(
    string CartelCode,
    string CartelNameVi,
    string LeaderVi,
    string DomainVi,
    int Standing,
    string TreatyStatus);

/// <summary>
/// Automated Trade Logistics Fleet and Commercial Cartel Warfare Service.
/// Enables hands-free recurring trade corridors and historical merchant guild diplomacy.
/// </summary>
public sealed class AutomatedLogisticsService
{
    private readonly IDbSession _db;
    private readonly ILog _log;

    public AutomatedLogisticsService(IDbSession db, ILog log)
    {
        _db = db;
        _log = log;
        EnsureCartels();
    }

    private void EnsureCartels()
    {
        var cartels = new[]
        {
            ("MI_SHI", 20, "FRIENDLY"),
            ("TRAN_SHI", 10, "NEUTRAL"),
            ("CHU_SHI", 5, "NEUTRAL"),
            ("VE_SHI", 0, "NEUTRAL")
        };

        foreach (var (code, standing, treaty) in cartels)
        {
            _db.Execute(
                """
                INSERT OR IGNORE INTO cv_cartel_reputation(cartel_code, standing, treaty_status)
                VALUES(@code, @standing, @treaty)
                """,
                new DbArgs().Add("@code", code).Add("@standing", standing).Add("@treaty", treaty));
        }
    }

    public IReadOnlyList<CartelReputationDef> GetCartels()
    {
        var raw = _db.Query(
            "SELECT cartel_code, standing, treaty_status FROM cv_cartel_reputation",
            row => (Code: row.GetString(0), Standing: row.GetInt32(1), Treaty: row.GetString(2)));

        return raw.Select(r => r.Code switch
        {
            "MI_SHI" => new CartelReputationDef("MI_SHI", "Từ Châu Mi Thị", "Mi Chúc", "🌾 Độc quyền Lương thảo & Vựa lúa miền Đông", r.Standing, r.Treaty),
            "TRAN_SHI" => new CartelReputationDef("TRAN_SHI", "Ký Châu Trăn Thị", "Trăn thị tộc trưởng", "⛏️ Khoáng thạch, Bách luyện cương & Tơ lụa Hà Bắc", r.Standing, r.Treaty),
            "CHU_SHI" => new CartelReputationDef("CHU_SHI", "Đông Ngô Chu Thị", "Chu Du tộc nhân", "⛵ Hạm đội thuyền buôn & Đường thủy Trường Giang", r.Standing, r.Treaty),
            "VE_SHI" => new CartelReputationDef("VE_SHI", "Trần Lưu Vệ Thị", "Vệ Tư", "🧂 Diêm nghiệp, Quan hệ Triều đình & Quân khí Tào Tháo", r.Standing, r.Treaty),
            _ => new CartelReputationDef(r.Code, r.Code, "Thương nhân", "Thương hội", r.Standing, r.Treaty)
        }).ToList();
    }

    public void AdjustCartelStanding(string cartelCode, int delta)
    {
        _db.Execute(
            """
            UPDATE cv_cartel_reputation
            SET standing = MAX(-100, MIN(100, standing + @delta))
            WHERE cartel_code = @code
            """,
            new DbArgs().Add("@code", cartelCode).Add("@delta", delta));
    }

    public IReadOnlyList<AutoRouteDef> GetRoutes()
    {
        return _db.Query(
            """
            SELECT id, name, from_city_id, to_city_id, buy_item_id, max_buy_price, sell_min_price, batch_size, active, last_run_day
            FROM cv_auto_route
            ORDER BY id
            """,
            row => new AutoRouteDef(
                row.GetInt64(0),
                row.GetString(1),
                row.GetInt64(2),
                row.GetInt64(3),
                row.GetInt64(4),
                row.GetInt64(5),
                row.GetInt64(6),
                row.GetInt32(7),
                row.GetInt32(8) == 1,
                row.GetInt32(9)));
    }

    public void CreateRoute(string name, long fromCityId, long toCityId, long buyItemId, long maxBuyPrice, long sellMinPrice, int batchSize)
    {
        _db.Execute(
            """
            INSERT INTO cv_auto_route(name, from_city_id, to_city_id, buy_item_id, max_buy_price, sell_min_price, batch_size, active, last_run_day)
            VALUES(@name, @from, @to, @item, @maxBuy, @minSell, @batch, 1, 0)
            """,
            new DbArgs()
                .Add("@name", name)
                .Add("@from", fromCityId)
                .Add("@to", toCityId)
                .Add("@item", buyItemId)
                .Add("@maxBuy", maxBuyPrice)
                .Add("@minSell", sellMinPrice)
                .Add("@batch", Math.Max(1, batchSize)));

        _log.Info($"Đã khởi tạo tuyến buôn tự động: {name}");
    }

    public void ToggleRoute(long routeId, bool active)
    {
        _db.Execute(
            "UPDATE cv_auto_route SET active = @act WHERE id = @id",
            new DbArgs().Add("@act", active ? 1 : 0).Add("@id", routeId));
    }

    public void DeleteRoute(long routeId)
    {
        _db.Execute("DELETE FROM cv_auto_route WHERE id = @id", DbArgs.One("@id", routeId));
    }

    /// <summary>
    /// Executes automated logistics steps on DayAdvanced.
    /// Runs autonomous trades between registered hubs, generating passive commerce cashflow.
    /// </summary>
    public void StepAutomatedRoutes(
        GameDate today,
        IMarketTrade trade,
        IWallet wallet,
        IWorldMap map,
        IItemCatalog items)
    {
        var routes = GetRoutes().Where(r => r.Active).ToList();
        if (routes.Count == 0) return;

        foreach (var route in routes)
        {
            // Runs every 4 in-game days per route
            if (today.DayIndex - route.LastRunDay < 4) continue;

            var buyQuote = trade.QuoteBuy(route.FromCityId, route.BuyItemId);
            var sellQuote = trade.QuoteSell(route.ToCityId, route.BuyItemId);

            if (buyQuote <= route.MaxBuyPrice && sellQuote >= route.SellMinPrice)
            {
                var totalCost = buyQuote * route.BatchSize;
                if (wallet.Liquid >= totalCost)
                {
                    var grossRevenue = sellQuote * route.BatchSize;
                    var netProfit = grossRevenue - totalCost;

                    if (netProfit > 0)
                    {
                        // Deduct buy cost, receive sell revenue => net profit added directly to wallet
                        wallet.Pay(netProfit, "logistics.auto_route", $"Lợi nhuận ròng tuyến buôn '{route.Name}': +{netProfit} Quan.");
                        _db.Execute("UPDATE cv_auto_route SET last_run_day = @day WHERE id = @id", new DbArgs().Add("@day", today.DayIndex).Add("@id", route.Id));
                        _log.Info($"Tuyến buôn '{route.Name}' hoàn tất chuyến hàng, thu về +{netProfit} Quan.");
                    }
                }
            }
        }
    }
}
