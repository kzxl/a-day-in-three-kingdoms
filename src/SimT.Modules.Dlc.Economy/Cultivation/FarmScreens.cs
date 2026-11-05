using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Contracts.Ui;

namespace SimT.Modules.Dlc.Economy;

internal sealed class FarmScreen : IScreenContribution
{
    private static readonly string[] Columns =
        ["Khu", "Thành", "Mẫu", "Đất", "Thủy Lợi", "Thuê/tháng", "Trạng thái", "Chín", "Dự thu"];

    private static readonly string[] FacilityColumns =
        ["Công Trình", "Cấp Bậc", "Tên Kiến Trúc", "Hiệu Quả & Dịch Vụ", "Nâng Cấp Kế", "Trạng Thái"];

    private static readonly string[] SupplyColumns =
        ["Phân Loại", "Vật Phẩm", "Công Dụng & Yêu Cầu", "Trong Hành Trang", "Đơn Giá", "Trạng Thái"];

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
            commands.Add(new ScreenCommand($"plot:inspect:{plot.Id}", "Khảo Sát 🌾"));

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

        var supplies = _service.GetSupplyInventory(currentCityId);
        var supplyRows = new List<ScreenRow>
        {
            new ScreenRow(
                ["Nông Cụ", "Cuốc Nông Nghiệp", "Cày xới, gieo hạt, bón phân", supplies.HasHoe ? "Đã có (1)" : "Chưa có ❌", "60 Quan", supplies.HasHoe ? "Sẵn sàng" : "Cần mua"],
                supplies.HasHoe ? [] : [new ScreenCommand("buy_tool:cuoc_nong", "Mua Cuốc (60 Quan)", CommandKind.Primary)],
                Title: "Cuốc Nông Nghiệp",
                Details: [new ScreenDetail("Tác dụng", "Bắt buộc phải có để gieo hạt và bón phân"), new ScreenDetail("Trạng thái", supplies.HasHoe ? "Đã trang bị trong bao" : "Chưa có")]),

            new ScreenRow(
                ["Nông Cụ", "Liềm Thu Hoạch", "Gặt lúa & nông sản khi chín", supplies.HasSickle ? "Đã có (1)" : "Chưa có ❌", "50 Quan", supplies.HasSickle ? "Sẵn sàng" : "Cần mua"],
                supplies.HasSickle ? [] : [new ScreenCommand("buy_tool:liem_gat", "Mua Liềm (50 Quan)", CommandKind.Primary)],
                Title: "Liềm Thu Hoạch",
                Details: [new ScreenDetail("Tác dụng", "Bắt buộc phải có để gặt lúa khi mùa vụ chín"), new ScreenDetail("Trạng thái", supplies.HasSickle ? "Đã trang bị trong bao" : "Chưa có")]),

            new ScreenRow(
                ["Nông Cụ", "Gầu Mương Thủy Lợi", "Thi công đào mương dẫn nước", supplies.HasWateringScoop ? "Đã có (1)" : "Chưa có ❌", "70 Quan", supplies.HasWateringScoop ? "Sẵn sàng" : "Cần mua"],
                supplies.HasWateringScoop ? [] : [new ScreenCommand("buy_tool:binh_tuoi", "Mua Gầu (70 Quan)", CommandKind.Primary)],
                Title: "Gầu Mương Thủy Lợi",
                Details: [new ScreenDetail("Tác dụng", "Dụng cụ đào mương thủy lợi cải tạo đất"), new ScreenDetail("Trạng thái", supplies.HasWateringScoop ? "Đã trang bị trong bao" : "Chưa có")]),

            new ScreenRow(
                ["Phân Bón", "Phân Bón Nông Nghiệp", "Cải tạo đất bạc màu (+30 phì)", $"{supplies.FertilizerCount} bao", "15 Quan", supplies.FertilizerCount > 0 ? "Còn hàng" : "Hết hàng ❌"],
                [new ScreenCommand("buy_fertilizer:1", "Mua 1 Bao (15 Quan)", CommandKind.Secondary), new ScreenCommand("buy_fertilizer:5", "Mua 5 Bao (75 Quan)", CommandKind.Primary)],
                Title: "Phân Bón Nông Nghiệp",
                Details: [new ScreenDetail("Tác dụng", "Phục hồi 30 điểm độ phì nhiêu của đất"), new ScreenDetail("Tồn kho", $"{supplies.FertilizerCount} bao")]),

            new ScreenRow(
                ["Hạt Giống", "Hạt Giống Lúa", "Gieo trồng Lúa thu hoạch Gạo", $"{supplies.RiceSeeds} bao", "5 Quan", supplies.RiceSeeds > 0 ? "Còn hạt" : "Cần mua"],
                [new ScreenCommand("buy_seed:gao:5", "Mua 5 Giống (25 Quan)", CommandKind.Primary)],
                Title: "Hạt Giống Lúa",
                Details: [new ScreenDetail("Sản lượng", "8 Gạo / mẫu"), new ScreenDetail("Thời gian", "18-30 ngày")]),

            new ScreenRow(
                ["Hạt Giống", "Hạt Giống Trà", "Gieo trồng Trà búp", $"{supplies.TeaSeeds} bao", "15 Quan", supplies.TeaSeeds > 0 ? "Còn hạt" : "Cần mua"],
                [new ScreenCommand("buy_seed:tra:5", "Mua 5 Giống (75 Quan)", CommandKind.Secondary)],
                Title: "Hạt Giống Trà",
                Details: [new ScreenDetail("Sản lượng", "2 Trà / mẫu"), new ScreenDetail("Thời gian", "24 ngày")]),

            new ScreenRow(
                ["Hạt Giống", "Cây Giống Dâu Tằm", "Trồng dâu nuôi tằm dệt Lụa", $"{supplies.MulberrySeedlings} cây", "20 Quan", supplies.MulberrySeedlings > 0 ? "Còn giống" : "Cần mua"],
                [new ScreenCommand("buy_seed:dau_tam:5", "Mua 5 Cây (100 Quan)", CommandKind.Secondary)],
                Title: "Cây Giống Dâu Tằm",
                Details: [new ScreenDetail("Sản lượng", "2 Lụa / mẫu"), new ScreenDetail("Thời gian", "30 ngày")]),

            new ScreenRow(
                ["Hạt Giống", "Hạt Giống Dược Thảo", "Trồng dược thảo thu hoạch Thuốc", $"{supplies.HerbSeeds} bao", "35 Quan", supplies.HerbSeeds > 0 ? "Còn hạt" : "Cần mua"],
                [new ScreenCommand("buy_seed:duoc_thao:5", "Mua 5 Giống (175 Quan)", CommandKind.Secondary)],
                Title: "Hạt Giống Dược Thảo",
                Details: [new ScreenDetail("Sản lượng", "2 Thuốc / mẫu"), new ScreenDetail("Thời gian", "20 ngày")])
        };

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
                new ScreenSection($"Kho Nông Cụ & Hạt Giống (Hành Trang Sở Tại · {currentCity?.NameVi ?? "Thành"})", SupplyColumns, supplyRows),
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
                new ScreenMetric("Nông cụ sở hữu", supplies.HasHoe && supplies.HasSickle ? "Đầy đủ" : "Thiếu cụ", "bộ"),
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
    private readonly IMarketWarehouse? _warehouse;

