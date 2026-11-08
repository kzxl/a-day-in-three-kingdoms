using SimT.Contracts.Data;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Core.Kernel;
using Xunit;

namespace SimT.Modules.Dlc.Tests;

public sealed class CityFamineAndDemographicsTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void Sufficient_food_promotes_prosperity_population_growth_and_peace()
    {
        using var kernel = _host.Boot();
        var court = kernel.Capabilities.Get<IPoliticalCourt>(CapabilityIds.CourtOffice);
        var trade = kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade);

        var citadelId = 1L; // Lạc Dương

        // Ensure plenty of grain in market
        trade.AdjustStock(citadelId, 1, 50); // +50 Gạo

        var initialDemo = court.GetCityDemographics(citadelId)!;
        var initialGrain = trade.StallStock(citadelId, 1);

        // Run monthly food simulation
        var result = court.SimulateMonthlyCityFoodAndFamine(citadelId, new GameDate(185, 2, 1));
        Assert.NotNull(result);

        var finalDemo = court.GetCityDemographics(citadelId)!;
        var finalGrain = trade.StallStock(citadelId, 1);

        // Assertions
        Assert.Equal(0, result.FoodDeficit);
        Assert.Equal(0, result.StarvationMonths);
        Assert.True(result.FoodSupplied > 0, "City must consume grain from market.");
        Assert.Equal(initialGrain - result.FoodSupplied, finalGrain);
        Assert.True(finalDemo.Population > initialDemo.Population, "Population must grow when food is ample.");
        Assert.True(finalDemo.ProsperityScore >= initialDemo.ProsperityScore, "Prosperity must increase with full nutrition.");
        Assert.Contains("an cư", result.NarrativeVi);
    }

    [Fact]
    public void Zero_grain_causes_starvation_riots_population_loss_and_desertion()
    {
        using var kernel = _host.Boot();
        var court = kernel.Capabilities.Get<IPoliticalCourt>(CapabilityIds.CourtOffice);
        var trade = kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade);
        var military = kernel.Capabilities.Get<IMilitaryCommand>(CapabilityIds.MilitaryCommand);

        var citadelId = 2L; // Trường An

        // Empty all food (gao #1, lua_mi #9) from the market to simulate severe famine
        var gaoStock = trade.StallStock(citadelId, 1);
        var wheatStock = trade.StallStock(citadelId, 9);
        trade.AdjustStock(citadelId, 1, -gaoStock);
        trade.AdjustStock(citadelId, 9, -wheatStock);

        var initialDemo = court.GetCityDemographics(citadelId)!;

        // Month 1 of famine
        var month1 = court.SimulateMonthlyCityFoodAndFamine(citadelId, new GameDate(185, 3, 1));
        Assert.NotNull(month1);
        Assert.Equal(1, month1.StarvationMonths);
        Assert.True(month1.FoodDeficit > 0);
        Assert.True(month1.NewUnrest > month1.OldUnrest, "Unrest should rise after first month of missing food.");

        // Month 2 of severe famine
        var month2 = court.SimulateMonthlyCityFoodAndFamine(citadelId, new GameDate(185, 4, 1));
        Assert.NotNull(month2);
        Assert.Equal(2, month2.StarvationMonths);

        // Riots, population flight, and garrison desertion
        Assert.True(month2.FinalPopulation < initialDemo.Population, "Famine must cause population shrinkage and refugee flight.");
        Assert.True(month2.NewUnrest > month1.NewUnrest, "Unrest must escalate sharply during prolonged famine.");
        Assert.True(month2.NewDefense < month1.NewDefense, "Garrison defense must degrade due to starvation and desertion.");
        Assert.Contains("Đại cơ hoang", month2.NarrativeVi);

        // Emergency grain requisition must be created
        var reqs = military.ActiveRequisitions(citadelId);
        var famineReq = reqs.FirstOrDefault(r => r.TargetItemCode == "gao" && r.TargetItemName.Contains("Cơ Hoang"));
        Assert.NotNull(famineReq);
        Assert.True(famineReq.RewardPerUnit >= 35);
    }

    [Fact]
    public void Merchant_supplying_grain_alleviates_famine_and_halts_population_decline()
    {
        using var kernel = _host.Boot();
        var court = kernel.Capabilities.Get<IPoliticalCourt>(CapabilityIds.CourtOffice);
        var trade = kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade);

        var citadelId = 3L; // Nghiệp Thành

        // 1. Force into famine
        trade.AdjustStock(citadelId, 1, -trade.StallStock(citadelId, 1));
        trade.AdjustStock(citadelId, 9, -trade.StallStock(citadelId, 9));
        court.SimulateMonthlyCityFoodAndFamine(citadelId, new GameDate(185, 3, 1));
        court.SimulateMonthlyCityFoodAndFamine(citadelId, new GameDate(185, 4, 1));

        var starvedDemo = court.GetCityDemographics(citadelId)!;
        Assert.Equal(2, starvedDemo.StarvationMonths);

        // 2. Merchant arrives with a huge convoy of grain
        trade.AdjustStock(citadelId, 1, 80); // Deliver 80 sacks of rice

        // 3. Next month simulation
        var reliefResult = court.SimulateMonthlyCityFoodAndFamine(citadelId, new GameDate(185, 5, 1));
        Assert.NotNull(reliefResult);

        var recoveredDemo = court.GetCityDemographics(citadelId)!;

        // Famine relieved!
        Assert.Equal(0, reliefResult.StarvationMonths);
        Assert.Equal(0, reliefResult.FoodDeficit);
        Assert.True(recoveredDemo.Population >= starvedDemo.Population, "Population decline must halt once food is restored.");
        Assert.True(recoveredDemo.ProsperityScore >= starvedDemo.ProsperityScore);
        Assert.Contains("Lương thực sung túc", reliefResult.NarrativeVi);
    }
}
