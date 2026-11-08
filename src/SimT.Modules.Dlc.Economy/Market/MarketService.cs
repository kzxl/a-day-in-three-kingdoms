using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Ledger;
using SimT.Contracts.Logging;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Economy;

/// <summary>
/// Trade + warehouse rules for <c>dlc.market</c>. Everything it touches is reached through a
/// capability, never through another module's assembly.
/// </summary>
internal sealed class MarketService : IMarketTrade, IMarketWarehouse, IMarketPortfolio, IPlayerStall, INpcMarket
{
    public const int DefaultCapacity = 200;
    public const long DefaultMonthlyRent = 150;
    public const long DefaultStallRentAmount = 80;

    public long DefaultStallRent => DefaultStallRentAmount;

    private readonly IDbSession _db;
    private readonly IGameClock _clock;
    private readonly IActionTime _actionTime;
    private readonly IWallet _wallet;
    private readonly IBag _bag;
    private readonly IItemCatalog _items;
    private readonly IWorldMap _map;
    private readonly ILog _log;
    private readonly INpcRegistry? _npcs;
    private readonly SimT.Modules.Dlc.Economy.Shocks.MarketShockEngine? _shocks;

    public MarketService(
        IDbSession db,
        IGameClock clock,
        IActionTime actionTime,
        IWallet wallet,
        IBag bag,
        IItemCatalog items,
        IWorldMap map,
        ILog log,
        INpcRegistry? npcs = null,
        SimT.Modules.Dlc.Economy.Shocks.MarketShockEngine? shocks = null)
    {
        _db = db;
        _clock = clock;
        _actionTime = actionTime;
        _wallet = wallet;
        _bag = bag;
        _items = items;
        _map = map;
        _log = log;
        _npcs = npcs;
        _shocks = shocks;
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

    public void AdjustStock(long cityId, long itemId, int delta)
    {
        _db.Execute(
            "UPDATE mk_stall_listing SET stock = MAX(0, stock + @delta) WHERE citadel_id=@city AND item_id=@item",
            new DbArgs().Add("@delta", delta).Add("@city", cityId).Add("@item", itemId));
    }

    /// <summary>
    /// One row per citadel and item per day. Prices are a function of stock, so the sample is
    /// also the record of how scarce the market was.
    /// </summary>
    public void RecordDailyPrices(GameDate date)
    {
        var rows = _db.Query(
            "SELECT citadel_id, item_id, stock, baseline_stock FROM mk_stall_listing",
            row => (City: row.GetInt64(0), Item: row.GetInt64(1), Stock: row.GetInt32(2), Baseline: row.GetInt32(3)));

        Dictionary<(long City, long Item), int>? rumorFactors = null;
        if (_hasRumorTable || _db.ScalarLong("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='cv_market_rumor'") > 0)
        {
            _hasRumorTable = true;
            rumorFactors = _db.Query(
                "SELECT citadel_id, item_id, price_mult_permille FROM cv_market_rumor WHERE is_active = 1 AND start_day <= @day AND end_day >= @day",
                r => (City: r.GetInt64(0), Item: r.GetInt64(1), Mult: r.GetInt32(2)),
                DbArgs.One("@day", date.DayIndex))
                .ToDictionary(x => (x.City, x.Item), x => x.Mult);
        }

        var activeShocks = _shocks?.GetActiveShocksMap(date.DayIndex);
        var month = date.Month;
        var samples = new List<(long City, long Item, long Price)>(rows.Count);
        foreach (var row in rows)
        {
            var item = _items.Find(row.Item);
            var city = _map.Find(row.City);
            if (item is null || city is null)
            {
                continue;
            }

            var baseQuote = MarketPricing.Quote(item, city, month, row.Stock, row.Baseline);
            var factor = rumorFactors != null && rumorFactors.TryGetValue((row.City, row.Item), out var f) ? f : 1000;
            var price = factor == 1000 ? baseQuote : Math.Max(1, baseQuote * factor / 1000);
            if (activeShocks != null && activeShocks.TryGetValue(row.City, out var cityShock))
            {
                var shockMult = SimT.Modules.Dlc.Economy.Shocks.MarketShockEngine.EvaluateShockMultiplier(cityShock.ShockType, cityShock.SeverityPermille, item.Code);
                if (shockMult != 1000)
                {
                    price = Math.Max(1, price * shockMult / 1000);
                }
            }
            samples.Add((row.City, row.Item, price));
        }

        _db.Transaction(() =>
        {
            const int chunkSize = 50;
            for (var i = 0; i < samples.Count; i += chunkSize)
            {
                var chunk = samples.Skip(i).Take(chunkSize).ToList();
                var sql = new System.Text.StringBuilder("INSERT OR IGNORE INTO mk_price_sample(citadel_id, item_id, day_index, buy_price) VALUES ");
                var args = new DbArgs();
                for (var j = 0; j < chunk.Count; j++)
                {
                    if (j > 0) sql.Append(", ");
                    sql.Append($"(@c{j}, @i{j}, @d{j}, @p{j})");
                    args.Add($"@c{j}", chunk[j].City)
                        .Add($"@i{j}", chunk[j].Item)
                        .Add($"@d{j}", date.DayIndex)
                        .Add($"@p{j}", chunk[j].Price);
                }
                _db.Execute(sql.ToString(), args);
            }

            // Sixty days is the whole window the screen ever reads.
            _db.Execute(
                "DELETE FROM mk_price_sample WHERE day_index < @cutoff",
                DbArgs.One("@cutoff", date.DayIndex - 60));
        });
    }

    private bool _hasRumorTable;

    private int GetRumorPriceFactor(long cityId, long itemId)
    {
        if (!_hasRumorTable)
        {
            _hasRumorTable = _db.ScalarLong("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='cv_market_rumor'") > 0;
            if (!_hasRumorTable)
            {
                return 1000;
            }
        }

        var factor = _db.ScalarLong(
            """
            SELECT price_mult_permille
            FROM cv_market_rumor
            WHERE citadel_id = @city AND item_id = @item AND is_active = 1 AND start_day <= @day AND end_day >= @day
            ORDER BY id DESC LIMIT 1
            """,
            new DbArgs().Add("@city", cityId).Add("@item", itemId).Add("@day", _clock.Today.DayIndex));

        return factor > 0 ? (int)factor : 1000;
    }

    public long QuoteBuy(long cityId, long itemId)
    {
        var item = RequireItem(itemId);
        var citadel = RequireCitadel(cityId);
        var baseQuote = MarketPricing.Quote(
            item,
            citadel,
            _clock.Today.Month,
            StallStock(cityId, itemId),
            StallBaseline(cityId, itemId));

        var factor = GetRumorPriceFactor(cityId, itemId);
        var price = factor == 1000 ? baseQuote : Math.Max(1, baseQuote * factor / 1000);
        if (_shocks is not null)
        {
            var shockMult = _shocks.GetShockMultiplierPermille(cityId, item.Code, _clock.Today.DayIndex);
            if (shockMult != 1000)
            {
                price = Math.Max(1, price * shockMult / 1000);
            }
        }
        return price;
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

        var unit = QuoteBuy(cityId, itemId);
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

        var unit = QuoteSell(cityId, itemId);
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

    public long BagValue(long cityId)
    {
        var city = _map.Find(cityId);
        if (city is null)
        {
            return 0;
        }

        var values = _bag.Rows(cityId);
        var total = 0L;
        foreach (var row in values)
        {
            var item = _items.Find(row.ItemId);
            if (item is null)
            {
                continue;
            }

            total += MarketPricing.SellQuote(MarketPricing.Quote(item, city, _clock.Today.Month, 1, 1)) * row.Quantity;
        }

        return total;
    }

    public long InventoryValue()
    {
        var total = 0L;
        foreach (var city in _map.Citadels)
        {
            total += BagValue(city.Id);
        }

        return total;
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

        _db.Execute("UPDATE mk_npc_offer SET quantity = max_quantity WHERE quantity < max_quantity");
    }

    /// <summary>Seeds stall supply from the catalog and the map. No foreign table is read here.</summary>
    public void EnsureListings(IItemCatalog items, IWorldMap map)
    {
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
            row => new NpcMarketOffer(
                row.GetInt64(0),
                row.GetInt64(1),
                row.GetInt64(2),
                row.GetInt64(3),
                row.GetInt32(4),
                row.GetInt32(5),
                row.GetInt64(6),
                row.GetInt32(7)),
            DbArgs.One("@city", cityId));
    }

    public IReadOnlyList<NpcMarketOffer> OffersByStall(long stallId)
    {
        return _db.Query(
            @"SELECT id, stall_id, npc_id, item_id, quantity, max_quantity, unit_price, discount_permille
              FROM mk_npc_offer WHERE stall_id = @stall AND quantity > 0
              ORDER BY id",
            row => new NpcMarketOffer(
                row.GetInt64(0),
                row.GetInt64(1),
                row.GetInt64(2),
                row.GetInt64(3),
                row.GetInt32(4),
                row.GetInt32(5),
                row.GetInt64(6),
                row.GetInt32(7)),
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

        if (offer.Quantity < quantity)
        {
            return TradeResult.Fail($"Sạp chỉ còn {offer.Quantity} đơn vị hàng.");
        }

        var item = _items.Find(offer.ItemId);
        if (item is null)
        {
            return TradeResult.Fail($"Hàng hóa mã {offer.ItemId} không tồn tại trong danh mục.");
        }

        var effectiveUnit = offer.UnitPrice * (1000 - offer.Discount) / 1000;
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
