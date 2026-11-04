using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Contracts.Ui;

namespace SimT.Modules.Dlc.Caravan;

/// <summary>Route board: what can be shipped or walked to from here, and what is already moving.</summary>
internal sealed class CaravanScreen : IScreenContribution
{
    private static readonly string[] RouteColumns = ["Đích", "Ngày đi", "Cước/đơn vị", "Trong túi"];
    private static readonly string[] RunColumns = ["Mã", "Đến", "Khởi hành", "Tới nơi", "Hàng", "Hao", "Trạng thái"];

    private readonly CaravanService _service;
    private readonly IWorldMap _map;
    private readonly IBag _bag;
    private readonly IPlayerState _player;
    private readonly IGameClock _clock;

    public CaravanScreen(CaravanService service, IWorldMap map, IBag bag, IPlayerState player, IGameClock clock)
    {
        _service = service;
        _map = map;
        _bag = bag;
        _player = player;
        _clock = clock;
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

        var runRows = new List<ScreenRow>();
        foreach (var run in _service.Runs)
        {
            var destination = _map.Find(run.ToCityId);
            runRows.Add(ScreenRow.Of([
                run.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                destination?.NameVi ?? run.ToCityId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                GameDate.FromDayIndex(run.DepartDayIndex).ToString(),
                GameDate.FromDayIndex(run.ArriveDayIndex).ToString(),
                run.Units.ToString(System.Globalization.CultureInfo.InvariantCulture),
                run.Lost > 0 ? $"-{run.Lost}" : "—",
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

        return new ScreenModel(
            ScreenId,
            $"{Title} — {city.NameVi}",
            $"Đang chở {_service.UnitsInTransit} đơn vị · túi tại thành {held} đơn vị",
            [
                new ScreenSection("Tuyến đường", RouteColumns, routeRows),
                new ScreenSection("Đoàn hàng", RunColumns, runRows)
            ],
            [],
            [
                new ScreenMetric("Đang chở", Fmt(_service.UnitsInTransit), "đơn vị"),
                new ScreenMetric("Đoàn đang đi", Fmt(running), "chuyến"),
                new ScreenMetric("Cước", Fmt(_service.FeePerUnit), "quan/đơn vị"),
                new ScreenMetric("Trong túi", Fmt(held), "đơn vị")
            ],
            alerts);
    }
}

/// <summary>Parses <c>send:&lt;cityId&gt;</c> and <c>goto:&lt;cityId&gt;</c>.</summary>
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
        if (separator <= 0 || !long.TryParse(commandId[(separator + 1)..], out var cityId))
        {
            return "Lệnh không hợp lệ.";
        }

        var from = _player.CurrentCityId;
        return commandId[..separator] switch
        {
            "send" => _service.Send(from, cityId).Message,
            "goto" => _service.Travel(from, cityId, _player).Message,
            var verb => $"Động từ '{verb}' không thuộc DLC caravan."
        };
    }
}
