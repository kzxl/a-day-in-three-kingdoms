using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Ledger;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Economy;

internal sealed partial class MarketService
{
    // -------------------------------------------------------- player stall

    public PlayerStallInfo? StallInfo(long cityId)
    {
        return _db.Query(
            "SELECT citadel_id, rent_per_month, hired_clerks, is_open FROM mk_player_stall WHERE citadel_id = @city",
            row => new PlayerStallInfo(row.GetInt64(0), row.GetInt64(1), row.GetInt32(2), row.GetInt32(3) == 1),
            DbArgs.One("@city", cityId))
            .FirstOrDefault();
    }

    public IReadOnlyList<PlayerShelfItem> Shelves(long cityId)
    {
        return _db.Query(
            "SELECT item_id, quantity, selling_price FROM mk_player_stall_shelf WHERE citadel_id = @city ORDER BY item_id",
            row => new PlayerShelfItem(row.GetInt64(0), row.GetInt32(1), row.GetInt64(2)),
            DbArgs.One("@city", cityId));
    }

    public bool RentStall(long cityId, out string message)
    {
        var city = _map.Find(cityId);
        if (city is null)
        {
            message = "Không tìm thấy thành này.";
            return false;
        }

        var existing = StallInfo(cityId);
        if (existing is not null)
        {
            message = $"Ngươi đã có sạp tại {city.NameVi}.";
            return false;
        }

        if (_wallet.Liquid < DefaultStallRentAmount)
        {
            message = $"Không đủ {DefaultStallRentAmount} quan để thuê sạp.";
            return false;
        }

        _actionTime.Charge(ActionCodes.StallRent);
        _wallet.Charge(DefaultStallRentAmount, JournalCodes.StallRent, $"Thuê sạp tại {city.NameVi} tháng đầu");

        _db.Execute(
            """
            INSERT INTO mk_player_stall (citadel_id, rent_per_month, hired_clerks, is_open, created_day)
            VALUES (@city, @rent, 1, 1, @day)
            """,
            new DbArgs().Add("@city", cityId).Add("@rent", DefaultStallRentAmount).Add("@day", _clock.Today.DayIndex));

        message = $"Thuê sạp tại {city.NameVi} thành công! Tiền thuê {DefaultStallRentAmount} quan/tháng, 1 tiểu nhị trông coi.";
        return true;
    }

    public bool Stock(long cityId, long itemId, int quantity, out string message)
    {
        if (quantity <= 0)
        {
            message = "Số lượng phải lớn hơn 0.";
            return false;
        }

        var stall = StallInfo(cityId);
        if (stall is null)
        {
            message = "Ngươi chưa có sạp tại thành này.";
            return false;
        }

        var item = RequireItem(itemId);
        var held = _bag.Quantity(cityId, itemId);
        if (held < quantity)
        {
            message = $"Trong túi chỉ có {held} {item.NameVi}.";
            return false;
        }

        _bag.Remove(cityId, itemId, quantity);

        var marketPrice = QuoteBuy(cityId, itemId);
        var defaultPrice = Math.Max(1L, (long)Math.Ceiling(marketPrice * 1.2));

        _db.Execute(
            """
            INSERT INTO mk_player_stall_shelf (citadel_id, item_id, quantity, selling_price)
            VALUES (@city, @item, @qty, @price)
            ON CONFLICT (citadel_id, item_id) DO UPDATE SET quantity = quantity + @qty
            """,
            new DbArgs().Add("@city", cityId).Add("@item", itemId).Add("@qty", quantity).Add("@price", defaultPrice));

        message = $"Đã bày {quantity} {item.NameVi} lên quầy (giá bán {defaultPrice} quan).";
        return true;
    }

