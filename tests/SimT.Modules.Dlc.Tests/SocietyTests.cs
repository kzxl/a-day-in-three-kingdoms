using SimT.Contracts.Playing;
using SimT.Core.Kernel;

namespace SimT.Modules.Dlc.Tests;

public sealed class SocietyTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void Society_initializes_all_127_npcs_with_activities_and_zero_deltas()
    {
        using var kernel = _host.Boot();
        var society = kernel.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);

        var activities = society.AllActivities();
        Assert.Equal(registry.All.Count, activities.Count);
        Assert.Equal(127, activities.Count);

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
}
