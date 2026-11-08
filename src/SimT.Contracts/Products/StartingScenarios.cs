using SimT.Contracts.Time;
namespace SimT.Contracts.Products;

public sealed record ScenarioFaction(string NameVi, string ColorHex);
public sealed record StartingScenario(string Code, string NameVi, string DescriptionVi,
    GameDate StartDate, string SuggestedCityCode, string SuggestedCityVi,
    IReadOnlyList<ScenarioFaction> Factions);

public static class StartingScenarios
{
    public static IReadOnlyList<StartingScenario> All { get; } = [
        new("184", "Khăn Vàng nổi dậy", "Hán thất suy vi, Thái Bình đạo nổi dậy. Thương lộ giữa các châu đang chờ người dám bước vào thời loạn.",
            new(184, 5, 2), "lac_duong", "Lạc Dương", [new("Hán triều", "#a8a29e"), new("Khăn Vàng", "#c9a227"), new("Tôn Kiên", "#3b82f6")]),
        new("190", "Chư hầu thảo Đổng", "Đổng Trác khống chế kinh thành, chư hầu chuẩn bị hội minh. Chiến sự sắp chia cắt những đường buôn quanh Lạc Dương.",
            new(190, 1, 1), "lac_duong", "Lạc Dương", [new("Đổng Trác", "#71717a"), new("Viên Thiệu", "#eab308"), new("Tào Tháo", "#ef4444"), new("Tôn Kiên", "#3b82f6")]),
        new("200", "Quan Độ", "Tào Tháo và Viên Thiệu đối đầu ở miền Bắc. Giang Đông vừa chuyển sang tay Tôn Quyền, còn Lưu Bị đang tìm đường lập nghiệp.",
            new(200, 7, 1), "hua_xuong", "Hứa Xương", [new("Tào Tháo", "#ef4444"), new("Viên Thiệu", "#eab308"), new("Tôn Quyền", "#3b82f6"), new("Lưu Biểu", "#14b8a6")]),
        new("208", "Xích Bích", "Tào Tháo đã làm chủ miền Bắc, binh phong đang hướng về Kinh Châu. Lưu Bị có Gia Cát Lượng trợ giúp, còn Tôn Quyền giữ Giang Đông.",
            new(208, 7, 1), "tuong_duong", "Tương Dương", [new("Tào Tháo", "#ef4444"), new("Lưu Bị", "#22c55e"), new("Tôn Quyền", "#3b82f6"), new("Lưu Biểu", "#14b8a6")])
    ];
}
