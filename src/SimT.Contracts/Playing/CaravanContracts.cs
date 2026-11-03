namespace SimT.Contracts.Playing;

/// <summary>Read surface of <c>dlc.caravan</c>, provided as the <c>caravan.guild</c> capability.</summary>
public interface ICaravanGuild
{
    IReadOnlyList<CaravanRunSummary> Runs { get; }

    long FeePerUnit { get; }

    int UnitsInTransit { get; }
}

public sealed record CaravanRunSummary(
    long Id,
    long FromCityId,
    long ToCityId,
    int DepartDayIndex,
    int ArriveDayIndex,
    string Status,
    int Units);
