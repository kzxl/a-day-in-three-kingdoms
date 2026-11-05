using SimT.Contracts.Playing;
using SimT.Core.Kernel;

namespace SimT.Modules.Dlc.Tests;

public sealed class CityStrollTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void Strolling_consumes_fatigue_and_relieves_stress()
    {
        using var kernel = _host.Boot();
        var stroll = kernel.Capabilities.Get<ICityStroll>(CapabilityIds.CityStroll);
        var attrs = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);

        var initialFatigue = attrs.Vitals.Fatigue;

        var result = stroll.Stroll(player.CurrentCityId);

        Assert.True(result.Ok);
        Assert.NotEmpty(result.Title);
        Assert.NotEmpty(result.Narrative);
        Assert.NotEmpty(result.RewardSummary);
        Assert.True(attrs.Vitals.Fatigue >= initialFatigue);
    }

    [Fact]
    public void Exhausted_player_cannot_stroll()
    {
        using var kernel = _host.Boot();
        var stroll = kernel.Capabilities.Get<ICityStroll>(CapabilityIds.CityStroll);
        var attrs = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);

        attrs.ApplyFatigue(100);

        var result = stroll.Stroll(player.CurrentCityId);

        Assert.False(result.Ok);
        Assert.Contains("kiệt sức", result.Narrative);
    }

    [Fact]
    public void Stroll_command_can_be_executed_via_world_map_screen()
    {
        using var kernel = _host.Boot();
        Assert.True(kernel.TryGetCommandHandler("world.map", out var handler));
        var msg = handler!.Execute("stroll");

        Assert.NotNull(msg);
        Assert.Contains("Du Ngoạn", msg);
    }
}
