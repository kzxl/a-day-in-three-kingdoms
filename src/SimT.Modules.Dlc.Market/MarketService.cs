using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Ledger;
using SimT.Contracts.Logging;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Market;

/// <summary>
/// Trade + warehouse rules for <c>dlc.market</c>. Everything it touches is reached through a
/// capability, never through another module's assembly.
/// </summary>
internal sealed class MarketService : IMarketTrade, IMarketWarehouse
{
    public const int DefaultCapacity = 200;
    public const long DefaultMonthlyRent = 150;

    private readonly IDbSession _db;
    private readonly IGameClock _clock;
    private readonly IActionTime _actionTime;
    private readonly IWallet _wallet;
    private readonly IBag _bag;
    private readonly IItemCatalog _items;
    private readonly IWorldMap _map;
    private readonly ILog _log;

    public MarketService(
        IDbSession db,
        IGameClock clock,
        IActionTime actionTime,
        IWallet wallet,
        IBag bag,
        IItemCatalog items,
        IWorldMap map,
        ILog log)
    {
        _db = db;
        _clock = clock;
        _actionTime = actionTime;
        _wallet = wallet;
        _bag = bag;
        _items = items;
        _map = map;
        _log = log;
    }

    long IMarketWarehouse.DefaultCapacity => DefaultCapacity;

    long IMarketWarehouse.DefaultMonthlyRent => DefaultMonthlyRent;

    public int StallStock(long cityId, long itemId) =>
        (int)_db.ScalarLong(
            "SELECT COALESCE(stock, 0) FROM mk_stall_listing WHERE citadel_id=@city AND item_id=@item",
            new DbArgs().Add("@city", cityId).Add("@item", itemId));

    public int StallBaseline(long cityId, long itemId) =>
        (int)_db.ScalarLong(
            "SELECT COALESCE((SELECT baseline_stock FROM mk_stall_listing WHERE citadel_id=@city AND item_id=@item), 40)",
            new DbArgs().Add("@city", cityId).Add("@item", itemId));

    public IReadOnlyList<long> PriceHistory(long cityId, long itemId, int days = 10) =>
        _db.Query(
            """
            SELECT buy_price FROM mk_price_sample
            WHERE citadel_id=@city AND item_id=@item
            ORDER BY day_index DESC
            LIMIT @days
            """,
            row => row.GetInt64(0),
            new DbArgs().Add("@city", cityId).Add("@item", itemId).Add("@days", Math.Clamp(days, 1, 60)))
        .AsEnumerable()
        .Reverse()
        .ToList();

    /// <summary>
    /// One row per citadel and item per day. Prices are a function of stock, so the sample is
    /// also the record of how scarce the market was.
    /// </summary>
    public void RecordDailyPrices(GameDate date)
    {
        var rows = _db.Query(
            "SELECT citadel_id, item_id, stock, baseline_stock FROM mk_stall_listing",
            row => (City: row.GetInt64(0), Item: row.GetInt64(1), Stock: row.GetInt32(2), Baseline: row.GetInt32(3)));

        _db.Transaction(() =>
        {
            foreach (var row in rows)
            {
                var item = _items.Find(row.Item);
                var city = _map.Find(row.City);
                if (item is null || city is null)
                {
                    continue;
                }

                var price = MarketPricing.Quote(item, city, date.Month, row.Stock, row.Baseline);
                _db.Execute(
                    """
                    INSERT OR IGNORE INTO mk_price_sample(citadel_id, item_id, day_index, buy_price)
                    VALUES(@city, @item, @day, @price)
                    """,
                    new DbArgs()
                        .Add("@city", row.City)
                        .Add("@item", row.Item)
                        .Add("@day", date.DayIndex)
                        .Add("@price", price));
            }

            // Sixty days is the whole window the screen ever reads.
            _db.Execute(
                "DELETE FROM mk_price_sample WHERE day_index < @cutoff",
                DbArgs.One("@cutoff", date.DayIndex - 60));
        });
    }

    public long QuoteBuy(long cityId, long itemId)
    {
        var item = RequireItem(itemId);
        var citadel = RequireCitadel(cityId);
        return MarketPricing.Quote(
            item,
            citadel,
            _clock.Today.Month,
            StallStock(cityId, itemId),
            StallBaseline(cityId, itemId));
    }

    public long QuoteSell(long cityId, long itemId) => MarketPricing.SellQuote(QuoteBuy(cityId, itemId));

