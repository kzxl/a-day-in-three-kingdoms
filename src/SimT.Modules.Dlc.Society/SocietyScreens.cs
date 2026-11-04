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
            new("filter:all", "Tất Cả", CommandKind.Secondary),
            new("filter:local", $"Tại {currentCitadelName}", CommandKind.Primary),
            new("filter:martial", "⚔️ Võ Nghiệp", CommandKind.Secondary),
            new("filter:commerce", "💰 Thương Lộ", CommandKind.Secondary),
            new("filter:scholarly", "📜 Mưu Lược", CommandKind.Secondary),
            new("filter:travel", "🗺️ Du Hành", CommandKind.Secondary)
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
