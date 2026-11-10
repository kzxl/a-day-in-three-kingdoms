using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Contracts.Ui;

namespace SimT.Modules.Dlc.Society;

internal sealed partial class SocietyChronicleScreen : IScreenContribution
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

    public ISocietyChronicle Chronicle => _chronicle;

    public IGameClock Clock => _clock;

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

        if (_filterKind == "karma")
        {
            return RenderKarmaScreen(currentCityId, currentCitadelName);
        }

        if (_filterKind == "rumors") return RenderRumors();

        if (_filterKind == "espionage") return RenderEspionage(currentCityId, currentCitadelName);

        var allEvents = _chronicle.RecentEvents(limit: 60);
        var localEvents = allEvents.Where(e => e.CitadelId == currentCityId).ToList();

        var displayedEvents = _filterKind switch
        {
            "local" => localEvents,
            "history" => _chronicle.RecentEvents(limit: 60, category: "history").ToList(),
            "military" => allEvents.Where(e => e.EventKind is "martial" or "history" or "turmoil").ToList(),
            "economy" => allEvents.Where(e => e.EventKind is "commerce" or "store" or "bankruptcy").ToList(),
            "social" => allEvents.Where(e => e.EventKind is "relationship" or "marriage" or "governance" or "scholarly").ToList(),
            "migration" => allEvents.Where(e => e.StatImpact != null && e.StatImpact.Contains("Di cư") || e.StatImpact != null && e.StatImpact.Contains("tị nạn") || e.StatImpact != null && e.StatImpact.Contains("Tránh giặc")).ToList(),
            "martial" => allEvents.Where(e => e.EventKind == "martial").ToList(),
            "commerce" => allEvents.Where(e => e.EventKind == "commerce").ToList(),
            "relationship" => allEvents.Where(e => e.EventKind == "relationship").ToList(),
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

        var migrationEvents = allEvents.Where(e => e.StatImpact != null && e.StatImpact.Contains("Di cư") || e.StatImpact != null && e.StatImpact.Contains("tị nạn") || e.StatImpact != null && e.StatImpact.Contains("Tránh giặc")).Take(6).ToList();
        if (_filterKind == "all" && migrationEvents.Count > 0)
        {
            sections.Add(new ScreenSection(
                $"🏃 Dòng Di Cư Thiên Hạ Gần Đây ({migrationEvents.Count})",
                Columns,
                migrationEvents.Select(ToRow).ToList()));
        }

        var headerTitle = _filterKind switch
        {
            "local" => $"Sự kiện tại {currentCitadelName}",
            "history" => "Sử biến thiên hạ · Đại cục Tam Quốc",
            "migration" => "Dòng di cư lánh loạn & tìm miền đất hứa",
            "martial" => "Phong vân võ nghiệp quần hùng",
            "commerce" => "Thương trường & đại lộ phú thương",
            "relationship" => "Nhân duyên trong thiên hạ",
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
            new("filter:all", "📜 Tất Cả", _filterKind == "all" ? CommandKind.Primary : CommandKind.Secondary),
            new("filter:military", "⚔️ Quân Sự", _filterKind == "military" ? CommandKind.Primary : CommandKind.Secondary),
            new("filter:economy", "💰 Kinh Tế", _filterKind == "economy" ? CommandKind.Primary : CommandKind.Secondary),
            new("filter:social", "👥 Nhân Vật", _filterKind == "social" ? CommandKind.Primary : CommandKind.Secondary),
            new("filter:local", $"📍 Tại {currentCitadelName}", _filterKind == "local" ? CommandKind.Primary : CommandKind.Secondary),
            new("filter:history", "🐉 Sử Biến", _filterKind == "history" ? CommandKind.Primary : CommandKind.Secondary),
            new("filter:rivals", "🏆 Phú Hào", _filterKind == "rivals" ? CommandKind.Primary : CommandKind.Secondary),
            new("filter:karma", "⚖️ Nhân Quả", _filterKind == "karma" ? CommandKind.Primary : CommandKind.Secondary),
            new("filter:espionage", "🕵️ Mật Thám & Kỳ Thư", _filterKind == "espionage" ? CommandKind.Primary : CommandKind.Secondary),
            new("filter:rumors", "Tin Đồn", _filterKind == "rumors" ? CommandKind.Primary : CommandKind.Secondary),
            new("filter:migration", "🏃 Di Cư", _filterKind == "migration" ? CommandKind.Primary : CommandKind.Secondary),
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
            alerts,
            $"Theo dõi biến động nhân sĩ tại {currentCitadelName} và bảng phú hào thiên hạ để nắm bắt cơ hội buôn bán hoặc kết giao.");
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
            new("filter:relationship", "Quan hệ", CommandKind.Secondary),
            new("filter:rumors", "Tin đồn", CommandKind.Secondary),
            new("filter:all", "Tất Cả", CommandKind.Secondary),
            new("filter:rivals", "🏆 Phú Hào", CommandKind.Primary),
            new("filter:karma", "⚖️ Nhân Quả", CommandKind.Secondary),
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
            alerts,
            $"Theo dõi biến động nhân sĩ tại {currentCitadelName} và bảng phú hào thiên hạ để nắm bắt cơ hội buôn bán hoặc kết giao.");
    }

    private ScreenModel RenderKarmaScreen(long currentCityId, string currentCitadelName)
    {
        var karma = _chronicle.Karma;
        var allSeeds = karma.GetAllSeeds();
        var activeSeeds = allSeeds.Where(s => !s.IsResolved).ToList();
        var history = karma.GetHistory();
        var today = (int)_clock.Today.DayIndex;
        var pendingInCitadel = karma.CheckPendingEvents(currentCityId, today);

        var seedColumns = new[] { "Chuỗi Nhân Quả", "Giai Đoạn", "Nơi Ứng Nghiệm", "Tích Hướng", "Đơm Hoa Kết Trái", "Trạng Thái" };
        var seedRows = activeSeeds.Select(s =>
        {
            var stageText = s.CurrentStage == 1 ? "Hạt Giống (Sơ Khởi)" : "Báo Ứng (Đơm Hoa)";
            var alignmentText = s.KarmaAlignment switch
            {
                "virtue" => "Thiện Nghiệp ☀️",
                "ruthless" => "Ác Duyên ⚡",
                _ => "Chưa Định Hình ⚖️"
            };
            var daysRemaining = s.TriggerAfterDay - today;
            var countdownText = daysRemaining <= 0
                ? "Sẵn sàng ứng nghiệm! ⏳"
                : $"Còn {daysRemaining} ngày (Ngày {s.TriggerAfterDay})";

            var statusText = s.CitadelId == currentCityId && daysRemaining <= 0
                ? "👉 Có Thể Kích Hoạt Tại Đây"
                : s.CitadelId == currentCityId
                    ? "Đang Ươm Mầm"
                    : $"Cần Tới {s.CitadelNameVi}";

            return new ScreenRow(
                [
                    s.TitleVi,
                    stageText,
                    s.CitadelNameVi,
                    alignmentText,
                    countdownText,
                    statusText
                ],
                s.CitadelId == currentCityId && daysRemaining <= 0
                    ? [new ScreenCommand($"karma:resolve:{s.Id}", "Kích Hoạt ⚖️", CommandKind.Primary)]
                    : Array.Empty<ScreenCommand>(),
                Title: $"{s.TitleVi} — {stageText}",
                Details:
                [
                    new ScreenDetail("Mã Chuỗi", s.ChainCode),
                    new ScreenDetail("Tiêu Đề", s.TitleVi),
                    new ScreenDetail("Thành Trì", s.CitadelNameVi),
                    new ScreenDetail("Giai Đoạn", stageText),
                    new ScreenDetail("Tích Hướng Đạo Đức", alignmentText),
                    new ScreenDetail("Thời Gian Ứng Nghiệm", countdownText),
                    new ScreenDetail("Hành Trạng Gần Nhất", s.LastOutcomeVi)
                ]);
        }).ToList();

        var historyColumns = new[] { "Thời Gian", "Chuỗi Nhân Quả", "Giai Đoạn", "Quyết Định", "Thành Quả / Báo Ứng", "Biến Động" };
        var historyRows = history.Select(h =>
        {
            var impactText = !string.IsNullOrWhiteSpace(h.StatDeltaSummary) ? h.StatDeltaSummary : "Không biến động";

            return new ScreenRow(
                [
                    GameDateCodec.Format(h.RecordedDate),
                    h.ChainCode,
                    $"Giai đoạn {h.Stage}",
                    h.DecisionVi,
                    h.ConsequenceVi,
                    impactText
                ],
                Array.Empty<ScreenCommand>(),
                Title: $"{h.ChainCode} — {h.DecisionVi}",
                Details:
                [
                    new ScreenDetail("Thời Gian", GameDateCodec.Format(h.RecordedDate)),
                    new ScreenDetail("Chuỗi Nhân Quả", h.ChainCode),
                    new ScreenDetail("Giai Đoạn", $"Giai đoạn {h.Stage}"),
                    new ScreenDetail("Quyết Định Của Bạn", h.DecisionVi),
                    new ScreenDetail("Nội Dung Báo Ứng", h.ConsequenceVi),
                    new ScreenDetail("Biến Động Thực Tế", impactText)
                ]);
        }).ToList();

        var sections = new List<ScreenSection>
        {
            new(
                $"🌱 HẠT GIỐNG NHÂN QUẢ ĐANG ƯƠM MẦM ({activeSeeds.Count} Hạt Giống)",
                seedColumns,
                seedRows)
        };

        if (historyRows.Count > 0)
        {
            sections.Add(new ScreenSection(
                $"📜 SỔ BÁO ỨNG & KÝ SỰ NHÂN DUYÊN ({historyRows.Count} Biến Cố Đã Định)",
                historyColumns,
                historyRows));
        }

        var goodSeeds = activeSeeds.Count(s => s.KarmaAlignment == "virtue");
        var ruthlessSeeds = activeSeeds.Count(s => s.KarmaAlignment == "ruthless");
        var moralTone = goodSeeds > ruthlessSeeds ? "Thuần Thiện Nhân Nghĩa" : ruthlessSeeds > goodSeeds ? "Thực Dụng / Đoạt Lợi" : "Trung Đạo Vô Vi";

        var metrics = new List<ScreenMetric>
        {
            new("Hạt Giống Đang Ươm", $"{activeSeeds.Count} chuỗi", "Đang tích lũy nhân duyên", MetricTone.Neutral),
            new("Nhân Quả Tại Thành", $"{pendingInCitadel.Count} sự kiện", currentCitadelName, pendingInCitadel.Count > 0 ? MetricTone.Warning : MetricTone.Neutral),
            new("Ký Sự Đã Ứng Nghiệm", $"{history.Count} biến cố", "Đã lưu vào quả báo đồ lục", MetricTone.Positive),
            new("Tâm Tính Duyên Khởi", moralTone, $"{goodSeeds} Thiện · {ruthlessSeeds} Lợi", goodSeeds >= ruthlessSeeds ? MetricTone.Positive : MetricTone.Warning)
        };

        var alerts = new List<ScreenAlert>();
        if (pendingInCitadel.Count > 0)
        {
            alerts.Add(new ScreenAlert(
                $"[Nhân Quả Đơm Hoa] Tại {currentCitadelName}, có {pendingInCitadel.Count} duyên khởi đã đủ ngày tháng chín muồi! Bạn có thể dạo phố hoặc kích hoạt sự kiện để đón nhận quả báo.",
                AlertLevel.Info));
        }
        else if (activeSeeds.Count == 0)
        {
            alerts.Add(new ScreenAlert("Tất cả hạt giống nhân quả đã đơm hoa kết trái toàn mỹ.", AlertLevel.Info));
        }

        var commands = new List<ScreenCommand>
        {
            new("filter:relationship", "Quan hệ", CommandKind.Secondary),
            new("filter:rumors", "Tin đồn", CommandKind.Secondary),
            new("filter:all", "Tất Cả", CommandKind.Secondary),
            new("filter:rivals", "🏆 Phú Hào", CommandKind.Secondary),
            new("filter:karma", "⚖️ Nhân Quả", CommandKind.Primary),
            new("filter:local", $"Tại {currentCitadelName}", CommandKind.Secondary),
            new("filter:martial", "⚔️ Võ Nghiệp", CommandKind.Secondary),
            new("filter:commerce", "💰 Thương Lộ", CommandKind.Secondary),
            new("filter:scholarly", "📜 Mưu Lược", CommandKind.Secondary),
            new("filter:travel", "🗺️ Du Hành", CommandKind.Secondary)
        };

        var status = $"Ký sự Nhân Quả & Nghiệp Duyên cập nhật đến {_clock.Today} · {activeSeeds.Count} hạt giống đang ươm mầm khắp thiên hạ.";

        return new ScreenModel(
            ScreenId,
            Title,
            status,
            sections,
            commands,
            metrics,
            alerts,
            $"Theo dõi biến động nhân sĩ tại {currentCitadelName} và bảng phú hào thiên hạ để nắm bắt cơ hội buôn bán hoặc kết giao.");
    }

    private static ScreenRow ToRow(SocietyLogEntry e)
    {
        var kindVi = e.EventKind switch
        {
            "history" => "🐉 Sử biến",
            "martial" => "⚔️ Võ nghiệp",
            "commerce" => "💰 Thương mại",
            "relationship" => "Nhân duyên",
            "scholarly" => "📜 Mưu thần",
            "governance" => "🏛️ Chính sự",
            "travel" => "🗺️ Du hành",
            _ => "Giang hồ"
        };

        var rowCommands = new List<ScreenCommand>();
        if (e.EventKind == "history" && e.StatImpact is not null && e.StatImpact.StartsWith("evt:", StringComparison.Ordinal))
        {
            var endIdx = e.StatImpact.IndexOf(' ');
            var evtCode = endIdx > 4 ? e.StatImpact[4..endIdx] : e.StatImpact[4..];
            rowCommands.Add(new ScreenCommand($"history:view:{evtCode}", "Xem Cảnh 📜", CommandKind.Primary));
        }
        if (e.NpcId > 0)
        {
            rowCommands.Add(new ScreenCommand($"npc:modal:{e.NpcId}", "Hồ Sơ 🔍", rowCommands.Count == 0 ? CommandKind.Primary : CommandKind.Secondary));
        }

        return new ScreenRow(
            [
                GameDateCodec.Format(e.Date),
                e.NpcNameVi,
                e.CitadelNameVi,
                kindVi,
                e.HeadlineVi,
                e.StatImpact ?? "—"
            ],
            rowCommands,
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
        "military" => "Quân sự & Chiến cuộc",
        "economy" => "Kinh tế & Thương nghiệp",
        "social" => "Nhân vật & Bang phái",
        "history" => "Sử biến thiên hạ",
        "rivals" => "Bảng phú hào",
        "karma" => "Nhân quả báo ứng",
        "migration" => "Dòng di cư thiên hạ",
        "local" => "Đồng thành",
        "martial" => "Võ nghiệp",
        "commerce" => "Thương lộ",
        "relationship" => "Quan hệ",
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

        if (commandId.StartsWith("history:view:"))
        {
            var code = commandId["history:view:".Length..];
            var evt = _screen.Chronicle.FindHistoricalEvent(code);
            return evt is not null
                ? $"[Sử Biến] {evt.TitleVi} ({GameDateCodec.Format(evt.ScheduledDate)})"
                : "Không tìm thấy sự biến lịch sử này.";
        }

        if (commandId.StartsWith("karma:resolve:"))
        {
            if (long.TryParse(commandId["karma:resolve:".Length..], out var seedId))
            {
                var seed = _screen.Chronicle.Karma.GetAllSeeds().FirstOrDefault(s => s.Id == seedId);
                if (seed is not null)
                {
                    var pending = _screen.Chronicle.Karma.CheckPendingEvents(seed.CitadelId, (int)_screen.Clock.Today.DayIndex)
                        .FirstOrDefault(p => p.SeedId == seedId);

                    if (pending is not null && pending.Options.Count > 0)
                    {
                        var res = _screen.Chronicle.Karma.ResolveChoice(seedId, pending.Options[0].ChoiceKey);
                        return $"[Nhân Quả Ứng Nghiệm] {res.OutcomeVi}";
                    }
                }
            }
            return "Không thể kích hoạt sự kiện nhân quả này.";
        }

        if (_screen.Chronicle is IEspionageNetwork espionage)
        {
            if (commandId.StartsWith("spy:deploy:") && long.TryParse(commandId["spy:deploy:".Length..], out var deployCitId))
            {
                return espionage.DeploySpyCell(deployCitId);
            }

            if (commandId.StartsWith("spy:whisper:"))
            {
                var parts = commandId["spy:whisper:".Length..].Split(':');
                if (parts.Length >= 2 && long.TryParse(parts[0], out var whisperCitId))
                {
                    return espionage.ExecuteCovertWhisper(whisperCitId, parts[1]);
                }
            }

            if (commandId.StartsWith("tome:seek:"))
            {
                var code = commandId["tome:seek:".Length..];
                return espionage.SeekHermitTome(code);
            }
        }

        return null;
    }
}
