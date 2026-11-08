namespace SimT.Contracts.Playing;

/// <summary>Read & action surface of <c>dlc.politics</c>, provided as the <c>politics.court</c> capability.</summary>
public interface IPoliticalCourt
{
    int Reputation { get; }

    long OfficeId { get; }

    string? OfficeName { get; }

    long MonthlyStipend { get; }

    IReadOnlyList<OfficeView> Offices { get; }

    long AllegianceFactionId { get; }

    string? AllegianceFactionName { get; }

    IReadOnlyList<FactionView> Factions { get; }

    IReadOnlyList<CityGovernanceView> Governances { get; }

    IReadOnlyList<DecreeLogView> RecentDecrees(int limit = 10);

    CourtResult JoinFaction(long factionId);

    CourtResult LeaveFaction();

    CourtResult EnactPolicy(string policyCode, long citadelId);

    void SetCityOwner(long citadelId, long factionId, long? governorNpcId = null) { }

    void AdjustCityStability(long citadelId, int unrestDelta, int defenseDelta) { }
}

public sealed record OfficeView(
    long Id,
    string Code,
    string NameVi,
    string NameEn,
    int Rank,
    long MonthlyStipend,
    int ReputationRequired);

public sealed record FactionView(
    long Id,
    string Code,
    string NameVi,
    long LeaderNpcId,
    long CapitalCityId,
    string IdealVi,
    int TroopCount,
    int TreasuryCopper,
    string ColorHex);

public sealed record CityGovernanceView(
    long CitadelId,
    long FactionId,
    long GovernorNpcId,
    int UnrestLevel,
    int DefenseStrength,
    int TaxRatePercent)
{
    public int Security => Math.Clamp(100 - UnrestLevel, 0, 100);
}

public sealed record DecreeLogView(
    long Id,
    long DayIndex,
    long CitadelId,
    string PolicyCode,
    int CostCopper,
    int ReputationGain,
    int UnrestDelta,
    string NarrativeVi);

public readonly record struct CourtResult(bool Ok, string Message)
{
    public static CourtResult Success(string message) => new(true, message);
    public static CourtResult Failure(string message) => new(false, message);
}
