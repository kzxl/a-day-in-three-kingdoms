using SimT.Contracts.Playing;
using SimT.Contracts.Ui;

namespace SimT.Modules.Base.Player;

/// <summary>
/// Screen presentation for the PoE-style Bách Gia Thiên Mệnh passive talent web.
/// Renders school clusters, active keystone modifiers, point economies, and one-off milestones.
/// </summary>
internal sealed class PlayerDestinyScreen : IScreenContribution
{
    private static readonly string[] TalentColumns =
        ["Học Phái", "Phẩm Cấp", "Bí Pháp Thiên Mệnh", "Hiệu Ứng Bị Động", "Yêu Cầu Môn Phái", "Trạng Thái"];

    private static readonly string[] MilestoneColumns =
        ["Mã Hiệu", "Lĩnh Vực", "Kỳ Ngộ Đột Phá", "Mô Tả & Điều Kiện", "Thưởng Thiên Mệnh", "Trạng Thái"];

    private readonly IPlayerDestiny _destiny;

    public PlayerDestinyScreen(IPlayerDestiny destiny)
    {
        _destiny = destiny;
    }

    public string ScreenId => "player.destiny";

    public string Title => "Thiên Mệnh";

    public int Order => 13;

    public ScreenModel Render()
    {
        _destiny.EvaluateMilestones();
        var state = _destiny.State;
        var allNodes = _destiny.AllNodes;

        var metrics = new List<ScreenMetric>
        {
            new("Tổng Thiên Mệnh", $"{state.TotalPointsEarned} Điểm", "Tổng điểm thu thập từ đời người và kỳ ngộ"),
            new("Đã Phân Bổ", $"{state.PointsSpent} Điểm", "Số điểm đã dùng để mở khóa các bí pháp"),
            new("Điểm Tự Do", $"{state.UniversalPointsAvailable} Điểm", "Điểm có thể kích hoạt mọi nhánh"),
            new("Điểm Ngộ Đạo", FormatResonancePoints(state.ResonancePointsAvailable), "Điểm tương thích chuyên biệt từng phái")
        };

        var talentRows = new List<ScreenRow>();
        foreach (var node in allNodes.OrderBy(n => n.School).ThenBy(n => n.Tier).ThenBy(n => n.GridX))
        {
            var isLearned = state.LearnedNodeIds.Contains(node.Id);
            var canLearn = !isLearned && _destiny.CanLearnNode(node.Id, out _);

            var schoolText = GetSchoolName(node.School);
            var tierText = GetTierName(node.Tier);
            var statusText = isLearned ? "Đã Đắc Đạo ✓" : canLearn ? "Khả Dụng ✦" : "Chưa Khai Thông";

            var commands = new List<ScreenCommand>();
            if (canLearn)
            {
                commands.Add(new ScreenCommand($"destiny:learn:{node.Id}", "Lĩnh Hội ✦", CommandKind.Primary));
            }

            var prereqDesc = node.RequiredSchoolPoints > 0
                ? $"{node.RequiredSchoolPoints} điểm môn phái"
                : "Căn Bản";

            var details = new List<ScreenDetail>
            {
                new("Mã Bí Pháp", node.Id),
                new("Thuộc Phái", schoolText),
                new("Cấp Độ", tierText),
                new("Chi Phí Điểm", $"{node.PointCost} Điểm"),
                new("Điểm Phái Yêu Cầu", prereqDesc),
                new("Mạch Tiên Quyết", node.PrerequisiteIds.Count > 0 ? string.Join(", ", node.PrerequisiteIds) : "Gốc Khởi Đầu"),
                new("Hiệu Ứng Bị Động", node.SummaryVi),
                new("Trạng Thái", statusText)
            };

            talentRows.Add(new ScreenRow(
                [
                    schoolText,
                    tierText,
                    node.NameVi,
                    node.SummaryVi,
                    prereqDesc,
                    statusText
                ],
                commands,
                Title: null,
                Details: details));
        }

        var milestoneRows = new List<ScreenRow>();
        foreach (var ms in state.Milestones)
        {
            var statusText = ms.IsCompleted ? $"Đạt được (Ngày {ms.CompletedDay}) ✓" : "Đang Thực Hiện…";

            var rewardText = ms.UniversalPoints > 0 && ms.ResonancePoints > 0
                ? $"+{ms.UniversalPoints} Tự Do, +{ms.ResonancePoints} {GetSchoolShortName(ms.ResonanceSchool)}"
                : ms.UniversalPoints > 0
                    ? $"+{ms.UniversalPoints} Điểm Tự Do"
                    : $"+{ms.ResonancePoints} Điểm {GetSchoolShortName(ms.ResonanceSchool)}";

            var details = new List<ScreenDetail>
            {
                new("Mã Kỳ Ngộ", ms.Code),
                new("Phân Loại", GetCategoryVi(ms.Category)),
                new("Chiến Tích", ms.NameVi),
                new("Mô Tả", ms.DescriptionVi),
                new("Phần Thưởng", rewardText),
                new("Tình Trạng", statusText)
            };

            milestoneRows.Add(new ScreenRow(
                [
                    ms.Code,
                    GetCategoryVi(ms.Category),
                    ms.NameVi,
                    ms.DescriptionVi,
                    rewardText,
                    statusText
                ],
                Array.Empty<ScreenCommand>(),
                Title: null,
                Details: details));
        }

        var sections = new List<ScreenSection>
        {
            new("MẠNG LƯỚI BÁCH GIA THIÊN MỆNH (TƯ TƯỞNG & BÍ PHÁP)", TalentColumns, talentRows),
            new("KỲ NGỘ & MỐC ĐỘT PHÁ THIÊN HẠ (NGUỒN ĐIỂM THIÊN MỆNH)", MilestoneColumns, milestoneRows)
        };

        var globalCommands = new List<ScreenCommand>
        {
            new("destiny:evaluate", "⚡ Cập Nhật Kỳ Ngộ", CommandKind.Secondary),
            new("destiny:respec", "↺ Tẩy Tủy Tu Tâm (Tẩy Điểm)", CommandKind.Secondary)
        };

        var activeKeystones = allNodes
            .Where(n => n.Tier == DestinyNodeTier.Keystone && state.LearnedNodeIds.Contains(n.Id))
            .Select(n => $"[{n.NameVi}]")
            .ToList();

        var activeMasteries = allNodes
            .Where(n => n.Tier == DestinyNodeTier.Mastery && state.LearnedNodeIds.Contains(n.Id))
            .Select(n => $"[✵ {n.NameVi}]")
            .ToList();

        var highlights = new List<string>();
        if (activeKeystones.Count > 0) highlights.Add($"Keystone: {string.Join(", ", activeKeystones)}");
        if (activeMasteries.Count > 0) highlights.Add($"Đại Tông Sư: {string.Join(", ", activeMasteries)}");

        var keystoneSummary = highlights.Count > 0
            ? string.Join(" · ", highlights)
            : "Chưa kích hoạt Keystone hoặc Mastery nào. Hãy định hình đường đi trên cây thiên mệnh!";

        return new ScreenModel(
            ScreenId,
            "Bách Gia Thiên Mệnh",
            $"Ngân sách thiên mệnh: {state.UniversalPointsAvailable} điểm tự do. {keystoneSummary}",
            sections,
            globalCommands,
            metrics,
            Recommendation: state.UniversalPointsAvailable > 0
                ? $"Bạn đang có {state.UniversalPointsAvailable} điểm Thiên Mệnh chưa dùng — hãy khai mở nút mệnh cách phù hợp với hướng phát triển."
                : "Hoàn thành kỳ ngộ và rèn luyện bách nghệ để tích lũy thêm điểm Thiên Mệnh.");
    }