    public bool Unstock(long cityId, long itemId, int quantity, out string message)
    {
        if (quantity <= 0)
        {
            message = "Số lượng phải lớn hơn 0.";
            return false;
        }

        var shelf = _db.Query(
            "SELECT quantity, selling_price FROM mk_player_stall_shelf WHERE citadel_id = @city AND item_id = @item",
            row => (Quantity: row.GetInt32(0), Price: row.GetInt64(1)),
            new DbArgs().Add("@city", cityId).Add("@item", itemId))
            .FirstOrDefault();

        if (shelf.Quantity <= 0)
        {
            message = "Trên quầy không có món này.";
            return false;
        }

        var actual = Math.Min(quantity, shelf.Quantity);
        var item = RequireItem(itemId);

        if (actual >= shelf.Quantity)
        {
            _db.Execute(
                "DELETE FROM mk_player_stall_shelf WHERE citadel_id = @city AND item_id = @item",
                new DbArgs().Add("@city", cityId).Add("@item", itemId));
        }
        else
        {
            _db.Execute(
                "UPDATE mk_player_stall_shelf SET quantity = quantity - @actual WHERE citadel_id = @city AND item_id = @item",
                new DbArgs().Add("@city", cityId).Add("@item", itemId).Add("@actual", actual));
        }

        _bag.Add(cityId, itemId, actual);
        message = $"Đã thu hồi {actual} {item.NameVi} về túi.";
        return true;
    }

    public bool SetPrice(long cityId, long itemId, long newPrice, out string message)
    {
        if (newPrice <= 0)
        {
            message = "Giá bán phải lớn hơn 0.";
            return false;
        }

        var item = RequireItem(itemId);
        var affected = _db.Execute(
            "UPDATE mk_player_stall_shelf SET selling_price = @price WHERE citadel_id = @city AND item_id = @item",
            new DbArgs().Add("@city", cityId).Add("@item", itemId).Add("@price", newPrice));

        if (affected == 0)
        {
            message = $"Không có {item.NameVi} trên quầy.";
            return false;
        }

        message = $"Đã chỉnh giá {item.NameVi} thành {newPrice} quan.";
        return true;
    }

    public bool StockAll(long cityId, out string message)
    {
        var stall = StallInfo(cityId);
        if (stall is null)
        {
            message = "Ngươi chưa có sạp tại thành này.";
            return false;
        }

        var stockedCount = 0;
        foreach (var item in _items.All)
        {
            var held = _bag.Quantity(cityId, item.Id);
            if (held > 0 && Stock(cityId, item.Id, held, out _))
            {
                stockedCount++;
            }
        }

        message = stockedCount > 0
            ? $"Đã bày {stockedCount} mặt hàng từ túi lên quầy!"
            : "Túi đồ rỗng, không có hàng để bày.";
        return stockedCount > 0;
    }

    public void DailyRetailSales(GameDate date)
    {
        var stalls = _db.Query(
            "SELECT citadel_id, hired_clerks FROM mk_player_stall WHERE is_open = 1",
            row => (CityId: row.GetInt64(0), Clerks: row.GetInt32(1)));
        if (stalls.Count == 0)
        {
            return;
        }

        foreach (var (cityId, clerks) in stalls)
        {
            var city = _map.Find(cityId);
            var shelves = Shelves(cityId);
            var totalRevenue = 0L;
            var soldLines = new List<string>();
            var dailyStallCapacity = Math.Max(2, clerks * 4);

            foreach (var shelf in shelves)
            {
                if (shelf.Quantity <= 0 || dailyStallCapacity <= 0) continue;

                var item = _items.Find(shelf.ItemId);
                if (item is null) continue;

                var marketPrice = QuoteBuy(cityId, item.Id);
                var ratio = marketPrice > 0 ? (shelf.SellingPrice * 100 / marketPrice) : 100;

                int sold;
                if (ratio <= 100)
                {
                    sold = Math.Min(shelf.Quantity, Math.Min(2, dailyStallCapacity));
                }
                else if (ratio <= 120)
                {
                    sold = Math.Min(shelf.Quantity, Math.Min(1, dailyStallCapacity));
                }
                else if (ratio <= 140)
                {
                    sold = (date.DayIndex % 2 == 0) ? Math.Min(shelf.Quantity, Math.Min(1, dailyStallCapacity)) : 0;
                }
                else
                {
                    sold = (date.DayIndex % 4 == 0) ? Math.Min(shelf.Quantity, Math.Min(1, dailyStallCapacity)) : 0;
                }

                if (sold > 0)
                {
                    dailyStallCapacity -= sold;
                    if (sold >= shelf.Quantity)
                    {
                        _db.Execute(
                            "DELETE FROM mk_player_stall_shelf WHERE citadel_id = @city AND item_id = @item",
                            new DbArgs().Add("@city", cityId).Add("@item", item.Id));
                    }
                    else
                    {
                        _db.Execute(
                            "UPDATE mk_player_stall_shelf SET quantity = quantity - @sold WHERE citadel_id = @city AND item_id = @item",
                            new DbArgs().Add("@city", cityId).Add("@item", item.Id).Add("@sold", sold));
                    }

                    var revenue = sold * shelf.SellingPrice;
                    totalRevenue += revenue;

                    var prefRows = _db.Query(
                        "SELECT npc_id, bid_markup_permille FROM mk_npc_preference WHERE category = @cat",
                        row => (NpcId: row.GetInt64(0), Markup: row.GetInt32(1)),
                        DbArgs.One("@cat", item.Category));

                    var localNpcs = _npcs?.FindByCitadel(cityId);
                    var matchedNpc = localNpcs?.FirstOrDefault(n => prefRows.Any(p => p.NpcId == n.Id))
                                  ?? (localNpcs != null && localNpcs.Count > 0 ? localNpcs[(int)(date.DayIndex % (uint)localNpcs.Count)] : null);

                    var buyerSuffix = matchedNpc != null ? $" (khách mua: {matchedNpc.NameVi})" : string.Empty;
                    soldLines.Add($"{sold} {item.NameVi}{buyerSuffix}");
                }
            }

            if (totalRevenue > 0)
            {
                _wallet.Pay(totalRevenue, JournalCodes.StallSale, $"Doanh thu tiệm buôn tại {city?.NameVi ?? cityId.ToString()} ({string.Join(", ", soldLines)})");
                _log.Info($"stall in {cityId} sold {string.Join(", ", soldLines)}: +{totalRevenue} quan on {date}");
            }
        }
    }

