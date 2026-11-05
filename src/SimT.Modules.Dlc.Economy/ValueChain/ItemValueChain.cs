using SimT.Contracts.Playing;

namespace SimT.Modules.Dlc.Economy.ValueChain;

/// <summary>
/// Economic value tier classification representing the closed macro-economic supply chain:
/// Tier 1: Raw Materials (Nông nghiệp, Tiều tiều mỏ, Thảo mộc dã ngoại)
/// Tier 2: Intermediate Goods (Sơ chế, Phôi luyện kim, Vải gai, Phân bón)
/// Tier 3: Manufactured & Equipment (Thành phẩm chế tác, Lương thảo, Nông cụ, Y dược, Binh trang)
/// Tier 4: Luxury & End-Sink (Xa xỉ phẩm, Quan lại biếu xén, Dưỡng thần, Xuất khẩu ngoại bang)
/// </summary>
public enum EconomicValueTier
{
    Tier1_Raw = 1,
    Tier2_Processed = 2,
    Tier3_Manufactured = 3,
    Tier4_Luxury_EndSink = 4
}

/// <summary>
/// Detailed value chain profile of a commodity item in the Three Kingdoms economic ecosystem.
/// </summary>
public sealed record ValueChainProfile(
    EconomicValueTier Tier,
    string TierNameVi,
    string BadgeVi,
    string CategoryVi,
    string PrimaryConsumerVi,
    string EconomicRoleVi,
    IReadOnlyList<string> Inputs,
    IReadOnlyList<string> Outputs);

