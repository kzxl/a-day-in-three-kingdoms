namespace SimT.Contracts.Playing;

/// <summary>Read surface of <c>dlc.cultivation</c>, provided as the <c>cultivation.estate</c> capability.</summary>
public interface ICultivationEstate
{
    IReadOnlyList<PlotView> Plots { get; }

    long MonthlyRentTotal { get; }

    int GrowingCount { get; }

    int ReadyCount { get; }
}

public sealed record PlotView(
    long Id,
    long CitadelId,
    int AreaMu,
    long MonthlyRent,
    bool Rented,
    string? CropStatus,
    int? ReadyDayIndex,
    int? YieldQuantity);
