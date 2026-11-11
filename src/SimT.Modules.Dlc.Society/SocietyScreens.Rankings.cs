using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Contracts.Ui;

namespace SimT.Modules.Dlc.Society;

/// <summary>
/// Thiên Hạ Phong Vân Bảng ("society.rankings") — 5-Pillar Aspirational NPC &amp; Player Hall of Rankings
/// with Next-Rival Target Tracker, Milestone Benchmarks, and Orthogonal Strategic Rivalry Actions.
/// </summary>
internal sealed class RealmRankingsScreen : IScreenContribution
{
    private readonly ISocietyChronicle _chronicle;
    private readonly IWorldMap _map;
    private readonly IPlayerState _player;
    private readonly IGameClock _clock;

    private RealmRankingCategory _activeCategory = RealmRankingCategory.Wealth;

    public RealmRankingsScreen(
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

    public string ScreenId => "society.rankings";

    public string Title => "Phong Vân Bảng";

    public int Order => 15;

    public ISocietyChronicle Chronicle => _chronicle;

    public void SetCategory(RealmRankingCategory category) => _activeCategory = category;

    public ScreenModel Render()
    {
        var allBoards = _chronicle.GetAllRealmRankings(top: 25);
        var activeBoard = allBoards.FirstOrDefault(b => b.Category == _activeCategory)
                          ?? _chronicle.GetRealmRanking(_activeCategory, top: 25);

        var currentCityName = _map.Find(_player.CurrentCityId)?.NameVi ?? "Châu quận";
        var p = activeBoard.PlayerEntry;
        var next = activeBoard.NextRivalToOvertake;
        var milestone = activeBoard.MilestoneTarget;

        // 1. Headline KPIs: Immediate Standing, Next Target Gap, and Grand Milestone
        var metrics = new List<ScreenMetric>
        {
            new(
                "Thứ Hạng Hiện Tại",
                $"#{p.Rank}",
                $"{p.TierTitleVi} ({p.Score:N0} {p.ScoreUnitVi})",
                p.Rank <= 10 ? MetricTone.Positive : MetricTone.Neutral),
            new(
                "Đối Thủ Cần Vượt Qua",
                next is not null ? $"{next.NameVi} (#{next.Rank})" : "Độc Cô Cầu Bại (#1)",
                next is not null ? $"Cần thêm +{activeBoard.GapToNextRival:N0} {p.ScoreUnitVi}" : "Đã đứng đầu thiên hạ!",
                MetricTone.Warning),
            new(
                "Cột Mốc Lớn Tiếp Theo",
                milestone is not null ? $"Hạng #{milestone.Rank} ({milestone.NameVi})" : "Thiên Hạ Đệ Nhất",
                milestone is not null ? $"Mốc điểm: {milestone.Score:N0} {p.ScoreUnitVi}" : "Đỉnh phong Tam Quốc",
                MetricTone.Positive),
            new(
                "Bảng Đang Xem",
                CategoryShortLabel(_activeCategory),
                $"Cập nhật {_clock.Today}",
                MetricTone.Neutral)
        };

        // 2. Alerts: Surface active rivalry & close overtake opportunities
        var alerts = new List<ScreenAlert>();
        if (next is not null && p.Score > 0 && activeBoard.GapToNextRival <= Math.Max(15, p.Score / 10))
        {
            alerts.Add(new ScreenAlert(
                $"🔥 [Sát Nút Đột Phá] Bạn chỉ còn kém {next.NameVi} (#{next.Rank}) đúng {activeBoard.GapToNextRival:N0} {p.ScoreUnitVi}! Chỉ cần thêm một bước tiến nữa sẽ vượt mặt đối thủ!",
                AlertLevel.Info));
        }

        var sections = new List<ScreenSection>();

        // Section 1: 5-Pillar Aspirational Overview ("MỤC TIÊU PHẤN ĐẤU 5 ĐẠI BẢNG")
        var overviewColumns = new[] { "Bảng Xếp Hạng", "Hạng Của Bạn", "Điểm Hiện Tại", "Người Đứng Trước", "Khoảng Cách Cần Vượt", "Đệ Nhất Thiên Hạ (#1)" };
        var overviewRows = allBoards.Select(b =>
        {
            var topOne = b.TopEntries.FirstOrDefault();
            var nextTargetText = b.NextRivalToOvertake is not null
                ? $"{b.NextRivalToOvertake.NameVi} (#{b.NextRivalToOvertake.Rank})"
                : "Đã đạt Đỉnh Phong";
            var gapText = b.NextRivalToOvertake is not null
                ? $"+{b.GapToNextRival:N0} {b.PlayerEntry.ScoreUnitVi}"
                : "—";
            var topOneText = topOne is not null
                ? $"👑 {topOne.NameVi} ({topOne.Score:N0} {topOne.ScoreUnitVi})"
                : "—";

            var tabKey = CategoryKey(b.Category);
            return new ScreenRow(
                [
                    CategoryShortLabel(b.Category),
                    $"#{b.PlayerEntry.Rank} ({b.PlayerEntry.TierTitleVi})",
                    $"{b.PlayerEntry.Score:N0} {b.PlayerEntry.ScoreUnitVi}",
                    nextTargetText,
                    gapText,
                    topOneText
                ],
                [new ScreenCommand($"tab:{tabKey}", "Xem Chi Tiết 📜", b.Category == _activeCategory ? CommandKind.Secondary : CommandKind.Primary)],
                Title: b.TitleVi,
                Details:
                [
                    new ScreenDetail("Bảng Xếp Hạng", b.TitleVi),
                    new ScreenDetail("Vị Thế Bản Thân", $"Hạng #{b.PlayerEntry.Rank} · {b.PlayerEntry.TierTitleVi} ({b.PlayerEntry.Score:N0} {b.PlayerEntry.ScoreUnitVi})"),
                    new ScreenDetail("Mục Tiêu Trước Mắt", b.NextRivalToOvertake is not null ? $"Vượt {b.NextRivalToOvertake.NameVi} (#{b.NextRivalToOvertake.Rank}) — cần thêm {b.GapToNextRival:N0} {b.PlayerEntry.ScoreUnitVi}" : "Giữ vững ngôi đầu"),
                    new ScreenDetail("Cột Mốc Vươn Tới", b.MilestoneTarget is not null ? $"Chinh phục Hạng #{b.MilestoneTarget.Rank} ({b.MilestoneTarget.NameVi} — {b.MilestoneTarget.Score:N0} {b.PlayerEntry.ScoreUnitVi})" : "Thiên Hạ Đệ Nhất"),
                    new ScreenDetail("Hướng Phấn Đấu", b.ProgressionAdviceVi)
                ]);
        }).ToList();

        sections.Add(new ScreenSection(
            "🎯 MỤC TIÊU PHẤN ĐẤU · TỔNG QUAN NGŨ ĐẠI PHONG VÂN BẢNG",
            overviewColumns,
            overviewRows));

        // Section 2: Active Detailed Leaderboard
        var detailColumns = new[] { "Hạng", "Nhân Vật", "Thân Phận / Danh Hiệu", "Thành Đang Trú", activeBoard.ScoreLabelVi, "Cảnh Giới / Đẳng Cấp", "Thực Lực Chi Tiết" };
        var detailRows = activeBoard.TopEntries.Select(e =>
        {
            var rankBadge = e.Rank switch
            {
                1 => "🥇 #1",
                2 => "🥈 #2",
                3 => "🥉 #3",
                _ => $"#{e.Rank}"
            };
            var nameCell = e.IsPlayer ? $"⭐ {e.NameVi} [BẠN]" : e.NameVi;

            var rowCmds = new List<ScreenCommand>();
            if (!e.IsPlayer)
            {
                rowCmds.Add(new ScreenCommand($"npc:modal:{e.EntityId}", "Hồ Sơ 🔍", CommandKind.Primary));
                if (_activeCategory == RealmRankingCategory.Wealth)
                {
                    rowCmds.Add(new ScreenCommand($"action:suppress:{e.EntityId}", "Đè Giá Thương Chiến ⚡ (600Q)", CommandKind.Danger));
                }
            }

            return new ScreenRow(
                [
                    rankBadge,
                    nameCell,
                    e.RoleOrTitleVi,
                    e.CitadelNameVi,
                    $"{e.Score:N0} {e.ScoreUnitVi}",
                    e.TierTitleVi,
                    e.SpecialtyOrBreakdownVi
                ],
                rowCmds,
                Title: $"{rankBadge} {e.NameVi} — {e.TierTitleVi}",
                Details:
                [
                    new ScreenDetail("Thứ Hạng", $"{rankBadge}"),
                    new ScreenDetail("Nhân Vật", e.NameVi),
                    new ScreenDetail("Thân Phận", e.RoleOrTitleVi),
                    new ScreenDetail("Thành Trì Hiện Tại", e.CitadelNameVi),
                    new ScreenDetail(activeBoard.ScoreLabelVi, $"{e.Score:N0} {e.ScoreUnitVi}"),
                    new ScreenDetail("Đẳng Cấp", e.TierTitleVi),
                    new ScreenDetail("Phân Tích Thực Lực", e.SpecialtyOrBreakdownVi)
                ]);
        }).ToList();

        sections.Add(new ScreenSection(
            $"{activeBoard.TitleVi} (Top Danh Nhân & Vị Thế Của Bạn)",
            detailColumns,
            detailRows,
            SectionViewMode.ReputationHallOfFame));

        // 3. Category Tabs + Orthogonal Strategic Actions
        var commands = new List<ScreenCommand>
        {
            new("tab:wealth", "💰 Phú Giả Bảng", _activeCategory == RealmRankingCategory.Wealth ? CommandKind.Primary : CommandKind.Secondary),
            new("tab:fame", "🌟 Danh Vọng Bảng", _activeCategory == RealmRankingCategory.Fame ? CommandKind.Primary : CommandKind.Secondary),
            new("tab:martial", "⚔️ Võ Thần Bảng", _activeCategory == RealmRankingCategory.Martial ? CommandKind.Primary : CommandKind.Secondary),
            new("tab:wisdom", "📜 Kỳ Tài Bảng", _activeCategory == RealmRankingCategory.Wisdom ? CommandKind.Primary : CommandKind.Secondary),
            new("tab:influence", "👑 Quyền Thế Bảng", _activeCategory == RealmRankingCategory.Influence ? CommandKind.Primary : CommandKind.Secondary),
            new("action:symposium", "🍵 Mở Đại Hội Luận Đạo (500Q · +Uy Danh/+Đức)", CommandKind.Primary),
            new("action:suppress", "⚡ Phát Động Thương Chiến Đè Giá (600Q · Bá Đạo)", CommandKind.Danger)
        };

        var status = $"Đang tại {currentCityName} · {CategoryShortLabel(_activeCategory)}: Hạng #{p.Rank} ({p.Score:N0} {p.ScoreUnitVi}) · {activeBoard.ProgressionAdviceVi}";

        return new ScreenModel(
            ScreenId,
            Title,
            status,
            sections,
            commands,
            metrics,
            alerts,
            activeBoard.ProgressionAdviceVi);
    }

    private static string CategoryShortLabel(RealmRankingCategory cat) => cat switch
    {
        RealmRankingCategory.Wealth => "💰 Phú Giả (Tiền Tài)",
        RealmRankingCategory.Fame => "🌟 Danh Vọng (Uy Danh)",
        RealmRankingCategory.Martial => "⚔️ Võ Thần (Chiến Lực)",
        RealmRankingCategory.Wisdom => "📜 Kỳ Tài (Mưu Lược)",
        _ => "👑 Quyền Thế (Thế Lực)"
    };

    private static string CategoryKey(RealmRankingCategory cat) => cat switch
    {
        RealmRankingCategory.Wealth => "wealth",
        RealmRankingCategory.Fame => "fame",
        RealmRankingCategory.Martial => "martial",
        RealmRankingCategory.Wisdom => "wisdom",
        _ => "influence"
    };
}

internal sealed class RealmRankingsCommandHandler : IScreenCommandHandler
{
    private readonly RealmRankingsScreen _screen;

