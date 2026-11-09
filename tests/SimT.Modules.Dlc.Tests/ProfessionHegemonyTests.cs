using SimT.Contracts.Data;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Core.Kernel;
using Xunit;

namespace SimT.Modules.Dlc.Tests;

public sealed class ProfessionHegemonyTests : IDisposable
{
    private sealed record DemoSnapshot(int Prosperity, int Starvation, string Status);
    private sealed record PlagueSnapshot(int Prosperity, long Population);

    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void ProfessionProgression_separates_skill_mastery_and_enterprise_cleanly()
    {
        using var kernel = _host.Boot();
        var guild = kernel.Capabilities.Get<IProfessionGuild>(CapabilityIds.ProfessionGuild);

        // Trade 4: luyen_kim
        var prog = guild.GetProgression(4);
        Assert.NotNull(prog);
        Assert.Equal("luyen_kim", prog.TradeCode);
        Assert.Equal(0, prog.SkillTier);
        Assert.Equal("Tiểu Hộ Cá Thể", prog.EnterpriseScaleVi);
        Assert.Equal("Thợ Mới", prog.MasteryRankVi);

        // Open workshop and hire artisans
        kernel.Db.Execute(
            "INSERT INTO pf_workshop (id, citadel_id, trade_id, name_vi, monthly_rent, furnace_level, hired_artisans, is_active, created_day_index) " +
            "VALUES (99, 1, 4, 'Lò Rèn Hoàng Gia', 100, 3, 4, 1, 1)");

        // Practice to tier 5
        kernel.Db.Execute(
            "INSERT OR REPLACE INTO pf_skill (trade_id, practice_count, tier) VALUES (4, 25, 5)");

        var advancedProg = guild.GetProgression(4);
        Assert.NotNull(advancedProg);
        Assert.Equal(5, advancedProg.SkillTier);
        Assert.Equal("Hóa cảnh (Tông sư)", advancedProg.SkillTitleVi);
        Assert.Equal("Tông Sư Xuất Trần", advancedProg.MasteryRankVi);
        Assert.Equal(1, advancedProg.WorkshopCount);
        Assert.Equal(4, advancedProg.TotalArtisansHired);
        Assert.True(advancedProg.TotalPassiveOutput > 0);
        Assert.Equal("Tiểu Tác Phường (Phân xưởng)", advancedProg.EnterpriseScaleVi);
        Assert.True(advancedProg.IsHegemonyUnlocked);
        Assert.Equal("Thiết Tượng Trấn Quốc", advancedProg.HegemonyTitleVi);
    }

    [Fact]
    public void CareerHegemony_blacksmith_tier5_enacts_imperial_armory_and_boosts_city_weapons()
    {
        using var kernel = _host.Boot();
        var guild = kernel.Capabilities.Get<IProfessionGuild>(CapabilityIds.ProfessionGuild);
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);

        var citadelId = 1L;

        // Setup Tier 5 blacksmith and level 3 furnace workshop
        kernel.Db.Execute("INSERT OR REPLACE INTO pf_skill (trade_id, practice_count, tier) VALUES (4, 25, 5)");
        kernel.Db.Execute(
            "INSERT INTO pf_workshop (id, citadel_id, trade_id, name_vi, monthly_rent, furnace_level, hired_artisans, is_active, created_day_index) " +
            "VALUES (99, @cit, 4, 'Lò Rèn Lạc Dương', 100, 3, 2, 1, 1)",
            DbArgs.One("@cit", citadelId));

        var initialBalance = wallet.Liquid;
        var initialFame = guild.TotalGuildFame;
        var initialWeapons = (int)kernel.Db.ScalarLong("SELECT weapons_stock FROM ml_citadel_armory WHERE citadel_id = @cit", DbArgs.One("@cit", citadelId));

        var result = guild.EnactHegemony(citadelId, "IMPERIAL_ARMORY");
        Assert.True(result.Ok);
        Assert.Contains("Thiết Tượng", result.Message);

