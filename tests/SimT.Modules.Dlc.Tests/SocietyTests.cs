using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Core.Kernel;

namespace SimT.Modules.Dlc.Tests;

public sealed class SocietyTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void Society_initializes_present_npcs_with_activities_and_zero_deltas()
    {
        using var kernel = _host.Boot();
        var society = kernel.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);

        var activities = society.AllActivities();
        Assert.Equal(registry.All.Count, activities.Count);

        // Check Quan Vũ (historical martial general)
        var guanYu = registry.All.First(n => n.NameVi.Contains("Quan Vũ"));
        Assert.NotNull(guanYu);
        var guanYuActivity = society.GetNpcActivity(guanYu.Id);
        Assert.NotNull(guanYuActivity);
        Assert.Equal("martial", guanYuActivity.FocusPath);
        Assert.True(guanYuActivity.CurrentWealth >= 1000);

        var deltas = society.GetStatDeltas(guanYu.Id);
        Assert.NotNull(deltas);
        Assert.Equal(0, deltas.TotalBonus);

        // Check Trương Lão Bản (merchant)
        var merchantA = registry.FindByCode("thuong_nhan_a");
        Assert.NotNull(merchantA);
        var merchantActivity = society.GetNpcActivity(merchantA.Id);
        Assert.NotNull(merchantActivity);
        Assert.Equal("merchant", merchantActivity.FocusPath);

        // Check inaugural chronicles
        var chronicles = society.RecentEvents(limit: 50);
        Assert.NotEmpty(chronicles);
        Assert.Contains(chronicles, c => c.HeadlineVi.Contains("tọa trấn"));
    }

    [Fact]
    public void Advancing_month_simulates_autonomous_npc_actions_and_stat_growth()
    {
        using var kernel = _host.Boot();
        var society = kernel.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle);

        var initialEventsCount = society.RecentEvents(limit: 100).Count;

        // Advance 30 days to cross month boundary
        kernel.AdvanceDays(30);

        var newEvents = society.RecentEvents(limit: 100);
        Assert.True(newEvents.Count > initialEventsCount, $"Expected events count to grow beyond {initialEventsCount}, got {newEvents.Count}");

        // Verify that some NPCs accumulated growth deltas
        var allActivities = society.AllActivities();
        var npcsWithGrowth = allActivities
            .Select(a => society.GetStatDeltas(a.NpcId))
            .Where(d => d.TotalBonus > 0)
            .ToList();

        Assert.NotEmpty(npcsWithGrowth);

        // Invariants must pass cleanly
        var failures = kernel.RunInvariants();
        Assert.Empty(failures);
    }

    [Fact]
    public void Chronicle_feed_filters_by_category_and_citadel()
    {
        using var kernel = _host.Boot();
        var society = kernel.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle);

        kernel.AdvanceDays(30);

        var allRecent = society.RecentEvents(limit: 100);
        Assert.NotEmpty(allRecent);

        var martialEvents = society.RecentEvents(limit: 100, category: "martial");
        Assert.All(martialEvents, e => Assert.Equal("martial", e.EventKind));

        var citadel1Events = society.RecentEvents(limit: 100, citadelId: 1);
        Assert.All(citadel1Events, e => Assert.Equal(1, e.CitadelId));
    }

    [Fact]
    public void Screen_renders_and_responds_to_commands()
    {
        using var kernel = _host.Boot();
        var screen = kernel.Ui.Screens.FirstOrDefault(s => s.ScreenId == "society.chronicle");
        Assert.NotNull(screen);

        var model = screen.Render();
        Assert.Equal("society.chronicle", model.ScreenId);
        Assert.Equal("Thế Thái", model.Title);
        Assert.NotEmpty(model.Sections);
        Assert.NotEmpty(model.Headline);

        // Test command handler filter
        Assert.True(kernel.TryGetCommandHandler("society.chronicle", out var handler));
        Assert.NotNull(handler);

        var status = handler.Execute("filter:martial");
        Assert.Contains("martial", status);

        var filteredModel = screen.Render();
        Assert.NotNull(filteredModel);
    }

    [Fact]
    public void NPC_registry_UpdateCitadel_moves_npc_between_citadels_and_persists()
    {
        using var kernel = _host.Boot();
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var db = kernel.Db;

        var npc = registry.FindByCode("npc_0001")!; // Lưu Bị
        var originalCit = npc.CitadelId;
        const long targetCit = 7; // Hứa Xương

        Assert.Contains(registry.FindByCitadel(originalCit), n => n.Id == npc.Id);
        Assert.DoesNotContain(registry.FindByCitadel(targetCit), n => n.Id == npc.Id);

        // Move to target
        registry.UpdateCitadel(npc.Id, targetCit);

        // Verify in-memory lookup
        var updated = registry.Find(npc.Id)!;
        Assert.Equal(targetCit, updated.CitadelId);

        Assert.DoesNotContain(registry.FindByCitadel(originalCit), n => n.Id == npc.Id);
        Assert.Contains(registry.FindByCitadel(targetCit), n => n.Id == npc.Id);

        // Verify DB persistence in np_npc
        var dbCit = db.ScalarLong("SELECT citadel_id FROM np_npc WHERE id = @id", DbArgs.One("@id", npc.Id));
        Assert.Equal(targetCit, dbCit);
    }

    [Fact]
    public void Society_migration_moves_npcs_and_records_chronicle_events()
    {
        using var kernel = _host.Boot();
        var society = kernel.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);

        // Advance 60 days (2 months) to allow migrations
        kernel.AdvanceDays(60);

        var travelEvents = society.RecentEvents(limit: 200, category: "travel");
        Assert.NotEmpty(travelEvents);

        // Verify that journey log contains migration/travel entries
        Assert.Contains(travelEvents, e => e.EventKind == "travel");

        // Verify activity location matches registry location
        foreach (var evt in travelEvents)
        {
            var act = society.GetNpcActivity(evt.NpcId);
            var npc = registry.Find(evt.NpcId);
            Assert.NotNull(act);
            Assert.NotNull(npc);
            Assert.Equal(npc.CitadelId, act.CurrentCitadelId);
        }
    }

    [Fact]
    public void Society_replenish_spawns_procedural_npcs_with_valid_attributes_and_skills()
    {
        using var kernel = _host.Boot();
        var society = kernel.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);

        var baselineCount = registry.All.Count;
        Assert.InRange(baselineCount, 1, 299);
        Assert.Equal(300, kernel.Db.ScalarLong("SELECT COUNT(*) FROM np_npc"));

        // Explicit procedural replenishment
        var spawned = society.ReplenishNpcs(kernel.Today, count: 2);
        Assert.Equal(2, spawned.Count);
        Assert.Equal(baselineCount + 2, registry.All.Count);

        foreach (var npc in spawned)
        {
            Assert.NotNull(npc);
            Assert.True(npc.Id > 300);
            Assert.NotEmpty(npc.NameVi);
            Assert.NotEmpty(npc.Role);
            Assert.True(npc.CitadelId >= 1 && npc.CitadelId <= 20);
            Assert.True(npc.Attributes.War > 0);
            Assert.NotEmpty(npc.Skills);

            var act = society.GetNpcActivity(npc.Id);
            Assert.NotNull(act);
            Assert.Equal(npc.CitadelId, act.CurrentCitadelId);
            Assert.True(act.CurrentWealth >= 500);

            var deltas = society.GetStatDeltas(npc.Id);
            Assert.NotNull(deltas);
        }

        // Check that chronicle logged the debut of procedural NPCs
        var debutLogs = society.RecentEvents(limit: 50);
        Assert.Contains(debutLogs, log => log.HeadlineVi.Contains("Nhân tài mới xuất thế"));
    }

    
    [Fact]
    public void Society_migration_responds_to_security_and_screen_filters_migration_flow()
    {
        using var kernel = _host.Boot();
        var society = kernel.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle);
        Assert.NotNull(society);

        // Advance 90 days (3 months) to allow multiple rounds of monthly simulation
        kernel.AdvanceDays(90);

        var allEvents = society.RecentEvents(limit: 100);
        Assert.NotEmpty(allEvents);

        // Test Society screen rendering with filter:migration
        var screenDef = kernel.Ui.Screens.FirstOrDefault(s => s.ScreenId == "society.chronicle");
        Assert.NotNull(screenDef);

        var modelBefore = screenDef.Render();
        Assert.NotNull(modelBefore);
        Assert.Contains(modelBefore.Commands, c => c.Id == "filter:migration");

        Assert.True(kernel.TryGetCommandHandler("society.chronicle", out var handler));
        var filterResult = handler!.Execute("filter:migration");
        Assert.Contains("migration", filterResult);

        var modelFiltered = screenDef.Render();
        Assert.NotNull(modelFiltered);
        Assert.True(modelFiltered.Sections.Any(s => s.Heading.Contains("Dòng di cư")), "Expected a section with Dòng di cư");

        var failures = kernel.RunInvariants();
        Assert.Empty(failures);
    }


    [Fact]
    public void Society_rng_is_campaign_seeded_and_deterministic()
    {
        // 1. Two separate campaigns with the exact same campaign seed produce 100% reproducible society events
        List<(long NpcId, string Headline, string Detail)> events1;
        using (var host1 = new DlcTestHost(seed: 424242))
        using (var kernel1 = host1.Boot())
        {
            kernel1.AdvanceDays(30);
            events1 = kernel1.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle)
                .RecentEvents(limit: 50)
                .Select(e => (e.NpcId, e.HeadlineVi, e.DetailVi))
                .ToList();
        }

        List<(long NpcId, string Headline, string Detail)> events2;
        using (var host2 = new DlcTestHost(seed: 424242))
        using (var kernel2 = host2.Boot())
        {
            kernel2.AdvanceDays(30);
            events2 = kernel2.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle)
                .RecentEvents(limit: 50)
                .Select(e => (e.NpcId, e.HeadlineVi, e.DetailVi))
                .ToList();
        }

        Assert.NotEmpty(events1);
        Assert.Equal(events1.Count, events2.Count);
        for (var i = 0; i < events1.Count; i++)
        {
            Assert.Equal(events1[i], events2[i]);
        }

        // 2. A campaign with a different seed produces divergent events
        List<(long NpcId, string Headline, string Detail)> events3;
        using (var host3 = new DlcTestHost(seed: 999999))
        using (var kernel3 = host3.Boot())
        {
            kernel3.AdvanceDays(30);
            events3 = kernel3.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle)
                .RecentEvents(limit: 50)
                .Select(e => (e.NpcId, e.HeadlineVi, e.DetailVi))
                .ToList();
        }

        Assert.NotEmpty(events3);
        var diff = events1.Count != events3.Count || !events1.SequenceEqual(events3);
        Assert.True(diff, "Different campaign seeds must produce divergent society simulation outcomes.");
    }
}