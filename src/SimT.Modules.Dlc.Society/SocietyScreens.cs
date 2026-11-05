using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Contracts.Ui;

namespace SimT.Modules.Dlc.Society;

internal sealed class SocietyChronicleScreen : IScreenContribution
{
    private static readonly string[] Columns = ["Ngày", "Nhân Sĩ", "Thành Trì", "Phân Loại", "Hành Trạng", "Biến Động"];

    private readonly ISocietyChronicle _chronicle;
    private readonly IWorldMap _map;
    private readonly IPlayerState _player;
    private readonly IGameClock _clock;

    private string _filterKind = "all";

    public SocietyChronicleScreen(
        ISocietyChronicle chronicle,
        IWorldMap map,
        IPlayerState player,
        IGameClock clock)
    {
        _chronicle = chronicle;
        _map = map;
        _player = player;
        _clock = clock;
    }

    public string ScreenId => "society.chronicle";

    public string Title => "Thế Thái";

    public int Order => 16;

    public void SetFilter(string filter) => _filterKind = filter;

    public ScreenModel Render()
    {
        var currentCityId = _player.CurrentCityId;
        var currentCitadel = _map.Find(currentCityId);
        var currentCitadelName = currentCitadel?.NameVi ?? "Châu quận";

        if (_filterKind == "rivals")
        {
            return RenderLeaderboard(currentCityId, currentCitadelName);
        }

        var allEvents = _chronicle.RecentEvents(limit: 60);
        var localEvents = allEvents.Where(e => e.CitadelId == currentCityId).ToList();

        var displayedEvents = _filterKind switch
        {
            "local" => localEvents,
            "martial" => allEvents.Where(e => e.EventKind == "martial").ToList(),
            "commerce" => allEvents.Where(e => e.EventKind == "commerce").ToList(),
            "scholarly" => allEvents.Where(e => e.EventKind == "scholarly").ToList(),
            "governance" => allEvents.Where(e => e.EventKind == "governance").ToList(),
            "travel" => allEvents.Where(e => e.EventKind == "travel").ToList(),
            _ => allEvents
        };

        var sections = new List<ScreenSection>();

        if (_filterKind == "all" && localEvents.Count > 0)
        {
            sections.Add(new ScreenSection(
                $"📍 Sự Kiện Đồng Thành · {currentCitadelName} ({localEvents.Count})",
                Columns,
                localEvents.Select(ToRow).ToList()));
        }

        var headerTitle = _filterKind switch
        {
            "local" => $"Sự kiện tại {currentCitadelName}",
            "martial" => "Phong vân võ nghiệp quần hùng",
            "commerce" => "Thương trường & đại lộ phú thương",
            "scholarly" => "Mưu thần & sĩ phu luận đàm",
            "governance" => "Chính sự châu quận & an dân",
            "travel" => "Hành trình vân du thiên hạ",
            _ => "Toàn bộ phong vân thế thái thiên hạ"
        };

        sections.Add(new ScreenSection(
            $"📜 {headerTitle} ({displayedEvents.Count})",
            Columns,
            displayedEvents.Select(ToRow).ToList()));

        var activities = _chronicle.AllActivities();
        var metrics = new List<ScreenMetric>
        {
            new("Sự kiện gần đây", $"{allEvents.Count} mục", "Toàn cõi 15 châu", MetricTone.Neutral),
            new("Tại đồng thành", $"{localEvents.Count} việc", currentCitadelName, MetricTone.Positive),
            new("Tổng nhân sĩ hoạt động", $"{activities.Count} người", "Tự chủ mưu sinh", MetricTone.Positive),
            new("Bộ lọc hiện tại", FilterLabel(_filterKind), "Bấm nút trên thanh lệnh để đổi", MetricTone.Warning)
        };

        var alerts = new List<ScreenAlert>();
        if (localEvents.Count == 0)
        {
            alerts.Add(new ScreenAlert($"Tháng này tại {currentCitadelName} sóng yên biển lặng, các danh sĩ đang âm thầm chuẩn bị thời cơ.", AlertLevel.Info));
        }

        var commands = new List<ScreenCommand>
        {
            new("filter:all", "Tất Cả", _filterKind == "all" ? CommandKind.Primary : CommandKind.Secondary),
            new("filter:rivals", "🏆 Phú Hào", _filterKind == "rivals" ? CommandKind.Primary : CommandKind.Secondary),
            new("filter:local", $"Tại {currentCitadelName}", _filterKind == "local" ? CommandKind.Primary : CommandKind.Secondary),
            new("filter:martial", "⚔️ Võ Nghiệp", _filterKind == "martial" ? CommandKind.Primary : CommandKind.Secondary),
            new("filter:commerce", "💰 Thương Lộ", _filterKind == "commerce" ? CommandKind.Primary : CommandKind.Secondary),
            new("filter:scholarly", "📜 Mưu Lược", _filterKind == "scholarly" ? CommandKind.Primary : CommandKind.Secondary),
            new("filter:travel", "🗺️ Du Hành", _filterKind == "travel" ? CommandKind.Primary : CommandKind.Secondary)
        };

        var status = $"Nhật ký thế thái cập nhật đến {_clock.Today} · {activities.Count} nhân sĩ tự chủ luyện tập, kinh doanh, chu du thiên hạ.";

        return new ScreenModel(
            ScreenId,
            Title,
            status,
            sections,
            commands,
            metrics,
            alerts);
    }

