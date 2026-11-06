using SimT.Contracts.Localization;

namespace SimT.Core.Localization.Languages;

/// <summary>
/// Built-in canonical Vietnamese system strings fallback.
/// Business domain strings are loaded dynamically from languages/ folder.
/// </summary>
public sealed class VietnameseLanguagePackage : BaseLanguagePackage
{
    public VietnameseLanguagePackage()
        : base(
            new LanguageInfo("vi", "Tiếng Việt", "Tiếng Việt", "vi-VN"),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["app.title"] = "SimT — Nhất Nhật Tam Quốc",
                ["app.subtitle"] = "Mô phỏng Xã hội, Thương Nghiệp & Chiến Lược Cổ Đại",
                ["app.version"] = "Phiên bản",
                ["app.settings"] = "Thiết Lập Trò Chơi",
                ["app.exit"] = "Thoát Trò Chơi",
                ["app.save"] = "Lưu Tiến Trình",
                ["app.load"] = "Tải Lại Tiến Trình",
                ["time.day"] = "Ngày",
                ["time.month"] = "Tháng",
                ["time.year"] = "Năm",
                ["time.turn"] = "Lượt",
                ["time.spring"] = "Mùa Xuân",
                ["time.summer"] = "Mùa Hạ",
                ["time.autumn"] = "Mùa Thu",
                ["time.winter"] = "Mùa Đông",
                ["common.ok"] = "Đồng ý",
                ["common.cancel"] = "Hủy bỏ",
                ["common.close"] = "Đóng"
            })
    {
    }
}
