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

    IReadOnlyList<EstateFacilityDef> Facilities { get; }

    IReadOnlyList<EstateFacilityDef> GetFacilities(long citadelId);

    FarmResult UpgradeFacility(long citadelId, string facilityType);

    FarmResult BuySeed(long cityId, string cropCode, int quantity = 1);

    FarmResult BuyFertilizer(long cityId, int quantity = 1);

    FarmResult BuyTool(long cityId, string toolCode);

    FarmSupplyInventory GetSupplyInventory(long cityId);
}

public sealed record FarmSupplyInventory(
    int RiceSeeds,
    int TeaSeeds,
    int MulberrySeedlings,
    int HerbSeeds,
    int FertilizerCount,
    bool HasHoe,
    bool HasSickle,
    bool HasWateringScoop);

public sealed record EstateFacilityDef(
    long Id,
    long CitadelId,
    string FacilityType,
    int Tier,
    int BuiltDay,
    string NameVi,
    string TierTitleVi,
    string EffectVi,
    long NextUpgradeCost,
    bool CanUpgrade);

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