    private ScreenModel RenderLeaderboard(long currentCityId, string currentCitadelName)
    {
        var leaderboard = _chronicle.Rivals.GetLeaderboard();
        var playerEntry = leaderboard.FirstOrDefault(e => e.IsPlayer);
        var topRival = leaderboard.FirstOrDefault(e => !e.IsPlayer);
        var rivals = _chronicle.Rivals.GetAllRivals();
        var totalFleets = rivals.Sum(r => r.FleetCarts);

        var leaderboardColumns = new[] { "Hạng", "Thương Hội / Phú Thương", "Cấp Bậc", "Tài Sản Ròng", "Đại Bản Doanh", "Sở Trường", "Thương Đoàn", "Hành Trạng" };
        var rows = leaderboard.Select(e =>
        {
            var badge = e.IsPlayer ? "⭐ " : "";
            var nameText = $"{badge}{e.EnterpriseName} ({e.Name})";
            var netWorthText = $"{e.EstimatedNetWorth:N0} Quan";
            var fleetText = e.IsPlayer ? "—" : $"{rivals.FirstOrDefault(r => r.NpcId == e.EntityId)?.FleetCarts ?? 1} Xe";

            return new ScreenRow(
                [
                    $"#{e.Rank}",
                    nameText,
                    $"[{e.TierTitle}]",
                    netWorthText,
                    e.BaseCitadel,
                    e.PrimarySpecialty,
                    fleetText,
                    e.Status
                ],
                e.IsPlayer ? Array.Empty<ScreenCommand>() : [new ScreenCommand($"npc:modal:{e.EntityId}", "Hồ Sơ 🔍", CommandKind.Primary)],
                Title: $"{e.EnterpriseName} — {e.TierTitle}",
                Details:
                [
                    new ScreenDetail("Thứ Hạng", $"#{e.Rank}"),
                    new ScreenDetail("Tên Thương Hội", e.EnterpriseName),
                    new ScreenDetail("Chủ Sự", e.Name),
                    new ScreenDetail("Cấp Bậc", e.TierTitle),
                    new ScreenDetail("Tài Sản Ước Tính", netWorthText),
                    new ScreenDetail("Đại Bản Doanh", e.BaseCitadel),
                    new ScreenDetail("Mặt Hàng Sở Trường", e.PrimarySpecialty),
                    new ScreenDetail("Tình Trạng Kinh Doanh", e.Status)
                ]);
        }).ToList();

        var sections = new List<ScreenSection>
        {
            new(
                $"🏆 BẢNG PHÚ HÀO THIÊN HẠ · CẠNH TRANH THƯƠNG NGHIỆP ({leaderboard.Count} Thương Gia)",
                leaderboardColumns,
                rows)
        };

        var commerceEvents = _chronicle.RecentEvents(limit: 20, category: "commerce");
        if (commerceEvents.Count > 0)
        {
            sections.Add(new ScreenSection(
                $"📈 Biến Động Thị Trường & Vận Chuyển Gần Đây ({commerceEvents.Count})",
                Columns,
                commerceEvents.Select(ToRow).ToList()));
        }

        var metrics = new List<ScreenMetric>
        {
            new("Hạng Thương Gia Của Bạn", playerEntry != null ? $"#{playerEntry.Rank} / {leaderboard.Count}" : "Chưa xếp hạng", playerEntry?.TierTitle ?? "", MetricTone.Positive),
            new("Tài Sản Ròng Người Chơi", playerEntry != null ? $"{playerEntry.EstimatedNetWorth:N0} Quan" : "0", "Tổng thực lực kinh tế", MetricTone.Positive),
            new("Phú Thương Dẫn Đầu", topRival != null ? $"{topRival.Name}" : "Trống", topRival != null ? $"{topRival.EstimatedNetWorth:N0} Quan" : "", MetricTone.Warning),
            new("Tổng Thương Đoàn AI", $"{totalFleets} Cỗ Xe Hàng", "Đang lưu thông 15 châu", MetricTone.Neutral)
        };

        var alerts = new List<ScreenAlert>();
        var aggressiveRival = rivals.OrderByDescending(r => r.MonthlyProfit).FirstOrDefault();
        if (aggressiveRival != null && aggressiveRival.MonthlyProfit > 0)
        {
            alerts.Add(new ScreenAlert(
                $"[Cạnh Tranh Thương Nghiệp] {aggressiveRival.EnterpriseName} ({aggressiveRival.NpcNameVi}) vừa thu lãi {aggressiveRival.MonthlyProfit:N0} Quan, tăng cường ảnh hưởng tại {aggressiveRival.CurrentCitadelNameVi}!",
                AlertLevel.Info));
        }

        var commands = new List<ScreenCommand>
        {
            new("filter:all", "Tất Cả", CommandKind.Secondary),
            new("filter:rivals", "🏆 Phú Hào", CommandKind.Primary),
            new("filter:local", $"Tại {currentCitadelName}", CommandKind.Secondary),
            new("filter:martial", "⚔️ Võ Nghiệp", CommandKind.Secondary),
            new("filter:commerce", "💰 Thương Lộ", CommandKind.Secondary),
            new("filter:scholarly", "📜 Mưu Lược", CommandKind.Secondary),
            new("filter:travel", "🗺️ Du Hành", CommandKind.Secondary)
        };

        var status = $"Bảng Xếp Hạng Phú Hào cập nhật đến {_clock.Today} · {rivals.Count} đối thủ thương nghiệp AI đang vận hành độc lập.";

        return new ScreenModel(
            ScreenId,
            Title,
            status,
            sections,
            commands,
            metrics,
            alerts);
    }

