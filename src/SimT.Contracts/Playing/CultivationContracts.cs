namespace SimT.Contracts.Playing;

/// <summary>Read surface of <c>dlc.cultivation</c>, provided as the <c>cultivation.estate</c> capability.</summary>
public interface ICultivationEstate
{
    IReadOnlyList<PlotView> Plots { get; }

    IReadOnlyList<HarvestLogDef> HarvestLogs { get; }

    long MonthlyRentTotal { get; }

    int GrowingCount { get; }

    int ReadyCount { get; }

    FarmResult Rent(long plotId, long cityId);

    FarmResult Plant(long plotId, long cityId, string? cropCode);

    FarmResult Harvest(long plotId, long cityId);

    FarmResult Fertilise(long plotId, long cityId);

    FarmResult Irrigate(long plotId, long cityId);
}

public sealed record PlotView(
    long Id,
    long CitadelId,
    int AreaMu,
    long MonthlyRent,
    bool Rented,
    int Soil,
    string? CropStatus,
    int? ReadyDayIndex,
    int? YieldQuantity,
    int IrrigationLevel = 0);

public sealed record HarvestLogDef(
    long Id,
    long PlotId,
    long CitadelId,
    string CropCode,
    int YieldQty,
    int HarvestDayIndex,
    int SoilAfter);

public readonly record struct FarmResult(bool Ok, string Message)
{
    public static FarmResult Success(string message) => new(true, message);

    public static FarmResult Failure(string message) => new(false, message);
}
