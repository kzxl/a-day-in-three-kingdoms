using SimT.Contracts.Playing;

namespace SimT.Modules.Dlc.Economy;

/// <summary>A crop a plot can grow: what it yields, how long it takes, how much land it needs, and its perennial lifecycle.</summary>
internal sealed record CropSpec(
    string Code,
    long OutputItemId,
    int FixedGrowthDays,
    int YieldPerMu,
    string SeedItemCode,
    long SeedItemId,
    string SeedNameVi,
    string Lifecycle = "ANNUAL",
    int MaxHarvests = 1,
    int RegrowthDays = 0,
    int MinSoil = 0,
    string CropTitleVi = "")
{
    // === 1. ANNUAL FIELD CROPS (Hoa màu ngắn ngày — 1 vụ đứt gốc) ===
    public static readonly CropSpec Rice = new(
        "gao", 1, 0, 8, "hat_giong_lua", 17, "Hạt giống lúa",
        Lifecycle: "ANNUAL", MaxHarvests: 1, RegrowthDays: 0, MinSoil: 0, CropTitleVi: "Lúa Nước (Ngắn ngày)");

    public static readonly CropSpec Wheat = new(
        "lua_mi", 9, 22, 7, "hat_giong_lua", 17, "Hạt giống mạch (Lúa mì)",
        Lifecycle: "ANNUAL", MaxHarvests: 1, RegrowthDays: 0, MinSoil: 0, CropTitleVi: "Lúa Mì Chịu Hàn (Ngắn ngày)");

    public static readonly CropSpec Hemp = new(
        "day_gai", 11, 20, 4, "hat_giong_dau_tam", 19, "Giống đay gai dệt vải",
        Lifecycle: "ANNUAL", MaxHarvests: 1, RegrowthDays: 0, MinSoil: 0, CropTitleVi: "Đay Gai Dệt Vải (Ngắn ngày)");

    public static readonly CropSpec Herb = new(
        "duoc_thao", 7, 20, 2, "hat_giong_duoc_thao", 20, "Hạt giống dược thảo",
        Lifecycle: "ANNUAL", MaxHarvests: 1, RegrowthDays: 0, MinSoil: 0, CropTitleVi: "Dược Thảo Thường (Ngắn ngày)");

    // === 2. PERENNIAL RATOON CROPS (Cây công nghiệp & lâm mộc lưu gốc — thu hoạch 4-5 vụ liên tiếp) ===
    public static readonly CropSpec Tea = new(
        "tra", 3, 24, 2, "hat_giong_tra", 18, "Hạt giống trà",
        Lifecycle: "PERENNIAL", MaxHarvests: 4, RegrowthDays: 14, MinSoil: 0, CropTitleVi: "Đồi Trà Cổ Thụ (Lưu gốc 4 vụ)");

    public static readonly CropSpec Mulberry = new(
        "dau_tam", 4, 30, 2, "hat_giong_dau_tam", 19, "Cây giống dâu tằm",
        Lifecycle: "PERENNIAL", MaxHarvests: 4, RegrowthDays: 15, MinSoil: 0, CropTitleVi: "Vườn Dâu Nuôi Tằm (Lưu gốc 4 vụ)");

    public static readonly CropSpec BambooTimber = new(
        "truc_lam", 6, 28, 5, "hat_giong_tra", 18, "Mầm giống trúc & tùng bách",
        Lifecycle: "PERENNIAL", MaxHarvests: 5, RegrowthDays: 16, MinSoil: 0, CropTitleVi: "Rừng Trúc Tùng Bách (Lưu gốc 5 vụ)");

    // === 3. AGED PERENNIAL CROPS (Linh dược & Linh mộc tích niên đại — đòi hỏi đất >= 70) ===
    public static readonly CropSpec Ginseng = new(
        "nhan_sam", 13, 45, 1, "hat_giong_duoc_thao", 20, "Mầm giống Nhân sâm núi",
        Lifecycle: "AGED_PERENNIAL", MaxHarvests: 2, RegrowthDays: 30, MinSoil: 70, CropTitleVi: "Nhân Sâm Linh Điền (Tích niên 2 vụ)");

    public static readonly CropSpec Agarwood = new(
        "tram_huong", 14, 50, 1, "hat_giong_dau_tam", 19, "Cây giống Dó bầu tụ trầm",
        Lifecycle: "AGED_PERENNIAL", MaxHarvests: 3, RegrowthDays: 25, MinSoil: 70, CropTitleVi: "Trầm Hương Kỳ Mộc (Tích niên 3 vụ)");

    public static readonly CropSpec[] All =
    [
        Rice, Wheat, Hemp, Herb,
        Tea, Mulberry, BambooTimber,
        Ginseng, Agarwood
    ];

    public bool IsPerennial => MaxHarvests > 1;

    public static CropSpec? Find(string? code) =>
        code is null ? Rice : All.FirstOrDefault(spec =>
            spec.Code.Equals(code, StringComparison.OrdinalIgnoreCase)
            || (code.Equals("hat_giong_lua", StringComparison.OrdinalIgnoreCase) && spec == Rice)
            || (code.Equals("hat_giong_tra", StringComparison.OrdinalIgnoreCase) && spec == Tea)
            || (code.Equals("hat_giong_dau_tam", StringComparison.OrdinalIgnoreCase) && spec == Mulberry)
            || (code.Equals("hat_giong_duoc_thao", StringComparison.OrdinalIgnoreCase) && spec == Herb)
            || (code.Equals("rice", StringComparison.OrdinalIgnoreCase) && spec == Rice)
            || (code.Equals("wheat", StringComparison.OrdinalIgnoreCase) && spec == Wheat)
            || (code.Equals("hemp", StringComparison.OrdinalIgnoreCase) && spec == Hemp)
            || (code.Equals("tea", StringComparison.OrdinalIgnoreCase) && spec == Tea)
            || (code.Equals("mulberry", StringComparison.OrdinalIgnoreCase) && spec == Mulberry)
            || (code.Equals("bamboo", StringComparison.OrdinalIgnoreCase) && spec == BambooTimber)
            || (code.Equals("herb", StringComparison.OrdinalIgnoreCase) && spec == Herb)
            || (code.Equals("ginseng", StringComparison.OrdinalIgnoreCase) && spec == Ginseng)
            || (code.Equals("sam", StringComparison.OrdinalIgnoreCase) && spec == Ginseng)
            || (code.Equals("agarwood", StringComparison.OrdinalIgnoreCase) && spec == Agarwood));

    public static CropSpec? FindBySeedCode(string seedCode) =>
        All.FirstOrDefault(spec => spec.SeedItemCode.Equals(seedCode, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Standard agricultural tools and supply identifiers for cultivation operations.</summary>
internal static class FarmingSupplies
{
    public const string HoeCode = "cuoc_nong";
    public const long HoeItemId = 22;
    public const string HoeNameVi = "Cuốc nông nghiệp";

    public const string SickleCode = "liem_gat";
    public const long SickleItemId = 23;
    public const string SickleNameVi = "Liềm thu hoạch";

    public const string ScoopCode = "binh_tuoi";
    public const long ScoopItemId = 24;
    public const string ScoopNameVi = "Gầu mương thủy lợi";

    public const string FertilizerCode = "phan_bon";
    public const long FertilizerItemId = 21;
    public const string FertilizerNameVi = "Phân bón nông nghiệp";
}

/// <summary>
/// Seasonal and soil arithmetic. Growth and yield are integer functions of the month and the
/// plot's soil, so a replay is identical everywhere and a designer can read the curve.
/// </summary>
internal static class CropCalendar
{
    public const int SoilCostPerHarvest = 25;
    public const int SoilRecoveryPerMonth = 5;
    public const long FertiliseCost = 15;
    public const int FertiliseGain = 30;
    public const int SoilCeiling = 100;

    public static int GrowthDays(CropSpec crop, int month, int irrigationTier = 0)
    {
        var days = crop.FixedGrowthDays > 0 ? crop.FixedGrowthDays : month switch
        {
            <= 3 => 20,
            <= 6 => 18,
            <= 9 => 22,
            _ => 30
        };

        var reduction = irrigationTier switch
        {
            >= 5 => 8,
            >= 4 => 6,
            >= 3 => 4,
            >= 2 => 2,
            _ => 0
        };

        return Math.Max(5, days - reduction);
    }

    /// <summary>Permille applied to the base yield: winter is poor, autumn is generous.</summary>
    public static int SeasonPermille(int month) => month switch
    {
        <= 3 => 1000,
        <= 6 => 1100,
        <= 9 => 1200,
        _ => 600
    };

    /// <summary>Permille applied for soil quality: 100 → 1000, 0 → 600.</summary>
    public static int SoilPermille(int soil) => 600 + (Math.Clamp(soil, 0, SoilCeiling) * 4);

    public static int Yield(int areaMu, CropSpec crop, int month, int soil, int irrigationTier = 0)
    {
        return RegionalYield(areaMu, crop, month, soil, region: null, irrigationTier);
    }

    public static int RegionPermille(string? region, string cropCode, int month)
    {
        if (string.IsNullOrWhiteSpace(region)) return 1000;
        var reg = region.ToLowerInvariant();

        // Northern frontier regions suffer severe frost in winter months (10..12, 1..2)
        if (reg is "u_chau" or "tinh_chau" or "luong_chau" or "ky_chau")
        {
            var isWinter = month is >= 10 or <= 2;
            if (isWinter)
            {
                return cropCode.Equals("duoc_thao", StringComparison.OrdinalIgnoreCase) ? 1150 : 550;
            }
            return 950;
        }

        // Southern riverlands & warm basins excel at Rice, Tea, and Mulberry in spring/summer
        if (reg is "giang_dong" or "kinh_chau" or "ich_chau" or "giao_chau")
        {
            var isWarm = month is >= 3 and <= 8;
            if (isWarm)
            {
                return 1250;
            }
            return 1050;
        }

        return 1000;
    }

    public static int RegionalGrowthDays(CropSpec crop, int month, string? region, int irrigationTier = 0)
    {
        var baseDays = GrowthDays(crop, month, irrigationTier);
        if (string.IsNullOrWhiteSpace(region)) return baseDays;
        var reg = region.ToLowerInvariant();
        if (reg is "u_chau" or "tinh_chau" or "luong_chau" && (month is >= 10 or <= 2))
        {
            return baseDays + 4;
        }
        if (reg is "giang_dong" or "kinh_chau" or "ich_chau" or "giao_chau" && (month is >= 3 and <= 8))
        {
            return Math.Max(5, baseDays - 2);
        }
        return baseDays;
    }

    public static int RegionalYield(int areaMu, CropSpec crop, int month, int soil, string? region, int irrigationTier = 0)
    {
        var seasonMult = crop.Code.Equals("lua_mi", StringComparison.OrdinalIgnoreCase) && (month is >= 10 or <= 2)
            ? 1150 // Lúa mì ưa khí hậu lạnh Thu-Đông
            : SeasonPermille(month);
        var permille = (long)seasonMult * SoilPermille(soil);
        var raw = (long)areaMu * crop.YieldPerMu * permille;
        var yieldBonusPermille = irrigationTier switch
        {
            >= 5 => 1300L,
            >= 4 => 1200L,
            >= 3 => 1100L,
            _ => 1000L
        };
        raw = (raw * yieldBonusPermille) / 1000L;
        var regionMult = RegionPermille(region, crop.Code, month);
        raw = (raw * regionMult) / 1000L;
        return Math.Max(1, (int)((raw + 500_000L) / 1_000_000L));
    }

    public static int RegrowthDays(CropSpec crop, int month, int irrigationTier = 0)
    {
        var baseRegrowth = crop.RegrowthDays > 0 ? crop.RegrowthDays : Math.Max(10, GrowthDays(crop, month, 0) / 2);
        // Winter dormancy slows perennial budding
        if (month is >= 11 or 1)
        {
            baseRegrowth += 6;
        }
        var reduction = irrigationTier switch
        {
            >= 5 => 5,
            >= 4 => 4,
            >= 3 => 3,
            >= 2 => 2,
            _ => 0
        };
        return Math.Max(5, baseRegrowth - reduction);
    }
}