    private static string FormatResonancePoints(IReadOnlyDictionary<DestinySchool, int> resonance)
    {
        var active = resonance
            .Where(kv => kv.Value > 0)
            .Select(kv => $"{GetSchoolShortName(kv.Key)}: +{kv.Value}")
            .ToList();

        return active.Count > 0 ? string.Join(", ", active) : "0 Điểm";
    }

    private static string GetSchoolShortName(DestinySchool school) => school switch
    {
        DestinySchool.DaoChu => "Đào Chu",
        DestinySchool.BaDao => "Pháp Gia",
        DestinySchool.VuongDao => "Nho Gia",
        DestinySchool.BinhGia => "Binh Gia",
        DestinySchool.MacGia => "Mặc Gia",
        DestinySchool.DaoGia => "Đạo Gia",
        _ => "Tự Do"
    };

    private static string GetSchoolName(DestinySchool school) => school switch
    {
        DestinySchool.DaoChu => "Đào Chu (Thương Mại)",
        DestinySchool.BaDao => "Pháp Gia (Đồn Điền)",
        DestinySchool.VuongDao => "Nho Gia (Vương Đạo)",
        DestinySchool.BinhGia => "Binh Gia (Tiêu Cục)",
        DestinySchool.MacGia => "Mặc Gia (Kỹ Nghệ)",
        DestinySchool.DaoGia => "Đạo Gia (Dưỡng Sinh)",
        _ => "Căn Bản"
    };

    private static string GetTierName(DestinyNodeTier tier) => tier switch
    {
        DestinyNodeTier.Root => "Khởi Điểm",
        DestinyNodeTier.Minor => "Tiểu Khí",
        DestinyNodeTier.Notable => "Đại Huệ",
        DestinyNodeTier.Keystone => "KEYSTONE",
        DestinyNodeTier.Mastery => "ĐẠI TÔNG SƯ ✵",
        _ => "Căn Bản"
    };

    private static string GetCategoryVi(string category) => category switch
    {
        "wealth" => "Tài Phú",
        "exploration" => "Cương Vực",
        "court" => "Quan Trường",
        "martial" => "Binh Nghiệp",
        "farm" => "Điền Trang",
        "caravan" => "Thương Đạo",
        "social" => "Xã Giao",
        "life" => "Sinh Mệnh",
        _ => category
    };
}
