using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Contracts.Ui;

namespace SimT.Modules.Dlc.Caravan;

/// <summary>Route board: what can be shipped or walked to from here, contracts, and what is already moving.</summary>
internal sealed class CaravanScreen : IScreenContribution
{
    private static readonly string[] RouteColumns = ["Đích", "Ngày đi", "Cước/đơn vị", "Trong túi"];
    private static readonly string[] ContractColumns = ["Hội Quán", "Nơi Nhận", "Nơi Giao", "Hàng Hóa", "Thù Lao", "Hạn Chót", "Trạng Thái"];
    private static readonly string[] RunColumns = ["Mã", "Đến", "Khởi hành", "Tới nơi", "Hàng", "Hao", "Hộ Vệ", "Trạng thái"];

    private readonly CaravanService _service;
    private readonly IWorldMap _map;
    private readonly IBag _bag;
    private readonly IPlayerState _player;
    private readonly IGameClock _clock;
    private readonly IItemCatalog _items;

    public CaravanScreen(
        CaravanService service,
        IWorldMap map,
        IBag bag,
        IPlayerState player,
        IGameClock clock,
        IItemCatalog items)
    {
        _service = service;
        _map = map;
        _bag = bag;
        _player = player;
        _clock = clock;
        _items = items;
    }

    private static string Fmt(long value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public string ScreenId => "caravan.routes";

    public string Title => "Thương Đội";

    public int Order => 40;

    public ScreenModel Render()
    {
        var city = _map.Find(_player.CurrentCityId);
        if (city is null)
        {
            return ScreenModel.Empty(ScreenId, Title, "Không xác định được thành hiện tại.");
        }

        var neighbours = _map.RouteNeighbours(city.Id);
        var held = _bag.TotalQuantity(city.Id);

        var routeRows = new List<ScreenRow>(neighbours.Count);
        foreach (var neighbourId in neighbours)
        {
            var destination = _map.Find(neighbourId);
            if (destination is null)
            {
                continue;
            }

            var days = _map.TravelDays(city.Id, neighbourId);
            routeRows.Add(new ScreenRow(
                [
                    $"{destination.NameVi} ({destination.NameEn})",
                    days.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _service.FeePerUnit.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    held.ToString(System.Globalization.CultureInfo.InvariantCulture)
                ],
                [
                    new ScreenCommand($"send:{neighbourId}", "Gửi hàng"),
                    new ScreenCommand($"goto:{neighbourId}", "Đi bộ")
                ]));
        }

        var contractRows = new List<ScreenRow>();
        foreach (var c in _service.Contracts)
        {
            var fromCity = _map.Find(c.FromCityId)?.NameVi ?? c.FromCityId.ToString();
            var toCity = _map.Find(c.ToCityId)?.NameVi ?? c.ToCityId.ToString();
            var item = _items.Find(c.ItemId)?.NameVi ?? $"Hàng #{c.ItemId}";
            var deadline = GameDateCodec.ShortVi(GameDate.FromDayIndex(c.DeadlineDayIndex));

            var statusVi = c.Status switch
            {
                "AVAILABLE" => "Chờ nhận",
                "IN_TRANSIT" => "Đang áp tải",
                "DELIVERED" => "Đã hoàn tất",
                "EXPIRED" => "Quá hạn",
                _ => c.Status
            };

            var commands = new List<ScreenCommand>();
            if (c.Status == "AVAILABLE" && c.FromCityId == city.Id)
            {
                commands.Add(new ScreenCommand($"accept:{c.Id}", "Nhận Tiêu"));
            }
            else if (c.Status == "IN_TRANSIT" && c.ToCityId == city.Id)
            {
                commands.Add(new ScreenCommand($"deliver:{c.Id}", "Giao Tiêu"));
            }

            contractRows.Add(new ScreenRow(
                [
                    c.GuildName,
                    fromCity,
                    toCity,
                    $"{c.Quantity} {item}",
                    $"{c.RewardCopper} Quan",
                    deadline,
                    statusVi
                ],
                commands));
        }

        var runRows = new List<ScreenRow>();
        foreach (var run in _service.Runs)
        {
            var destination = _map.Find(run.ToCityId);
            var escortText = run.EscortNpcId.HasValue ? $"NPC #{run.EscortNpcId.Value}" : "Không";
            runRows.Add(ScreenRow.Of([
                run.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                destination?.NameVi ?? run.ToCityId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                GameDateCodec.ShortVi(GameDate.FromDayIndex(run.DepartDayIndex)),
                GameDateCodec.ShortVi(GameDate.FromDayIndex(run.ArriveDayIndex)),
                run.Units.ToString(System.Globalization.CultureInfo.InvariantCulture),
                run.Lost > 0 ? $"-{run.Lost}" : "—",
                escortText,
                run.Status == "RUNNING" ? "đang đi" : "đã tới"
            ]));
        }

        var alerts = new List<ScreenAlert>();
        foreach (var run in _service.Runs.Where(run => run.Status == "RUNNING"))
        {
            var daysLeft = run.ArriveDayIndex - _clock.Today.DayIndex;
            if (daysLeft <= 2)
            {
                var destinationName = _map.Find(run.ToCityId)?.NameVi ?? "đích";
                alerts.Add(new ScreenAlert(
                    $"Đoàn {run.Id} tới {destinationName} sau {Math.Max(0, daysLeft)} ngày ({run.Units} đơn vị).",
                    AlertLevel.Warning));
            }
        }

        var running = _service.Runs.Count(run => run.Status == "RUNNING");
        var activeContracts = _service.Contracts.Count(c => c.Status == "IN_TRANSIT");

        return new ScreenModel(
            ScreenId,
            $"{Title} — {city.NameVi}",
            $"Đang chở {_service.UnitsInTransit} đơn vị · hợp đồng đang áp tải: {activeContracts} · túi tại thành {held} đơn vị",
            [
                new ScreenSection("Hợp Đồng Áp Tiêu (Thương Hội)", ContractColumns, contractRows),
                new ScreenSection("Tuyến Đường & Lộ Trình", RouteColumns, routeRows),
                new ScreenSection("Đoàn Hàng Đang Di Chuyển", RunColumns, runRows)
            ],
            [],
            [
                new ScreenMetric("Đang chở", Fmt(_service.UnitsInTransit), "đơn vị"),
                new ScreenMetric("Đoàn đang đi", Fmt(running), "chuyến"),
                new ScreenMetric("Áp tiêu nhận", Fmt(activeContracts), "hợp đồng"),
                new ScreenMetric("Trong túi", Fmt(held), "đơn vị")
            ],
            alerts);
    }
}

/// <summary>Parses <c>send:&lt;cityId&gt;</c>, <c>goto:&lt;cityId&gt;</c>, <c>accept:&lt;contractId&gt;</c>, <c>deliver:&lt;contractId&gt;</c>.</summary>
internal sealed class CaravanCommandHandler : IScreenCommandHandler
{
    private readonly CaravanService _service;
    private readonly IPlayerState _player;

    public CaravanCommandHandler(CaravanService service, IPlayerState player)
    {
        _service = service;
        _player = player;
    }

    public string ScreenId => "caravan.routes";

    public string? Execute(string commandId)
    {
        var separator = commandId.IndexOf(':');
        if (separator <= 0 || !long.TryParse(commandId[(separator + 1)..], out var targetId))
        {
            return "Lệnh không hợp lệ.";
        }

        var from = _player.CurrentCityId;
        return commandId[..separator] switch
        {
            "send" => _service.Send(from, targetId).Message,
            "goto" => _service.Travel(from, targetId, _player).Message,
            "accept" => _service.AcceptContract(targetId).Message,
            "deliver" => _service.DeliverContract(targetId).Message,
            var verb => $"Động từ '{verb}' không thuộc DLC caravan."
        };
    }
}