        var finalWeapons = (int)kernel.Db.ScalarLong("SELECT weapons_stock FROM ml_citadel_armory WHERE citadel_id = @cit", DbArgs.One("@cit", citadelId));
        Assert.Equal(initialWeapons + 20, finalWeapons);
        Assert.Equal(initialBalance + 500, wallet.Liquid);
        Assert.Equal(initialFame + 30, guild.TotalGuildFame);
    }

    [Fact]
    public void CareerHegemony_farmer_tier5_enacts_grand_irrigation_wipes_famine_and_boosts_market_grain()
    {
        using var kernel = _host.Boot();
        var guild = kernel.Capabilities.Get<IProfessionGuild>(CapabilityIds.ProfessionGuild);
        var trade = kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade);
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);

        var citadelId = 1L;

        // Setup Tier 5 farmer and fund wallet
        kernel.Db.Execute("INSERT OR REPLACE INTO pf_skill (trade_id, practice_count, tier) VALUES (1, 25, 5)");
        wallet.Pay(1000, "test", "Fund irrigation project");

        // Put city into famine state
        kernel.Db.Execute(
            "UPDATE po_city_demographics SET starvation_months = 3, prosperity_score = 40 WHERE citadel_id = @cit",
            DbArgs.One("@cit", citadelId));

        var initialGrain = trade.StallStock(citadelId, 1);
        var initialProsperity = (int)kernel.Db.ScalarLong("SELECT prosperity_score FROM po_city_demographics WHERE citadel_id = @cit", DbArgs.One("@cit", citadelId));

        var result = guild.EnactHegemony(citadelId, "GRAND_IRRIGATION");
        Assert.True(result.Ok);
        Assert.Contains("Thần Nông", result.Message);

        var finalDemo = kernel.Db.QueryOne(
            "SELECT prosperity_score, starvation_months, famine_status_vi FROM po_city_demographics WHERE citadel_id = @cit",
            r => new DemoSnapshot(r.GetInt32(0), r.GetInt32(1), r.GetString(2)),
            DbArgs.One("@cit", citadelId))!;

        Assert.Equal(initialProsperity + 10, finalDemo.Prosperity);
        Assert.Equal(0, finalDemo.Starvation); // Famine wiped out
        Assert.Contains("Thủy lợi", finalDemo.Status);
        Assert.Equal(initialGrain + 50, trade.StallStock(citadelId, 1)); // +50 grain to market
    }

    [Fact]
    public void CareerHegemony_apothecary_tier5_enacts_plague_relief_and_restores_order()
    {
        using var kernel = _host.Boot();
        var guild = kernel.Capabilities.Get<IProfessionGuild>(CapabilityIds.ProfessionGuild);
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);

        var citadelId = 1L;

        // Trade 5: duoc_su
        kernel.Db.Execute("INSERT OR REPLACE INTO pf_skill (trade_id, practice_count, tier) VALUES (5, 25, 5)");
        wallet.Pay(500, "test", "Fund relief medicines");

        kernel.Db.Execute(
            "UPDATE po_city_demographics SET prosperity_score = 50, population = 100000 WHERE citadel_id = @cit",
            DbArgs.One("@cit", citadelId));

        var result = guild.EnactHegemony(citadelId, "PLAGUE_RELIEF");
        Assert.True(result.Ok);
        Assert.Contains("Y Thánh", result.Message);

        var finalPlague = kernel.Db.QueryOne(
            "SELECT prosperity_score, population FROM po_city_demographics WHERE citadel_id = @cit",
            r => new PlagueSnapshot(r.GetInt32(0), r.GetInt64(1)),
            DbArgs.One("@cit", citadelId))!;

        Assert.Equal(55, finalPlague.Prosperity);
        Assert.Equal(100500, finalPlague.Population);
    }
}
