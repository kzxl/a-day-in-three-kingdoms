using System.Globalization;
using SimT.Contracts.Playing;
using SimT.Contracts.Ui;

namespace SimT.Modules.Base.Player;

/// <summary>
/// World Renown, Prestige, and Moral Alignment Screen (Uy Danh Thiên Hạ).
/// Provides complete insight into player's fame tier, unlocked perks,
/// 5 strategic regional influences, Three Kingdoms hall-of-fame leaderboard, and deeds chronicle.
/// </summary>
internal sealed class PlayerReputationScreen : IScreenContribution
{
    private static readonly string[] TierColumns =
        ["Cấp Bậc", "Danh Hiệu", "Ngưỡng Uy Danh", "Tiến Độ", "Đặc Quyền Mở Khóa"];

    private static readonly string[] RegionColumns =
        ["Khu Vực", "Uy Danh Vùng", "Danh Tiếng Bản Địa", "Đặc Quyền Thương Dịch & Giao Tiếp"];

    private static readonly string[] LeaderboardColumns =
        ["Hạng", "Nhân Vật", "Thân Phận / Tước Hiệu", "Thành Đang Trú", "Uy Danh", "Cấp Bậc"];

    private static readonly string[] DeedColumns =
        ["Ngày", "Hạng Mục", "Biến Động", "Chiến Tích", "Tường Trình Chi Tiết"];

    private readonly IPlayerReputation _reputation;

    public PlayerReputationScreen(IPlayerReputation reputation)
    {
        _reputation = reputation;
    }

    public string ScreenId => "player.reputation";

    public string Title => "Uy Danh";

    public int Order => 12;

