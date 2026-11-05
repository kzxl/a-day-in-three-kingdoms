using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Contracts.Ui;

namespace SimT.Modules.Dlc.Market;

/// <summary>
/// Stall screen: headline numbers, the goods on the shelf, and one row per item carrying its own
/// buy/sell actions with quantity and a cost preview.
/// </summary>
internal sealed class StallScreen : IScreenContribution
{
    private static readonly string[] Columns =
        ["Vật phẩm", "Giá mua", "Giá bán", "Tồn sạp", "Mức cung", "Trong túi"];

    private readonly IMarketTrade _trade;
    private readonly INpcMarket _npcMarket;
    private readonly IItemCatalog _items;
    private readonly IWorldMap _map;
    private readonly IBag _bag;
    private readonly IPlayerState _player;
    private readonly IGameClock _clock;
    private readonly IWallet _wallet;
    private readonly IActionTime _actionTime;
    private readonly INpcRegistry? _npcRegistry;

    public StallScreen(
        IMarketTrade trade,
        INpcMarket npcMarket,
        IItemCatalog items,
        IWorldMap map,
        IBag bag,
        IPlayerState player,
        IGameClock clock,
        IWallet wallet,
        IActionTime actionTime,
        INpcRegistry? npcRegistry = null)
    {
        _trade = trade;
        _npcMarket = npcMarket;
        _items = items;
        _map = map;
        _bag = bag;
        _player = player;
        _clock = clock;
        _wallet = wallet;
        _actionTime = actionTime;
        _npcRegistry = npcRegistry;
    }

    public string ScreenId => "market.stall";

    public string Title => "Chợ Đầu Mối";

    public int Order => 20;

