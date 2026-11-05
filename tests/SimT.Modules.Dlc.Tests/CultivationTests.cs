using SimT.Contracts.Ledger;
using SimT.Contracts.Playing;
using SimT.Core.Kernel;
using SimT.Modules.Dlc.Cultivation;

namespace SimT.Modules.Dlc.Tests;

/// <summary>
/// Land, seasons and rent. A plot must cost money every month whether or not anything grows,
/// and the harvest must land in the bag only on the ready day.
/// </summary>
public sealed class CultivationTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void Renting_a_plot_charges_the_first_month_and_marks_it_rented()
    {
        using var kernel = _host.Boot();
        var plot = HomePlot(kernel);
        var before = kernel.Ledger.Balance;

        var message = Command(kernel, $"rent:{plot.Id}");

        Assert.Contains("Thuê khu", message);
        Assert.Equal(before - plot.MonthlyRent, kernel.Ledger.Balance);
        Assert.True(Find(kernel, plot.Id).Rented);
        Assert.Contains(kernel.Ledger.Recent(20), line => line.JournalCode == JournalCodes.FarmRent);
    }

    [Fact]
    public void A_plot_in_another_city_cannot_be_rented()
    {
        using var kernel = _host.Boot();
        var otherCity = Guild(kernel).Plots.First(plot => plot.CitadelId != Player(kernel).CurrentCityId);

        var message = Command(kernel, $"rent:{otherCity.Id}");

        Assert.Contains("đúng thành", message);
        Assert.False(Find(kernel, otherCity.Id).Rented);
    }

    [Fact]
    public void Sowing_costs_seed_money_and_sets_a_ready_day()
    {
        using var kernel = _host.Boot();
        var plot = HomePlot(kernel);
        Command(kernel, $"rent:{plot.Id}");
        var before = kernel.Ledger.Balance;
        var message = Command(kernel, $"plant:{plot.Id}");

        // Sowing costs days, so the schedule starts from the date after the action.
        var planted = kernel.Today;

        Assert.Contains("ngày nữa chín", message);
        Assert.Equal(before - (FarmService.SeedCostPerMu * plot.AreaMu), kernel.Ledger.Balance);
        var growing = Find(kernel, plot.Id);
        Assert.Equal("GROWING", growing.CropStatus);
        Assert.Equal(
            (int)planted.AddDays(CropCalendar.GrowthDays(CropSpec.Rice, planted.Month)).DayIndex,
            growing.ReadyDayIndex!.Value);
        Assert.Equal(CropCalendar.Yield(plot.AreaMu, CropSpec.Rice, planted.Month, plot.Soil), growing.YieldQuantity!.Value);
    }

    [Fact]
    public void Harvest_before_the_ready_day_is_refused()
    {
        using var kernel = _host.Boot();
        var plot = HomePlot(kernel);
        Command(kernel, $"rent:{plot.Id}");
        Command(kernel, $"plant:{plot.Id}");

        var message = Command(kernel, $"harvest:{plot.Id}");

        Assert.Contains("chưa chín", message);
        Assert.Equal(0, Bag(kernel).Quantity(Player(kernel).CurrentCityId, GrainItem));
    }

    [Fact]
    public void Harvest_on_the_ready_day_puts_grain_in_the_bag()
    {
        using var kernel = _host.Boot();
        var plot = HomePlot(kernel);
        Command(kernel, $"rent:{plot.Id}");
        Command(kernel, $"plant:{plot.Id}");
        var expected = Find(kernel, plot.Id).YieldQuantity!.Value;

        kernel.AdvanceDays(CropCalendar.GrowthDays(CropSpec.Rice, kernel.Today.Month));

        Assert.Equal("READY", Find(kernel, plot.Id).CropStatus);
        var message = Command(kernel, $"harvest:{plot.Id}");

        Assert.Contains("Gặt được", message);
        Assert.Equal(expected, Bag(kernel).Quantity(Player(kernel).CurrentCityId, GrainItem));
        Assert.Null(Find(kernel, plot.Id).CropStatus);
    }

    [Fact]
    public void A_harvested_plot_can_be_sown_again()
    {
        using var kernel = _host.Boot();
        var plot = HomePlot(kernel);
        Command(kernel, $"rent:{plot.Id}");
        Command(kernel, $"plant:{plot.Id}");
        kernel.AdvanceDays(CropCalendar.GrowthDays(CropSpec.Rice, kernel.Today.Month));
        Command(kernel, $"harvest:{plot.Id}");

        var message = Command(kernel, $"plant:{plot.Id}");

        Assert.Contains("gieo", message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("GROWING", Find(kernel, plot.Id).CropStatus);
    }

    [Fact]
    public void Sowing_twice_on_the_same_plot_is_refused()
    {
        using var kernel = _host.Boot();
        var plot = HomePlot(kernel);
        Command(kernel, $"rent:{plot.Id}");
        Command(kernel, $"plant:{plot.Id}");

        var message = Command(kernel, $"plant:{plot.Id}");

        Assert.Contains("đang có cây", message);
    }

    [Fact]
    public void Monthly_land_rent_is_charged_even_with_nothing_growing()
    {
        using var kernel = _host.Boot();
        var plot = HomePlot(kernel);
        Command(kernel, $"rent:{plot.Id}");
        var before = kernel.Ledger.Balance;

        kernel.AdvanceDays(31);

        Assert.Equal(before - plot.MonthlyRent, kernel.Ledger.Balance);
        Assert.Equal(plot.MonthlyRent, Guild(kernel).MonthlyRentTotal);
    }

    [Fact]
    public void Winter_yields_less_than_summer_for_the_same_land()
    {
        var summer = CropCalendar.Yield(2, CropSpec.Rice, 5, 100);
        var winter = CropCalendar.Yield(2, CropSpec.Rice, 11, 100);

        Assert.True(winter < summer, $"winter {winter} should be below summer {summer}");
    }

    [Fact]
    public void Planting_mulberry_and_herb_crops_yields_silk_and_medicine()
    {
        using var kernel = _host.Boot();
        var plot = HomePlot(kernel);
        Command(kernel, $"rent:{plot.Id}");

        // Sowing mulberry (dau_tam) -> fixed 40 days -> yields Silk (item 4)
        var plantMsg = Command(kernel, $"plant:{plot.Id}:dau_tam");
        Assert.Contains("Lụa", plantMsg);

        kernel.AdvanceDays(41);
        Assert.Equal("READY", Find(kernel, plot.Id).CropStatus);

        var harvestMsg = Command(kernel, $"harvest:{plot.Id}");
        Assert.Contains("Gặt được", harvestMsg);

        // Silk item ID is 4
        Assert.True(Bag(kernel).Quantity(Player(kernel).CurrentCityId, 4) > 0);

        // Harvest log contains entry
        var logs = Guild(kernel).HarvestLogs;
        Assert.Contains(logs, l => l.PlotId == plot.Id && l.CropCode == "dau_tam");
    }

    [Fact]
    public void Irrigating_plot_improves_waterway_and_soil()
    {
        using var kernel = _host.Boot();
        var plot = HomePlot(kernel);
        Command(kernel, $"rent:{plot.Id}");

        var balanceBefore = kernel.Ledger.Balance;
        var msg = Command(kernel, $"irrigate:{plot.Id}");

        Assert.Contains("Đào mương thủy lợi thành công", msg);
        Assert.Equal(balanceBefore - 80, kernel.Ledger.Balance);

        var refreshed = Find(kernel, plot.Id);
        Assert.Equal(1, refreshed.IrrigationLevel);
    }

    [Fact]
    public void Invariants_hold_after_a_full_agricultural_cycle()
    {
        using var kernel = _host.Boot();
        var plot = HomePlot(kernel);
        Command(kernel, $"rent:{plot.Id}");
        Command(kernel, $"plant:{plot.Id}");
        kernel.AdvanceDays(CropCalendar.GrowthDays(CropSpec.Rice, kernel.Today.Month));
        Command(kernel, $"harvest:{plot.Id}");
        Command(kernel, $"plant:{plot.Id}");
        kernel.AdvanceDays(40);

        Assert.Empty(kernel.RunInvariants());
    }

    [Fact]
    public void Upgrading_estate_facility_advances_tier_and_charges_ledger()
    {
        using var kernel = _host.Boot();
        var cityId = Player(kernel).CurrentCityId;
        var estate = Guild(kernel);
        var initialBalance = kernel.Ledger.Balance;

        // Upgrade Granary tier 0 -> 1 (costs 100)
        var msg1 = Command(kernel, $"upgrade_fac:{cityId}:GRANARY");
        Assert.Contains("Nâng cấp thành công", msg1);
        Assert.Contains("bậc 1/5", msg1);
        Assert.Equal(initialBalance - 100, kernel.Ledger.Balance);

        var facilities = estate.GetFacilities(cityId);
        var granary = Assert.Single(facilities, f => f.FacilityType == "GRANARY");
        Assert.Equal(1, granary.Tier);
        Assert.Equal(250, granary.NextUpgradeCost);
        Assert.True(granary.CanUpgrade);

        // Upgrade Granary tier 1 -> 2 (costs 250)
        var msg2 = Command(kernel, $"upgrade_fac:{cityId}:GRANARY");
        Assert.Contains("Nâng cấp thành công", msg2);
        Assert.Contains("bậc 2/5", msg2);
        Assert.Equal(initialBalance - 350, kernel.Ledger.Balance);

        facilities = estate.GetFacilities(cityId);
        granary = Assert.Single(facilities, f => f.FacilityType == "GRANARY");
        Assert.Equal(2, granary.Tier);
    }

    [Fact]
    public void Irrigation_facility_reduces_growth_days_and_boosts_yield()
    {
        using var kernel = _host.Boot();
        var cityId = Player(kernel).CurrentCityId;
        var plot = HomePlot(kernel);
        Command(kernel, $"rent:{plot.Id}");

        // Upgrade Irrigation to Tier 3 (Tier 1: 80, Tier 2: 200, Tier 3: 500)
        Command(kernel, $"upgrade_fac:{cityId}:IRRIGATION");
        Command(kernel, $"upgrade_fac:{cityId}:IRRIGATION");
        Command(kernel, $"upgrade_fac:{cityId}:IRRIGATION");

        var facilities = Guild(kernel).GetFacilities(cityId);
        var irrigation = Assert.Single(facilities, f => f.FacilityType == "IRRIGATION");
        Assert.Equal(3, irrigation.Tier);

        // Plant rice
        var msg = Command(kernel, $"plant:{plot.Id}");
        Assert.Contains("ngày nữa chín", msg);

        var growing = Find(kernel, plot.Id);
        var baseDays = CropCalendar.GrowthDays(CropSpec.Rice, kernel.Today.Month);
        var expectedDays = CropCalendar.GrowthDays(CropSpec.Rice, kernel.Today.Month, irrigationTier: 3);
        Assert.True(expectedDays < baseDays, "Tier 3 irrigation must reduce growth days");

        var expectedYield = CropCalendar.Yield(plot.AreaMu, CropSpec.Rice, kernel.Today.Month, plot.Soil, irrigationTier: 3);
        Assert.Equal(expectedYield, growing.YieldQuantity!.Value);
    }

    private const long GrainItem = 1;

    private static PlotView HomePlot(SimKernel kernel) =>
        Guild(kernel).Plots.First(plot => plot.CitadelId == Player(kernel).CurrentCityId);

    private static PlotView Find(SimKernel kernel, long plotId) =>
        Guild(kernel).Plots.Single(plot => plot.Id == plotId);

    private static string Command(SimKernel kernel, string commandId)
    {
        Assert.True(kernel.TryGetCommandHandler("cultivation.farm", out var handler), "farm screen is not registered");
        return handler!.Execute(commandId) ?? string.Empty;
    }

    private static IPlayerState Player(SimKernel kernel) =>
        kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);

    private static IBag Bag(SimKernel kernel) =>
        kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);

    private static ICultivationEstate Guild(SimKernel kernel) =>
        kernel.Capabilities.Get<ICultivationEstate>(CapabilityIds.CultivationEstate);
}
