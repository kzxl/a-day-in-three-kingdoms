namespace SimT.Contracts.Playing;

/// <summary>Read surface of <c>dlc.politics</c>, provided as the <c>politics.court</c> capability.</summary>
public interface IPoliticalCourt
{
    int Reputation { get; }

    long OfficeId { get; }

    string? OfficeName { get; }

    long MonthlyStipend { get; }

    IReadOnlyList<OfficeView> Offices { get; }
}

public sealed record OfficeView(
    long Id,
    string Code,
    string NameVi,
    string NameEn,
    int Rank,
    long MonthlyStipend,
    int ReputationRequired);
