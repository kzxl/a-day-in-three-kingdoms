namespace SimT.Contracts.Playing;

/// <summary>Read surface of <c>dlc.caravan</c>, provided as the <c>caravan.guild</c> capability.</summary>
public interface ICaravanGuild
{
    IReadOnlyList<CaravanRunSummary> Runs { get; }

    IReadOnlyList<FreightContractDef> Contracts { get; }

    long FeePerUnit { get; }

    int UnitsInTransit { get; }

    CaravanFleetDef Fleet { get; }

    IReadOnlyList<MarketRumorDef> Rumors { get; }

    int GetRumorPriceFactorPermille(long citadelId, long itemId);

    CaravanCrewDef Crew { get; }

    CaravanResult AssignCrew(string role, long? npcId);

    CaravanResult UpgradeFleet();

    CaravanResult Send(long fromCity, long toCity);

    CaravanResult SendWithEscort(long fromCity, long toCity, long? escortNpcId);

    CaravanResult SendWithEscortFee(long fromCity, long toCity, int escortFee);

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

/// <summary>
/// Status and capabilities of the player's commercial logistics fleet (Xe Thồ, Thiết Xa, Vận Thuyền).
/// </summary>
public sealed record CaravanFleetDef(
    int Tier,
    string NameVi,
    double CapacityMult,
    double CostDiscount,
    double DefenseBonus,
    double SpoilageReduction);

public readonly record struct CaravanResult(bool Ok, string Message)
{
    public static CaravanResult Success(string message) => new(true, message);

    public static CaravanResult Failure(string message) => new(false, message);
}

/// <summary>
/// Geopolitical/macroeconomic price shock or commercial rumor in the realm.
/// </summary>
public sealed record MarketRumorDef(
    long Id,
    long CitadelId,
    string CitadelNameVi,
    long ItemId,
    string ItemNameVi,
    string HeadlineVi,
    string DetailVi,
    int PriceMultPermille,
    int ExpireDaysRemaining);

/// <summary>
/// Specialist crew members assigned to manage, guide and defend the caravan.
/// </summary>
public sealed record CaravanCrewDef(
    long? EscortNpcId,
    string? EscortNameVi,
    int EscortWar,
    long? AccountantNpcId,
    string? AccountantNameVi,
    int AccountantCom,
    long? NavigatorNpcId,
    string? NavigatorNameVi,
    int NavigatorLog);