    public ScreenModel Render()
    {
        var city = _map.Find(_player.CurrentCityId);
        if (city is null)
        {
            return ScreenModel.Empty(ScreenId, Title, "Không xác định được thành trấn hiện tại.");
        }

        var buyCost = _actionTime.Quote(ActionCodes.TradeBuy).ToString();
        var rowsByCategory = new Dictionary<string, List<ScreenRow>>();
        var alerts = new List<ScreenAlert>();
        var emptyShelves = 0;

        foreach (var item in _items.All)
        {
            var buy = _trade.QuoteBuy(city.Id, item.Id);
            var sell = _trade.QuoteSell(city.Id, item.Id);
            var stock = _trade.StallStock(city.Id, item.Id);
            var baseline = _trade.StallBaseline(city.Id, item.Id);
            var held = _bag.Quantity(city.Id, item.Id);
            var supply = SupplyLabel(stock, baseline);

            if (stock == 0)
            {
                emptyShelves++;
                alerts.Add(new ScreenAlert($"Sạp cạn {item.NameVi}: giá sẽ cao nhất khi nhập lại.", AlertLevel.Warning));
            }
            else if (baseline > 0 && stock < baseline / 4)
            {
                alerts.Add(new ScreenAlert($"{item.NameVi} gần cạn ({stock}/{baseline}).", AlertLevel.Info));
            }

            if (!rowsByCategory.TryGetValue(item.Category, out var catRows))
            {
                catRows = new List<ScreenRow>();
                rowsByCategory[item.Category] = catRows;
            }

            catRows.Add(new ScreenRow(
                [
                    $"{item.NameVi} ({item.NameEn})",
                    Fmt(buy),
                    Fmt(sell),
                    Fmt(stock),
                    supply,
                    Fmt(held)
                ],
                [
                    new ScreenCommand($"buy:{item.Id}", "Mua 1", Cost: buyCost),
                    new ScreenCommand($"sell:{item.Id}", "Bán 1", Cost: buyCost),
                    new ScreenCommand($"buy:{item.Id}", "Mua…", RequiresQuantity: true, Preview: BuyPreview(buy, stock), Cost: buyCost),
                    new ScreenCommand($"sell:{item.Id}", "Bán…", RequiresQuantity: true, Preview: SellPreview(sell, held), Cost: buyCost),
                    new ScreenCommand($"item:modal:{item.Id}", "Kỳ Vật 🔍", CommandKind.Secondary)
                ],
                Title: item.NameVi,
                Details:
                [
                    new ScreenDetail("Giá gốc", Fmt(item.BasePrice) + " quan"),
                    new ScreenDetail("Giá mua tại đây", Fmt(buy) + " quan"),
                    new ScreenDetail("Giá bán lại", Fmt(sell) + " quan"),
                    new ScreenDetail("Tồn sạp", $"{stock}/{baseline}"),
                    new ScreenDetail("Mức cung", supply),
                    new ScreenDetail("Trong túi", Fmt(held)),
                    new ScreenDetail("Hạn sử dụng", item.ShelfLifeDays <= 0 ? "bền" : $"{item.ShelfLifeDays} ngày"),
                    new ScreenDetail("Hệ số vùng", $"{city.PriceFactorPermille / 1000.0:0.00}"),
                    new ScreenDetail("Giá 10 ngày", Trend(_trade.PriceHistory(city.Id, item.Id)))
                ]));
        }

        var season = GameDateCodec.SeasonVi(_clock.Today.Month);
        var tracked = _items.All.FirstOrDefault();
        var trackedHistory = tracked is null ? Array.Empty<long>() : _trade.PriceHistory(city.Id, tracked.Id);

        var metrics = new List<ScreenMetric>
        {
            new("Tiền mặt", Fmt(_wallet.Liquid), "quan", MetricTone.Neutral),
            new(
                tracked is null ? "Xu hướng" : $"Giá {tracked.NameVi}",
                tracked is null ? "—" : Trend(trackedHistory),
                tracked is null ? null : $"10 ngày · {trackedHistory.DefaultIfEmpty(0).Min()}–{trackedHistory.DefaultIfEmpty(0).Max()} quan",
                MetricTone.Neutral),
            new("Hàng trong túi", Fmt(_bag.TotalQuantity(city.Id)), "đơn vị", MetricTone.Neutral),
            new("Sạp cạn", Fmt(emptyShelves), $"trên {_items.All.Count} mặt hàng",
                emptyShelves > 0 ? MetricTone.Warning : MetricTone.Neutral)
        };

        var categoryMap = new Dictionary<string, (string Title, int Order)>(StringComparer.OrdinalIgnoreCase)
        {
            ["grain"] = ("🌾 Lương Thực & Nhu Yếu", 1),
            ["food"] = ("🍱 Thực Phẩm & Đồ Uống", 2),
            ["luxury"] = ("👑 Thượng Phẩm & Xa Xỉ", 3),
            ["medicine"] = ("💊 Dược Liệu & Thuốc Men", 4),
            ["material"] = ("⛏️ Khoáng Sản & Vật Liệu", 5)
        };

        var sections = rowsByCategory
            .OrderBy(pair => categoryMap.TryGetValue(pair.Key, out var meta) ? meta.Order : 99)
            .Select(pair => new ScreenSection(
                categoryMap.TryGetValue(pair.Key, out var meta) ? meta.Title : pair.Key,
                Columns,
                pair.Value))
            .ToList();

        var npcOffers = _npcMarket.OffersAt(city.Id);
        if (npcOffers.Count > 0)
        {
            var stalls = _npcMarket.StallsAt(city.Id);
            var npcColumns = new[] { "Vật phẩm", "Sạp hàng", "Chủ sạp", "Giá gốc", "Ưu đãi", "Còn lại" };
            var npcRows = new List<ScreenRow>();

            foreach (var offer in npcOffers)
            {
                var item = _items.Find(offer.ItemId);
                if (item is null) continue;

                var stall = stalls.FirstOrDefault(s => s.Id == offer.StallId);
                var stallName = stall?.NameVi ?? "Sạp Chợ";
                var npc = _npcRegistry?.Find(offer.NpcId);
                var npcName = npc?.NameVi ?? "Thương Gia";

                var effectivePrice = offer.UnitPrice * (1000 - offer.DiscountPermille) / 1000;
                var discountText = offer.DiscountPermille > 0 ? $"-{offer.DiscountPermille / 10}%" : "Chuẩn";

                npcRows.Add(new ScreenRow(
                    [
                        item.NameVi,
                        stallName,
                        npcName,
                        Fmt(offer.UnitPrice) + " q",
                        $"{Fmt(effectivePrice)} q ({discountText})",
                        offer.Quantity.ToString()
                    ],
                    [
                        new ScreenCommand($"npc_buy:{offer.Id}", "Mua 1", Cost: buyCost),
                        new ScreenCommand($"npc_buy:{offer.Id}", "Mua…", RequiresQuantity: true,
                            Preview: BuyOfferPreview(effectivePrice, offer.Quantity), Cost: buyCost),
                        new ScreenCommand($"npc:modal:{offer.NpcId}", "Hồ Sơ NPC 🔍", CommandKind.Secondary)
                    ],
                    Title: $"{item.NameVi} — {stallName}",
                    Details:
                    [
                        new ScreenDetail("Mặt hàng", item.NameVi),
                        new ScreenDetail("Sạp hàng", stallName),
                        new ScreenDetail("Chủ sạp", npcName),
                        new ScreenDetail("Đơn giá gốc", Fmt(offer.UnitPrice) + " quan"),
                        new ScreenDetail("Ưu đãi giảm giá", discountText),
                        new ScreenDetail("Giá thực mua", Fmt(effectivePrice) + " quan"),
                        new ScreenDetail("Tồn kho sạp", $"{offer.Quantity}/{offer.MaxQuantity}"),
                        new ScreenDetail("Ngành hàng", stall?.Sector ?? "Tổng hợp")
                    ]));
            }

            sections.Insert(0, new ScreenSection("🎪 Sạp Chuyên Doanh & Hàng NPC Giảm Giá", npcColumns, npcRows));
        }

        return new ScreenModel(
            ScreenId,
            $"{Title} — {city.NameVi}",
            $"Mùa {season} · hệ số vùng {city.PriceFactorPermille / 1000.0:0.00} · giá tăng khi sạp cạn",
            sections,
            [],
            metrics,
            alerts);
    }