/// <summary>
/// Static registry and utility for the 4-Tier Economic Value Chain.
/// Provides deep-module integration across farming plots, craft workshops, and market stalls.
/// </summary>
public static class ItemValueChain
{
    private static readonly Dictionary<string, ValueChainProfile> Profiles = new(StringComparer.OrdinalIgnoreCase)
    {
        // === TIER 1: RAW MATERIALS ===
        ["gao"] = new(
            EconomicValueTier.Tier1_Raw,
            "Nguyên Liệu Thô",
            "🌾 [Bậc 1: Thô]",
            "Lương thực căn bản",
            "Bách tính, Quân đội & Điền trang",
            "Lương thực thiết yếu duy trì ổn định dân sinh và hành quân.",
            [],
            ["ruou", "ca_kho"]),

        ["lua_mi"] = new(
            EconomicValueTier.Tier1_Raw,
            "Nguyên Liệu Thô",
            "🌾 [Bậc 1: Thô]",
            "Nông sản hoa màu",
            "Lò bánh, Xưởng ủ rượu & Bách tính",
            "Nông sản miền Bắc dùng làm lương thực dự trữ và nấu rượu.",
            [],
            ["ruou"]),

        ["than_da"] = new(
            EconomicValueTier.Tier1_Raw,
            "Nguyên Liệu Thô",
            "⛏️ [Bậc 1: Thô]",
            "Nhiên liệu khoáng thạch",
            "Lò rèn, Xưởng luyện kim & Sưởi ấm",
            "Khoáng thạch nhiệt năng cao, nhiên liệu không thể thiếu để nấu quặng.",
            [],
            ["sat", "dong", "cuoc_nong", "liem_gat"]),

        ["go"] = new(
            EconomicValueTier.Tier1_Raw,
            "Nguyên Liệu Thô",
            "🪵 [Bậc 1: Thô]",
            "Lâm sản khai thác",
            "Thợ mộc, Xưởng đóng xe & Xây dựng",
            "Gỗ rừng phục vụ dựng nhà, đóng xe ngựa đoàn buôn và cán binh khí.",
            [],
            ["cuoc_nong", "liem_gat", "binh_tuoi"]),

        ["hat_giong_lua"] = new(
            EconomicValueTier.Tier1_Raw,
            "Nguyên Liệu Thô",
            "🌱 [Bậc 1: Thô]",
            "Hạt giống gieo cấy",
            "Nông dân & Điền trang",
            "Mầm sống của ruộng đồng, sản sinh ra thóc gạo mùa thu hoạch.",
            [],
            ["gao"]),

        ["hat_giong_tra"] = new(
            EconomicValueTier.Tier1_Raw,
            "Nguyên Liệu Thô",
            "🌱 [Bậc 1: Thô]",
            "Hạt giống gieo cấy",
            "Trang trại trà Giang Nam",
            "Cây giống trà đặc sản miền núi, cho thu hoạch lá trà xanh thượng hạng.",
            [],
            ["tra"]),

        ["hat_giong_dau_tam"] = new(
            EconomicValueTier.Tier1_Raw,
            "Nguyên Liệu Thô",
            "🌱 [Bậc 1: Thô]",
            "Cây giống dâu tằm",
            "Làng nghề dệt vải",
            "Cung cấp lá nuôi tằm nhả tơ, khởi nguồn của tơ lụa Trung Nguyên.",
            [],
            ["vai", "lua", "gam"]),

        ["hat_giong_duoc_thao"] = new(
            EconomicValueTier.Tier1_Raw,
            "Nguyên Liệu Thô",
            "🌱 [Bậc 1: Thô]",
            "Hạt giống dược liệu",
            "Y quán & Dược điền",
            "Dược thảo trị bệnh, dùng bào chế kim sang dược và đan dịch bổ khí.",
            [],
            ["thuoc", "sam"]),

        // === TIER 2: INTERMEDIATE PROCESSED GOODS ===
        ["muoi"] = new(
            EconomicValueTier.Tier2_Processed,
            "Vật Liệu Sơ Chế",
            "🧂 [Bậc 2: Sơ Chế]",
            "Diêm nghiệp độc quyền",
            "Toàn dân, Binh doanh & Ướp thực phẩm",
            "Muối biển và mỏ muối phơi sấy, huyết mạch ngân sách triều đình.",
            [],
            ["ca_kho"]),

        ["ca_kho"] = new(
            EconomicValueTier.Tier2_Processed,
            "Vật Liệu Sơ Chế",
            "🐟 [Bậc 2: Sơ Chế]",
            "Thực phẩm chế biến",
            "Quân lương dã chiến & Bách tính",
            "Cá tươi sông ngòi phơi khô ướp muối, lương thực dã chiến không lo ôi thiu.",
            ["muoi"],
            []),

        ["sat"] = new(
            EconomicValueTier.Tier2_Processed,
            "Vật Liệu Sơ Chế",
            "⚙️ [Bậc 2: Sơ Chế]",
            "Kim khí luyện đúc",
            "Xưởng vũ khí & Nông cụ",
            "Phôi sắt tinh luyện từ lò nung than đá, nền móng của võ bị và nông khí.",
            ["than_da"],
            ["cuoc_nong", "liem_gat"]),

        ["dong"] = new(
            EconomicValueTier.Tier2_Processed,
            "Vật Liệu Sơ Chế",
            "🪙 [Bậc 2: Sơ Chế]",
            "Kim loại màu chế tác",
            "Tiền cuộc đúc tiền & Thợ khí",
            "Đồng thau nguyên khối, dùng đúc ngũ thù tiền, đỉnh đồng và đồ nghi trượng.",
            ["than_da"],
            []),

        ["vai"] = new(
            EconomicValueTier.Tier2_Processed,
            "Vật Liệu Sơ Chế",
            "🧵 [Bậc 2: Sơ Chế]",
            "Dệt may bình dân",
            "May mặc thứ dân & Lều quân doanh",
            "Vải gai thô dệt thủ công, dùng may áo vải thứ dân và bạt che quân nhu.",
            ["hat_giong_dau_tam"],
            []),

        ["phan_bon"] = new(
            EconomicValueTier.Tier2_Processed,
            "Vật Liệu Sơ Chế",
            "🌿 [Bậc 2: Sơ Chế]",
            "Vật tư điền trang",
            "Điền trang & Ruộng lúa",
            "Ủ mục từ phế phẩm hữu cơ và tro than, phục hồi độ phì nhiêu của thổ nhưỡng.",
            [],
            []),

        // === TIER 3: MANUFACTURED & MILITARY EQUIPMENT ===
        ["ruou"] = new(
            EconomicValueTier.Tier3_Manufactured,
            "Thành Phẩm & Tiêu Dùng",
            "🍶 [Bậc 3: Thành Phẩm]",
            "Mỹ tửu dân gian",
            "Quán trọ, Tiệc rượu hào kiệt & Tửu lầu",
            "Rượu nếp ủ men truyền thống, gia tăng hào cảm khi giao tế và giải tỏa lao lực.",
            ["gao", "lua_mi"],
            []),

        ["lua"] = new(
            EconomicValueTier.Tier3_Manufactured,
            "Thành Phẩm & Tiêu Dùng",
            "👘 [Bậc 3: Thành Phẩm]",
            "Vải vóc trung lưu",
            "Thương gia, Thân sĩ & Cung đình",
            "Lụa óng ả dệt từ tơ tằm thượng hạng, cống phẩm giá trị liên thành.",
            ["hat_giong_dau_tam"],
            ["gam"]),

        ["thuoc"] = new(
            EconomicValueTier.Tier3_Manufactured,
            "Thành Phẩm & Tiêu Dùng",
            "💊 [Bậc 3: Thành Phẩm]",
            "Y dược tề thế",
            "Thương binh, Bệnh nhân & Y quán",
            "Đan dược tán bột từ thảo mộc quý, cứu trị vết thương đao kiếm và dịch bệnh.",
            ["hat_giong_duoc_thao"],
            []),

        ["cuoc_nong"] = new(
            EconomicValueTier.Tier3_Manufactured,
            "Thành Phẩm & Tiêu Dùng",
            "🛠️ [Bậc 3: Thành Phẩm]",
            "Khí cụ canh tác",
            "Nông hộ điền trang",
            "Nông cụ lưỡi sắt tra cán gỗ dẻo dai, khai khẩn và xới đất năng suất cao.",
            ["sat", "go"],
            []),

        ["liem_gat"] = new(
            EconomicValueTier.Tier3_Manufactured,
            "Thành Phẩm & Tiêu Dùng",
            "🛠️ [Bậc 3: Thành Phẩm]",
            "Khí cụ thu hoạch",
            "Nông hộ gặt lúa",
            "Lưỡi thép hình bán nguyệt sắc bén, công cụ bắt buộc khi thu hoạch mùa màng.",
            ["sat", "go"],
            []),

        ["binh_tuoi"] = new(
            EconomicValueTier.Tier3_Manufactured,
            "Thành Phẩm & Tiêu Dùng",
            "🛠️ [Bậc 3: Thành Phẩm]",
            "Thủy lợi nông khí",
            "Công trình mương máng",
            "Gầu múc nước dẫn thủy nhập điền, ngăn ngừa khô hạn đất đai.",
            ["go"],
            []),

        // === TIER 4: LUXURY & END-GAME SINKS ===
        ["tra"] = new(
            EconomicValueTier.Tier4_Luxury_EndSink,
            "Thượng Phẩm Xa Xỉ",
            "🍵 [Bậc 4: Thượng Phẩm]",
            "Văn hóa phẩm nhã nhặn",
            "Danh sĩ, Văn nhân & Trà quán cao cấp",
            "Trà búp sấy khô thanh tao, vật phẩm kết giao danh sĩ và đàm đạo chính sự.",
            ["hat_giong_tra"],
            []),

        ["gam"] = new(
            EconomicValueTier.Tier4_Luxury_EndSink,
            "Thượng Phẩm Xa Xỉ",
            "👑 [Bậc 4: Thượng Phẩm]",
            "Tơ lụa hoàng tộc",
            "Hoàng thất, Vương hầu & Chư hầu",
            "Gấm Thục thêu hoa ngũ sắc rực rỡ, trang phục quý tộc và bảo vật ngoại giao.",
            ["lua"],
            []),

        ["sam"] = new(
            EconomicValueTier.Tier4_Luxury_EndSink,
            "Thượng Phẩm Xa Xỉ",
            "✨ [Bậc 4: Thượng Phẩm]",
            "Dược liệu thần diệu",
            "Lão thần, Bệnh hiểm nghèo & Đại y sư",
            "Nhân sâm ngàn năm vùng Liêu Đông, bổ khí hoàn dương, bảo vật kéo dài tuổi thọ.",
            ["hat_giong_duoc_thao"],
            []),

        ["tram_huong"] = new(
            EconomicValueTier.Tier4_Luxury_EndSink,
            "Thượng Phẩm Xa Xỉ",
            "🪔 [Bậc 4: Thượng Phẩm]",
            "Hương liệu quý hiếm",
            "Cung điện, Thiền viện & Tế lễ",
            "Trầm tự nhiên từ rừng sâu phương Nam, xua tan uế khí, lễ vật tiến cống thiên tử.",
            [],
            [])
    };

