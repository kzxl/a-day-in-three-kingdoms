using SimT.Contracts.Data;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Core.Kernel;
using Xunit;

namespace SimT.Modules.Dlc.Tests;

public sealed class CityForgeAndMilitaryProcurementTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void City_forge_converts_market_iron_to_weapons_and_arms_garrison()
    {
        using var kernel = _host.Boot();
        var military = kernel.Capabilities.Get<IMilitaryCommand>(CapabilityIds.MilitaryCommand);
        var trade = kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade);

        var citadelId = 1L; // Lạc Dương

        // Ensure market has plenty of iron and coal
        trade.AdjustStock(citadelId, 5, 20);  // +20 Sắt
        trade.AdjustStock(citadelId, 15, 20); // +20 Than đá

        var initialIron = trade.StallStock(citadelId, 5);
        var initialArmory = military.GetCitadelArmory(citadelId);

        // Run monthly simulation
        var result = military.SimulateMonthlyArmoryAndProcurement(citadelId, new GameDate(185, 2, 1));

        var finalIron = trade.StallStock(citadelId, 5);
        var finalArmory = military.GetCitadelArmory(citadelId);

        // Assertions
        Assert.True(result.ArmsForged > 0, "Autonomous forge must produce weapons when iron is available.");
        Assert.True(result.IronConsumed > 0, "Forge must consume iron from market.");
        Assert.Equal(initialIron - result.IronConsumed, finalIron);
        Assert.True(result.ArmsSupplied > 0, "Garrison must receive weapons.");
        Assert.Equal(0, result.WeaponsDeficit);
        Assert.True(result.NewDefenseStrength >= result.OldDefenseStrength, "Well-equipped garrison maintains or increases defense.");
        Assert.Equal(finalArmory.WeaponsStock, result.FinalWeapons);
    }

    [Fact]
    public void Iron_depletion_causes_forge_starvation_and_crashes_defense_strength()
    {
        using var kernel = _host.Boot();
        var military = kernel.Capabilities.Get<IMilitaryCommand>(CapabilityIds.MilitaryCommand);
        var trade = kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade);

        var citadelId = 2L; // Trường An

        // Empty iron and empty armory to simulate total supply chain breakdown
        var currentIron = trade.StallStock(citadelId, 5);
        trade.AdjustStock(citadelId, 5, -currentIron); // Set iron to 0

        kernel.Db.Execute(
            "UPDATE ml_citadel_armory SET weapons_stock = 0 WHERE citadel_id = @citId",
            DbArgs.One("@citId", citadelId));

        var initialSecurity = military.GetCitadelSecurity(citadelId);

        // Run monthly simulation under total iron famine
        var result = military.SimulateMonthlyArmoryAndProcurement(citadelId, new GameDate(185, 3, 1));

        // Forge should be paralyzed
        Assert.Equal(0, result.ArmsForged);
        Assert.Equal(0, result.IronConsumed);
        Assert.True(result.WeaponsDeficit > 0, "Garrison must suffer weapons shortage when armory is empty.");
        Assert.True(result.NewDefenseStrength < result.OldDefenseStrength, "Defense strength must crash during weapon deficit.");
        Assert.Contains("Khủng hoảng quân giới", result.StatusVi);

        // Check that an emergency supply requisition with high reward was automatically generated
        var requisitions = military.ActiveRequisitions(citadelId);
        var emergencyIronReq = requisitions.FirstOrDefault(r => r.TargetItemCode == "sat");
        Assert.NotNull(emergencyIronReq);
        Assert.Equal(120, emergencyIronReq.RewardPerUnit); // Double base price bounty!
        Assert.True(emergencyIronReq.QuantityNeeded >= result.WeaponsDeficit * 3);

        // Security should also reflect lower defense
        var postCrisisSecurity = military.GetCitadelSecurity(citadelId);
        Assert.True(postCrisisSecurity <= initialSecurity);
    }

    [Fact]
    public void Merchant_supplying_iron_restores_city_forges_and_recovers_defense()
    {
        using var kernel = _host.Boot();
        var military = kernel.Capabilities.Get<IMilitaryCommand>(CapabilityIds.MilitaryCommand);
        var trade = kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade);

        var citadelId = 3L; // Nghiệp Thành

        // 1. Trigger crisis
        var currentIron = trade.StallStock(citadelId, 5);
        trade.AdjustStock(citadelId, 5, -currentIron);
        kernel.Db.Execute(
            "UPDATE ml_citadel_armory SET weapons_stock = 0 WHERE citadel_id = @citId",
            DbArgs.One("@citId", citadelId));
        kernel.Db.Execute(
            "UPDATE po_city_governance SET defense_strength = 35 WHERE citadel_id = @citId",
            DbArgs.One("@citId", citadelId));

        var crisisResult = military.SimulateMonthlyArmoryAndProcurement(citadelId, new GameDate(185, 3, 1));
        Assert.True(crisisResult.WeaponsDeficit > 0);
        var depressedDefense = crisisResult.NewDefenseStrength;

        // 2. Merchant arrives and sells a convoy of iron to the city market
        trade.AdjustStock(citadelId, 5, 30);  // +30 Sắt
        trade.AdjustStock(citadelId, 15, 20); // +20 Than đá

        // 3. Next monthly simulation
        var recoveryResult = military.SimulateMonthlyArmoryAndProcurement(citadelId, new GameDate(185, 4, 1));

        Assert.True(recoveryResult.ArmsForged > 0, "Forges should resume production immediately once iron is delivered.");
        Assert.True(recoveryResult.IronConsumed > 0);
        Assert.Equal(0, recoveryResult.WeaponsDeficit);
        Assert.True(recoveryResult.NewDefenseStrength > depressedDefense, "Defense strength must start recovering after arms supply restored.");
        Assert.Equal("Binh giới sung túc, quân nhu nghiêm mật", recoveryResult.StatusVi);
    }

    [Fact]
    public void War_danger_increases_garrison_procurement_demand()
    {
        using var kernel = _host.Boot();
        var military = kernel.Capabilities.Get<IMilitaryCommand>(CapabilityIds.MilitaryCommand);

        var citadelId = 4L; // Trác Huyện

        // Normal demand baseline
        var baselineResult = military.SimulateMonthlyArmoryAndProcurement(citadelId, new GameDate(185, 5, 1));

        // Insert high danger rebel fortress near citadel 4
        var nextId = kernel.Db.ScalarLong("SELECT COALESCE(MAX(id), 0) + 1 FROM ml_stronghold");
        kernel.Db.Execute(
            "INSERT INTO ml_stronghold (id, citadel_id, name_vi, bandit_faction, danger_level, loot_gold, item_reward_code, cleared_count, last_cleared_date) " +
            "VALUES (@id, @citId, 'Hắc Sơn Đại Doanh Trại', 'hac_son', 8, 1000, 'sat', 0, NULL)",
            new DbArgs().Add("@id", nextId).Add("@citId", citadelId));

        // Run simulation again
        var warResult = military.SimulateMonthlyArmoryAndProcurement(citadelId, new GameDate(185, 6, 1));

        Assert.True(warResult.GarrisonDemand > baselineResult.GarrisonDemand,
            $"Garrison weapon demand under war danger ({warResult.GarrisonDemand}) must exceed baseline ({baselineResult.GarrisonDemand}).");
    }
}