    public ScreenModel Render()
    {
        var standing = _reputation.Standing;
        var regions = _reputation.RegionalStandings;
        var deeds = _reputation.RecentDeeds(15);
        var leaderboard = _reputation.GetLeaderboard(12);

        // Find player's rank
        var playerLeaderboardEntry = leaderboard.FirstOrDefault(e => e.IsPlayer);
        var playerRankText = playerLeaderboardEntry is not null ? $"Hạng {playerLeaderboardEntry.Rank}" : "Đang cập nhật";

        // 1. Headline KPIs
        var headline = new List<ScreenMetric>
        {
            new("Cấp Bậc Danh Vọng", standing.RankVi, standing.TitleVi, MetricTone.Positive),
            new("Uy Danh Hiện Có", standing.Fame.ToString("N0", CultureInfo.InvariantCulture),
                standing.CurrentTier >= 6 ? "Đỉnh phong" : $"Cần {standing.NextTierFame - standing.Fame:N0} điểm nữa ({standing.ProgressToNextTierPercent}%)",
                MetricTone.Neutral),
            new("Xếp Hạng Thiên Hạ", playerRankText, "Bảng Phong Thần", MetricTone.Positive),
            new("Phẩm Hạnh & Khí Tiết", $"Đức: {standing.Virtue} · Tín: {standing.Credibility}",
                standing.Virtue >= 60 ? "Nhân từ trọng nghĩa" : "Bình phàm thế nhân",
                standing.Virtue >= 60 ? MetricTone.Positive : MetricTone.Neutral)
        };

        // 2. Attention Alerts
        var attention = new List<ScreenAlert>();
        if (standing.CurrentTier < 6 && standing.ProgressToNextTierPercent >= 80)
        {
            attention.Add(new ScreenAlert(
                $"Tiến cảnh danh vọng đạt {standing.ProgressToNextTierPercent}%! Hãy thực hiện thêm chiến tích hoặc phát chẩn để đột phá lên đẳng cấp tiếp theo.",
                AlertLevel.Info));
        }

        // 3. Sections
        var sections = new List<ScreenSection>();

        // Section 1: Tier Ladder & Perks
        var tierRows = new List<ScreenRow>
        {
            BuildTierRow(0, "Hạ Sĩ", "Vô Danh Tiểu Tốt", "0", "Chưa có danh tiếng, buôn bán nhỏ lẻ hoặc làm thuê.", standing),
            BuildTierRow(1, "Bản Huyện Hào Kiệt", "Hào Kiệt Mới Nổi", "100", "Mở sạp hàng riêng ('Tiệm Của Bạn'), giảm 2% chi phí thị trường.", standing),
            BuildTierRow(2, "Quận Huyện Danh Sĩ", "Châu Huyện Chi Danh", "300", "Giao hảo danh sĩ Phẩm B, giảm 5% phí giao thương vùng hiện tại.", standing),
            BuildTierRow(3, "Châu Mục Hiền Sĩ", "Danh Chấn Một Vùng", "600", "Gia nhập thế lực chư hầu, chiêu mộ môn khách phẩm A, giảm 8% cước vận tải.", standing),
            BuildTierRow(4, "Triều Đình Trọng Thần", "Danh Chấn Hoa Hạ", "1.200", "Yết kiến Thái Thú & Thứ Sử, mở chức quan Tam Phẩm trở lên, giảm 12% thuế chợ.", standing),
            BuildTierRow(5, "Cột Trụ Giang Sơn", "Thiên Hạ Tri Danh", "2.500", "Danh tướng phẩm S (Quan Vũ, Triệu Vân...) tôn kính tiếp đãi; thỉnh viện binh.", standing),
            BuildTierRow(6, "Tam Quốc Chí Tôn", "Thiên Hạ Vô Song", "5.000+", "Đỉnh cao danh vọng thời loạn, vạn dân ngưỡng vọng, lưu danh thiên cổ sử sách.", standing)
        };
        sections.Add(new ScreenSection("CẤP BẬC DANH VỌNG & ĐẶC QUYỀN MỞ KHÓA", TierColumns, tierRows));

        // Section 2: Regional Standing
        var regionRows = regions.Select(r => new ScreenRow(
            [
                r.RegionNameVi,
                $"{r.Fame:N0} điểm",
                r.StandingTierVi,
                r.InfluenceSummaryVi
            ],
            Array.Empty<ScreenCommand>()
        )).ToList();
        sections.Add(new ScreenSection("UY DANH THEO 5 ĐẠI KHU VỰC CHIẾN LƯỢC", RegionColumns, regionRows));

        // Section 3: Hall of Fame / Leaderboard
        var leaderboardRows = leaderboard.Select(e => new ScreenRow(
            [
                $"#{e.Rank}",
                e.IsPlayer ? $"★ {e.NameVi} [BẢN THÂN]" : e.NameVi,
                e.RoleOrTitle,
                e.LocationName,
                $"{e.Fame:N0}",
                e.RenownTierVi
            ],
            Array.Empty<ScreenCommand>()
        )).ToList();
        sections.Add(new ScreenSection("BẢNG PHONG THẦN · XẾP HẠNG UY DANH THIÊN HẠ", LeaderboardColumns, leaderboardRows));

        // Section 4: Deeds Ledger
        var deedRows = deeds.Select(d => new ScreenRow(
            [
                d.DateText,
                FormatCategory(d.Category),
                FormatDelta(d.FameDelta, d.VirtueDelta, d.CredibilityDelta),
                d.TitleVi,
                d.NarrativeVi
            ],
            Array.Empty<ScreenCommand>()
        )).ToList();
        sections.Add(new ScreenSection("NHẬT KÝ CHIẾN TÍCH & SỰ KIỆN RẠNG DANH", DeedColumns, deedRows));

        // 4. Global Action Commands
        var commands = new List<ScreenCommand>
        {
            new("rep.charity_small", "Phát Chẩn Cứu Bần (50 Quan)", CommandKind.Primary),
            new("rep.charity_large", "Đại Tế Bá Tánh (200 Quan)", CommandKind.Primary),
            new("rep.banquet", "Yến Tiệc Hào Kiệt (100 Quan)", CommandKind.Secondary),
            new("rep.proclaim", "Tuyên Dương Đại Nghĩa (-20 Thể lực)", CommandKind.Secondary)
        };

        var statusLine = $"Danh hiệu: {standing.TitleVi} · Uy danh: {standing.Fame:N0} · {standing.PerkSummaryVi}";

        return new ScreenModel(
            ScreenId: ScreenId,
            Title: Title,
            StatusLine: statusLine,
            Sections: sections,
            Commands: commands,
            Metrics: headline,
            Alerts: attention);
    }

    private static ScreenRow BuildTierRow(
        int tier,
        string rank,
        string title,
        string threshold,
        string perk,
        ReputationStanding standing)
    {
        var isActive = standing.CurrentTier == tier;
        var isUnlocked = standing.CurrentTier >= tier;
        var status = isActive
            ? $"► Đang giữ ({standing.ProgressToNextTierPercent}%)"
            : isUnlocked ? "✓ Đã mở khóa" : "Chưa đạt";

        return new ScreenRow(
            [
                isActive ? $"★ Cấp {tier} ({rank})" : $"Cấp {tier} ({rank})",
                title,
                threshold,
                status,
                perk
            ],
            Array.Empty<ScreenCommand>()
        );
    }

    private static string FormatCategory(string cat) => cat switch
    {
        "milestone" => "Cột Mốc",
        "trade" => "Thương Dịch",
        "military" => "Binh Nghiệp",
        "duel" => "Lôi Đài",
        "court" => "Quan Lộ",
        "charity" => "Thi Ân",
        "social" => "Giao Tế",
        "speech" => "Khí Tiết",
        _ => "Chiến Tích"
    };

    private static string FormatDelta(int fame, int virtue, int cred)
    {
        var parts = new List<string>(3);
        if (fame != 0) parts.Add($"+{fame} Uy danh");
        if (virtue != 0) parts.Add($"+{virtue} Đức");
        if (cred != 0) parts.Add($"+{cred} Tín");
        return parts.Count > 0 ? string.Join(", ", parts) : "Không đổi";
    }
}
