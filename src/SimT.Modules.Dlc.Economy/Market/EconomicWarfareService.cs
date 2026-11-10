using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Events;
using SimT.Contracts.Logging;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Economy;

internal sealed class EconomicWarfareService : IEconomicWarfare
{
    private readonly IDbSession _db;
    private readonly IGameClock _clock;
    private readonly IWallet _wallet;
    private readonly IBag _bag;
    private readonly IItemCatalog _items;
    private readonly IWorldMap _map;
    private readonly ILog _log;
    private readonly IMarketTrade _trade;
    private readonly IEventPublisher? _events;
    private readonly SimT.Contracts.Modules.ICapabilityBroker? _capabilities;

    public EconomicWarfareService(
        IDbSession db,
        IGameClock clock,
        IWallet wallet,
        IBag bag,
        IItemCatalog items,
        IWorldMap map,
        ILog log,
        IMarketTrade trade,
        IEventPublisher? events = null,
        SimT.Contracts.Modules.ICapabilityBroker? capabilities = null)
    {
        _db = db;
        _clock = clock;
        _wallet = wallet;
        _bag = bag;
        _items = items;
        _map = map;
        _log = log;
        _trade = trade;
        _events = events;
        _capabilities = capabilities;
    }

    public IReadOnlyList<CorneredMarketView> ActiveCorneredMarkets =>
        _db.Query(
            """
            SELECT id, citadel_id, item_id, cornered_day, hoarded_quantity, severity, status
            FROM mk_cornered_market
            WHERE status = 'ACTIVE'
            ORDER BY id DESC
            """,
            r =>
            {
                var id = r.GetInt64(0);
                var citId = r.GetInt64(1);
                var itemId = r.GetInt64(2);
                var day = r.GetInt32(3);
                var qty = r.GetInt32(4);
                var sev = r.GetInt32(5);
                var status = r.GetString(6);

                var citName = _map.Find(citId)?.NameVi ?? $"Thành #{citId}";
                var item = _items.Find(itemId);
                var itemName = item?.NameVi ?? $"Hàng #{itemId}";
                var category = item?.Category ?? "Chung";

                var surge = sev switch { 1 => 60, 2 => 120, _ => 200 };
                var unrest = sev switch { 1 => 10, 2 => 20, _ => 35 };
                var def = sev switch { 1 => 5, 2 => 15, _ => 25 };
                var impact = $"Giá cả +{surge}%, Bất ổn +{unrest}%, Phòng thủ thành -{def} điểm";

                return new CorneredMarketView(
                    id, citId, citName, itemId, itemName, category, day, qty, sev, status, surge, unrest, def, impact);
            });

