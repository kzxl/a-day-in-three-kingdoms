using SimT.Contracts.Playing;

namespace SimT.Modules.Dlc.Cultivation;

/// <summary>A crop a plot can grow: what it yields, how long it takes, how much land it needs, and the required seed item.</summary>
internal sealed record CropSpec(
    string Code,
    long OutputItemId,
    int FixedGrowthDays,
    int YieldPerMu,
    string SeedItemCode,
    long SeedItemId,
    string SeedNameVi)
{
    public static readonly CropSpec Rice = new("gao", 1, 0, 8, "hat_giong_lua", 17, "Hạt giống lúa");
    public static readonly CropSpec Tea = new("tra", 3, 24, 2, "hat_giong_tra", 18, "Hạt giống trà");
    public static readonly CropSpec Mulberry = new("dau_tam", 4, 30, 2, "hat_giong_dau_tam", 19, "Cây giống dâu tằm");
    public static readonly CropSpec Herb = new("duoc_thao", 7, 20, 2, "hat_giong_duoc_thao", 20, "Hạt giống dược thảo");

    public static readonly CropSpec[] All = [Rice, Tea, Mulberry, Herb];

    public static CropSpec? Find(string? code) =>
        code is null ? Rice : All.FirstOrDefault(spec =>
            spec.Code.Equals(code, StringComparison.OrdinalIgnoreCase)
            || spec.SeedItemCode.Equals(code, StringComparison.OrdinalIgnoreCase)
            || (code.Equals("rice", StringComparison.OrdinalIgnoreCase) && spec == Rice)
            || (code.Equals("tea", StringComparison.OrdinalIgnoreCase) && spec == Tea)
            || (code.Equals("mulberry", StringComparison.OrdinalIgnoreCase) && spec == Mulberry)
            || (code.Equals("herb", StringComparison.OrdinalIgnoreCase) && spec == Herb));

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
        var permille = (long)SeasonPermille(month) * SoilPermille(soil);
        var raw = (long)areaMu * crop.YieldPerMu * permille;
        var yieldBonusPermille = irrigationTier switch
        {
            >= 5 => 1300L,
            >= 4 => 1200L,
            >= 3 => 1100L,
            _ => 1000L
        };
        raw = (raw * yieldBonusPermille) / 1000L;
        return (int)((raw + 500_000L) / 1_000_000L);
    }
}
