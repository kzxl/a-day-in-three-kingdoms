using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Contracts.Ui;
using SimT.Modules.Dlc.Economy.Caravan;

namespace SimT.Modules.Dlc.Economy;

/// <summary>Route board: what can be shipped or walked to from here, contracts, and what is already moving.</summary>
internal sealed class CaravanScreen : IScreenContribution
{
    private static readonly string[] RumorColumns = ["Thành Trấn", "Mặt Hàng", "Biến Động Giá", "Thời Hạn Còn", "Sự Kiện"];
    private static readonly string[] CrewColumns = ["Chức Vụ", "Nhân Sự", "Chỉ Số Trọng Yếu", "Hiệu Quả Kỹ Năng"];
    private static readonly string[] RouteColumns = ["Đích", "Ngày đi", "Cước/đơn vị", "Trong túi"];
    private static readonly string[] ContractColumns = ["Hội Quán", "Nơi Nhận", "Nơi Giao", "Hàng Hóa", "Thù Lao", "Hạn Chót", "Trạng Thái"];
    private static readonly string[] RunColumns = ["Mã", "Đến", "Khởi hành", "Tới nơi", "Hàng", "Hao", "Hộ Vệ", "Trạng thái"];
    private static readonly string[] CartelColumns = ["Đại Thương Bang", "Chưởng Sự", "Lĩnh Vực Độc Quyền", "Hào Cảm", "Quan Hệ Minh Ước"];
    private static readonly string[] AutoRouteColumns = ["Tuyến Tự Động", "Hàng Hóa", "Trần Mua / Sàn Bán", "Lô Hàng", "Trạng Thái"];

    private readonly CaravanService _service;
    private readonly IWorldMap _map;
    private readonly IBag _bag;
    private readonly IPlayerState _player;
    private readonly IGameClock _clock;
    private readonly IItemCatalog _items;
    private readonly INpcRegistry? _npcs;
    private readonly AutomatedLogisticsService? _logistics;