    private static ScreenRow ToRow(SocietyLogEntry e)
    {
        var kindVi = e.EventKind switch
        {
            "martial" => "⚔️ Võ nghiệp",
            "commerce" => "💰 Thương mại",
            "scholarly" => "📜 Mưu thần",
            "governance" => "🏛️ Chính sự",
            "travel" => "🗺️ Du hành",
            _ => "Giang hồ"
        };

        return new ScreenRow(
            [
                GameDateCodec.Format(e.Date),
                e.NpcNameVi,
                e.CitadelNameVi,
                kindVi,
                e.HeadlineVi,
                e.StatImpact ?? "—"
            ],
            [
                new ScreenCommand($"npc:modal:{e.NpcId}", "Hồ Sơ 🔍", CommandKind.Primary)
            ],
            Title: $"{e.NpcNameVi} — {e.HeadlineVi}",
            Details:
            [
                new ScreenDetail("Thời Gian", GameDateCodec.Format(e.Date)),
                new ScreenDetail("Nhân Sĩ", e.NpcNameVi),
                new ScreenDetail("Thành Trì", e.CitadelNameVi),
                new ScreenDetail("Phân Loại", kindVi),
                new ScreenDetail("Tiêu Đề", e.HeadlineVi),
                new ScreenDetail("Hành Trạng Chi Tiết", e.DetailVi),
                new ScreenDetail("Ảnh Hưởng Thực Tế", e.StatImpact ?? "Không biến động")
            ]);
    }

    private static string FilterLabel(string filter) => filter switch
    {
        "rivals" => "Bảng phú hào",
        "local" => "Đồng thành",
        "martial" => "Võ nghiệp",
        "commerce" => "Thương lộ",
        "scholarly" => "Mưu lược",
        "governance" => "Chính sự",
        "travel" => "Du hành",
        _ => "Tất cả"
    };
}

internal sealed class SocietyCommandHandler : IScreenCommandHandler
{
    private readonly SocietyChronicleScreen _screen;

    public SocietyCommandHandler(SocietyChronicleScreen screen)
    {
        _screen = screen;
    }

    public string ScreenId => "society.chronicle";

    public string? Execute(string commandId)
    {
        if (commandId.StartsWith("filter:"))
        {
            var filter = commandId["filter:".Length..];
            _screen.SetFilter(filter);
            return $"Đã chuyển bộ lọc: {filter}";
        }

        return null;
    }
}
