namespace SimT.Contracts.Playing;

/// <summary>Read surface of <c>dlc.caravan</c>, provided as the <c>caravan.guild</c> capability.</summary>
public interface ICaravanGuild
{
    IReadOnlyList<CaravanRunSummary> Runs { get; }

    IReadOnlyList<FreightContractDef> Contracts { get; }

    long FeePerUnit { get; }

    int UnitsInTransit { get; }

    CaravanResult Send(long fromCity, long toCity);

    CaravanResult SendWithEscort(long fromCity, long toCity, long? escortNpcId);

    CaravanResult Travel(long fromCity, long toCity, IPlayerState player);

    CaravanResult AcceptContract(long contractId);

    CaravanResult DeliverContract(long contractId);
}

public sealed record CaravanRunSummary(
    long Id,
    long FromCityId,
    long ToCityId,
    int DepartDayIndex,
    int ArriveDayIndex,
    string Status,
    int Units,
    int Lost,
    long? EscortNpcId = null,
    int AmbushLoss = 0);

public sealed record FreightContractDef(
    long Id,
    string GuildName,
    long FromCityId,
    long ToCityId,
    long ItemId,
    int Quantity,
    int RewardCopper,
    int DeadlineDayIndex,
    string Status,
    int? AcceptedDayIndex = null,
    int? CompletedDayIndex = null);

public readonly record struct CaravanResult(bool Ok, string Message)
{
    public static CaravanResult Success(string message) => new(true, message);

    public static CaravanResult Failure(string message) => new(false, message);
}