    public CaravanScreen(
        CaravanService service,
        IWorldMap map,
        IBag bag,
        IPlayerState player,
        IGameClock clock,
        IItemCatalog items,
        INpcRegistry? npcs = null,
        AutomatedLogisticsService? logistics = null)
    {
        _service = service;
        _map = map;
        _bag = bag;
        _player = player;
        _clock = clock;
        _items = items;
        _npcs = npcs;
        _logistics = logistics;
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
        var fleet = _service.Fleet;

        var rumorRows = new List<ScreenRow>();
        foreach (var r in _service.Rumors)
        {
            var multText = r.PriceMultPermille > 1000
                ? $"+{(r.PriceMultPermille - 1000) / 10}%"
                : $"-{(1000 - r.PriceMultPermille) / 10}%";
            rumorRows.Add(new ScreenRow(
                [
                    r.CitadelNameVi,
                    r.ItemNameVi,
                    multText,
                    $"{r.ExpireDaysRemaining} ngày",
                    r.HeadlineVi
                ],
                [],
                Title: r.HeadlineVi,
                Details:
                [
                    new ScreenDetail("Thành Trấn", r.CitadelNameVi),
                    new ScreenDetail("Mặt Hàng", r.ItemNameVi),
                    new ScreenDetail("Thời Hạn", $"{r.ExpireDaysRemaining} ngày tới"),
                    new ScreenDetail("Tác Động", $"Giá giao dịch biến động {multText}"),
                    new ScreenDetail("Chi Tiết", r.DetailVi),
                    new ScreenDetail("Mẹo Đầu Cơ", "Chở mặt hàng này tới bán tại thành đang có biến cố để thu siêu lợi nhuận!")
                ]));
        }

        var crew = _service.Crew;
        var crewRows = new List<ScreenRow>();

        // Escort row
        var escortCommands = new List<ScreenCommand>();
        if (crew.EscortNpcId.HasValue)
        {
            escortCommands.Add(new ScreenCommand("unassign_crew:escort", "Bãi Nhiệm ✖"));
        }
        else if (_npcs is not null)
        {
            var bestEscort = _npcs.FindByCitadel(city.Id)
                .OrderByDescending(n => n.Attributes.War)
                .FirstOrDefault();
            if (bestEscort is not null && bestEscort.Attributes.War >= 40)
            {
                escortCommands.Add(new ScreenCommand($"assign_crew:escort:{bestEscort.Id}", $"Thuê {bestEscort.NameVi} (Võ {bestEscort.Attributes.War})"));
            }
        }
        crewRows.Add(new ScreenRow(
            [
                "Tiêu Đầu 🛡️",
                crew.EscortNpcId.HasValue ? crew.EscortNameVi! : "(Trống)",
                crew.EscortNpcId.HasValue ? $"Võ Lực: {crew.EscortWar}" : "—",
                crew.EscortNpcId.HasValue ? "Trấn áp cướp đường, giảm thiểu tối đa tổn thất hàng hóa" : "Chưa có tiêu đầu, dễ bị đạo tặc phục kích khi đi xa"
            ],
            escortCommands,
            Title: "Tiêu Đầu (Hộ Vệ)",
            Details:
            [
                new ScreenDetail("Nhiệm Vụ", "Áp tải, bảo vệ hàng hóa và đẩy lùi cướp đường khi vận chuyển viễn chinh"),
                new ScreenDetail("Chỉ Số Then Chốt", "Võ Lực (WAR)")
            ]));

        // Accountant row
        var acctCommands = new List<ScreenCommand>();
        if (crew.AccountantNpcId.HasValue)
        {
            acctCommands.Add(new ScreenCommand("unassign_crew:accountant", "Bãi Nhiệm ✖"));
        }
        else if (_npcs is not null)
        {
            var bestAcct = _npcs.FindByCitadel(city.Id)
                .OrderByDescending(n => n.Attributes.Com)
                .FirstOrDefault();
            if (bestAcct is not null && bestAcct.Attributes.Com >= 40)
            {
                acctCommands.Add(new ScreenCommand($"assign_crew:accountant:{bestAcct.Id}", $"Thuê {bestAcct.NameVi} (Thương {bestAcct.Attributes.Com})"));
            }
        }
        var acctDiscount = crew.AccountantCom > 0 ? Math.Min(25, crew.AccountantCom * 25 / 100) : 0;
        crewRows.Add(new ScreenRow(
            [
                "Chưởng Quỹ 💰",
                crew.AccountantNpcId.HasValue ? crew.AccountantNameVi! : "(Trống)",
                crew.AccountantNpcId.HasValue ? $"Thương Tài: {crew.AccountantCom}" : "—",
                crew.AccountantNpcId.HasValue ? $"Giảm {acctDiscount}% cước phí vận chuyển hàng hóa toàn tuyến" : "Chưa có chưởng quỹ, tính nguyên giá cước vận tải"
            ],
            acctCommands,
            Title: "Chưởng Quỹ (Kế Toán)",
            Details:
            [
                new ScreenDetail("Nhiệm Vụ", "Quản lý sổ sách, đàm phán cước phí xe trạm, tối ưu hóa chi phí lộ trình"),
                new ScreenDetail("Chỉ Số Then Chốt", "Thương Tài (COM)")
            ]));

        // Navigator row
        var navCommands = new List<ScreenCommand>();
        if (crew.NavigatorNpcId.HasValue)
        {
            navCommands.Add(new ScreenCommand("unassign_crew:navigator", "Bãi Nhiệm ✖"));
        }
        else if (_npcs is not null)
        {
            var bestNav = _npcs.FindByCitadel(city.Id)
                .OrderByDescending(n => n.Attributes.Log)
                .FirstOrDefault();
            if (bestNav is not null && bestNav.Attributes.Log >= 40)
            {
                navCommands.Add(new ScreenCommand($"assign_crew:navigator:{bestNav.Id}", $"Thuê {bestNav.NameVi} (Hậu {bestNav.Attributes.Log})"));
            }
        }
        crewRows.Add(new ScreenRow(
            [
                "Dẫn Đường 🧭",
                crew.NavigatorNpcId.HasValue ? crew.NavigatorNameVi! : "(Trống)",
                crew.NavigatorNpcId.HasValue ? $"Hậu Cần: {crew.NavigatorLog}" : "—",
                (crew.NavigatorNpcId.HasValue && crew.NavigatorLog >= 60) ? "Rút ngắn 1 ngày di chuyển trên tuyến viễn chinh (>=3 ngày)" : "Thông thuộc đường sá, chuẩn bị lương thảo nước uống"
            ],
            navCommands,
            Title: "Dẫn Đường (Thổ Địa)",
            Details:
            [
                new ScreenDetail("Nhiệm Vụ", "Thông thạo địa hình, vạch lối tắt hiểm yếu, rút ngắn ngày đường"),
                new ScreenDetail("Chỉ Số Then Chốt", "Hậu Cần (LOG)")
            ]));

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
                    new ScreenCommand($"route:inspect:{city.Id}:{neighbourId}", "Chi Tiết 🗺️"),
                    new ScreenCommand($"send:{neighbourId}", "Gửi hàng 📦"),
                    new ScreenCommand($"send_escort:{neighbourId}", "Thuê Tiêu Sư 🛡️", CommandKind.Primary),
                    new ScreenCommand($"goto:{neighbourId}", "Lên Đường 🐎")
                ],
                Title: $"{destination.NameVi} ({days} ngày đường)",
                Details:
                [
                    new ScreenDetail("Đích Đến", destination.NameVi),
                    new ScreenDetail("Lộ Trình", $"{days} ngày đi bộ"),
                    new ScreenDetail("Đội Xe Vận Tải", fleet.NameVi),
                    new ScreenDetail("Bảo Hiểm Áp Tiêu", "Thuê tiêu sư (50 Quan) để phòng tránh 100% thổ phỉ cướp hàng.")
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

        var topCommands = new List<ScreenCommand>();
        if (fleet.Tier < 3)
        {
            var nextCost = fleet.Tier == 1 ? 600 : 1500;
            topCommands.Add(new ScreenCommand("upgrade_fleet", $"Nâng Cấp Đội Xe ({nextCost} Quan)", CommandKind.Primary));
        }

        if (neighbours.Count > 0)
        {
            var firstDest = _map.Find(neighbours[0]);
            topCommands.Add(new ScreenCommand($"auto_route:create:{neighbours[0]}:1", $"Lập Tuyến Tự Động ➔ {firstDest?.NameVi ?? "Lân Thành"}"));
        }

        var cartelRows = new List<ScreenRow>();
        var autoRouteRows = new List<ScreenRow>();
        if (_logistics is not null)
        {
            foreach (var cartel in _logistics.GetCartels())
            {
                var cCmds = new List<ScreenCommand>
                {
                    new($"cartel:gift:{cartel.CartelCode}", "Lễ Vật (300 Quan)")
                };
                if (cartel.TreatyStatus != "ALLIED" && cartel.Standing >= 35)
                {
                    cCmds.Add(new($"cartel:ally:{cartel.CartelCode}", "Ký Minh Ước (500 Quan)", CommandKind.Primary));
                }

                var treatyVi = cartel.TreatyStatus switch
                {
                    "ALLIED" => "📜 Đồng Minh Độc Quyền (+15% Lãi)",
                    "FRIENDLY" => "🤝 Thân Hảo (+5% Lãi)",
                    _ => "Trung Lập"
                };

                cartelRows.Add(new ScreenRow(
                    [
                        cartel.CartelNameVi,
                        cartel.LeaderVi,
                        cartel.DomainVi,
                        $"{cartel.Standing}/100",
                        treatyVi
                    ],
                    cCmds,
                    Title: cartel.CartelNameVi,
                    Details:
                    [
                        new ScreenDetail("Thương Bang", cartel.CartelNameVi),
                        new ScreenDetail("Chưởng Sự", cartel.LeaderVi),
                        new ScreenDetail("Độc Quyền", cartel.DomainVi),
                        new ScreenDetail("Hào Cảm", $"{cartel.Standing}/100"),
                        new ScreenDetail("Minh Ước", treatyVi)
                    ]));
            }

            foreach (var ar in _logistics.GetRoutes())
            {
                var fromName = _map.Find(ar.FromCityId)?.NameVi ?? $"#{ar.FromCityId}";
                var toName = _map.Find(ar.ToCityId)?.NameVi ?? $"#{ar.ToCityId}";
                var itemName = _items.Find(ar.BuyItemId)?.NameVi ?? $"Hàng #{ar.BuyItemId}";
                var arCmds = new List<ScreenCommand>
                {
                    new($"auto_route:toggle:{ar.Id}", ar.Active ? "Tạm Dừng" : "Kích Hoạt"),
                    new($"auto_route:delete:{ar.Id}", "Xóa Tuyến")
                };

                autoRouteRows.Add(new ScreenRow(
                    [
                        $"{ar.Name} ({fromName} ➔ {toName})",
                        itemName,
                        $"Mua <={ar.MaxBuyPrice}q · Bán >={ar.SellMinPrice}q",
                        $"{ar.BatchSize} đv / 4 ngày",
                        ar.Active ? "🟢 Đang vận hành" : "⏸️ Đang tạm dừng"
                    ],
                    arCmds));
            }
        }

        return new ScreenModel(
            ScreenId,
            $"{Title} — {city.NameVi}",
            $"Đội xe: {fleet.NameVi} (Bậc {fleet.Tier}/3) · Đang chở {_service.UnitsInTransit} đv · Hợp đồng: {activeContracts} · Túi: {held} đv",
            [
                new ScreenSection("Thương Cơ Thị Trường & Tin Tức Thời Thế", RumorColumns, rumorRows),
                new ScreenSection("Tứ Đại Thương Bang & Minh Ước Độc Quyền", CartelColumns, cartelRows),
                new ScreenSection("Tuyến Thương Mại Tự Động (Thương Đoàn Thường Trực)", AutoRouteColumns, autoRouteRows),
                new ScreenSection("Biên Chế Nhân Sự Thương Đội", CrewColumns, crewRows, SectionViewMode.CaravanDispatchBoard),
                new ScreenSection("Hợp Đồng Áp Tiêu (Đại Thương Bang)", ContractColumns, contractRows, SectionViewMode.CaravanDispatchBoard),
                new ScreenSection("Tuyến Đường & Lộ Trình", RouteColumns, routeRows, SectionViewMode.CaravanDispatchBoard),
                new ScreenSection("Đoàn Hàng Đang Di Chuyển", RunColumns, runRows)
            ],
            topCommands,
            [
                new ScreenMetric("Đội Xe", fleet.NameVi, $"Bậc {fleet.Tier}/3", MetricTone.Positive),
                new ScreenMetric("Đang chở", Fmt(_service.UnitsInTransit), "đơn vị"),
                new ScreenMetric("Đoàn đang đi", Fmt(running), "chuyến"),
                new ScreenMetric("Áp tiêu nhận", Fmt(activeContracts), "hợp đồng"),
                new ScreenMetric("Trong túi", Fmt(held), "đơn vị")
            ],
            alerts,
            $"Chọn tuyến đường có chênh lệch giá cao và thuê hộ vệ đi kèm trước khi phái thương đoàn xuất phát từ {city.NameVi}.");
    }
}

