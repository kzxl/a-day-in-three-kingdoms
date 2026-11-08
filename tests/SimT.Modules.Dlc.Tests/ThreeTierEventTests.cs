using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Core.Kernel;
using SimT.Core.Modules;

namespace SimT.Modules.Dlc.Tests;

public sealed class ThreeTierEventTests : IDisposable
{
    private readonly DlcTestHost _host = new();
    public void Dispose() => _host.Dispose();

    private static ISocietyChronicle Society(SimKernel k) => k.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle);
    private static INpcRegistry Npcs(SimKernel k) => k.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);

    [Fact]
    public void Historical_event_diverges_when_npc_precondition_broken()
    {
        using var k = _host.Boot();
        var society = Society(k);

        // Find evt_184_khan_vang (184-08-15), requires Truong Giac (npc_0004) alive
        var evt = society.FindHistoricalEvent("evt_184_khan_vang");
        Assert.NotNull(evt);
        Assert.NotEmpty(evt.Preconditions);
        Assert.Equal("npc_alive", evt.Preconditions[0].Type);

        // Mark Truong Giac deceased before the event triggers
        var truongGiac = Npcs(k).FindByCode("npc_0004");
        Assert.NotNull(truongGiac);
        Npcs(k).MarkDeceased(truongGiac.Id, "Bệnh mất sớm tại Quảng Tông");

        // Advance 110 days across 184-08-15
        k.AdvanceDays(110);

        var updated = society.FindHistoricalEvent("evt_184_khan_vang")!;
        Assert.Equal("diverged", updated.Status);
        Assert.True(updated.IsDiverged);
        Assert.Equal("evt_184_khan_vang_alt", updated.DivergenceCode);
        Assert.Contains("Khăn Vàng Tan Tác Sớm", updated.DivergenceTitleVi);

        // Journey log should record the divergence
        var divLogs = k.Db.ScalarLong("SELECT COUNT(*) FROM so_journey_log WHERE event_kind='history' AND stat_impact LIKE '%[Sử Biến - Rẽ Nhánh]%'");
        Assert.True(divLogs > 0);
    }

    [Fact]
    public void Dynamic_world_event_triggers_when_citadel_unrest_is_elevated()
    {
        using var k = _host.Boot();
        var society = Society(k);

        // Set high unrest on Citadel 1 (Lạc Dương) in po_city_governance
        k.Db.Execute("UPDATE po_city_governance SET unrest_level = 65, defense_strength = 30 WHERE citadel_id = 1");

        // Evaluate dynamic events
        var triggered = society.EvaluateAndTriggerDynamicEvents(k.Today);
        Assert.NotNull(triggered);
        Assert.Equal("order", triggered.Category);
        Assert.Equal(1, triggered.CitadelId);

        // Query dynamic events from chronicle
        var dynamicEvents = society.GetDynamicEvents();
        Assert.Contains(dynamicEvents, e => e.CitadelId == 1 && e.Category == "order");

        // Verify stored in DB
        var rowCount = k.Db.ScalarLong("SELECT COUNT(*) FROM so_dynamic_event WHERE citadel_id = 1");
        Assert.True(rowCount > 0);
    }

    [Fact]
    public void Personal_npc_event_triggers_when_affinity_is_high()
    {
        using var k = _host.Boot();
        var society = Society(k);

        // Ensure 2 living NPCs are at the same citadel (e.g. Citadel 1)
        var activities = society.AllActivities();
        var living = activities.Where(a => !a.IsDeceased).Take(2).ToList();
        Assert.True(living.Count >= 2);

        var npcA = living[0].NpcId;
        var npcB = living[1].NpcId;
        var citId = living[0].CurrentCitadelId;

        // Move npcB to same citadel
        k.Db.Execute("UPDATE so_npc_activity SET current_citadel_id = @cit WHERE npc_id = @b", new DbArgs().Add("@cit", citId).Add("@b", npcB));

        // Insert high affinity bond
        k.Db.Execute(@"
            INSERT INTO so_npc_bond (npc_a, npc_b, affinity, trust)
            VALUES (@a, @b, 75, 60)
            ON CONFLICT(npc_a, npc_b) DO UPDATE SET affinity = 75;
        ", new DbArgs().Add("@a", Math.Min(npcA, npcB)).Add("@b", Math.Max(npcA, npcB)));

        var triggered = society.EvaluateAndTriggerPersonalEvents(k.Today);
        Assert.NotNull(triggered);
        Assert.Equal("sworn_brother", triggered.Kind);

        var personalEvents = society.GetPersonalEvents();
        Assert.NotEmpty(personalEvents);

        var rowCount = k.Db.ScalarLong("SELECT COUNT(*) FROM so_personal_event WHERE kind = 'sworn_brother'");
        Assert.True(rowCount > 0);
    }

    [Fact]
    public void Unified_events_chronicle_aggregates_all_three_tiers()
    {
        using var k = _host.Boot();
        var society = Society(k);

        // 1. Trigger dynamic event
        k.Db.Execute("UPDATE po_city_governance SET unrest_level = 80 WHERE citadel_id = 2");
        society.EvaluateAndTriggerDynamicEvents(k.Today);

        // 2. Trigger personal event
        var living = society.AllActivities().Where(a => !a.IsDeceased).Take(2).ToList();
        var npcA = living[0].NpcId;
        var npcB = living[1].NpcId;
        k.Db.Execute("UPDATE so_npc_activity SET current_citadel_id = 2 WHERE npc_id = @b", DbArgs.One("@b", npcB));
        k.Db.Execute("UPDATE so_npc_activity SET current_citadel_id = 2 WHERE npc_id = @a", DbArgs.One("@a", npcA));
        k.Db.Execute(@"
            INSERT INTO so_npc_bond (npc_a, npc_b, affinity, trust)
            VALUES (@a, @b, 85, 70)
            ON CONFLICT(npc_a, npc_b) DO UPDATE SET affinity = 85;
        ", new DbArgs().Add("@a", Math.Min(npcA, npcB)).Add("@b", Math.Max(npcA, npcB)));
        society.EvaluateAndTriggerPersonalEvents(k.Today);

        // 3. Advance to trigger first historical event (184-08-15)
        var targetDate = new GameDate(184, 8, 20);
        k.AdvanceDays((int)(targetDate.DayIndex - k.Today.DayIndex));

        var unified = society.GetUnifiedEvents();
        Assert.NotEmpty(unified);

        // All 3 tiers should be present
        Assert.Contains(unified, u => u.Tier == SandboxEventTier.Historical);
        Assert.Contains(unified, u => u.Tier == SandboxEventTier.Dynamic);
        Assert.Contains(unified, u => u.Tier == SandboxEventTier.Personal);
    }
}
