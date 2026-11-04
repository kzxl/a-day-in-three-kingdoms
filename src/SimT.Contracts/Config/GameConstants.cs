namespace SimT.Contracts.Config;

/// <summary>
/// Typed view of <c>assets/config/game-constants.json</c>. The JSON file stays the single
/// source of truth shared with the legacy web project; an architecture test fails when
/// <see cref="Time.GameDate.DaysPerMonth"/> drifts from <see cref="DaysPerMonth"/>.
/// </summary>
public sealed record GameConstants(
    string CurrencyNameVi,
    string CurrencyNameEn,
    string GrainNameVi,
    string GrainNameEn,
    int DaysPerMonth,
    int MonthsPerYear,
    int PhasesPerDay,
    int FatigueCap,
    double GrainPerDay,
    int DailyFatigueRecovery,
    string FallbackDate,
    int EraStartYear,
    int EraEndYear);