    public TradeResult Buy(long cityId, long itemId, int quantity)
    {
        if (quantity <= 0)
        {
            return TradeResult.Fail("Số lượng phải lớn hơn 0.");
        }

        var item = _items.Find(itemId);
        var citadel = _map.Find(cityId);
        if (item is null || citadel is null)
        {
            return TradeResult.Fail("Mặt hàng hoặc thành trấn không tồn tại.");
        }

        var stock = StallStock(cityId, itemId);
        if (stock < quantity)
        {
            return TradeResult.Fail("Sạp không đủ hàng.");
        }

        var unit = MarketPricing.Quote(item, citadel, _clock.Today.Month, stock, StallBaseline(cityId, itemId));
        var total = MarketPricing.Total(unit, quantity);
        if (_wallet.Liquid < total)
        {
            return TradeResult.Fail($"Không đủ tiền mặt: cần {total}, có {_wallet.Liquid}.");
        }

        _db.Transaction(() =>
        {
            _actionTime.Charge(ActionCodes.TradeBuy);
            _wallet.Charge(total, JournalCodes.MarketBuy, $"Mua {quantity} {item.NameVi}");
            _bag.Add(cityId, itemId, quantity);
            _db.Execute(
                "UPDATE mk_stall_listing SET stock = stock - @quantity WHERE citadel_id=@city AND item_id=@item",
                new DbArgs().Add("@city", cityId).Add("@item", itemId).Add("@quantity", quantity));
        });

        return new TradeResult(true, unit, total, $"Mua {quantity} {item.NameVi} hết {total} Quan.");
    }

    public TradeResult Sell(long cityId, long itemId, int quantity)
    {
        if (quantity <= 0)
        {
            return TradeResult.Fail("Số lượng phải lớn hơn 0.");
        }

        var item = _items.Find(itemId);
        var citadel = _map.Find(cityId);
        if (item is null || citadel is null)
        {
            return TradeResult.Fail("Mặt hàng hoặc thành trấn không tồn tại.");
        }

        if (_bag.Quantity(cityId, itemId) < quantity)
        {
            return TradeResult.Fail("Trong túi không đủ hàng.");
        }

        var stock = StallStock(cityId, itemId);
        var baseline = StallBaseline(cityId, itemId);
        if (stock + quantity > MarketPricing.BuyBackCapacity(baseline))
        {
            return TradeResult.Fail(
                $"Sạp chỉ thu mua thêm {Math.Max(0, MarketPricing.BuyBackCapacity(baseline) - stock)} đơn vị nữa.");
        }

        var unit = MarketPricing.SellQuote(
            MarketPricing.Quote(item, citadel, _clock.Today.Month, stock, baseline));
        var total = MarketPricing.Total(unit, quantity);

        var moved = false;
        _db.Transaction(() =>
        {
            _actionTime.Charge(ActionCodes.TradeSell);
            moved = _bag.Remove(cityId, itemId, quantity);
            if (!moved)
            {
                throw new SimException("market.bag_race", "Bag changed while selling; the transaction was rolled back.");
            }

            _wallet.Pay(total, JournalCodes.MarketSell, $"Bán {quantity} {item.NameVi}");
            _db.Execute(
                """
                INSERT INTO mk_stall_listing(citadel_id, item_id, stock) VALUES(@city, @item, @quantity)
                ON CONFLICT(citadel_id, item_id) DO UPDATE SET stock = stock + excluded.stock
                """,
                new DbArgs().Add("@city", cityId).Add("@item", itemId).Add("@quantity", quantity));
        });

        if (!moved)
        {
            return TradeResult.Fail("Trong túi không đủ hàng.");
        }

        return new TradeResult(true, unit, total, $"Bán {quantity} {item.NameVi} được {total} Quan.");
    }

    public WarehouseInfo? Info(long cityId)
    {
        var warehouse = _db.QueryOne(
            "SELECT citadel_id, capacity, monthly_rent FROM mk_warehouse WHERE citadel_id=@city",
            row => new WarehouseInfo(row.GetInt64(0), row.GetInt32(1), row.GetInt64(2), 0),
            DbArgs.One("@city", cityId));

        if (warehouse is null)
        {
            return null;
        }

        var used = (int)_db.ScalarLong(
            "SELECT COALESCE(SUM(quantity), 0) FROM mk_warehouse_stock WHERE citadel_id=@city",
            DbArgs.One("@city", cityId));

        return warehouse with { Used = used };
    }

    public IReadOnlyList<WarehouseStockView> Stock(long cityId) =>
        _db.Query(
            "SELECT item_id, quantity, age_days FROM mk_warehouse_stock WHERE citadel_id=@city ORDER BY item_id",
            row => new WarehouseStockView(row.GetInt64(0), row.GetInt32(1), row.GetInt32(2)),
            DbArgs.One("@city", cityId));

