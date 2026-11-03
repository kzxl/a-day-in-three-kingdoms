using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Contracts.Ui;

namespace SimT.Modules.Dlc.Cultivation;

internal sealed class FarmScreen : IScreenContribution
{
    private static readonly string[] Columns =
        ["Khu", "Thành", "Mẫu", "Đất", "Thuê/tháng", "Trạng thái", "Chín", "Dự thu"];

    private readonly FarmService _service;
    private readonly IWorldMap _map;
    private readonly IPlayerState _player;
    private readonly IGameClock _clock;

    public FarmScreen(FarmService service, IWorldMap map, IPlayerState player, IGameClock clock)
    {
        _service = service;
        _map = map;
        _player = player;
        _clock = clock;
    }

    public string ScreenId => "cultivation.farm";

    public string Title => "Nông Tang";

    public int Order => 45;

    public ScreenModel Render()
    {
        var rows = new List<ScreenRow>();

        foreach (var plot in _service.Plots)
        {
            var city = _map.Find(plot.CitadelId);
            var atCity = plot.CitadelId == _player.CurrentCityId;
            var commands = new List<ScreenCommand>();

            if (atCity)
            {
                if (!plot.Rented)
                {
                    commands.Add(new ScreenCommand($"rent:{plot.Id}", "Thuê"));
                }
                else if (plot.CropStatus is null)
                {
                    commands.Add(new ScreenCommand($"plant:{plot.Id}:gao", "Gieo lúa"));
                    commands.Add(new ScreenCommand($"plant:{plot.Id}:tra", "Gieo trà"));
                }
                else if (plot.CropStatus == "READY")
                {
                    commands.Add(new ScreenCommand($"harvest:{plot.Id}", "Gặt"));
                }

                if (plot.Rented && plot.Soil < CropCalendar.SoilCeiling)
                {
                    commands.Add(new ScreenCommand($"fertilise:{plot.Id}", "Bón phân"));
                }
            }

            rows.Add(new ScreenRow(
                [
                    plot.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    city?.NameVi ?? plot.CitadelId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    plot.AreaMu.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    $"{plot.Soil}/100",
                    plot.MonthlyRent.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Describe(plot),
                    plot.ReadyDayIndex is null
                        ? "—"
                        : GameDate.FromDayIndex(plot.ReadyDayIndex.Value).ToString(),
                    plot.YieldQuantity?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "—"
                ],
                commands));
        }

        var status = $"Đang thuê {_service.Plots.Count(plot => plot.Rented)} khu · "
                     + $"phí ruộng {_service.MonthlyRentTotal} Quan/tháng · "
                     + $"{_service.GrowingCount} cây đang lớn, {_service.ReadyCount} chờ gặt";

        return new ScreenModel(
            ScreenId,
            Title,
            status,
            [new ScreenSection("Ruộng", Columns, rows)],
            []);
    }

    private string Describe(PlotView plot)
    {
        if (!plot.Rented)
        {
            return "chưa thuê";
        }

        return plot.CropStatus switch
        {
            null => "đất trống",
            "READY" => "chín, chờ gặt",
            _ => plot.ReadyDayIndex is null
                ? "đang lớn"
                : $"đang lớn (còn {Math.Max(0, plot.ReadyDayIndex.Value - _clock.Today.DayIndex)} ngày)"
        };
    }
}

internal sealed class FarmCommandHandler : IScreenCommandHandler
{
    private readonly FarmService _service;
    private readonly IPlayerState _player;

    public FarmCommandHandler(FarmService service, IPlayerState player)
    {
        _service = service;
        _player = player;
    }

    public string ScreenId => "cultivation.farm";

    public string? Execute(string commandId)
    {
        var parts = commandId.Split(':');
        if (parts.Length < 2 || !long.TryParse(parts[1], out var plotId))
        {
            return "Lệnh không hợp lệ.";
        }

        var city = _player.CurrentCityId;
        return parts[0] switch
        {
            "rent" => _service.Rent(plotId, city).Message,
            "plant" => _service.Plant(plotId, city, parts.Length > 2 ? parts[2] : null).Message,
            "harvest" => _service.Harvest(plotId, city).Message,
            "fertilise" => _service.Fertilise(plotId, city).Message,
            var verb => $"Động từ '{verb}' không thuộc DLC cultivation."
        };
    }
}
