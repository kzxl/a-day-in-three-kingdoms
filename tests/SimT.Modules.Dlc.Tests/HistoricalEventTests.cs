using System.Text.Json;
using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;
using SimT.Contracts.Time;
using SimT.Core.Kernel;
using SimT.Core.Modules;

namespace SimT.Modules.Dlc.Tests;

public sealed class HistoricalEventTests : IDisposable
{
    private readonly DlcTestHost _host = new();
    public void Dispose() => _host.Dispose();

    private static ISocietyChronicle Society(SimKernel k) => k.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle);
    private static IPlayerState Player(SimKernel k) => k.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
    private static INpcRegistry Npcs(SimKernel k) => k.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
    private static IWorldMap Map(SimKernel k) => k.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);
    private static IPoliticalCourt Court(SimKernel k) => k.Capabilities.Get<IPoliticalCourt>(CapabilityIds.CourtOffice);
    private static ITavernRumors Rumors(SimKernel k) => k.Capabilities.Get<ITavernRumors>(CapabilityIds.TavernRumors);

    [Fact]
    public void All_14_historical_events_are_seeded_in_chronological_order_184_to_220()
    {
        using var k = _host.Boot();
        var events = Society(k).GetHistoricalEvents();
        Assert.Equal(14, events.Count);
        Assert.Equal("evt_184_khan_vang", events[0].Code);
        Assert.Equal("evt_220_tao_phi_xung_de", events[^1].Code);
        Assert.All(events, e =>
        {
            Assert.Equal("pending", e.Status);
            Assert.NotEmpty(e.TitleVi);
            Assert.NotEmpty(e.NarrativeVi);
            Assert.NotEmpty(e.Choices);
            Assert.NotEmpty(e.Impacts);
        });
    }

    [Fact]
    public void Event_applies_on_exact_date_once_even_when_advancing_multiple_days_and_supports_choice()
    {
        using var k = _host.Boot();
        var society = Society(k);
        var wallet = k.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        wallet.Pay(500, "test", "Test fund");

        // Move player to Quảng Tông (5), primary citadel of evt_184_khan_vang
        Player(k).MoveTo(5, k.Today);

        // Advance from 184-05-02 to 184-08-10 (98 days): still before 184-08-15
        k.AdvanceDays(98);
        Assert.Equal(new GameDate(184, 8, 10), k.Today);
        Assert.Equal("pending", society.FindHistoricalEvent("evt_184_khan_vang")!.Status);

        // Jump 10 days at once across 184-08-15 -> 184-08-20
        k.AdvanceDays(10);
        Assert.Equal(new GameDate(184, 8, 20), k.Today);

        var evt = society.FindHistoricalEvent("evt_184_khan_vang")!;
        Assert.Equal("occurred", evt.Status);
        Assert.Equal(new GameDate(184, 8, 15), evt.OccurredDate);

        // Advance another 5 days: must NOT re-apply or duplicate journey log entry
        k.AdvanceDays(5);
        var historyLogs = k.Db.ScalarLong("SELECT COUNT(*) FROM so_journey_log WHERE event_kind='history' AND stat_impact LIKE 'evt:evt_184_khan_vang%'");
        Assert.Equal(1, historyLogs);

        // Player is at Quảng Tông (5), which is in RelatedCitadelIds (5, 4, 3) -> scene is queued
        var pendingSceneEvt = society.DequeuePendingHistoricalEvent(Player(k).CurrentCityId);
        Assert.NotNull(pendingSceneEvt);
        Assert.Equal("evt_184_khan_vang", pendingSceneEvt!.Code);

        var beforeLiquid = wallet.Liquid;
        var outcome = society.ResolveHistoricalEventChoice("evt_184_khan_vang", "supply_medicines");
        Assert.True(outcome.Ok);
        Assert.Equal(-80, outcome.CopperDelta);
        Assert.Equal(beforeLiquid - 80, wallet.Liquid);
        Assert.Equal("viewed", society.FindHistoricalEvent("evt_184_khan_vang")!.Status);

        // Replaying choice must not charge a second time
        var replayOutcome = society.ResolveHistoricalEventChoice("evt_184_khan_vang", "supply_medicines");
        Assert.True(replayOutcome.Ok);
        Assert.Equal(0, replayOutcome.CopperDelta);
        Assert.Equal(beforeLiquid - 80, wallet.Liquid);

        // Chronicle screen filter:history displays the historical event
        k.Ui.TryGetCommandHandler("society.chronicle", out var handler);
        handler!.Execute("filter:history");
        var model = k.Ui.Screens.Single(s => s.ScreenId == "society.chronicle").Render();
        Assert.Contains(model.Sections, s => s.Rows.Any(r => r.Cells.Any(c => c.Contains("Khăn Vàng"))));
    }

    [Fact]
    public void Distant_player_gets_status_notice_and_city_owner_updates_map_faction_color()
    {
        using var k = _host.Boot();
        var society = Society(k);

        // Move player to Thành Đô (15), far from Lạc Dương (1)
        Player(k).MoveTo(15, k.Today);

        // Before 189-09-25, Lạc Dương (1) belongs to faction 1 (#ef4444)
        var beforeMap = MapScene.Build(k.Capabilities, k.Today, 1000)!;
        var lacDuongBefore = beforeMap.Cities.Single(c => c.Id == 1);
        Assert.Equal(1, lacDuongBefore.FactionId);
        Assert.Equal("#ef4444", lacDuongBefore.FactionColor);

        // Advance to 189-09-25 (Đổng Trác tiến kinh Lạc Dương)
        var daysTo189 = (int)(new GameDate(189, 9, 25).DayIndex - k.Today.DayIndex);
        k.AdvanceDays(daysTo189);

        // Distant player gets status notice, NOT pending scene at city 15
        Assert.Null(society.DequeuePendingHistoricalEvent(Player(k).CurrentCityId));
        var notice = society.ConsumeLastHistoricalStatus();
        Assert.NotNull(notice);
        Assert.Contains("Đổng Trác", notice);

        // Lạc Dương (1) is now owned by Đổng Trác (faction 6, #71717a) on MapScene
        var afterMap = MapScene.Build(k.Capabilities, k.Today, 1000)!;
        var lacDuongAfter = afterMap.Cities.Single(c => c.Id == 1);
        Assert.Equal(6, lacDuongAfter.FactionId);
        Assert.Equal("Đổng Trác", lacDuongAfter.FactionNameVi);
        Assert.Equal("#71717a", lacDuongAfter.FactionColor);
    }

    [Fact]
    public void Campaign_starting_after_event_date_applies_only_final_state_without_shocks_or_scene()
    {
        var dir = Path.Combine(Path.GetDirectoryName(_host.DatabasePath)!, "late-start");
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "late.db");
        var productPath = Path.Combine(dir, "product-190.json");
        File.WriteAllText(productPath, JsonSerializer.Serialize(new
        {
            id = "late-190",
            name = "Late Campaign 190",
            modules = ModuleLoader.Discover(AppContext.BaseDirectory).Select(m => m.Manifest.Id).OrderBy(x => x).ToArray(),
            seed = 1840502,
            startDate = "190-01-01",
            startLocation = "lac_duong",
            startingCash = 2000
        }));

        using var k = SimKernel.Open(new KernelOptions(
            dbPath, AppContext.BaseDirectory, productPath, SimT.Testing.TestHost.ConstantsPath,
            BackupDirectory: Path.Combine(dir, "backups")));

        var society = Society(k);
        Assert.Equal(new GameDate(190, 1, 1), k.Today);

        // 184 and 189 events are already viewed, no pending scene, no distant notice
        Assert.Equal("viewed", society.FindHistoricalEvent("evt_184_khan_vang")!.Status);
        Assert.Equal("viewed", society.FindHistoricalEvent("evt_189_dong_trac_lac_duong")!.Status);
        Assert.Null(society.DequeuePendingHistoricalEvent(Player(k).CurrentCityId));
        Assert.Null(society.ConsumeLastHistoricalStatus());

        // Final states ARE applied: Lạc Dương owned by Đổng Trác (6), Trương Giác (npc_0004) deceased, Lữ Bố (npc_0057) at Lạc Dương (1)
        Assert.Equal(6, Court(k).Governances.Single(g => g.CitadelId == 1).FactionId);
        var truongGiac = Npcs(k).FindByCode("npc_0004")!;
        Assert.True(truongGiac.IsDeceased(society));
        var luBo = Npcs(k).FindByCode("npc_0057")!;
        Assert.Equal(1, luBo.CurrentCitadelId(society));
        Assert.Equal(6, society.GetNpcActivity(luBo.Id)!.AllegianceFactionId);

        // Temporary market shocks and route hazards were NOT triggered
        var activeShocks = k.Db.ScalarLong("SELECT COUNT(*) FROM mk_city_shock");
        Assert.Equal(0, activeShocks);
        Assert.False(Map(k).IsRouteBlocked(1, 2));
    }

    [Fact]
    public void Old_save_opened_at_later_date_applies_historical_state_and_migration_is_repeatable()
    {
        using (var k = _host.Boot())
        {
            k.Save();
        }

        using (var db = SimT.Core.Data.SqliteSession.Open(_host.DatabasePath))
        {
            // Simulate an old save at 193-01-01 before historical events ran
            db.Execute("INSERT INTO core_state(key, value) VALUES('date', '193-01-01') ON CONFLICT(key) DO UPDATE SET value='193-01-01'");
            db.Execute("UPDATE so_historical_event SET status='pending', occurred_date=NULL, choice_code=NULL");

            // Migration 0304 must be repeatable without error or data loss
            var sql = File.ReadAllText(Path.Combine(DlcTestHost.RepoRoot,
                "src/SimT.Modules.Dlc.Society/migrations/0304-society-historical-events.sql"));
            db.Execute(sql);
            db.Execute(sql);
        }

        using var reopened = _host.Boot();
        var society = Society(reopened);
        Assert.Equal(new GameDate(193, 1, 1), reopened.Today);

        // Events up to 192 are applied as viewed final state; 196+ remain pending
        Assert.Equal("viewed", society.FindHistoricalEvent("evt_184_khan_vang")!.Status);
        Assert.Equal("viewed", society.FindHistoricalEvent("evt_191_ton_kien_tuong_duong")!.Status);
        Assert.Equal("viewed", society.FindHistoricalEvent("evt_192_lu_bo_tru_dong_trac")!.Status);
        Assert.Equal("pending", society.FindHistoricalEvent("evt_196_hien_de_hua_xuong")!.Status);

        // Tôn Kiên (npc_0014) and Đổng Trác (npc_0023) are deceased; Trường An (2) owned by faction 6
        Assert.True(Npcs(reopened).FindByCode("npc_0014")!.IsDeceased(society));
        Assert.True(Npcs(reopened).FindByCode("npc_0023")!.IsDeceased(society));
        Assert.Equal(6, Court(reopened).Governances.Single(g => g.CitadelId == 2).FactionId);
    }

    [Fact]
    public void Npc_death_hides_npc_from_city_and_marks_profile_deceased_without_deleting_db_row()
    {
        using var k = _host.Boot();
        var society = Society(k);
        var reg = Npcs(k);

        var truongGiacBefore = reg.FindByCode("npc_0004")!;
        var citId = truongGiacBefore.CurrentCitadelId(society);
        Assert.Contains(NpcLocation.BuildCityNpcs(reg, society, citId), s => s.NpcId == truongGiacBefore.Id);

        // Advance to 184-08-15 (evt_184_khan_vang marks npc_0004 Trương Giác deceased)
        var days = (int)(new GameDate(184, 8, 15).DayIndex - k.Today.DayIndex);
        k.AdvanceDays(days);

        // DB row still exists
        var dbCount = k.Db.ScalarLong("SELECT COUNT(*) FROM np_npc WHERE code='npc_0004'");
        Assert.Equal(1, dbCount);

        var truongGiacAfter = reg.FindByCode("npc_0004")!;
        Assert.True(truongGiacAfter.IsDeceased(society));
        Assert.Contains("Đã mất", truongGiacAfter.BioVi);

        // Removed from BuildCityNpcs
        Assert.DoesNotContain(NpcLocation.BuildCityNpcs(reg, society, citId), s => s.NpcId == truongGiacAfter.Id);

        // Shown as "Đã mất" in npc.directory screen
        var dirModel = k.Ui.Screens.Single(s => s.ScreenId == "npc.directory").Render();
        var row = dirModel.Sections.SelectMany(s => s.Rows).Single(r => r.Cells.Any(c => c.Contains("Trương Giác")));
        Assert.Contains(row.Cells, c => c.Contains("Đã mất"));
    }

    [Fact]
    public void Foreshadow_rumor_appears_within_30_days_before_event_and_verifies_on_event_day()
    {
        using var k = _host.Boot();
        Assert.True(k.Capabilities.IsProvided(CapabilityIds.HistoricalRumorEvents));
        var source = k.Capabilities.Get<IEventRumorSource>(CapabilityIds.HistoricalRumorEvents);
        k.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet).Pay(5000, "test", "Test");

        // Advance to 184-07-20 (25 days before 184-08-15)
        var daysToWindow = (int)(new GameDate(184, 7, 20).DayIndex - k.Today.DayIndex);
        k.AdvanceDays(daysToWindow);

        var upcoming = source.Upcoming(k.Today.DayIndex, 30);
        var hint = Assert.Single(upcoming, h => h.Key == "evt_184_khan_vang");
        Assert.False(source.HasOccurred("evt_184_khan_vang", k.Today.DayIndex));

        var heard = Rumors(k).Listen();
        var eventRumor = Assert.Single(heard, r => r.Kind == "event");
        Assert.Null(Rumors(k).History().Single(r => r.Id == eventRumor.Id).VerifiedTruth);

        // Advance 26 days across 184-08-15 -> rumor is verified
        k.AdvanceDays(26);
        Assert.True(source.HasOccurred("evt_184_khan_vang", k.Today.DayIndex));
        Assert.NotNull(Rumors(k).History().Single(r => r.Id == eventRumor.Id).VerifiedTruth);
    }
}