    public RealmRankingsCommandHandler(RealmRankingsScreen screen)
    {
        _screen = screen;
    }

    public string ScreenId => "society.rankings";

    public string? Execute(string commandId)
    {
        if (commandId.StartsWith("tab:", StringComparison.Ordinal))
        {
            var key = commandId["tab:".Length..].ToLowerInvariant();
            var cat = key switch
            {
                "wealth" => RealmRankingCategory.Wealth,
                "fame" => RealmRankingCategory.Fame,
                "martial" => RealmRankingCategory.Martial,
                "wisdom" => RealmRankingCategory.Wisdom,
                "influence" => RealmRankingCategory.Influence,
                _ => RealmRankingCategory.Wealth
            };
            _screen.SetCategory(cat);
            return $"Đã chuyển sang bảng xếp hạng: {cat}";
        }

        if (commandId == "action:symposium")
        {
            var res = _screen.Chronicle.HostGrandSymposium();
            return res.NarrativeVi;
        }

        if (commandId == "action:suppress")
        {
            var res = _screen.Chronicle.LaunchCommercialSuppression(null);
            return res.NarrativeVi;
        }

        if (commandId.StartsWith("action:suppress:", StringComparison.Ordinal) &&
            long.TryParse(commandId["action:suppress:".Length..], out var rivalId))
        {
            var res = _screen.Chronicle.LaunchCommercialSuppression(rivalId);
            return res.NarrativeVi;
        }

        return null;
    }
}
