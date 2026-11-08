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

    public string GiftCartel(string cartelCode, IWallet wallet)
    {
        const long giftCost = 300;
        var cartel = GetCartels().FirstOrDefault(c => c.CartelCode.Equals(cartelCode, StringComparison.OrdinalIgnoreCase));
        if (cartel is null) return $"Không tìm thấy đại thương bang '{cartelCode}'.";
        if (wallet.Liquid < giftCost) return $"Cần {giftCost} Quan để dâng lễ vật kết giao {cartel.CartelNameVi}, trong ví chỉ có {wallet.Liquid} Quan.";

        wallet.Charge(giftCost, "caravan.cartel_gift", $"Lễ vật kết giao {cartel.CartelNameVi} ({cartel.LeaderVi})");
        AdjustCartelStanding(cartel.CartelCode, 15);
        var newStanding = Math.Min(100, cartel.Standing + 15);
        if (newStanding >= 20 && cartel.TreatyStatus == "NEUTRAL")
        {
            _db.Execute("UPDATE cv_cartel_reputation SET treaty_status = 'FRIENDLY' WHERE cartel_code = @code", DbArgs.One("@code", cartel.CartelCode));
        }
        return $"Đã dâng lễ vật 300 Quan kết giao {cartel.CartelNameVi} ({cartel.LeaderVi}): Hào cảm tăng lên {newStanding}/100!";
    }

    public string SignCartelTreaty(string cartelCode, IWallet wallet)
    {
        const long treatyFee = 500;
        var cartel = GetCartels().FirstOrDefault(c => c.CartelCode.Equals(cartelCode, StringComparison.OrdinalIgnoreCase));
        if (cartel is null) return $"Không tìm thấy đại thương bang '{cartelCode}'.";
        if (cartel.TreatyStatus == "ALLIED") return $"Bạn đã ký Minh Ước Thông Thương Độc Quyền với {cartel.CartelNameVi} rồi.";
        if (cartel.Standing < 35) return $"Cần đạt tối thiểu 35 điểm hào cảm với {cartel.CartelNameVi} (hiện có {cartel.Standing}/100) mới được ký Minh Ước.";
        if (wallet.Liquid < treatyFee) return $"Cần {treatyFee} Quan tiền cọc thương hội để ký Minh Ước với {cartel.CartelNameVi}.";

        wallet.Charge(treatyFee, "caravan.cartel_treaty", $"Ký Minh Ước Thông Thương cùng {cartel.CartelNameVi}");
        _db.Execute("UPDATE cv_cartel_reputation SET treaty_status = 'ALLIED', standing = MIN(100, standing + 10) WHERE cartel_code = @code", DbArgs.One("@code", cartel.CartelCode));
        return $"Đã ký thành công Minh Ước Thông Thương Độc Quyền với {cartel.CartelNameVi}! Hưởng đặc quyền miễn thuế bến bãi & tăng +20% lợi nhuận tuyến buôn tự động.";
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
        IItemCatalog items,
        ICaravanGuild? caravan = null)
    {
        var routes = GetRoutes().Where(r => r.Active).ToList();
        if (routes.Count == 0) return;

        var cartels = GetCartels();
        var alliedCount = cartels.Count(c => c.TreatyStatus == "ALLIED");
        var friendlyCount = cartels.Count(c => c.TreatyStatus == "FRIENDLY");
        var cartelBonusPermille = 1000 + (alliedCount * 150) + (friendlyCount * 50);

        var maxFleetBatch = caravan is not null ? caravan.Fleet.Tier * 25 : 100;

        foreach (var route in routes)
        {
            // Runs every 4 in-game days per route
            if (today.DayIndex - route.LastRunDay < 4) continue;

            var buyQuote = trade.QuoteBuy(route.FromCityId, route.BuyItemId);
            var sellQuote = trade.QuoteSell(route.ToCityId, route.BuyItemId);

            if (buyQuote <= route.MaxBuyPrice && sellQuote >= route.SellMinPrice)
            {
                var effectiveBatch = Math.Min(route.BatchSize, maxFleetBatch);
                var totalCost = buyQuote * effectiveBatch;
                if (wallet.Liquid >= totalCost)
                {
                    var grossRevenue = sellQuote * effectiveBatch;
                    var rawProfit = grossRevenue - totalCost;
                    var netProfit = (rawProfit * cartelBonusPermille) / 1000;

                    if (netProfit > 0)
                    {
                        trade.AdjustStock(route.FromCityId, route.BuyItemId, -Math.Min(10, effectiveBatch / 2));
                        trade.AdjustStock(route.ToCityId, route.BuyItemId, Math.Min(10, effectiveBatch / 2));
                        wallet.Pay(netProfit, "logistics.auto_route", $"Lợi nhuận ròng tuyến buôn '{route.Name}': +{netProfit} Quan.");
                        _db.Execute("UPDATE cv_auto_route SET last_run_day = @day WHERE id = @id", new DbArgs().Add("@day", today.DayIndex).Add("@id", route.Id));
                        _log.Info($"Tuyến buôn '{route.Name}' hoàn tất chuyến hàng, thu về +{netProfit} Quan.");
                    }
                }
            }
        }
    }
}
