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
    private readonly IItemCatalog _items;
    private readonly IWorldMap _map;
    private readonly IBag _bag;
    private readonly IPlayerState _player;
    private readonly IGameClock _clock;
    private readonly IWallet _wallet;

    public StallScreen(
        IMarketTrade trade,
        IItemCatalog items,
        IWorldMap map,
        IBag bag,
        IPlayerState player,
        IGameClock clock,
        IWallet wallet)
    {
        _trade = trade;
        _items = items;
        _map = map;
        _bag = bag;
        _player = player;
        _clock = clock;
        _wallet = wallet;
    }

    public string ScreenId => "market.stall";

    public string Title => "Sạp Chợ";

    public int Order => 20;

    public ScreenModel Render()
    {
        var city = _map.Find(_player.CurrentCityId);
        if (city is null)
        {
            return ScreenModel.Empty(ScreenId, Title, "Không xác định được thành trấn hiện tại.");
        }

        var rows = new List<ScreenRow>(_items.All.Count);
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

            rows.Add(new ScreenRow(
                [
                    $"{item.NameVi} ({item.NameEn})",
                    Fmt(buy),
                    Fmt(sell),
                    Fmt(stock),
                    supply,
                    Fmt(held)
                ],
                [
                    new ScreenCommand($"buy:{item.Id}", "Mua 1"),
                    new ScreenCommand($"sell:{item.Id}", "Bán 1"),
                    new ScreenCommand($"buy:{item.Id}", "Mua…", RequiresQuantity: true, Preview: BuyPreview(buy, stock)),
                    new ScreenCommand($"sell:{item.Id}", "Bán…", RequiresQuantity: true, Preview: SellPreview(sell, held))
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
                    new ScreenDetail("Hệ số vùng", $"{city.PriceFactorPermille / 1000.0:0.00}")
                ]));
        }

        var season = GameDateCodec.SeasonVi(_clock.Today.Month);
        var metrics = new List<ScreenMetric>
        {
            new("Tiền mặt", Fmt(_wallet.Liquid), "quan", MetricTone.Neutral),
            new("Hàng trong túi", Fmt(_bag.TotalQuantity(city.Id)), "đơn vị", MetricTone.Neutral),
            new("Sạp cạn", Fmt(emptyShelves), $"trên {_items.All.Count} mặt hàng",
                emptyShelves > 0 ? MetricTone.Warning : MetricTone.Neutral),
            new("Mặt hàng", Fmt(_items.All.Count), "loại", MetricTone.Neutral)
        };

        return new ScreenModel(
            ScreenId,
            $"{Title} — {city.NameVi}",
            $"Mùa {season} · hệ số vùng {city.PriceFactorPermille / 1000.0:0.00} · giá tăng khi sạp cạn",
            [new ScreenSection("Hàng hóa", Columns, rows)],
            [],
            metrics,
            alerts);
    }

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

    public WarehouseScreen(IMarketWarehouse warehouse, IItemCatalog items, IWorldMap map, IBag bag, IPlayerState player)
    {
        _warehouse = warehouse;
        _items = items;
        _map = map;
        _bag = bag;
        _player = player;
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
                [new ScreenCommand("rent", $"Thuê kho ({_warehouse.DefaultMonthlyRent} quan)")],
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
                        Preview: TakePreview(line.Quantity, held, item.NameVi))
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

        return new ScreenModel(
            ScreenId,
            $"{Title} — {city.NameVi}",
            $"Sức chứa {info.Used}/{info.Capacity} · phí {info.MonthlyRent} quan/tháng · hàng dễ hỏng mất dần mỗi ngày",
            [new ScreenSection("Tồn kho", Columns, rows)],
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