    private static readonly ValueChainProfile FallbackProfile = new(
        EconomicValueTier.Tier2_Processed,
        "Hàng Hóa Tổng Hợp",
        "📦 [Bậc 2: Hàng Hóa]",
        "Vật phẩm thông dụng",
        "Thương nhân & Bách tính",
        "Hàng hóa lưu thông thương nghiệp.",
        [],
        []);

    /// <summary>
    /// Retrieves the value chain profile of a given item code.
    /// </summary>
    public static ValueChainProfile GetProfile(string itemCode) =>
        Profiles.TryGetValue(itemCode, out var profile) ? profile : FallbackProfile;

    /// <summary>
    /// Retrieves the economic tier of an item code.
    /// </summary>
    public static EconomicValueTier GetTier(string itemCode) => GetProfile(itemCode).Tier;

    /// <summary>
    /// Formatted Vietnamese badge string suitable for UI grids and screen details.
    /// </summary>
    public static string FormatBadge(string itemCode) => GetProfile(itemCode).BadgeVi;

    /// <summary>
    /// Check if item A serves as a direct input recipe ingredient for item B.
    /// </summary>
    public static bool IsInputFor(string inputCode, string targetCode)
    {
        var target = GetProfile(targetCode);
        return target.Inputs.Contains(inputCode, StringComparer.OrdinalIgnoreCase);
    }
}
