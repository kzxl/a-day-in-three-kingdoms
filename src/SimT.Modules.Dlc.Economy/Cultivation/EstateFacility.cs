using SimT.Contracts.Playing;

namespace SimT.Modules.Dlc.Economy;

/// <summary>
/// Specifications and benefits for multi-tier estate facilities (Công Trình Điền Trang / Ổ Bảo).
/// Each facility has 5 tiers with distinct economic, security or agricultural utility.
/// </summary>
internal static class FacilitySpecs
{
    public const string Granary = "GRANARY";
    public const string Irrigation = "IRRIGATION";
    public const string Brewery = "BREWERY";
    public const string Defense = "DEFENSE";

    public static readonly string[] AllTypes = [Granary, Irrigation, Brewery, Defense];

    public static string TypeName(string type) => type switch
    {
        Granary => "Vựa Thao Thương",
        Irrigation => "Thủy Lực Chi Thao",
        Brewery => "Tửu Phường Danh Môn",
        Defense => "Ổ Bảo Tường Lũy",
        _ => "Công Trình Điền Trang"
    };

    public static (string Name, string TierTitle, string Effect, long Cost) GetTierInfo(string type, int tier) => (type, tier) switch
    {
        (Granary, 1) => ("Vựa Thao Thương", "Chòi Thóc Tre Nứa", "Trữ tối đa 150 lương, giảm 25% hao mòn ẩm mốc", 100),
        (Granary, 2) => ("Vựa Thao Thương", "Vựa Thóc Gạch Ngói", "Trữ tối đa 400 lương, giảm 50% hao mòn", 250),
        (Granary, 3) => ("Vựa Thao Thương", "Đại Sùng Bảo Tồn", "Trữ 1.000 lương, giảm 80% hao mòn, tăng 5% giá bán thóc", 600),
        (Granary, 4) => ("Vựa Thao Thương", "Thiên Phủ Quân Lương Khố", "Trữ 2.500 lương, miễn 100% mốc, tăng 10% giá bán thóc", 1500),
        (Granary, 5) => ("Vựa Thao Thương", "Càn Khôn Đại Kho", "Trữ 5.000 lương, giảm 50% chi phí giống gieo hạt", 3500),

        (Irrigation, 1) => ("Thủy Lực Chi Thao", "Mương Dẫn Nước Đá", "+10 độ phì đất, đất hồi +2/tháng", 80),
        (Irrigation, 2) => ("Thủy Lực Chi Thao", "Guồng Long Cốt Phiên Xa", "+20 độ phì đất, đất hồi +4/tháng, lúa chín sớm 2 ngày", 200),
        (Irrigation, 3) => ("Thủy Lực Chi Thao", "Kênh Đào Liên Hoàn", "+30 độ phì đất, đất hồi +6/tháng, chín sớm 4 ngày, +10% sản lượng", 500),
        (Irrigation, 4) => ("Thủy Lực Chi Thao", "Hồ Trữ Nước Đập Tràn", "+40 độ phì đất, đất hồi +8/tháng, chín sớm 6 ngày, +20% sản lượng", 1200),
        (Irrigation, 5) => ("Thủy Lực Chi Thao", "Đô Giang Yển Thần Công", "Đất luôn tối thiểu 80, hồi +12/tháng, +30% sản lượng toàn khu", 3000),

        (Brewery, 1) => ("Tửu Phường Danh Môn", "Lò Ủ Rượu Nếp Dân Gian", "Ủ lúa thành Rượu nếp dân dã, lợi nhuận 1.4x", 120),
        (Brewery, 2) => ("Tửu Phường Danh Môn", "Hầm Rượu Thiệu Hưng", "Nấu Thiệu Hưng Danh Tửu, làm quà tặng danh sĩ +5 Thân mật", 300),
        (Brewery, 3) => ("Tửu Phường Danh Môn", "Tửu Các Đỗ Khang", "Nấu Đỗ Khang Danh Tửu, quà tặng danh sĩ +12 Thân mật", 700),
        (Brewery, 4) => ("Tửu Phường Danh Môn", "Ngự Thiềm Tửu Phường", "Nấu Cung Đình Ngọc Dịch, quà tặng danh tướng +20 Thân mật", 1800),
        (Brewery, 5) => ("Tửu Phường Danh Môn", "Quỳnh Tương Ngọc Lộ Cung", "Danh tửu thiên hạ, thu lãi bổng lộc thụ động 100 Quan/tháng", 4000),

        (Defense, 1) => ("Ổ Bảo Tường Lũy", "Hàng Rào Cọc Nhọn", "Giảm 50% nguy cơ trộm cắp hoa lợi nông trang", 90),
        (Defense, 2) => ("Ổ Bảo Tường Lũy", "Chòi Canh Tháp Tên", "Triệt tiêu 100% trộm cắp nội bộ, an ninh tăng cao", 220),
        (Defense, 3) => ("Ổ Bảo Tường Lũy", "Thao Trường Bộ Khúc", "Nuôi dưỡng bộ khúc hộ vệ, tăng +5 EXP Thống soái/tháng", 550),
        (Defense, 4) => ("Ổ Bảo Tường Lũy", "Kiên Thành Ổ Bảo", "Tường đá kiên cố, hồi phục thể lực gấp đôi khi nghỉ ngơi tại đây", 1400),
        (Defense, 5) => ("Ổ Bảo Tường Lũy", "Thiết Huyết Quân Doanh", "Căn cứ phòng thủ bất khả xâm phạm, uy danh hiển hách (+100 Danh tiếng)", 3200),

        _ => ("Công Trình", "Chưa xây", "Chưa có hiệu ứng", 100)
    };
}