    private Func<int, string> BuyOfferPreview(long unitPrice, int stock) => quantity =>
    {
        if (quantity > stock)
        {
            return $"Sạp NPC chỉ còn {stock} đơn vị.";
        }

        var total = unitPrice * quantity;
        return total > _wallet.Liquid
            ? $"Thiếu {Fmt(total - _wallet.Liquid)} quan cho {quantity} đơn vị."
            : $"{quantity} × {Fmt(unitPrice)} = {Fmt(total)} quan · còn {Fmt(_wallet.Liquid - total)}";
    };

    private Func<int, string> BuyPreview(long unitPrice, int stock) => quantity =>
    {
        if (quantity > stock)
        {
            return $"Sạp chỉ còn {stock} đơn vị.";
        }

        var total = unitPrice * quantity;
        return total > _wallet.Liquid
            ? $"Thiếu {Fmt(total - _wallet.Liquid)} quan cho {quantity} đơn vị."
            : $"{quantity} × {Fmt(unitPrice)} = {Fmt(total)} quan · còn {Fmt(_wallet.Liquid - total)}";
    };

    private static Func<int, string> SellPreview(long unitPrice, int held) => quantity =>
        quantity > held
            ? $"Trong túi chỉ có {held} đơn vị."
            : $"Bán {quantity}: +{Fmt(unitPrice * quantity)} quan";

    /// <summary>
    /// A text sparkline: the game is read, not watched, so a trend can be a handful of characters
    /// wide. The ladder is ASCII on purpose — block glyphs render as tofu in Consolas.
    /// </summary>
    private static string Trend(IReadOnlyList<long> values)
    {
        const string blocks = ".:-=+*#@";

        if (values.Count == 0)
        {
            return "—";
        }

        if (values.Count == 1)
        {
            return Fmt(values[0]) + " quan";
        }

        var min = values.Min();
        var max = values.Max();
        if (max == min)
        {
            return new string(blocks[3], values.Count) + $" {Fmt(min)}";
        }

        var chars = new char[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            var level = (int)((values[index] - min) * 7 / (max - min));
            chars[index] = blocks[Math.Clamp(level, 0, 7)];
        }

        return new string(chars) + $" {Fmt(min)}–{Fmt(max)}";
    }

    private static string SupplyLabel(int stock, int baseline)
    {
        if (baseline <= 0)
        {
            return "—";
        }

        if (stock == 0)
        {
            return "cạn";
        }

        if (stock < baseline / 2)
        {
            return "khan";
        }

        return stock > baseline ? "dư" : "đủ";
    }

