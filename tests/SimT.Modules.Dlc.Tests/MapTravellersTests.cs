using System.Text.Json;
using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;
using SimT.Core.Kernel;
using SimT.Core.Modules;

namespace SimT.Modules.Dlc.Tests;

public sealed class MapTravellersTests : IDisposable
{
    private readonly DlcTestHost _host = new();
    public void Dispose() => _host.Dispose();

    [Fact]
    public void Payload_contains_real_rivals_and_stable_progress_with_empty_compatible_defaults()
    {
        using var k = _host.Boot();
        var society = k.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle);
        var first = MapScene.Build(k.Capabilities, k.Today, 2000)!;
        var second = MapScene.Build(k.Capabilities, k.Today, 2000)!;
        Assert.NotEmpty(first.Travellers.Where(t => t.Kind == "rival"));
        Assert.Equal(first.Travellers, second.Travellers);
        Assert.InRange(first.Travellers.Count, 1, 30);
        foreach (var t in first.Travellers.Where(t => t.Kind == "rival"))
        {
            var r = society.Rivals.GetAllRivals().Single(r => $"rival:{r.NpcId}" == t.Id);
            Assert.Equal(r.CurrentCitadelId, t.FromId);
            Assert.Equal(r.TargetCitadelId, t.ToId);
            Assert.Equal(r.NpcNameVi, t.LabelVi);
            Assert.InRange(t.Progress, 0, 1);
        }
        Assert.Empty((first with { Travellers = Array.Empty<MapTravellerView>() }).Travellers);
        if (Environment.GetEnvironmentVariable("SIMT_MAP_QA") is { Length: > 0 } directory)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "map-real.json"), JsonSerializer.Serialize(first));
        }
    }

    [Fact]
    public void Blocked_route_is_avoided_and_isolated_city_refuses_command_without_spending_time_or_cash()
    {
        using var k = _host.Boot();
        var map = k.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);
        var before = map.QuoteTravel(1, 7, TravelMode.Walk);
        Assert.True(before.ActualDays > 0);
        map.SetRouteHazard(1, 7, 30, true, k.Today.DayIndex + 10, "Quan lộ bị phong tỏa.");
        Assert.True(map.IsRouteBlocked(7, 1));
        var detour = map.QuoteTravel(1, 7, TravelMode.Walk);
        Assert.True(detour.ActualDays > 0);
        Assert.DoesNotContain(Enumerable.Range(0, detour.PathNodeIds.Count - 1), i =>
            (detour.PathNodeIds[i] == 1 && detour.PathNodeIds[i + 1] == 7) ||
            (detour.PathNodeIds[i] == 7 && detour.PathNodeIds[i + 1] == 1));
        foreach (var r in map.Routes.Where(r => r.FromCitadelId == 1 || r.ToCitadelId == 1))
            map.SetRouteHazard(r.FromCitadelId, r.ToCitadelId, 30, true, k.Today.DayIndex + 10, "Quan lộ bị phong tỏa.");
        var quote = map.QuoteTravel(1, 7, TravelMode.Walk);
        Assert.Equal(0, quote.ActualDays);
        Assert.Empty(quote.PathNodeIds);
        Assert.Equal("Quan lộ bị phong tỏa.", quote.BlockedReasonVi);
        var date = k.Today;
        var wallet = k.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        var cash = wallet.Liquid;
        k.Ui.TryGetCommandHandler("world.map", out var handler);
        Assert.Equal(quote.BlockedReasonVi, handler!.Execute("travel:7:horse"));
        Assert.Equal(date, k.Today);
        Assert.Equal(cash, wallet.Liquid);
        Assert.Equal(1, k.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState).CurrentCityId);
        var scene = MapScene.Build(k.Capabilities, k.Today, cash)!;
        Assert.Contains(scene.Routes, r => r.BlockedReasonVi == quote.BlockedReasonVi);
        if (Environment.GetEnvironmentVariable("SIMT_MAP_QA") is { Length: > 0 } directory)
            File.WriteAllText(Path.Combine(directory, "map-blocked.json"), JsonSerializer.Serialize(scene));
        k.AdvanceDays(11);
        Assert.False(map.IsRouteBlocked(1, 7));
        Assert.True(map.QuoteTravel(1, 7, TravelMode.Walk).ActualDays > 0);
    }

    [Fact]
    public void Historical_route_impacts_reach_world_map_and_armies_only_while_active()
    {
        using var k = _host.Boot();
        var society = k.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle);
        var e = society.GetHistoricalEvents().First(e => e.Impacts.Any(i => i.Kind == "route_danger"));
        k.AdvanceDays((int)(e.ScheduledDate.DayIndex - k.Today.DayIndex));
        var scene = MapScene.Build(k.Capabilities, k.Today, 2000)!;
        Assert.Contains(scene.Travellers, t => t.Kind == "army" && t.Id.Contains(e.Code));
        var impact = e.Impacts.First(i => i.Kind == "route_danger");
        Assert.InRange(k.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap)
            .GetRouteStatus(impact.CitadelId, impact.TargetCitadelId!.Value).Danger, 1, 3);
        var map = k.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);
        var road = map.Routes.Single(r => (r.FromCitadelId == impact.CitadelId && r.ToCitadelId == impact.TargetCitadelId) ||
            (r.ToCitadelId == impact.CitadelId && r.FromCitadelId == impact.TargetCitadelId));
        Assert.True(map.TravelDays(impact.CitadelId, impact.TargetCitadelId.Value) > road.Days);
        k.AdvanceDays(e.Impacts.Max(i => i.DurationDays) + 1);
        Assert.DoesNotContain(MapScene.Build(k.Capabilities, k.Today, 2000)!.Travellers,
            t => t.Kind == "army" && t.Id.Contains(e.Code));
    }

    [Fact]
    public void Player_caravan_payload_uses_departure_and_arrival_days()
    {
        using var k = _host.Boot();
        var guild = k.Capabilities.Get<ICaravanGuild>(CapabilityIds.CaravanGuild);
        k.Capabilities.Get<IBag>(CapabilityIds.PlayerBag).Add(1, 1, 10);
        var sent = guild.Send(1, 7);
        var run = Assert.Single(guild.Runs.Where(r => r.Status == "RUNNING"));
        var t = Assert.Single(MapScene.Build(k.Capabilities, k.Today, 2000)!.Travellers.Where(t => t.Kind == "caravan"));
        Assert.Equal(run.FromCityId, t.FromId);
        Assert.Equal(run.ToCityId, t.ToId);
        Assert.Equal((k.Today.DayIndex - run.DepartDayIndex) / (double)Math.Max(1, run.ArriveDayIndex - run.DepartDayIndex), t.Progress);
        Assert.Equal($"caravan:{run.Id}", t.Id);
    }

    [Fact]
    public void Disabled_society_boots_and_payload_has_no_travellers()
    {
        var directory = Path.GetDirectoryName(_host.DatabasePath)!;
        var product = Path.Combine(directory, "no-society.json");
        File.WriteAllText(product, JsonSerializer.Serialize(new {
            id = "no-society", name = "Test", seed = 1840502, startLocation = "lac_duong", startingCash = 2000,
            modules = ModuleLoader.Discover(AppContext.BaseDirectory).Select(m => m.Manifest.Id)
                .Where(id => id != "dlc.society").ToArray()
        }));
        using var k = SimKernel.Open(new KernelOptions(DatabasePath: _host.DatabasePath, ModulesDirectory: AppContext.BaseDirectory,
            ProductPath: product, ConstantsPath: SimT.Testing.TestHost.ConstantsPath, BackupDirectory: Path.Combine(directory, "backups")));
        Assert.Empty(MapScene.Build(k.Capabilities, k.Today, 2000)!.Travellers);
    }
}
