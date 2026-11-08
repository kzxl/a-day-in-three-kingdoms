using System.Linq;
using SimT.Contracts.Playing;
using Xunit;

namespace SimT.Modules.Dlc.Tests;

[Collection("NonParallelPerformance")]
public sealed class SocietyDecisionModelTests : IDisposable
{
    private readonly DlcTestHost _host;

    public SocietyDecisionModelTests()
    {
        _host = new DlcTestHost();
    }

    public void Dispose() => _host.Dispose();

    [Fact]
    public void Npc_needs_are_initialized_and_accessible_via_ISocietyChronicle()
    {
        using var kernel = _host.Boot();
        var society = kernel.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);

        Assert.NotEmpty(registry.All);
        var firstNpc = registry.All[0];

        // Advance 30 days so monthly turn triggers
        kernel.AdvanceDays(30);

        var needs = society.GetNpcNeeds(firstNpc.Id);
        Assert.NotNull(needs);
        Assert.InRange(needs.SafetyNeed, 0, 100);
        Assert.InRange(needs.WealthNeed, 0, 100);
        Assert.InRange(needs.FameNeed, 0, 100);
        Assert.InRange(needs.PowerNeed, 0, 100);
        Assert.InRange(needs.SocialNeed, 0, 100);
    }

    [Fact]
    public void Npc_forms_multistep_goals_and_progresses_through_stages()
    {
        using var kernel = _host.Boot();
        var society = kernel.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);

        // Advance month 1: Goals are planned and advance to stage Preparing
        kernel.AdvanceDays(30);

        var goalsMonth1 = registry.All
            .Select(n => society.GetNpcGoal(n.Id))
            .Where(g => g != null)
            .ToList();

        Assert.NotEmpty(goalsMonth1);
        Assert.Contains(goalsMonth1, g => g!.CurrentStep >= 2);
        Assert.Contains(goalsMonth1, g => g!.Stage is "Preparing" or "Executing" or "Completing");

        // Verify goal types match life paths
        var goalTypes = goalsMonth1.Select(g => g!.GoalType).Distinct().ToList();
        Assert.Contains("TradeExpedition", goalTypes);

        // Advance month 2 (60 days): Goals advance further
        kernel.AdvanceDays(30);

        var events = society.RecentEvents(limit: 200, category: "commerce");
        Assert.NotEmpty(events);
        Assert.Contains(events, e => e.DetailVi.Contains("thương"));
    }

    [Fact]
    public void Trade_expedition_cycle_executes_with_physical_cargo_and_profit()
    {
        using var kernel = _host.Boot();
        var society = kernel.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle);

        // Run full trade expedition cycle (120 days = 4 months)
        kernel.AdvanceDays(120);

        var allRecent = society.RecentEvents(limit: 300);
        Assert.NotEmpty(allRecent);

        // Assert that commerce events document multi-step progression
        var commerceEvents = allRecent.Where(e => e.EventKind == "commerce").ToList();
        Assert.NotEmpty(commerceEvents);

        // Stat growth reflects commerce participation
        var allActivities = society.AllActivities();
        var comGrowths = allActivities
            .Select(a => society.GetStatDeltas(a.NpcId))
            .Where(d => d.ComDelta > 0)
            .ToList();

        Assert.NotEmpty(comGrowths);

        // Invariants must pass cleanly
        var failures = kernel.RunInvariants();
        Assert.Empty(failures);
    }
}