    public void ChargeMonthlyStallRent(GameDate date)
    {
        var stalls = _db.Query(
            "SELECT citadel_id, rent_per_month FROM mk_player_stall",
            row => (CityId: row.GetInt64(0), Rent: row.GetInt64(1)));

        foreach (var stall in stalls)
        {
            var city = _map.Find(stall.CityId);
            var cityName = city?.NameVi ?? stall.CityId.ToString();
            var key = $"mkt.stall_lease:{stall.CityId}:{date.Year}:{date.Month}";

            try
            {
                _wallet.Charge(
                    stall.Rent,
                    JournalCodes.StallRent,
                    $"Thuê sạp {cityName} tháng {date.Month}/{date.Year}",
                    idempotencyKey: key);

                _log.Info($"charged monthly stall rent {stall.Rent} in {cityName} on {date}");
            }
            catch (SimException error) when (error.Code == "wallet.short")
            {
                _log.Info($"stall in {cityName} missed rent on {date}: short of cash");
            }
        }
    }

    // ------------------------------------------------------------- INpcMarket (Phase 2)

    public IReadOnlyList<MarketStallDef> StallsAt(long cityId)
    {
        return _db.Query(
            @"SELECT id, citadel_id, sector, name_vi, owner_kind, npc_id, reputation
              FROM mk_stall WHERE citadel_id = @city ORDER BY id",
            row => new MarketStallDef(
                row.GetInt64(0),
                row.GetInt64(1),
                row.GetString(2),
                row.GetString(3),
                row.GetString(4),
                row.IsDbNull(5) ? null : row.GetInt64(5),
                row.GetInt32(6)),
            DbArgs.One("@city", cityId));
    }

    public IReadOnlyList<NpcMarketOffer> OffersAt(long cityId)
    {
        return _db.Query(
            @"SELECT o.id, o.stall_id, o.npc_id, o.item_id, o.quantity, o.max_quantity, o.unit_price, o.discount_permille
              FROM mk_npc_offer o
              JOIN mk_stall s ON s.id = o.stall_id
              WHERE s.citadel_id = @city AND o.quantity > 0
              ORDER BY o.id",
            row =>
            {
                var npcId = row.GetInt64(2);
                var itemId = row.GetInt64(3);
                return new NpcMarketOffer(
                    row.GetInt64(0),
                    row.GetInt64(1),
                    npcId,
                    itemId,
                    AdjustNpcOfferStock(npcId, row.GetInt32(4)),
                    AdjustNpcOfferStock(npcId, row.GetInt32(5)),
                    AdjustNpcOfferBasePrice(cityId, npcId, itemId, row.GetInt64(6)),
                    row.GetInt32(7));
            },
            DbArgs.One("@city", cityId));
    }

