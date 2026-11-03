using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Contracts.Ui;

namespace SimT.Modules.Dlc.Market;

/// <summary>
/// Stall screen: one row per item with the live ask/bid, the remaining stall supply and the
/// quantity in the player's bag for that city.
/// </summary>
internal sealed class StallScreen : IScreenContribution
{
    private static readonly string[] Columns = ["Vật phẩm", "Giá mua", "Giá bán", "Tồn sạp", "Mức cung", "Trong túi"];

    private readonly IMarketTrade _trade;
    private readonly IItemCatalog _items;
    private readonly IWorldMap _map;
    private readonly IBag _bag;
    private readonly IPlayerState _player;
    private readonly IGameClock _clock;

    public StallScreen(IMarketTrade trade, IItemCatalog items, IWorldMap map, IBag bag, IPlayerState player, IGameClock clock)
    {
        _trade = trade;
        _items = items;
        _map = map;
        _bag = bag;
        _player = player;
        _clock = clock;
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
        foreach (var item in _items.All)
        {
            var buy = _trade.QuoteBuy(city.Id, item.Id);
            var sell = _trade.QuoteSell(city.Id, item.Id);
            var stock = _trade.StallStock(city.Id, item.Id);
            var baseline = _trade.StallBaseline(city.Id, item.Id);
            var held = _bag.Quantity(city.Id, item.Id);

            rows.Add(new ScreenRow(
                [
                    $"{item.NameVi} ({item.NameEn})",
                    Fmt(buy),
                    Fmt(sell),
                    Fmt(stock),
                    SupplyLabel(stock, baseline),
                    Fmt(held)
                ],
                [
                    new ScreenCommand($"buy:{item.Id}", "Mua 1"),
                    new ScreenCommand($"sell:{item.Id}", "Bán 1")
                ]));
        }

        var season = _clock.Today.Month switch
        {
            <= 3 => "Xuân",
            <= 6 => "Hạ",
            <= 9 => "Thu",
            _ => "Đông"
        };

        var status = $"{city.NameVi} · mùa {season} · hệ số vùng {city.PriceFactorPermille / 1000.0:0.00}";

        return new ScreenModel(
            ScreenId,
            $"{Title} — {city.NameVi}",
            status,
            [new ScreenSection("Hàng hóa", Columns, rows)],
            []);
    }

    private static string Fmt(long value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

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
}

/// <summary>
/// Warehouse screen for the current city. Until a warehouse is rented the screen offers the
/// rent command and nothing else, which keeps the DLC state visible instead of implicit.
/// </summary>
internal sealed class WarehouseScreen : IScreenContribution
{
    private static readonly string[] Columns = ["Vật phẩm", "Trong kho", "Hạn (ngày)", "Trong túi"];

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
                $"Chưa thuê kho. Sức chứa {_warehouse.DefaultCapacity}, phí {_warehouse.DefaultMonthlyRent} Quan/tháng.",
                [],
                [new ScreenCommand("rent", $"Thuê kho ({_warehouse.DefaultMonthlyRent} Quan)")]);
        }

        var rows = new List<ScreenRow>(_items.All.Count);
        foreach (var item in _items.All)
        {
            var stored = _warehouse.StockAt(city.Id, item.Id);
            var held = _bag.Quantity(city.Id, item.Id);
            if (stored == 0 && held == 0)
            {
                continue;
            }

            rows.Add(new ScreenRow(
                [
                    item.NameVi,
                    Fmt(stored),
                    item.ShelfLifeDays <= 0 ? "bền" : Fmt(item.ShelfLifeDays),
                    Fmt(held)
                ],
                [
                    new ScreenCommand($"store:{item.Id}", "Gửi 1"),
                    new ScreenCommand($"take:{item.Id}", "Lấy 1")
                ]));
        }

        return new ScreenModel(
            ScreenId,
            $"{Title} — {city.NameVi}",
            $"Sức chứa {info.Used}/{info.Capacity} · phí {info.MonthlyRent} Quan/tháng",
            [new ScreenSection("Tồn kho", Columns, rows)],
            []);
    }

    private static string Fmt(long value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