    public FarmCommandHandler(FarmService service, IPlayerState player, IMarketWarehouse? warehouse = null)
    {
        _service = service;
        _player = player;
        _warehouse = warehouse;
    }

    public string ScreenId => "cultivation.farm";

    public string? Execute(string commandId)
    {
        if (commandId.StartsWith("buy_seed", StringComparison.Ordinal))
        {
            var sparts = commandId.Split(':');
            var cropCode = "gao";
            var qty = 5;
            if (sparts.Length >= 4 && long.TryParse(sparts[1], out _))
            {
                cropCode = sparts[2];
                if (int.TryParse(sparts[3], out var q)) qty = q;
            }
            else
            {
                if (sparts.Length > 1) cropCode = sparts[1];
                if (sparts.Length > 2 && int.TryParse(sparts[2], out var q)) qty = q;
            }
            return _service.BuySeed(_player.CurrentCityId, cropCode, qty).Message;
        }

        if (commandId.StartsWith("buy_fertilizer", StringComparison.Ordinal))
        {
            var fparts = commandId.Split(':');
            var qty = 1;
            if (fparts.Length >= 3 && long.TryParse(fparts[1], out _))
            {
                if (int.TryParse(fparts[2], out var q)) qty = q;
            }
            else if (fparts.Length > 1 && int.TryParse(fparts[1], out var q))
            {
                qty = q;
            }
            return _service.BuyFertilizer(_player.CurrentCityId, qty).Message;
        }

        if (commandId.StartsWith("buy_tool:", StringComparison.Ordinal))
        {
            var tparts = commandId.Split(':');
            var toolCode = tparts.Length >= 3 && long.TryParse(tparts[1], out _)
                ? tparts[2]
                : (tparts.Length > 1 ? tparts[1] : "cuoc_nong");
            return _service.BuyTool(_player.CurrentCityId, toolCode).Message;
        }

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
            "harvest_store" => _service.HarvestAndStore(plotId, city, _warehouse).Message,
            "fertilise" => _service.Fertilise(plotId, city).Message,
            "irrigate" => _service.Irrigate(plotId, city).Message,
            var verb => $"Động từ '{verb}' không thuộc DLC cultivation."
        };
    }
}
