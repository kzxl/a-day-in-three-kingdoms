using SimT.Contracts.Playing;

namespace SimT.Modules.Dlc.Cultivation;

/// <summary>A crop a plot can grow: what it yields, how long it takes and how much land it needs.</summary>
internal sealed record CropSpec(string Code, long OutputItemId, int FixedGrowthDays, int YieldPerMu)
{
    public static readonly CropSpec Rice = new("gao", 1, 0, 6);
    public static readonly CropSpec Tea = new("tra", 3, 35, 2);

    public static readonly CropSpec[] All = [Rice, Tea];

    public static CropSpec? Find(string? code) =>
        code is null ? Rice : All.FirstOrDefault(spec => spec.Code == code);
}

/// <summary>
/// Seasonal and soil arithmetic. Growth and yield are integer functions of the month and the
/// plot's soil, so a replay is identical everywhere and a designer can read the curve.
/// </summary>
internal static class CropCalendar
{
    public const int SoilCostPerHarvest = 25;
    public const int SoilRecoveryPerMonth = 5;
    public const long FertiliseCost = 30;
    public const int FertiliseGain = 30;
    public const int SoilCeiling = 100;

    public static int GrowthDays(CropSpec crop, int month) =>
        crop.FixedGrowthDays > 0 ? crop.FixedGrowthDays : month switch
        {
            <= 3 => 20,
            <= 6 => 18,
            <= 9 => 22,
            _ => 30
        };

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

    public static int Yield(int areaMu, CropSpec crop, int month, int soil)
    {
        var permille = (long)SeasonPermille(month) * SoilPermille(soil);
        var raw = (long)areaMu * crop.YieldPerMu * permille;
        return (int)((raw + 500_000L) / 1_000_000L);
    }
}
