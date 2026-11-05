using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Contracts.Ui;

namespace SimT.Modules.Dlc.Cultivation;

internal sealed class FarmScreen : IScreenContribution
{
    private static readonly string[] Columns =
        ["Khu", "Thành", "Mẫu", "Đất", "Thủy Lợi", "Thuê/tháng", "Trạng thái", "Chín", "Dự thu"];

    private static readonly string[] FacilityColumns =
        ["Công Trình", "Cấp Bậc", "Tên Kiến Trúc", "Hiệu Quả & Dịch Vụ", "Nâng Cấp Kế", "Trạng Thái"];

    private static readonly string[] HarvestColumns =
        ["Khu", "Thành", "Cây Trồng", "Sản Lượng", "Ngày Gặt", "Đất Còn"];

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
                    commands.Add(new ScreenCommand($"plant:{plot.Id}:dau_tam", "Gieo dâu"));
                    commands.Add(new ScreenCommand($"plant:{plot.Id}:duoc_thao", "Gieo dược"));
                }
                else if (plot.CropStatus == "READY")
                {
                    commands.Add(new ScreenCommand($"harvest:{plot.Id}", "Gặt"));
                }

                if (plot.Rented && plot.Soil < CropCalendar.SoilCeiling)
                {
                    commands.Add(new ScreenCommand($"fertilise:{plot.Id}", "Bón phân"));
                }

                if (plot.Rented && plot.IrrigationLevel < 2)
                {
                    commands.Add(new ScreenCommand($"irrigate:{plot.Id}", "Đào mương (80 Quan)"));
                }
            }

            var irrigationText = plot.IrrigationLevel > 0 ? $"Bậc {plot.IrrigationLevel}/2" : "Chưa có";

            rows.Add(new ScreenRow(
                [
                    plot.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    city?.NameVi ?? plot.CitadelId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    plot.AreaMu.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    $"{plot.Soil}/100",
                    irrigationText,
                    plot.MonthlyRent.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Describe(plot),
                    plot.ReadyDayIndex is null
                        ? "—"
                        : GameDateCodec.ShortVi(GameDate.FromDayIndex(plot.ReadyDayIndex.Value)),
                    plot.YieldQuantity?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "—"
                ],
                commands,
                Title: $"Khu {plot.Id} · {city?.NameVi}",
                Details:
                [
                    new ScreenDetail("Diện tích", $"{plot.AreaMu} mẫu"),
                    new ScreenDetail("Đất", $"{plot.Soil}/100"),
                    new ScreenDetail("Thủy lợi", irrigationText),
                    new ScreenDetail("Thuê", $"{plot.MonthlyRent} quan/tháng"),
                    new ScreenDetail("Trạng thái", Describe(plot)),
                    new ScreenDetail("Ngày chín", plot.ReadyDayIndex is null
                        ? "—"
                        : GameDateCodec.ShortVi(GameDate.FromDayIndex(plot.ReadyDayIndex.Value))),
                    new ScreenDetail("Dự thu", plot.YieldQuantity?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "—")
                ]));
        }

        var currentCityId = _player.CurrentCityId;
        var currentCity = _map.Find(currentCityId);
        var facilities = _service.GetFacilities(currentCityId);
        var facilityRows = new List<ScreenRow>();

        foreach (var fac in facilities)
        {
            var commands = new List<ScreenCommand>();
            if (fac.CanUpgrade)
            {
                commands.Add(new ScreenCommand($"upgrade_fac:{currentCityId}:{fac.FacilityType}", $"Nâng cấp ({fac.NextUpgradeCost} Quan)"));
            }

            var tierStars = fac.Tier switch
            {
                1 => "★☆☆☆☆ (Cấp 1)",
                2 => "★★☆☆☆ (Cấp 2)",
                3 => "★★★☆☆ (Cấp 3)",
                4 => "★★★★☆ (Cấp 4)",
                5 => "★★★★★ (Cấp 5 - Hóa Cảnh)",
                _ => "☆☆☆☆☆ (Chưa xây)"
            };

            var nextText = fac.CanUpgrade ? $"{fac.NextUpgradeCost} Quan" : "Đã đạt cấp tối đa";

            facilityRows.Add(new ScreenRow(
                [
                    fac.NameVi,
                    tierStars,
                    fac.TierTitleVi,
                    fac.EffectVi,
                    nextText,
                    fac.CanUpgrade ? "Có thể nâng cấp" : "Tối đa"
                ],
                commands,
                Title: $"{fac.NameVi} · {fac.TierTitleVi}",
                Details:
                [
                    new ScreenDetail("Công trình", fac.NameVi),
                    new ScreenDetail("Cấp bậc", tierStars),
                    new ScreenDetail("Tên kiến trúc", fac.TierTitleVi),
                    new ScreenDetail("Hiệu quả", fac.EffectVi),
                    new ScreenDetail("Phí nâng cấp kế", nextText)
                ]));
        }

        var harvestRows = new List<ScreenRow>();
        foreach (var h in _service.HarvestLogs)
        {
            var city = _map.Find(h.CitadelId)?.NameVi ?? h.CitadelId.ToString();
            harvestRows.Add(ScreenRow.Of([
                h.PlotId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                city,
                h.CropCode,
                h.YieldQty.ToString(System.Globalization.CultureInfo.InvariantCulture),
                GameDateCodec.ShortVi(GameDate.FromDayIndex(h.HarvestDayIndex)),
                $"{h.SoilAfter}/100"
            ]));
        }

        var rented = _service.Plots.Count(plot => plot.Rented);
        var alerts = new List<ScreenAlert>();
        if (_service.ReadyCount > 0)
        {
            alerts.Add(new ScreenAlert($"{_service.ReadyCount} khu đã chín, gặt ngay kẻo mất vụ.", AlertLevel.Warning));
        }

        foreach (var plot in _service.Plots.Where(plot => plot.Rented && plot.Soil < 30))
        {
            alerts.Add(new ScreenAlert($"Khu {plot.Id} bạc màu ({plot.Soil}/100), nên bón phân.", AlertLevel.Info));
        }

        var status = $"Đang thuê {rented} khu · "
                     + $"phí ruộng {_service.MonthlyRentTotal} quan/tháng · "
                     + $"{_service.GrowingCount} đang lớn, {_service.ReadyCount} chờ gặt · "
                     + $"{facilities.Count(f => f.Tier > 0)} công trình điền trang tại {currentCity?.NameVi ?? "châu thành"}";

        return new ScreenModel(
            ScreenId,
            Title,
            status,
            [
                new ScreenSection("Điền Trang Đang Quản Lý", Columns, rows),
                new ScreenSection($"Công Trình Điền Trang (Ổ Bảo Tại {currentCity?.NameVi ?? "Sở Tại"})", FacilityColumns, facilityRows),
                new ScreenSection("Lịch Sử Mùa Vụ (Gặt Gần Đây)", HarvestColumns, harvestRows)
            ],
            [],
            [
                new ScreenMetric("Khu đang thuê", Fmt(rented), "khu"),
                new ScreenMetric("Phí ruộng", Fmt(_service.MonthlyRentTotal), "quan/tháng"),
                new ScreenMetric("Đang lớn", Fmt(_service.GrowingCount), "cây"),
                new ScreenMetric("Chờ gặt", Fmt(_service.ReadyCount), "khu",
                    _service.ReadyCount > 0 ? MetricTone.Warning : MetricTone.Neutral),
                new ScreenMetric("Công trình Ổ Bảo", Fmt(facilities.Count(f => f.Tier > 0)), "đã xây")
            ],
            alerts);
    }

    private static string Fmt(long value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

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
        if (commandId.StartsWith("upgrade_fac:", StringComparison.Ordinal))
        {
            var fparts = commandId.Split(':');
            if (fparts.Length >= 3 && long.TryParse(fparts[1], out var targetCityId))
            {
                return _service.UpgradeFacility(targetCityId, fparts[2]).Message;
            }
            return "Lệnh nâng cấp không hợp lệ.";
        }

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
            "irrigate" => _service.Irrigate(plotId, city).Message,
            var verb => $"Động từ '{verb}' không thuộc DLC cultivation."
        };
    }
}