    public IReadOnlyList<NpcMarketOffer> OffersByStall(long stallId)
    {
        return _db.Query(
            @"SELECT o.id, o.stall_id, o.npc_id, o.item_id, o.quantity, o.max_quantity, o.unit_price, o.discount_permille, s.citadel_id
              FROM mk_npc_offer o
              JOIN mk_stall s ON s.id = o.stall_id
              WHERE o.stall_id = @stall AND o.quantity > 0
              ORDER BY o.id",
            row =>
            {
                var npcId = row.GetInt64(2);
                var itemId = row.GetInt64(3);
                var cityId = row.GetInt64(8);
                return new NpcMarketOffer(
                    row.GetInt64(0),
                    row.GetInt64(1),
                    npcId,
                    itemId,
                    AdjustNpcOfferStock(npcId, row.GetInt32(4)),
                    AdjustNpcOfferStock(npcId, row.GetInt32(5)),
                    AdjustNpcOfferBasePrice(cityId, npcId, itemId, row.GetInt64(6)),
                    row.GetInt32(7));
            },
            DbArgs.One("@stall", stallId));
    }

    private sealed record NpcOfferDetailRow(
        long Id,
        long StallId,
        long NpcId,
        long ItemId,
        int Quantity,
        int MaxQuantity,
        long UnitPrice,
        int Discount,
        long CitadelId,
        string StallName);

    public TradeResult BuyFromNpcOffer(long cityId, long offerId, int quantity)
    {
        if (quantity <= 0)
        {
            return TradeResult.Fail("Số lượng mua phải lớn hơn 0.");
        }

        var offer = _db.QueryOne(
            @"SELECT o.id, o.stall_id, o.npc_id, o.item_id, o.quantity, o.max_quantity, o.unit_price, o.discount_permille, s.citadel_id, s.name_vi
              FROM mk_npc_offer o
              JOIN mk_stall s ON s.id = o.stall_id
              WHERE o.id = @id",
            row => new NpcOfferDetailRow(
                row.GetInt64(0),
                row.GetInt64(1),
                row.GetInt64(2),
                row.GetInt64(3),
                row.GetInt32(4),
                row.GetInt32(5),
                row.GetInt64(6),
                row.GetInt32(7),
                row.GetInt64(8),
                row.GetString(9)),
            DbArgs.One("@id", offerId));

        if (offer is null)
        {
            return TradeResult.Fail("Mặt hàng chào bán của NPC không tồn tại.");
        }

        if (offer.CitadelId != cityId)
        {
            return TradeResult.Fail("Sạp hàng này không ở thành trì hiện tại.");
        }

        var availableQty = AdjustNpcOfferStock(offer.NpcId, offer.Quantity);
        if (availableQty < quantity)
        {
            return TradeResult.Fail($"Sạp chỉ còn {availableQty} đơn vị hàng.");
        }

        var item = _items.Find(offer.ItemId);
        if (item is null)
        {
            return TradeResult.Fail($"Hàng hóa mã {offer.ItemId} không tồn tại trong danh mục.");
        }

        var adjustedUnitPrice = AdjustNpcOfferBasePrice(cityId, offer.NpcId, offer.ItemId, offer.UnitPrice);
        var effectiveUnit = adjustedUnitPrice * (1000 - offer.Discount) / 1000;
        if (effectiveUnit < 1) effectiveUnit = 1;
        var total = effectiveUnit * quantity;

        if (_wallet.Liquid < total)
        {
            return TradeResult.Fail($"Cần {total} Quan nhưng chỉ có {_wallet.Liquid} Quan tiền mặt.");
        }

        _wallet.Charge(total, "mk.npc_buy", $"Mua {quantity} {item.NameVi} từ {offer.StallName}");

        _db.Execute(
            "UPDATE mk_npc_offer SET quantity = quantity - @qty WHERE id = @id",
            new DbArgs().Add("@qty", quantity).Add("@id", offerId));

        _bag.Add(cityId, offer.ItemId, quantity);

        return new TradeResult(true, effectiveUnit, total, $"Đã mua {quantity} {item.NameVi} tại '{offer.StallName}' hết {total} Quan.");
    }
}
