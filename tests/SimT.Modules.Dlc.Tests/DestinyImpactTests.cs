using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Core.Kernel;
using SimT.Testing;

namespace SimT.Modules.Dlc.Tests;

public sealed class DestinyImpactTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void Market_pricing_reflects_destiny_discounts_and_bonuses()
    {
        using var kernel = _host.Boot();
        var trade = kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade);

        var baseBuy = trade.QuoteBuy(1, 4);
        var baseSell = trade.QuoteSell(1, 4);

        // Learn Dao Chu Root (-3% buy discount) & Margin (+4% sell bonus)
        kernel.Db.Execute("INSERT INTO pl_destiny_talent(player_id, node_id, learned_day) VALUES (1, 'daochu_root', 66000)");
        kernel.Db.Execute("INSERT INTO pl_destiny_talent(player_id, node_id, learned_day) VALUES (1, 'daochu_minor_margin', 66000)");

        var discountedBuy = trade.QuoteBuy(1, 4);
        var boostedSell = trade.QuoteSell(1, 4);

        Assert.True(discountedBuy < baseBuy);
        Assert.True(boostedSell > baseSell);
    }

    [Fact]
    public void Farm_harvest_applies_yield_bonus_and_soil_drain_reduction()
    {
        using var kernel = _host.Boot();
        var farm = kernel.Capabilities.Get<ICultivationEstate>(CapabilityIds.CultivationEstate);
        var bag = kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);

        // Rent plot 1 and set crop to READY (planted 66000, ready 66005)
        kernel.Db.Execute("UPDATE fm_plot SET rented = 1, soil = 100 WHERE id = 1");
        kernel.Db.Execute("INSERT INTO fm_crop(id, plot_id, item_id, planted_day, ready_day, status, yield_qty) VALUES(999, 1, 1, 66000, 66005, 'READY', 20)");
        bag.Add(1, 23, 1); // Sickle (#23)

        // Learn Ba Dao Root (+5% yield) and Minor Soil (-15% soil drain)
        kernel.Db.Execute("INSERT INTO pl_destiny_talent(player_id, node_id, learned_day) VALUES (1, 'badao_root', 66000)");
        kernel.Db.Execute("INSERT INTO pl_destiny_talent(player_id, node_id, learned_day) VALUES (1, 'badao_minor_soil', 66000)");

        var harvestResult = farm.Harvest(1, 1);
        Assert.True(harvestResult.Ok, harvestResult.Message);

        var plotAfter = farm.Plots.First(p => p.Id == 1);
        // Soil drain: standard is 15. With 15% reduction -> 13 loss -> 87 left
        Assert.True(plotAfter.Soil > 75);

        // Harvested quantity in bag should be >= 21 (base 20 * 1.05 = 21)
        var harvested = bag.Quantity(1, 1);
        Assert.True(harvested >= 21);
    }

    [Fact]
    public void Tactical_duel_and_vitals_apply_destiny_modifiers()
    {
        using var kernel = _host.Boot();
        var attrs = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);

        var initialMaxHp = attrs.Vitals.MaxHp;
        var initialMaxStamina = attrs.Vitals.MaxStamina;

        // Learn Dao Gia Root (+10 max stamina) and Minor Longevity (+15 max health)
        kernel.Db.Execute("INSERT INTO pl_destiny_talent(player_id, node_id, learned_day) VALUES (1, 'daogia_root', 66000)");
        kernel.Db.Execute("INSERT INTO pl_destiny_talent(player_id, node_id, learned_day) VALUES (1, 'daogia_minor_longevity', 66000)");

        Assert.Equal(initialMaxHp + 15, attrs.Vitals.MaxHp);
        Assert.Equal(initialMaxStamina + 10, attrs.Vitals.MaxStamina);

        // Test Fame Gain Bonus
        var repBefore = kernel.Capabilities.Get<IPlayerReputation>(CapabilityIds.PlayerReputation).Standing.Fame;
        kernel.Db.Execute("INSERT INTO pl_destiny_talent(player_id, node_id, learned_day) VALUES (1, 'vuongdao_root', 66000)"); // +15% fame
        attrs.GainFame(100, "Thiên hạ đại sự");
        var repAfter = kernel.Capabilities.Get<IPlayerReputation>(CapabilityIds.PlayerReputation).Standing.Fame;
        Assert.True(repAfter - repBefore >= 115);
    }

    [Fact]
    public void Politics_court_stipend_applies_destiny_bonus()
    {
        using var kernel = _host.Boot();
        var court = kernel.Capabilities.Get<IPoliticalCourt>(CapabilityIds.CourtOffice);

        // Appoint to an office (Huyện Thừa, id 1, base stipend 80)
        kernel.Db.Execute("INSERT INTO po_state (key, value) VALUES ('office_id', 1) ON CONFLICT(key) DO UPDATE SET value = 1");
        var baseStipend = court.MonthlyStipend;
        Assert.Equal(80, baseStipend);

        // Learn Vuong Dao Notable Court (+25% stipend)
        kernel.Db.Execute("INSERT INTO pl_destiny_talent(player_id, node_id, learned_day) VALUES (1, 'vuongdao_notable_court', 66000)");

        Assert.Equal(100, court.MonthlyStipend); // 80 * 1.25 = 100
    }
}