    public int StockAt(long cityId, long itemId) =>
        (int)_db.ScalarLong(
            "SELECT COALESCE(quantity, 0) FROM mk_warehouse_stock WHERE citadel_id=@city AND item_id=@item",
            new DbArgs().Add("@city", cityId).Add("@item", itemId));

    public bool Store(long cityId, long itemId, int quantity, out string message)    {
        if (quantity <= 0)
        {
            message = "Số lượng phải lớn hơn 0.";
            return false;
        }

        var warehouse = Info(cityId);
        if (warehouse is null)
        {
            message = "Chưa thuê kho ở thành này.";
            return false;
        }

        if (warehouse.Used + quantity > warehouse.Capacity)
        {
            message = $"Kho đầy: {warehouse.Used}/{warehouse.Capacity}.";
            return false;
        }

        if (_bag.Quantity(cityId, itemId) < quantity)
        {
            message = "Trong túi không đủ hàng.";
            return false;
        }

        _db.Transaction(() =>
        {
            if (!_bag.Remove(cityId, itemId, quantity))
            {
                throw new SimException("market.bag_race", "Bag changed while storing; rolled back.");
            }

            _db.Execute(
                """
                INSERT INTO mk_warehouse_stock(citadel_id, item_id, quantity) VALUES(@city, @item, @quantity)
                ON CONFLICT(citadel_id, item_id) DO UPDATE SET quantity = quantity + excluded.quantity
                """,
                new DbArgs().Add("@city", cityId).Add("@item", itemId).Add("@quantity", quantity));
        });

        message = $"Gửi {quantity} vào kho.";
        return true;
    }

    public bool Take(long cityId, long itemId, int quantity, out string message)
    {
        if (quantity <= 0)
        {
            message = "Số lượng phải lớn hơn 0.";
            return false;
        }

        var inStock = (int)_db.ScalarLong(
            "SELECT COALESCE(quantity, 0) FROM mk_warehouse_stock WHERE citadel_id=@city AND item_id=@item",
            new DbArgs().Add("@city", cityId).Add("@item", itemId));

        if (inStock < quantity)
        {
            message = "Kho không đủ hàng.";
            return false;
        }

        _db.Transaction(() =>
        {
            if (inStock == quantity)
            {
                _db.Execute(
                    "DELETE FROM mk_warehouse_stock WHERE citadel_id=@city AND item_id=@item",
                    new DbArgs().Add("@city", cityId).Add("@item", itemId));
            }
            else
            {
                _db.Execute(
                    "UPDATE mk_warehouse_stock SET quantity = quantity - @quantity WHERE citadel_id=@city AND item_id=@item",
                    new DbArgs().Add("@city", cityId).Add("@item", itemId).Add("@quantity", quantity));
            }

            _bag.Add(cityId, itemId, quantity);
        });

        message = $"Lấy {quantity} khỏi kho.";
        return true;
    }

    public bool Rent(long cityId, out string message)
    {
        if (Info(cityId) is not null)
        {
            message = "Đã thuê kho ở thành này.";
            return false;
        }

        if (_wallet.Liquid < DefaultMonthlyRent)
        {
            message = $"Cần {DefaultMonthlyRent} Quan để thuê kho.";
            return false;
        }

        _db.Transaction(() =>
        {
            _actionTime.Charge(ActionCodes.MarketRent);
            _wallet.Charge(DefaultMonthlyRent, JournalCodes.WarehouseRent, "Thuê kho tháng đầu");
            _db.Execute(
                "INSERT INTO mk_warehouse(citadel_id, capacity, monthly_rent) VALUES(@city, @capacity, @rent)",
                new DbArgs().Add("@city", cityId).Add("@capacity", DefaultCapacity).Add("@rent", DefaultMonthlyRent));
        });

        message = $"Thuê kho {DefaultCapacity} đơn vị, {DefaultMonthlyRent} Quan/tháng.";
        return true;
    }

    /// <summary>Called on every month boundary. Rent is a real cashflow, never a silent freebie.</summary>
    public void ChargeMonthlyRent(GameDate date)
    {
        var warehouses = _db.Query(
            "SELECT citadel_id, monthly_rent FROM mk_warehouse ORDER BY citadel_id",
            row => (CityId: row.GetInt64(0), Rent: row.GetInt64(1)));

        foreach (var warehouse in warehouses)
        {
            if (_wallet.Liquid < warehouse.Rent)
            {
                _log.Warn($"Chưa trả được tiền kho {warehouse.Rent} Quan tại thành {warehouse.CityId} ngày {date}.");
                continue;
            }

            _wallet.Charge(
                warehouse.Rent,
                JournalCodes.WarehouseRent,
                $"Tiền kho tháng {date.Month:00}/{date.Year}",
                $"market.warehouse_rent:{warehouse.CityId}:{date.DayIndex}");
        }
    }