/// <summary>Parses <c>send:&lt;cityId&gt;</c>, <c>send_escort:&lt;cityId&gt;</c>, <c>goto:&lt;cityId&gt;</c>, <c>accept:&lt;contractId&gt;</c>, <c>deliver:&lt;contractId&gt;</c>, <c>upgrade_fleet</c>.</summary>
internal sealed class CaravanCommandHandler : IScreenCommandHandler
{
    private readonly CaravanService _service;
    private readonly IPlayerState _player;
    private readonly AutomatedLogisticsService? _logistics;
    private readonly IWallet? _wallet;
    private readonly IWorldMap? _map;
    private readonly IItemCatalog? _items;

    public CaravanCommandHandler(
        CaravanService service,
        IPlayerState player,
        AutomatedLogisticsService? logistics = null,
        IWallet? wallet = null,
        IWorldMap? map = null,
        IItemCatalog? items = null)
    {
        _service = service;
        _player = player;
        _logistics = logistics;
        _wallet = wallet;
        _map = map;
        _items = items;
    }

    public string ScreenId => "caravan.routes";

    public string? Execute(string commandId)
    {
        if (string.Equals(commandId, "upgrade_fleet", StringComparison.Ordinal))
        {
            return _service.UpgradeFleet().Message;
        }

        if (commandId.StartsWith("cartel:", StringComparison.Ordinal) && _logistics is not null && _wallet is not null)
        {
            var cparts = commandId.Split(':');
            if (cparts.Length >= 3)
            {
                return cparts[1] switch
                {
                    "gift" => _logistics.GiftCartel(cparts[2], _wallet),
                    "ally" => _logistics.SignCartelTreaty(cparts[2], _wallet),
                    _ => "Lệnh thương bang không hợp lệ."
                };
            }
            return "Lệnh thương bang không hợp lệ.";
        }

        if (commandId.StartsWith("auto_route:", StringComparison.Ordinal) && _logistics is not null)
        {
            var rparts = commandId.Split(':');
            if (rparts.Length >= 3)
            {
                if (rparts[1] == "create" && long.TryParse(rparts[2], out var toCity))
                {
                    var itemId = rparts.Length >= 4 && long.TryParse(rparts[3], out var iid) ? iid : 1L;
                    var fromCity = _player.CurrentCityId;
                    var fromName = _map?.Find(fromCity)?.NameVi ?? $"Thành #{fromCity}";
                    var toName = _map?.Find(toCity)?.NameVi ?? $"Thành #{toCity}";
                    var itemDef = _items?.Find(itemId);
                    var basePrice = itemDef?.BasePrice ?? 15;
                    var routeName = $"Tuyến {itemDef?.NameVi ?? "Hàng"} {fromName} - {toName}";
                    _logistics.CreateRoute(routeName, fromCity, toCity, itemId, basePrice * 2, Math.Max(1, basePrice / 2), 25);
                    return $"Đã thiết lập tuyến buôn tự động: {routeName}!";
                }
                if (rparts[1] == "toggle" && long.TryParse(rparts[2], out var toggleId))
                {
                    var existing = _logistics.GetRoutes().FirstOrDefault(r => r.Id == toggleId);
                    if (existing is null) return "Không tìm thấy tuyến buôn tự động.";
                    _logistics.ToggleRoute(toggleId, !existing.Active);
                    return existing.Active ? $"Đã tạm dừng tuyến '{existing.Name}'." : $"Đã kích hoạt lại tuyến '{existing.Name}'.";
                }
                if (rparts[1] == "delete" && long.TryParse(rparts[2], out var delId))
                {
                    _logistics.DeleteRoute(delId);
                    return "Đã xóa tuyến buôn tự động.";
                }
            }
            return "Lệnh tuyến tự động không hợp lệ.";
        }

        if (commandId.StartsWith("unassign_crew:", StringComparison.Ordinal))
        {
            var role = commandId["unassign_crew:".Length..];
            return _service.AssignCrew(role, null).Message;
        }

        if (commandId.StartsWith("assign_crew:", StringComparison.Ordinal))
        {
            var parts = commandId.Split(':');
            if (parts.Length >= 3 && long.TryParse(parts[2], out var npcId))
            {
                return _service.AssignCrew(parts[1], npcId).Message;
            }
            return "Lệnh bổ nhiệm không hợp lệ.";
        }

        var separator = commandId.IndexOf(':');
        if (separator <= 0 || !long.TryParse(commandId[(separator + 1)..], out var targetId))
        {
            return "Lệnh không hợp lệ.";
        }

        var from = _player.CurrentCityId;
        return commandId[..separator] switch
        {
            "send" => _service.Send(from, targetId).Message,
            "send_escort" => _service.SendWithEscortFee(from, targetId, 50).Message,
            "goto" => _service.Travel(from, targetId, _player).Message,
            "accept" => _service.AcceptContract(targetId).Message,
            "deliver" => _service.DeliverContract(targetId).Message,
            var verb => $"Động từ '{verb}' không thuộc DLC caravan."
        };
    }
}
