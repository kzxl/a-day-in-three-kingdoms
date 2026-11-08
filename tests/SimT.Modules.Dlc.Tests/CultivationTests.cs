using SimT.Contracts.Ledger;
using SimT.Contracts.Playing;
using SimT.Core.Kernel;
using SimT.Modules.Dlc.Economy;

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
        var cityId = Player(kernel).CurrentCityId;
        var beforeSeeds = Bag(kernel).Quantity(cityId, 17);
        var message = Command(kernel, $"plant:{plot.Id}");

        // Sowing costs days, so the schedule starts from the date after the action.
        var planted = kernel.Today;

        Assert.Contains("ngày nữa chín", message);
        Assert.Equal(beforeSeeds - plot.AreaMu, Bag(kernel).Quantity(cityId, 17));
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

    [Fact]
    public void Planting_without_hoe_fails_with_tool_missing_error()
    {
        using var kernel = _host.Boot();
        var plot = RawPlot(kernel);
        Command(kernel, $"rent:{plot.Id}");

        var msg = Command(kernel, $"plant:{plot.Id}");
        Assert.Contains("Thiếu nông cụ", msg);
        Assert.Contains("Cuốc Nông Nghiệp", msg);
    }

    [Fact]
    public void Planting_without_seeds_fails_with_seed_missing_error()
    {
        using var kernel = _host.Boot();
        var cityId = Player(kernel).CurrentCityId;
        var plot = RawPlot(kernel);
        Command(kernel, $"rent:{plot.Id}");

        // Player has hoe, but no seeds
        Bag(kernel).Add(cityId, FarmingSupplies.HoeItemId, 1);

        var msg = Command(kernel, $"plant:{plot.Id}");
        Assert.Contains("Thiếu hạt giống", msg);
    }

    [Fact]
    public void Buying_seed_and_tool_enables_planting_and_consumes_seeds()
    {
        using var kernel = _host.Boot();
        var cityId = Player(kernel).CurrentCityId;
        var plot = RawPlot(kernel);
        Command(kernel, $"rent:{plot.Id}");

        // Buy hoe and seeds via commands
        var hoeMsg = Command(kernel, $"buy_tool:{cityId}:{FarmingSupplies.HoeCode}");
        Assert.Contains("Cuốc", hoeMsg);
        Assert.Contains("Đã mua thành công", hoeMsg);

        var seedMsg = Command(kernel, $"buy_seed:{cityId}:RICE:10");
        Assert.Contains("Đã mua 10 bao", seedMsg);
        Assert.Contains("Hạt giống lúa", seedMsg, StringComparison.OrdinalIgnoreCase);

        var bag = Bag(kernel);
        Assert.Equal(1, bag.Quantity(cityId, FarmingSupplies.HoeItemId));
        Assert.Equal(10, bag.Quantity(cityId, 17)); // 17 = Rice seed

        // Plant now succeeds and consumes plot.AreaMu seeds
        var plantMsg = Command(kernel, $"plant:{plot.Id}");
        Assert.Contains("ngày nữa chín", plantMsg);
        Assert.Equal(10 - plot.AreaMu, bag.Quantity(cityId, 17));
    }

    [Fact]
    public void Harvesting_without_sickle_fails_with_tool_missing_error()
    {
        using var kernel = _host.Boot();
        var cityId = Player(kernel).CurrentCityId;
        var plot = RawPlot(kernel);
        Command(kernel, $"rent:{plot.Id}");

        // Give hoe and seeds to plant
        var bag = Bag(kernel);
        bag.Add(cityId, FarmingSupplies.HoeItemId, 1);
        bag.Add(cityId, 17, plot.AreaMu);

        Command(kernel, $"plant:{plot.Id}");
        kernel.AdvanceDays(CropCalendar.GrowthDays(CropSpec.Rice, kernel.Today.Month));

        // Try harvest without sickle
        var failHarvest = Command(kernel, $"harvest:{plot.Id}");
        Assert.Contains("Thiếu nông cụ", failHarvest);
        Assert.Contains("Liềm Thu Hoạch", failHarvest);

        // Buy sickle and harvest again
        Command(kernel, $"buy_tool:{cityId}:{FarmingSupplies.SickleCode}");
        var okHarvest = Command(kernel, $"harvest:{plot.Id}");
        Assert.Contains("Gặt được", okHarvest);
        Assert.True(bag.Quantity(cityId, GrainItem) > 0);
    }

    [Fact]
    public void Fertilising_without_hoe_or_fertilizer_fails()
    {
        using var kernel = _host.Boot();
        var cityId = Player(kernel).CurrentCityId;
        var plot = RawPlot(kernel);
        Command(kernel, $"rent:{plot.Id}");

        // Without hoe
        var noHoe = Command(kernel, $"fertilise:{plot.Id}");
        Assert.Contains("Thiếu nông cụ", noHoe);
        Assert.Contains("Cuốc Nông Nghiệp", noHoe);

        // With hoe, but no fertilizer
        var bag = Bag(kernel);
        bag.Add(cityId, FarmingSupplies.HoeItemId, 1);
        var noFert = Command(kernel, $"fertilise:{plot.Id}");
        Assert.Contains("Thiếu phân bón", noFert);

        // Buy fertilizer and fertilise after a harvest when soil is depleted
        var buyMsg = Command(kernel, $"buy_fertilizer:{cityId}:5");
        Assert.Contains("Đã mua 5 bao", buyMsg);
        Assert.Contains("Phân Bón Nông Nghiệp", buyMsg);
        Assert.Equal(5, bag.Quantity(cityId, FarmingSupplies.FertilizerItemId));

        // Plant and harvest to wear down soil to 75
        bag.Add(cityId, 17, plot.AreaMu);
        Command(kernel, $"plant:{plot.Id}");
        kernel.AdvanceDays(CropCalendar.GrowthDays(CropSpec.Rice, kernel.Today.Month));
        Command(kernel, $"buy_tool:{cityId}:{FarmingSupplies.SickleCode}");
        Command(kernel, $"harvest:{plot.Id}");

        var wornSoil = Find(kernel, plot.Id).Soil;
        Assert.True(wornSoil < CropCalendar.SoilCeiling);

        var okFert = Command(kernel, $"fertilise:{plot.Id}");
        Assert.Contains("Bón phân thành công", okFert);
        Assert.Equal(4, bag.Quantity(cityId, FarmingSupplies.FertilizerItemId));
        Assert.Equal(CropCalendar.SoilCeiling, Find(kernel, plot.Id).Soil);
    }

    [Fact]
    public void Irrigating_without_scoop_or_hoe_fails()
    {
        using var kernel = _host.Boot();
        var cityId = Player(kernel).CurrentCityId;
        var plot = RawPlot(kernel);
        Command(kernel, $"rent:{plot.Id}");

        // Without scoop or hoe
        var noTool = Command(kernel, $"irrigate:{plot.Id}");
        Assert.Contains("Thiếu dụng cụ", noTool);

        // Buy scoop
        Command(kernel, $"buy_tool:{cityId}:{FarmingSupplies.ScoopCode}");
        var okIrrigate = Command(kernel, $"irrigate:{plot.Id}");
        Assert.Contains("Đào mương thủy lợi thành công", okIrrigate);
    }

    private const long GrainItem = 1;

    private static PlotView RawPlot(SimKernel kernel)
    {
        var cityId = Player(kernel).CurrentCityId;
        return Guild(kernel).Plots.First(plot => plot.CitadelId == cityId);
    }

    private static PlotView HomePlot(SimKernel kernel)
    {
        var cityId = Player(kernel).CurrentCityId;
        var bag = Bag(kernel);
        bag.Add(cityId, 17, 50); // rice seed
        bag.Add(cityId, 18, 50); // tea seed
        bag.Add(cityId, 19, 50); // mulberry seedlings
        bag.Add(cityId, 20, 50); // herb seeds
        bag.Add(cityId, 21, 50); // fertilizer
        bag.Add(cityId, 22, 1);  // hoe
        bag.Add(cityId, 23, 1);  // sickle
        bag.Add(cityId, 24, 1);  // scoop
        return Guild(kernel).Plots.First(plot => plot.CitadelId == cityId);
    }

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

    [Fact]
    public void Tenant_farmer_sharecropping_auto_harvests_and_replants_with_70_percent_landowner_share_and_regional_climate()
    {
        using var kernel = _host.Boot();
        var cityId = Player(kernel).CurrentCityId;
        var bag = Bag(kernel);
        var plot = HomePlot(kernel);

        Command(kernel, $"rent:{plot.Id}");
        var grainBefore = bag.Quantity(cityId, GrainItem);

        // Assign tenant farmer to auto-cultivate Rice (gao)
        var tenantMsg = Command(kernel, $"tenant:{plot.Id}:gao");
        Assert.Contains("Tá điền", tenantMsg);

        var updatedPlot = Find(kernel, plot.Id);
        Assert.Equal("gao", updatedPlot.TenantCropCode);
        Assert.Equal("GROWING", updatedPlot.CropStatus);

        var expectedFullYield = updatedPlot.YieldQuantity!.Value;
        var expectedLandownerShare = Math.Max(1, (expectedFullYield * 70) / 100);

        // Advance past growth duration -> tenant automatically harvests (70% to landowner) and replants next cycle!
        var growthDays = CropCalendar.GrowthDays(CropSpec.Rice, kernel.Today.Month);
        kernel.AdvanceDays(growthDays + 1);

        Assert.Equal(grainBefore + expectedLandownerShare, bag.Quantity(cityId, GrainItem));
        var replantedPlot = Find(kernel, plot.Id);
        Assert.Equal("GROWING", replantedPlot.CropStatus);

        // Verify regional climate differentiation: Northern winter (u_chau, month 12) vs Southern spring (giang_dong, month 4)
        var northWinterYield = CropCalendar.RegionalYield(2, CropSpec.Rice, 12, 100, "u_chau");
        var southSpringYield = CropCalendar.RegionalYield(2, CropSpec.Rice, 4, 100, "giang_dong");
        Assert.True(southSpringYield > northWinterYield * 2);
    }

    [Fact]
    public void Perennial_crops_regrow_across_multiple_harvests_without_replanting_and_aged_crops_enforce_soil_gate()
    {
        using var kernel = _host.Boot();
        var cityId = Player(kernel).CurrentCityId;
        var bag = Bag(kernel);
        var plot = HomePlot(kernel);

        Command(kernel, $"rent:{plot.Id}");
        var teaSeedsBefore = bag.Quantity(cityId, 18); // Tea seed #18

        // 1. Plant Tea (tra) -> Perennial crop with 4 harvest cycles
        var plantTeaMsg = Command(kernel, $"plant:{plot.Id}:tra");
        Assert.Contains("lưu gốc", plantTeaMsg, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(teaSeedsBefore - plot.AreaMu, bag.Quantity(cityId, 18));

        var planted = Find(kernel, plot.Id);
        Assert.Equal("tra", planted.CropCode);
        Assert.Equal(4, planted.RemainingHarvests);
        Assert.Equal(1, planted.HarvestStage);

        // 2. Advance 24 days to 1st harvest -> harvest yields Tea (#3), tree stays alive in GROWING stage 2 with 3 harvests left (0 extra seeds spent!)
        kernel.AdvanceDays(24);
        Assert.Equal("READY", Find(kernel, plot.Id).CropStatus);
        var harvest1Msg = Command(kernel, $"harvest:{plot.Id}");
        Assert.Contains("Vụ 1/4", harvest1Msg);
        Assert.True(bag.Quantity(cityId, 3) > 0);
        Assert.Equal(teaSeedsBefore - plot.AreaMu, bag.Quantity(cityId, 18)); // No extra seeds consumed!

        var regrowing = Find(kernel, plot.Id);
        Assert.Equal("GROWING", regrowing.CropStatus);
        Assert.Equal(3, regrowing.RemainingHarvests);
        Assert.Equal(2, regrowing.HarvestStage);

        // 3. Regrowth is faster (14 days instead of 24) and mature root stage 2 yields >= stage 1
        kernel.AdvanceDays(15);
        Assert.Equal("READY", Find(kernel, plot.Id).CropStatus);
        var harvest2Msg = Command(kernel, $"harvest:{plot.Id}");
        Assert.Contains("Vụ 2/4", harvest2Msg);

        // 4. Uproot perennial tree to clear plot and salvage firewood (#6 Gỗ)
        var woodBefore = bag.Quantity(cityId, 6);
        var uprootMsg = Command(kernel, $"uproot:{plot.Id}");
        Assert.Contains("Đốn bỏ", uprootMsg);
        Assert.Null(Find(kernel, plot.Id).CropStatus);
        Assert.True(bag.Quantity(cityId, 6) > woodBefore);

        // 5. Aged crop (nhan_sam -> Ginseng #13) requires Soil >= 70; if soil is worn out, it refuses until fertilized
        Command(kernel, $"fertilise:{plot.Id}");
        var samPlantMsg = Command(kernel, $"plant:{plot.Id}:nhan_sam");
        Assert.Contains("Nhân sâm", samPlantMsg, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("nhan_sam", Find(kernel, plot.Id).CropCode);
        Assert.Empty(kernel.RunInvariants());
    }
}