    public IReadOnlyList<SmuggleRouteOpportunity> AvailableSmuggleOpportunities
    {
        get
        {
            var opportunities = new List<SmuggleRouteOpportunity>();
            var war = _capabilities?.TryGet<IGeopoliticalWar>(CapabilityIds.GeopoliticalWar, out var gw) == true ? gw : null;
            var citadels = _map.Citadels;
            var currentDay = _clock.Today.DayIndex;

            // Contraband strategic items
            var targetItemCodes = new[] { "rice", "herb", "iron", "wine" };
            var targetItems = _items.All.Where(i => targetItemCodes.Contains(i.Code)).ToList();

            foreach (var dest in citadels)
            {
                var isBesieged = war?.IsCitadelUnderSiege(dest.Id) == true;
                var conflict = war?.GetConflictForCitadel(dest.Id);
                var isContested = conflict is not null && conflict.Intensity >= 2;

                if (!isBesieged && !isContested) continue;

                // Pick adjacent origins
                var adjacentRoutes = _map.Routes.Where(r => r.ToCitadelId == dest.Id || r.FromCitadelId == dest.Id).ToList();
                foreach (var route in adjacentRoutes)
                {
                    var originId = route.FromCitadelId == dest.Id ? route.ToCitadelId : route.FromCitadelId;
                    var origin = _map.Find(originId);
                    if (origin is null) continue;

                    foreach (var item in targetItems)
                    {
                        var basePrice = item.BasePrice;
                        var buyPrice = _trade.QuoteBuy(originId, item.Id);
                        var smuggleSellPrice = (long)(basePrice * 3.5);
                        if (smuggleSellPrice <= buyPrice) continue;

                        var profit = smuggleSellPrice - buyPrice;
                        var margin = buyPrice > 0 ? (int)(profit * 100L / buyPrice) : 250;

                        var risk = 45;
                        if (_capabilities?.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) == true && attrs is not null)
                        {
                            risk -= (attrs.Attributes.Log / 5) + (attrs.Attributes.War / 10);
                        }
                        risk = Math.Clamp(risk, 15, 75);

                        var riskLevel = risk <= 25 ? "THẤP" : risk <= 45 ? "TRUNG BÌNH" : risk <= 65 ? "NGUY HIỂM" : "TỬ ĐỊA";
                        var reason = isBesieged
                            ? $"Thành {dest.NameVi} đang bị vây hãm nghiêm trọng! Cạn kiệt {item.NameVi}, thương lái liều mình tiếp tế được trả giá cắt cổ."
                            : $"Tiền tuyến {dest.NameVi} giao tranh khốc liệt! Quân nhu thiết yếu đang được thu mua bằng giá vàng.";

                        opportunities.Add(new SmuggleRouteOpportunity(
                            originId,
                            origin.NameVi,
                            dest.Id,
                            dest.NameVi,
                            item.Id,
                            item.NameVi,
                            basePrice,
                            buyPrice,
                            smuggleSellPrice,
                            margin,
                            risk,
                            riskLevel,
                            reason));
                    }
                }
            }

            return opportunities;
        }
    }

    public IReadOnlyList<SmuggleRunLogView> RecentSmuggleRuns(int limit = 6) =>
        _db.Query(
            """
            SELECT id, day_index, origin_citadel_id, dest_citadel_id, item_id, quantity, revenue, merits_earned, is_intercepted, narrative_vi
            FROM mk_smuggle_run_log
            ORDER BY id DESC LIMIT @lim
            """,
            r =>
            {
                var id = r.GetInt64(0);
                var day = r.GetInt32(1);
                var origId = r.GetInt64(2);
                var destId = r.GetInt64(3);
                var itemId = r.GetInt64(4);
                var qty = r.GetInt32(5);
                var rev = (long)r.GetInt32(6);
                var merits = r.GetInt32(7);
                var intercepted = r.GetInt32(8) == 1;
                var narrative = r.GetString(9);

                var origName = _map.Find(origId)?.NameVi ?? $"Thành #{origId}";
                var destName = _map.Find(destId)?.NameVi ?? $"Thành #{destId}";
                var itemName = _items.Find(itemId)?.NameVi ?? $"Hàng #{itemId}";

                return new SmuggleRunLogView(
                    id, day, origId, origName, destId, destName, itemId, itemName, qty, rev, merits, intercepted, narrative);
            },
            DbArgs.One("@lim", limit));

    public bool IsMarketCornered(long citadelId, long itemId) =>
        _db.ScalarLong(
            "SELECT COUNT(*) FROM mk_cornered_market WHERE citadel_id = @c AND item_id = @i AND status = 'ACTIVE'",
            DbArgs.One("@c", citadelId).Add("@i", itemId)) > 0;

    public int GetCorneringPriceFactorPercent(long citadelId, long itemId)
    {
        var sev = _db.ScalarLong(
            "SELECT COALESCE(MAX(severity), 0) FROM mk_cornered_market WHERE citadel_id = @c AND item_id = @i AND status = 'ACTIVE'",
            DbArgs.One("@c", citadelId).Add("@i", itemId));

        return sev switch
        {
            1 => 60,
            2 => 120,
            >= 3 => 200,
            _ => 0
        };
    }

    public TradeResult CornerMarket(long citadelId, long itemId, int quantity)
    {
        if (quantity <= 0) return TradeResult.Fail("Số lượng gom hàng phải lớn hơn 0.");

        var item = _items.Find(itemId);
        var citadel = _map.Find(citadelId);
        if (item is null || citadel is null) return TradeResult.Fail("Không tìm thấy thông tin mặt hàng hoặc thành trấn.");

        var stock = _trade.StallStock(citadelId, itemId);
        var baseline = _trade.StallBaseline(citadelId, itemId);

        if (quantity > stock)
        {
            return TradeResult.Fail($"Sạp không đủ hàng (Hiện có {stock}, cần gom {quantity}).");
        }

        if (quantity < Math.Min(25, baseline / 2))
        {
            return TradeResult.Fail($"Số lượng {quantity} quá ít, không đủ gây đột biến khan hiếm cục bộ (Cần ít nhất {Math.Min(25, baseline / 2)} kiện).");
        }

        var unitPrice = _trade.QuoteBuy(citadelId, itemId);
        var total = unitPrice * quantity;

        if (_wallet.Liquid < total)
        {
            return TradeResult.Fail($"Ngân quỹ không đủ: Cần {total:N0} Quan, hiện có {_wallet.Liquid:N0} Quan.");
        }

        _wallet.Charge(total, "MARKET_CORNERING", $"Gom vét độc quyền {quantity} kiện {item.NameVi} tại {citadel.NameVi}");
        _trade.AdjustStock(citadelId, itemId, -quantity);
        _bag.Add(citadelId, itemId, quantity);

        int severity = quantity >= baseline ? 3 : quantity >= (baseline * 3 / 4) ? 2 : 1;
        var day = _clock.Today.DayIndex;

        _db.Execute(
            """
            INSERT INTO mk_cornered_market (citadel_id, item_id, cornered_day, hoarded_quantity, severity, status)
            VALUES (@c, @i, @day, @qty, @sev, 'ACTIVE')
            """,
            DbArgs.One("@c", citadelId)
                .Add("@i", itemId)
                .Add("@day", day)
                .Add("@qty", quantity)
                .Add("@sev", severity));

        var unrestPen = severity switch { 1 => 10, 2 => 20, _ => 35 };
        var defPen = severity switch { 1 => 5, 2 => 15, _ => 25 };

        _events?.Publish(new MarketCorneredEvent(citadelId, itemId, severity, _clock.Today));

        var surge = severity switch { 1 => 60, 2 => 120, _ => 200 };
        _log.Info($"[EconomicWarfare] Market Cornered in {citadel.NameVi} for {item.NameVi}: +{surge}% price surge, +{unrestPen}% unrest");

        return new TradeResult(
            true,
            unitPrice,
            total,
            $"Gom vét thành công {quantity} kiện {item.NameVi} tại {citadel.NameVi}! Thị trường sụp đổ vào thế lũng đoạn: Giá mua tăng +{surge}%, Lòng dân bất ổn +{unrestPen}%, Phòng thủ thành suy yếu -{defPen} điểm.");
    }

    public SmuggleResult ExecuteSmuggleRun(long originCitadelId, long destCitadelId, long itemId, int quantity)
    {
        if (quantity <= 0) return SmuggleResult.Fail("Số lượng áp tải buôn lậu phải lớn hơn 0.");

        var item = _items.Find(itemId);
        var originCitadel = _map.Find(originCitadelId);
        var destCitadel = _map.Find(destCitadelId);

        if (item is null || originCitadel is null || destCitadel is null)
        {
            return SmuggleResult.Fail("Thông tin địa điểm hoặc mặt hàng không hợp lệ.");
        }

        var inBag = _bag.Quantity(originCitadelId, itemId);
        if (inBag < quantity)
        {
            return SmuggleResult.Fail($"Trong tay nải tại {originCitadel.NameVi} chỉ có {inBag} kiện {item.NameVi}, không đủ {quantity} kiện để áp tải.");
        }

        var basePrice = item.BasePrice;
        var smugglePrice = (long)(basePrice * 3.5);
        var totalRevenue = smugglePrice * quantity;

        int risk = 45;
        if (quantity > 50) risk += 15;
        if (_capabilities?.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) == true && attrs is not null)
        {
            risk -= (attrs.Attributes.Log / 5) + (attrs.Attributes.War / 10);
        }
        risk = Math.Clamp(risk, 15, 75);

        var currentDay = _clock.Today.DayIndex;
        bool intercepted = (currentDay + originCitadelId * 13 + destCitadelId * 17) % 100 < risk;

        if (intercepted)
        {
            _bag.Remove(originCitadelId, itemId, quantity);
            var fine = Math.Min(_wallet.Balance, (long)(quantity * basePrice * 0.5));
            if (fine > 0)
            {
                _wallet.Charge(fine, "SMUGGLE_FINE", $"Bị tuần binh biên ải bắt giữ hàng lậu và phạt vạ");
            }

            var narrative = $"Đoàn xe chở lậu {quantity} kiện {item.NameVi} từ {originCitadel.NameVi} tới {destCitadel.NameVi} bị tuần binh phục kích bắt trọn! Toàn bộ hàng bị tịch thu, bị phạt vạ {fine:N0} Quan.";
            _db.Execute(
                """
                INSERT INTO mk_smuggle_run_log (day_index, origin_citadel_id, dest_citadel_id, item_id, quantity, revenue, merits_earned, is_intercepted, narrative_vi)
                VALUES (@day, @orig, @dest, @item, @qty, 0, 0, 1, @narrative)
                """,
                DbArgs.One("@day", currentDay)
                    .Add("@orig", originCitadelId)
                    .Add("@dest", destCitadelId)
                    .Add("@item", itemId)
                    .Add("@qty", quantity)
                    .Add("@narrative", narrative));

            return new SmuggleResult(false, true, quantity, 0, fine, 0, narrative);
        }
        else
        {
            _bag.Remove(originCitadelId, itemId, quantity);
            _wallet.Pay(totalRevenue, "SMUGGLE_PROFIT", $"Lợi nhuận áp tải lậu {quantity} kiện {item.NameVi} vào thành {destCitadel.NameVi}");

            var merits = Math.Max(5, quantity / 10);
            _events?.Publish(new ContrabandDeliveredEvent(destCitadelId, itemId, quantity, _clock.Today));

            var narrative = $"Áp giải trót lọt {quantity} kiện {item.NameVi} vượt tiền tuyến phong tỏa tới {destCitadel.NameVi}! Thu về {totalRevenue:N0} Quan (+{merits} Huân công cứu viện thành trì).";
            _db.Execute(
                """
                INSERT INTO mk_smuggle_run_log (day_index, origin_citadel_id, dest_citadel_id, item_id, quantity, revenue, merits_earned, is_intercepted, narrative_vi)
                VALUES (@day, @orig, @dest, @item, @qty, @rev, @merits, 0, @narrative)
                """,
                DbArgs.One("@day", currentDay)
                    .Add("@orig", originCitadelId)
                    .Add("@dest", destCitadelId)
                    .Add("@item", itemId)
                    .Add("@qty", quantity)
                    .Add("@rev", (int)Math.Min(int.MaxValue, totalRevenue))
                    .Add("@merits", merits)
                    .Add("@narrative", narrative));

            return new SmuggleResult(true, false, quantity, totalRevenue, 0, merits, narrative);
        }
    }

    public void SimulateEconomicWarfarePulse(GameDate date)
    {
        var currentDay = date.DayIndex;
        // Expire cornered markets older than 60 days
        _db.Execute(
            "UPDATE mk_cornered_market SET status = 'RESOLVED' WHERE status = 'ACTIVE' AND (@day - cornered_day) >= 60",
            DbArgs.One("@day", currentDay));
    }
}