    /// <summary>
    /// Daily decay of perishables in the warehouse. One unit of an item with a 60-day shelf life
    /// loses 1/60 of itself per day; the carry column preserves the remainder so slow decay is
    /// not rounded away. Items with a shelf life of 0 never spoil.
    /// </summary>
    public void SpoilOneDay(GameDate date)
    {
        var rows = _db.Query(
            "SELECT citadel_id, item_id, quantity, spoilage_carry, age_days FROM mk_warehouse_stock",
            row => (City: row.GetInt64(0), Item: row.GetInt64(1), Quantity: row.GetInt32(2),
                    Carry: row.GetInt32(3), Age: row.GetInt32(4)));

        foreach (var row in rows)
        {
            var item = _items.Find(row.Item);
            if (item is null || item.ShelfLifeDays <= 0)
            {
                continue;
            }

            var age = row.Age + 1;
            if (age >= item.ShelfLifeDays)
            {
                _db.Execute(
                    "DELETE FROM mk_warehouse_stock WHERE citadel_id=@city AND item_id=@item",
                    new DbArgs().Add("@city", row.City).Add("@item", row.Item));
                _log.Info($"spoiled all {row.Quantity} x {item.Code} in warehouse {row.City} on {date}");
                continue;
            }

            var rate = Math.Max(1, 1000 / item.ShelfLifeDays);
            var raw = (row.Quantity * rate) + row.Carry;
            var lost = raw / 1000;
            var carry = raw % 1000;
            var remaining = row.Quantity - lost;

            if (remaining <= 0)
            {
                _db.Execute(
                    "DELETE FROM mk_warehouse_stock WHERE citadel_id=@city AND item_id=@item",
                    new DbArgs().Add("@city", row.City).Add("@item", row.Item));
                _log.Info($"spoiled {row.Quantity} x {item.Code} in warehouse {row.City} on {date}");
                continue;
            }

            _db.Execute(
                "UPDATE mk_warehouse_stock SET quantity=@quantity, spoilage_carry=@carry, age_days=@age " +
                "WHERE citadel_id=@city AND item_id=@item",
                new DbArgs().Add("@city", row.City).Add("@item", row.Item)
                    .Add("@quantity", remaining).Add("@carry", carry).Add("@age", age));

            if (lost > 0)
            {
                _log.Info($"spoiled {lost} x {item.Code} in warehouse {row.City} on {date}");
            }
        }
    }

    /// <summary>
    /// Monthly restock: a stall refills 60% of the gap back to its baseline, so buying a market
    /// dry has a lasting but not permanent effect. Supply is a real constraint, not a number
    /// that silently resets.
    /// </summary>
    public void RestockMonthly(GameDate date)
    {
        var rows = _db.Query(
            "SELECT citadel_id, item_id, stock, baseline_stock FROM mk_stall_listing WHERE stock < baseline_stock",
            row => (City: row.GetInt64(0), Item: row.GetInt64(1), Stock: row.GetInt32(2), Baseline: row.GetInt32(3)));

        foreach (var row in rows)
        {
            var refill = Math.Max(1, (row.Baseline - row.Stock) * 60 / 100);
            _db.Execute(
                "UPDATE mk_stall_listing SET stock = stock + @refill WHERE citadel_id=@city AND item_id=@item",
                new DbArgs().Add("@city", row.City).Add("@item", row.Item).Add("@refill", refill));
        }

        if (rows.Count > 0)
        {
            _log.Info($"restocked {rows.Count} stall listing(s) on {date}");
        }
    }

    /// <summary>Seeds stall supply from the catalog and the map. No foreign table is read here.</summary>
    public void EnsureListings(IItemCatalog items, IWorldMap map)
    {
        var existing = _db.ScalarLong("SELECT COUNT(*) FROM mk_stall_listing");
        if (existing > 0)
        {
            return;
        }

        _db.Transaction(() =>
        {
            foreach (var citadel in map.Citadels)
            {
                foreach (var item in items.All)
                {
                    _db.Execute(
                        """
                        INSERT OR IGNORE INTO mk_stall_listing(citadel_id, item_id, stock, baseline_stock)
                        VALUES(@city, @item, 40, 40)
                        """,
                        new DbArgs().Add("@city", citadel.Id).Add("@item", item.Id));
                }
            }
        });
    }

    private ItemDef RequireItem(long itemId) =>
        _items.Find(itemId) ?? throw new SimException("market.item_missing", $"Item {itemId} is not in the catalog.");

    private CitadelDef RequireCitadel(long cityId) =>
        _map.Find(cityId) ?? throw new SimException("market.citadel_missing", $"Citadel {cityId} is not on the map.");
}
