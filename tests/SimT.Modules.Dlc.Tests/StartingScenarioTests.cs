using System.Text.Json;
using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Contracts.Products;
using SimT.Contracts.Scenes;
using SimT.Contracts.Time;
using SimT.Core.Kernel;
using SimT.Core.Modules;

namespace SimT.Modules.Dlc.Tests;

public sealed class StartingScenarioTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "simt-scenarios", Guid.NewGuid().ToString("N"));
    private string Database => Path.Combine(_directory, "campaign.db");
    private string Product => Path.Combine(_directory, "product.json");
    private SimKernel Open(GameDate? date = null, string? disabled = null)
    {
        Directory.CreateDirectory(_directory);
        if (date.HasValue)
            File.WriteAllText(Product, JsonSerializer.Serialize(new
            {
                id = "scenario-test", name = "Kịch bản", seed = 1840502,
                modules = ModuleLoader.Discover(AppContext.BaseDirectory).Select(d => d.Manifest.Id).Where(id => id != disabled).ToArray(),
                startDate = GameDateCodec.Format(date.Value), startLocation = "lac_duong", startingCash = 2000
            }));
        return SimKernel.Open(new KernelOptions(Database, AppContext.BaseDirectory, Product,
            Path.Combine(DlcTestHost.RepoRoot, "assets/config/game-constants.json"), BackupDirectory: Path.Combine(_directory, "backups")));
    }
    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
    private static INpcRegistry Npcs(SimKernel k) => k.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
    private static IPoliticalCourt Court(SimKernel k) => k.Capabilities.Get<IPoliticalCourt>(CapabilityIds.CourtOffice);
    private static ISocietyChronicle Society(SimKernel k) => k.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle);

    [Theory]
    [InlineData("184", 184, 5, 2)]
    [InlineData("190", 190, 1, 1)]
    [InlineData("200", 200, 7, 1)]
    [InlineData("208", 208, 7, 1)]
    public void Scenario_dates_and_permanent_state_survive_reopening(string code, int year, int month, int day)
    {
        var scenario = StartingScenarios.All.Single(s => s.Code == code);
        Assert.Equal(new GameDate(year, month, day), scenario.StartDate);
        long[] owners;
        using (var k = Open(scenario.StartDate))
        {
            Assert.Equal(scenario.StartDate, k.Today);
            owners = Court(k).Governances.Select(g => g.FactionId).ToArray();
            Assert.Equal(20, owners.Length);
            Assert.DoesNotContain(Court(k).Factions, f => f.NameVi is "Tào Ngụy" or "Thục Hán" or "Đông Ngô");
            Assert.All(Society(k).GetHistoricalEvents().Where(e => e.ScheduledDate.DayIndex <= k.Today.DayIndex),
                e => Assert.Equal("viewed", e.Status));
            Assert.All(Society(k).GetHistoricalEvents().Where(e => e.ScheduledDate.DayIndex > k.Today.DayIndex),
                e => Assert.Equal("pending", e.Status));
            Assert.Null(Society(k).DequeuePendingHistoricalEvent(1));
            k.Save();
        }
        using var reopened = Open();
        Assert.Equal(scenario.StartDate, reopened.Today);
        Assert.Equal(owners, Court(reopened).Governances.Select(g => g.FactionId));
    }

    [Fact]
    public void Han_owns_Luoyang_and_most_cities_in_184()
    {
        using var k = Open(StartingScenarios.All[0].StartDate);
        var map = MapScene.Build(k.Capabilities, k.Today, 1000)!;
        Assert.Equal("Hán triều", map.Cities.Single(c => c.Id == 1).FactionNameVi);
        Assert.Equal(19, map.Cities.Count(c => c.FactionNameVi == "Hán triều"));
        Assert.Equal("Khăn Vàng", map.Cities.Single(c => c.Id == 5).FactionNameVi);
    }

    [Fact]
    public void Guandu_has_Cao_at_Xuchang_Yuan_at_Ye_and_Dong_Zhuo_deceased()
    {
        using var k = Open(StartingScenarios.All[2].StartDate);
        var map = MapScene.Build(k.Capabilities, k.Today, 1000)!;
        Assert.Equal("Tào Tháo", map.Cities.Single(c => c.Id == 7).FactionNameVi);
        Assert.Equal("Viên Thiệu", map.Cities.Single(c => c.Id == 3).FactionNameVi);
        Assert.Equal("Lưu Chương", map.Cities.Single(c => c.Id == 15).FactionNameVi);
        Assert.Equal("Tôn Quyền", map.Cities.Single(c => c.Id == 13).FactionNameVi);
        Assert.True(Npcs(k).FindByCode("npc_0023")!.IsDeceasedFlag);
    }

    [Fact]
    public void Red_Cliffs_has_Zhuge_Liang_serving_Liu_Bei_and_Liu_Biao_still_in_Jingzhou()
    {
        using var k = Open(StartingScenarios.All[3].StartDate);
        var npc = Npcs(k).FindByCode("npc_0025")!;
        Assert.Contains(npc, Npcs(k).All);
        Assert.Equal(2, Society(k).GetNpcActivity(npc.Id)!.AllegianceFactionId);
        Assert.Equal(1, Court(k).Governances.Single(g => g.CitadelId == 3).FactionId);
        Assert.Equal(8, Court(k).Governances.Single(g => g.CitadelId == 12).FactionId);
        Assert.DoesNotContain(Npcs(k).All, n => n.Code == "npc_0075"); // Chung Hội, born 225
    }

    [Fact]
    public void Underage_and_unborn_stay_out_of_rosters_then_arrive_at_home_at_fifteen()
    {
        using (var k = Open(new GameDate(216, 12, 30)))
        {
            var jiang = Npcs(k).FindByCode("npc_0031")!;
            var zhong = Npcs(k).FindByCode("npc_0075")!;
            Assert.False(Npcs(k).IsPresent(jiang.Id));
            Assert.False(Npcs(k).IsPresent(zhong.Id));
            Assert.Throws<SimT.Contracts.SimException>(() =>
                k.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork).ChatWithNpc(jiang.Id));
            Assert.DoesNotContain(Npcs(k).FindByCitadel(jiang.CitadelId), n => n.Id == jiang.Id);
            var network = NpcBondScene.Build(k.Capabilities.Get<INpcBonds>(INpcBonds.CapabilityId), Npcs(k), 3);
            Assert.DoesNotContain(network.Nodes, n => n.NpcId == jiang.Id);
            k.AdvanceDays(1);
            Assert.True(Npcs(k).IsPresent(jiang.Id));
            Assert.Equal(19, Npcs(k).Find(jiang.Id)!.CitadelId);
            Assert.Equal(19, Society(k).GetNpcActivity(jiang.Id)!.CurrentCitadelId);
            k.Save();
        }
        using var reopened = Open();
        Assert.Contains(Npcs(reopened).All, n => n.Code == "npc_0031");
    }

    [Fact]
    public void Unknown_births_are_present_and_silent_deaths_log_once_across_reload()
    {
        using (var k = Open(new GameDate(207, 12, 30)))
        {
            Assert.Null(k.Db.ScalarString("SELECT birth_year FROM np_attribute WHERE npc_id=5"));
            Assert.True(Npcs(k).IsPresent(5)); // Quan Vũ: birth year disputed
            var guo = Npcs(k).FindByCode("npc_0037")!;
            Assert.False(guo.IsDeceasedFlag);
            k.AdvanceDays(1);
            Assert.True(Npcs(k).Find(guo.Id)!.IsDeceasedFlag);
            Assert.Equal(1, k.Db.ScalarLong("SELECT COUNT(*) FROM so_journey_log WHERE stat_impact='lifetime:36:207'"));
            k.Save();
        }
        using var reopened = Open();
        Assert.Equal(1, reopened.Db.ScalarLong("SELECT COUNT(*) FROM so_journey_log WHERE stat_impact='lifetime:36:207'"));
    }

    [Fact]
    public void Legacy_save_keeps_ownership_locations_and_living_roster_when_new_migrations_arrive()
    {
        using (var k = Open(new GameDate(184, 5, 2)))
        {
            k.Db.Execute("UPDATE po_city_governance SET faction_id=2,governor_npc_id=3 WHERE citadel_id=1");
            Npcs(k).UpdateCitadel(24, 15);
            k.Db.Execute("DROP TABLE np_lifetime; DROP TABLE np_lifetime_campaign; DROP TABLE po_faction_name");
            k.Db.Execute("DELETE FROM so_historical_event WHERE code IN ('evt_188_su_quan','evt_189_chu_hau')");
            k.Db.Execute("DELETE FROM core_migration WHERE migration_id LIKE '0108-%' OR migration_id LIKE '0704-%' OR migration_id LIKE '0306-%'");
            k.Save();
        }
        using var old = Open();
        Assert.Equal(new GameDate(184, 5, 2), old.Today);
        Assert.Equal(2, Court(old).Governances.Single(g => g.CitadelId == 1).FactionId);
        Assert.Equal(300, Npcs(old).All.Count);
        Assert.Equal(15, Npcs(old).Find(24)!.CitadelId);
        Assert.Equal("Tào Tháo", Court(old).Factions.Single(f => f.Id == 1).NameVi);
        Assert.DoesNotContain(Society(old).GetHistoricalEvents(), e => e.Code == "evt_188_su_quan");
        Assert.Equal(0, old.Db.ScalarLong("SELECT enabled FROM np_lifetime_campaign"));
    }

    [Theory]
    [InlineData(191, "Tôn Kiên", "Tào Tháo", "Lưu Bị")]
    [InlineData(199, "Tôn Sách", "Tào Tháo", "Lưu Bị")]
    [InlineData(208, "Tôn Quyền", "Tào Tháo", "Lưu Bị")]
    [InlineData(220, "Tôn Quyền", "Tào Ngụy", "Lưu Bị")]
    [InlineData(221, "Tôn Quyền", "Tào Ngụy", "Thục Hán")]
    [InlineData(229, "Đông Ngô", "Tào Ngụy", "Thục Hán")]
    public void Names_follow_the_current_year(int year, string wu, string wei, string shu)
    {
        using var k = Open(new GameDate(year, 1, 1));
        Assert.Equal(wu, Court(k).Factions.Single(f => f.Id == 3).NameVi);
        Assert.Equal(wei, Court(k).Factions.Single(f => f.Id == 1).NameVi);
        Assert.Equal(shu, Court(k).Factions.Single(f => f.Id == 2).NameVi);
    }

    [Fact]
    public void New_migrations_can_be_reexecuted_without_resetting_an_existing_campaign()
    {
        using var k = Open(new GameDate(208, 7, 1));
        k.Save();
        var owners = Court(k).Governances.ToArray();
        var logs = k.Db.ScalarLong("SELECT COUNT(*) FROM so_journey_log");
        var rows = k.Db.ScalarLong("SELECT COUNT(*) FROM np_lifetime");
        foreach (var path in new[] {
            "src/SimT.Modules.Base.Npc/migrations/0108-historical-lifetimes.sql",
            "src/SimT.Modules.Dlc.Power/migrations/0704-starting-world.sql",
            "src/SimT.Modules.Dlc.Society/migrations/0306-starting-world-transitions.sql" })
        {
            var sql = File.ReadAllText(Path.Combine(DlcTestHost.RepoRoot, path));
            k.Db.Execute(sql);
            k.Db.Execute(sql);
        }
        Assert.Equal(owners, Court(k).Governances);
        Assert.Equal(logs, k.Db.ScalarLong("SELECT COUNT(*) FROM so_journey_log"));
        Assert.Equal(rows, k.Db.ScalarLong("SELECT COUNT(*) FROM np_lifetime"));
        Assert.Equal(24, Society(k).GetHistoricalEvents().Count);
    }

    [Fact]
    public void Faction_names_change_at_year_boundary_without_reopening()
    {
        using var k = Open(new GameDate(220, 12, 30));
        Assert.Equal("Lưu Bị", Court(k).Factions.Single(f => f.Id == 2).NameVi);
        k.AdvanceDays(1);
        Assert.Equal("Thục Hán", Court(k).Factions.Single(f => f.Id == 2).NameVi);
    }

    [Theory]
    [InlineData("dlc.power")]
    [InlineData("dlc.society")]
    [InlineData("dlc.character")]
    public void Optional_dlc_can_be_disabled_for_late_scenarios(string disabled)
    {
        using var k = Open(new GameDate(208, 7, 1), disabled);
        Assert.Equal(new GameDate(208, 7, 1), k.Today);
        Assert.DoesNotContain(Npcs(k).All, n => n.Code == "npc_0031");
        k.AdvanceDays(1);
        Assert.Empty(k.RunInvariants());
    }
}