    private static string Fmt(long value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// Warehouse screen: capacity, rent and spoilage risk up front; each stored line can be moved in
/// or out by quantity.
/// </summary>
internal sealed class WarehouseScreen : IScreenContribution
{
    private static readonly string[] Columns = ["Vật phẩm", "Trong kho", "Tuổi", "Hạn", "Trong túi"];

    private readonly IMarketWarehouse _warehouse;
    private readonly IItemCatalog _items;
    private readonly IWorldMap _map;
    private readonly IBag _bag;
    private readonly IPlayerState _player;
    private readonly IActionTime _actionTime;

    public WarehouseScreen(
        IMarketWarehouse warehouse,
        IItemCatalog items,
        IWorldMap map,
        IBag bag,
        IPlayerState player,
        IActionTime actionTime)
    {
        _warehouse = warehouse;
        _items = items;
        _map = map;
        _bag = bag;
        _player = player;
        _actionTime = actionTime;
    }

    public string ScreenId => "market.warehouse";

    public string Title => "Kho Hàng";

    public int Order => 30;

    public ScreenModel Render()
    {
        var city = _map.Find(_player.CurrentCityId);
        if (city is null)
        {
            return ScreenModel.Empty(ScreenId, Title, "Không xác định được thành trấn hiện tại.");
        }

        var info = _warehouse.Info(city.Id);
        if (info is null)
        {
            return new ScreenModel(
                ScreenId,
                $"{Title} — {city.NameVi}",
                $"Chưa thuê kho. Sức chứa {_warehouse.DefaultCapacity}, phí {_warehouse.DefaultMonthlyRent} quan/tháng.",
                [],
                [new ScreenCommand(
                    "rent",
                    $"Thuê kho ({_warehouse.DefaultMonthlyRent} quan)",
                    Cost: _actionTime.Quote(ActionCodes.MarketRent).ToString())],
                [
                    new ScreenMetric("Sức chứa", Fmt(_warehouse.DefaultCapacity), "đơn vị"),
                    new ScreenMetric("Phí thuê", Fmt(_warehouse.DefaultMonthlyRent), "quan/tháng", MetricTone.Warning),
                    new ScreenMetric("Hàng bền", "không hư", "nếu hạn = 0")
                ],
                [new ScreenAlert($"Kho tại {city.NameVi} chưa thuê: hàng dễ hỏng đang nằm trong túi.", AlertLevel.Info)]);
        }

        var stock = _warehouse.Stock(city.Id);
        var rows = new List<ScreenRow>();
        var alerts = new List<ScreenAlert>();
        var value = 0L;
        var atRisk = 0;

        foreach (var line in stock)
        {
            var item = _items.Find(line.ItemId);
            if (item is null)
            {
                continue;
            }

            var held = _bag.Quantity(city.Id, item.Id);
            value += item.BasePrice * line.Quantity;

            if (item.ShelfLifeDays > 0)
            {
                var daysLeft = item.ShelfLifeDays - line.AgeDays;
                if (daysLeft <= 10)
                {
                    atRisk += line.Quantity;
                    alerts.Add(new ScreenAlert(
                        $"{item.NameVi} còn {daysLeft} ngày trước khi hỏng ({line.Quantity} đơn vị).",
                        daysLeft <= 3 ? AlertLevel.Critical : AlertLevel.Warning));
                }
            }

            rows.Add(new ScreenRow(
                [
                    item.NameVi,
                    Fmt(line.Quantity),
                    Fmt(line.AgeDays),
                    item.ShelfLifeDays <= 0 ? "bền" : Fmt(item.ShelfLifeDays),
                    Fmt(held)
                ],
                [
                    new ScreenCommand($"store:{item.Id}", "Gửi 1"),
                    new ScreenCommand($"take:{item.Id}", "Lấy 1"),
                    new ScreenCommand($"store:{item.Id}", "Gửi…", RequiresQuantity: true,
                        Preview: StorePreview(info, item.Id, item.NameVi)),
                    new ScreenCommand($"take:{item.Id}", "Lấy…", RequiresQuantity: true,
                        Preview: TakePreview(line.Quantity, held, item.NameVi)),
                    new ScreenCommand($"item:modal:{item.Id}", "Kỳ Vật 🔍", CommandKind.Secondary)
                ],
                Title: item.NameVi,
                Details:
                [
                    new ScreenDetail("Trong kho", Fmt(line.Quantity)),
                    new ScreenDetail("Đã lưu", $"{line.AgeDays} ngày"),
                    new ScreenDetail("Hạn", item.ShelfLifeDays <= 0 ? "không hỏng" : $"{item.ShelfLifeDays} ngày"),
                    new ScreenDetail("Giá trị", Fmt(item.BasePrice * line.Quantity) + " quan"),
                    new ScreenDetail("Trong túi", Fmt(held))
                ]));
        }

        var full = info.Used * 100 / Math.Max(1, info.Capacity);
        if (full >= 80)
        {
            alerts.Add(new ScreenAlert($"Kho đã dùng {info.Used}/{info.Capacity} chỗ.", AlertLevel.Warning));
        }

        var metrics = new List<ScreenMetric>
        {
            new("Sức chứa", $"{info.Used}/{info.Capacity}", $"{full}% đã dùng", full >= 80 ? MetricTone.Warning : MetricTone.Neutral),
            new("Phí thuê", Fmt(info.MonthlyRent), "quan/tháng", MetricTone.Neutral),
            new("Giá trị tồn", Fmt(value), "quan", MetricTone.Positive),
            new("Sắp hỏng", Fmt(atRisk), "đơn vị trong 10 ngày", atRisk > 0 ? MetricTone.Warning : MetricTone.Neutral)
        };

        var commands = rows.Count == 0
            ? Array.Empty<ScreenCommand>()
            : Array.Empty<ScreenCommand>();

        var bagRows = new List<ScreenRow>();
        foreach (var item in _items.All)
        {
            var held = _bag.Quantity(city.Id, item.Id);
            if (held <= 0) continue;

            bagRows.Add(new ScreenRow(
                [
                    item.NameVi,
                    Fmt(held),
                    "—",
                    item.ShelfLifeDays <= 0 ? "bền" : Fmt(item.ShelfLifeDays),
                    Fmt(held)
                ],
                [
                    new ScreenCommand($"store:{item.Id}", "Gửi 1 Vào Kho"),
                    new ScreenCommand($"store:{item.Id}", "Gửi…", RequiresQuantity: true,
                        Preview: StorePreview(info, item.Id, item.NameVi)),
                    new ScreenCommand($"item:modal:{item.Id}", "Kỳ Vật 🔍", CommandKind.Secondary)
                ],
                Title: item.NameVi,
                Details:
                [
                    new ScreenDetail("Trong túi đồ", Fmt(held)),
                    new ScreenDetail("Hạn dùng", item.ShelfLifeDays <= 0 ? "không hỏng" : $"{item.ShelfLifeDays} ngày"),
                    new ScreenDetail("Giá trị ước tính", Fmt(item.BasePrice * held) + " quan")
                ]));
        }

        var sections = new List<ScreenSection>
        {
            new ScreenSection($"Tồn Kho Thương Điếm ({rows.Count} mặt hàng)", Columns, rows)
        };

        if (bagRows.Count > 0)
        {
            sections.Add(new ScreenSection($"Hành Trang Sở Tại ({bagRows.Count} mặt hàng đang mang theo)", Columns, bagRows));
        }

        return new ScreenModel(
            ScreenId,
            $"{Title} — {city.NameVi}",
            $"Sức chứa {info.Used}/{info.Capacity} · phí {info.MonthlyRent} quan/tháng · hàng dễ hỏng mất dần mỗi ngày",
            sections,
            commands,
            metrics,
            alerts);
    }

    private Func<int, string> StorePreview(WarehouseInfo info, long itemId, string name)
    {
        var free = Math.Max(0, info.Capacity - info.Used);
        return quantity =>
        {
            if (quantity > free)
            {
                return $"Kho chỉ còn {free} chỗ.";
            }

            return $"Gửi {quantity} {name}: còn {free - quantity} chỗ trống.";
        };
    }

    private static Func<int, string> TakePreview(int stored, int held, string name) => quantity =>
        quantity > stored
            ? $"Kho chỉ có {stored} đơn vị."
            : $"Lấy {quantity} {name}: túi thành {held + quantity}.";

    private static string Fmt(long value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// Player-owned retail stall screen.
/// </summary>
internal sealed class PlayerStallScreen : IScreenContribution
{
    private static readonly string[] ShelfColumns = ["Vật phẩm", "Số lượng", "Giá niêm yết", "Giá sỉ chợ", "Biên độ", "Dự kiến bán"];
    private static readonly string[] BagColumns = ["Vật phẩm", "Trong túi", "Giá sỉ chợ", "Giá đề xuất (+20%)"];

    private readonly IPlayerStall _playerStall;
    private readonly IMarketTrade _trade;
    private readonly IItemCatalog _items;
    private readonly IWorldMap _map;
    private readonly IBag _bag;
    private readonly IPlayerState _player;
    private readonly IGameClock _clock;
    private readonly IWallet _wallet;
    private readonly IActionTime _actionTime;

    public PlayerStallScreen(
        IPlayerStall playerStall,
        IMarketTrade trade,
        IItemCatalog items,
        IWorldMap map,
        IBag bag,
        IPlayerState player,
        IGameClock clock,
        IWallet wallet,
        IActionTime actionTime)
    {
        _playerStall = playerStall;
        _trade = trade;
        _items = items;
        _map = map;
        _bag = bag;
        _player = player;
        _clock = clock;
        _wallet = wallet;
        _actionTime = actionTime;
    }

    public string ScreenId => "market.player_stall";

    public string Title => "Tiệm Của Bạn";

    public int Order => 21;

    public ScreenModel Render()
    {
        var city = _map.Find(_player.CurrentCityId);
        if (city is null)
        {
            return ScreenModel.Empty(ScreenId, Title, "Không xác định được thành trấn hiện tại.");
        }

        var stall = _playerStall.StallInfo(city.Id);
        if (stall is null)
        {
            return new ScreenModel(
                ScreenId,
                $"{Title} — {city.NameVi}",
                "Ngươi chưa mở tiệm tại thành này. Mở sạp để tự động bán lẻ kiếm chênh lệch mỗi ngày.",
                [
                    new ScreenSection(
                        "Dịch vụ mở sạp kinh doanh",
                        ["Khoản mục", "Chi tiết"],
                        [
                            ScreenRow.Of(["Mô hình", "Bán lẻ tự động (Retail)"]),
                            ScreenRow.Of(["Tiền thuê sạp", $"{_playerStall.DefaultStallRent} quan/tháng"]),
                            ScreenRow.Of(["Tiểu nhị", "1 người trông coi (đã bao gồm trong chi phí thuê)"]),
                            ScreenRow.Of(["Bày hàng", "Bày hàng từ túi lên quầy với 1 thao tác click"]),
                            ScreenRow.Of(["Doanh thu", "Khách NPC mua hàng tự động mỗi ngày (tiền nạp trực tiếp vào ví)"])
                        ])
                ],
                [
                    new ScreenCommand("stall:rent", $"Thuê sạp mở tiệm ({_playerStall.DefaultStallRent} quan/tháng)",
                        Cost: _actionTime.Quote(ActionCodes.StallRent).ToString())
                ],
                [
                    new("Tiền thuê", $"{_playerStall.DefaultStallRent}", "quan/tháng", MetricTone.Neutral),
                    new("Mô hình", "Bán lẻ", "tự động", MetricTone.Neutral),
                    new("Tình trạng", "Chưa thuê", null, MetricTone.Warning)
                ]);
        }

        var shelves = _playerStall.Shelves(city.Id);
        var shelfRows = new List<ScreenRow>(shelves.Count);
        var totalShelfUnits = 0;

        foreach (var shelf in shelves)
        {
            totalShelfUnits += shelf.Quantity;
            var item = _items.Find(shelf.ItemId);
            if (item is null) continue;

            var marketPrice = _trade.QuoteBuy(city.Id, item.Id);
            var marginPercent = marketPrice > 0 ? (int)((shelf.SellingPrice - marketPrice) * 100 / marketPrice) : 0;
            var marginStr = marginPercent >= 0 ? $"+{marginPercent}%" : $"{marginPercent}%";

            string speed;
            if (marginPercent <= 0) speed = "Rất nhanh (2-3/ngày)";
            else if (marginPercent <= 25) speed = "Vừa phải (1-2/ngày)";
            else if (marginPercent <= 50) speed = "Chậm (0-1/ngày)";
            else speed = "Rất chậm (khó bán)";

            shelfRows.Add(new ScreenRow(
                [
                    $"{item.NameVi} ({item.NameEn})",
                    shelf.Quantity.ToString(),
                    $"{shelf.SellingPrice} quan",
                    $"{marketPrice} quan",
                    marginStr,
                    speed
                ],
                [
                    new ScreenCommand($"stall:unstock:{item.Id}:1", "Thu hồi 1"),
                    new ScreenCommand($"stall:unstock:{item.Id}:{shelf.Quantity}", "Thu hồi hết"),
                    new ScreenCommand($"stall:markup:{item.Id}:10", "+10% Giá"),
                    new ScreenCommand($"stall:markup:{item.Id}:-10", "−10% Giá"),
                    new ScreenCommand($"item:modal:{item.Id}", "Kỳ Vật 🔍", CommandKind.Secondary)
                ],
                Title: item.NameVi,
                Details:
                [
                    new ScreenDetail("Mặt hàng", item.NameVi),
                    new ScreenDetail("Ngành hàng", item.Category),
                    new ScreenDetail("Số lượng trên quầy", $"{shelf.Quantity} đơn vị"),
                    new ScreenDetail("Giá niêm yết bán lẻ", $"{shelf.SellingPrice} quan"),
                    new ScreenDetail("Giá sỉ tại chợ", $"{marketPrice} quan"),
                    new ScreenDetail("Tỷ suất lợi nhuận", marginStr),
                    new ScreenDetail("Tốc độ tiêu thụ", speed)
                ]));
        }

        var bagRows = new List<ScreenRow>();
        foreach (var item in _items.All)
        {
            var held = _bag.Quantity(city.Id, item.Id);
            if (held <= 0) continue;

            var marketPrice = _trade.QuoteBuy(city.Id, item.Id);
            var suggestedPrice = Math.Max(1L, (long)Math.Ceiling(marketPrice * 1.2));

            bagRows.Add(new ScreenRow(
                [
                    $"{item.NameVi} ({item.NameEn})",
                    held.ToString(),
                    $"{marketPrice} quan",
                    $"{suggestedPrice} quan (+20%)"
                ],
                [
                    new ScreenCommand($"stall:stock:{item.Id}:1", "Bày 1"),
                    new ScreenCommand($"stall:stock:{item.Id}:5", "Bày 5"),
                    new ScreenCommand($"stall:stock:{item.Id}:{held}", "Bày hết"),
                    new ScreenCommand($"item:modal:{item.Id}", "Kỳ Vật 🔍", CommandKind.Secondary)
                ],
                Title: item.NameVi,
                Details:
                [
                    new ScreenDetail("Trong túi", $"{held} đơn vị"),
                    new ScreenDetail("Giá chợ hiện tại", $"{marketPrice} quan"),
                    new ScreenDetail("Giá gợi ý bày sạp", $"{suggestedPrice} quan")
                ]));
        }

        var sections = new List<ScreenSection>
        {
            new("Hàng đang bày trên quầy", ShelfColumns, shelfRows),
            new("Hàng trong túi có thể bày bán", BagColumns, bagRows)
        };

        var metrics = new List<ScreenMetric>
        {
            new("Tiền thuê", $"{stall.MonthlyRent}", "quan/tháng", MetricTone.Neutral),
            new("Tiểu nhị", $"{stall.HiredClerks} người", "trông coi", MetricTone.Neutral),
            new("Mặt hàng", $"{shelves.Count}", "loại trên quầy", MetricTone.Neutral),
            new("Tổng lượng hàng", $"{totalShelfUnits}", "đơn vị", totalShelfUnits > 0 ? MetricTone.Positive : MetricTone.Warning)
        };

        var commands = new List<ScreenCommand>();
        if (bagRows.Count > 0)
        {
            commands.Add(new ScreenCommand("stall:stock_all", "Bày tất cả hàng từ túi lên quầy", Kind: CommandKind.Primary));
        }

        return new ScreenModel(
            ScreenId,
            $"{Title} — {city.NameVi}",
            $"Tiệm buôn đang mở · khách mua hàng tự động mỗi ngày (Space) · phí thuê {stall.MonthlyRent} quan/tháng",
            sections,
            commands,
            metrics);
    }
}

