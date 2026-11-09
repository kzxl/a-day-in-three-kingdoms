using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Core.Kernel;
using Xunit;

namespace SimT.Modules.Dlc.Tests;

public sealed class GeopoliticalConflictTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void GeopoliticalWar_capability_is_registered_and_queryable()
    {
        using var kernel = _host.Boot();
        var war = kernel.Capabilities.Get<IGeopoliticalWar>(CapabilityIds.GeopoliticalWar);
        Assert.NotNull(war);

        var active = war.ActiveConflicts();
        Assert.NotNull(active);
    }

    [Fact]
    public void TriggerConflict_creates_and_escalates_frontline_and_siege()
    {
        using var kernel = _host.Boot();
        var war = kernel.Capabilities.Get<IGeopoliticalWar>(CapabilityIds.GeopoliticalWar);

        // Faction 1 (Cao Ngụy) attacks Citadel 11 (Hán Trung) held by Faction 2 (Thục Hán)
        war.TriggerConflict(1, 2, 11, initialIntensity: 1);

        var conflict = war.GetConflictForCitadel(11);
        Assert.NotNull(conflict);
        Assert.Equal(1, conflict.AttackerFactionId);
        Assert.Equal(2, conflict.DefenderFactionId);
        Assert.Equal(11, conflict.TargetCitadelId);
        Assert.Equal(1, conflict.Intensity);
        Assert.Equal("SKIRMISH", conflict.WarStage);

        var status = war.GetCitadelWarStatus(11);
        Assert.True(status.IsFrontline);
        Assert.False(status.IsBesieged);
        Assert.False(war.IsCitadelUnderSiege(11));

        // Escalate intensity to full siege
        war.TriggerConflict(1, 2, 11, initialIntensity: 2);
        var escalated = war.GetConflictForCitadel(11);
        Assert.NotNull(escalated);
        Assert.Equal(2, escalated.Intensity);
        Assert.Equal("SIEGE", escalated.WarStage);

        var escalatedStatus = war.GetCitadelWarStatus(11);
        Assert.True(escalatedStatus.IsBesieged);
        Assert.True(war.IsCitadelUnderSiege(11));
    }

    [Fact]
    public void Monthly_simulation_advances_siege_and_hands_over_citadel_on_breach()
    {
        using var kernel = _host.Boot();
        var war = kernel.Capabilities.Get<IGeopoliticalWar>(CapabilityIds.GeopoliticalWar);
        var db = kernel.Db;

        // Ensure Citadel 11 belongs to Faction 2 initially
        db.Execute("UPDATE po_city_governance SET faction_id = 2, defense_strength = 50, unrest_level = 10 WHERE citadel_id = 11");

        // Attacker Faction 1 launches high intensity breach campaign
        war.TriggerConflict(1, 2, 11, initialIntensity: 3);

        // Force siege progress to 95 to verify imminent handover
        db.Execute("UPDATE po_faction_conflict SET siege_progress = 95 WHERE target_citadel_id = 11 AND status = 'ACTIVE'");

        // Advance 30 days to trigger monthly pulse
        kernel.AdvanceDays(30);

        // Verify that Citadel 11 was conquered and handed over to Faction 1
        var newFaction = db.ScalarLong("SELECT faction_id FROM po_city_governance WHERE citadel_id = 11");
        Assert.Equal(1, newFaction);

        // Defense was reduced and unrest spiked
        var unrest = db.ScalarLong("SELECT unrest_level FROM po_city_governance WHERE citadel_id = 11");
        Assert.True(unrest >= 70, $"Expected unrest >= 70, got {unrest}");

        // Conflict should now be resolved with victory
        var conflict = db.Query(
            "SELECT status, war_stage FROM po_faction_conflict WHERE target_citadel_id = 11",
            r => (Status: r.GetString(0), Stage: r.GetString(1)));
        Assert.NotEmpty(conflict);
        Assert.Equal("RESOLVED_VICTORY", conflict[0].Status);
        Assert.Equal("PACIFIED", conflict[0].Stage);
    }

    [Fact]
    public void Under_siege_citadel_experiences_war_commodity_price_surges()
    {
        using var kernel = _host.Boot();
        var war = kernel.Capabilities.Get<IGeopoliticalWar>(CapabilityIds.GeopoliticalWar);
        var market = kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade);

        var citadelId = 11; // Hán Trung
        var riceId = 1;     // Lương thảo (Grain/Food)
        var ironId = 5;     // Thiết khoáng (Iron/Metals)
        var silkId = 4;     // Gấm lụa (Luxury)

        var baselineRicePrice = market.QuoteBuy(citadelId, riceId);
        var baselineIronPrice = market.QuoteBuy(citadelId, ironId);
        var baselineSilkPrice = market.QuoteBuy(citadelId, silkId);

        // Trigger active siege
        war.TriggerConflict(1, 2, citadelId, initialIntensity: 2);
        Assert.True(war.IsCitadelUnderSiege(citadelId));

        var wartimeRicePrice = market.QuoteBuy(citadelId, riceId);
        var wartimeIronPrice = market.QuoteBuy(citadelId, ironId);
        var wartimeSilkPrice = market.QuoteBuy(citadelId, silkId);

        // Grain & Iron must surge significantly (+100% and +120%)
        Assert.True(wartimeRicePrice >= baselineRicePrice * 1.8,
            $"Expected rice surge, baseline={baselineRicePrice}, wartime={wartimeRicePrice}");
        Assert.True(wartimeIronPrice >= baselineIronPrice * 1.9,
            $"Expected iron surge, baseline={baselineIronPrice}, wartime={wartimeIronPrice}");

        // Luxuries drop under siege as survival dominates (-25%)
        Assert.True(wartimeSilkPrice < baselineSilkPrice,
            $"Expected luxury discount under siege, baseline={baselineSilkPrice}, wartime={wartimeSilkPrice}");
    }

    [Fact]
    public void Caravans_experience_war_delay_when_heading_to_warzone()
    {
        using var kernel = _host.Boot();
        var war = kernel.Capabilities.Get<IGeopoliticalWar>(CapabilityIds.GeopoliticalWar);
        var caravan = kernel.Capabilities.Get<ICaravanGuild>(CapabilityIds.CaravanGuild);
        var map = kernel.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        var bag = kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);

        wallet.Pay(10000, "TEST", "Setup");

        var fromCit = 1; // Lạc Dương
        var toCit = 2;   // Trường An

        // Give player goods in origin
        bag.Add(fromCit, 1, 10);

        // Normal dispatch travel days
        var initialRes = caravan.Send(fromCit, toCit);
        Assert.True(initialRes.Ok);
        var normalRun = caravan.Runs.OrderBy(r => r.Id).First();
        var normalDuration = normalRun.ArriveDayIndex - normalRun.DepartDayIndex;

        // Reset goods
        bag.Add(fromCit, 1, 10);

        // Now place destination Citadel under siege
        war.TriggerConflict(1, 2, toCit, initialIntensity: 2);
        Assert.True(war.IsCitadelUnderSiege(toCit));

        var warRes = caravan.Send(fromCit, toCit);
        Assert.True(warRes.Ok);
        var warRun = caravan.Runs.OrderBy(r => r.Id).Last();
        var warDuration = warRun.ArriveDayIndex - warRun.DepartDayIndex;

        // Travel duration must be lengthened (+50% due to hostile siege checkpoints & detours)
        Assert.True(warDuration > normalDuration,
            $"Expected war delay: normal={normalDuration} days, war={warDuration} days");
    }

    [Fact]
    public void Invariants_pass_cleanly_with_geopolitical_conflicts()
    {
        using var kernel = _host.Boot();
        var war = kernel.Capabilities.Get<IGeopoliticalWar>(CapabilityIds.GeopoliticalWar);

        // Trigger multiple conflicts across the realm
        war.TriggerConflict(1, 2, 11, 2);
        war.TriggerConflict(1, 3, 17, 1);

        // Run monthly simulation
        kernel.AdvanceDays(60);

        // All system invariants must be satisfied
        var failures = kernel.RunInvariants();
        Assert.Empty(failures);
    }
}
