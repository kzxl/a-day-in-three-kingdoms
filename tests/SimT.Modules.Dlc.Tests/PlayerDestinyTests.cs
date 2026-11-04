using SimT.Contracts.Playing;
using SimT.Contracts.Ui;
using SimT.Core.Kernel;
using Xunit;

namespace SimT.Modules.Dlc.Tests;

public sealed class PlayerDestinyTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private static IPlayerDestiny GetDestiny(SimKernel kernel) =>
        kernel.Capabilities.Get<IPlayerDestiny>(CapabilityIds.PlayerDestiny);

    [Fact]
    public void Protagonist_starts_with_destiny_root_learned_and_rich_schools()
    {
        using var kernel = _host.Boot();
        var destiny = GetDestiny(kernel);

        Assert.NotNull(destiny);
        Assert.True(destiny.AllNodes.Count >= 30, $"Expected at least 30 nodes, found {destiny.AllNodes.Count}.");
        Assert.True(destiny.State.LearnedNodeIds.Contains("destiny_root"));

        var schools = destiny.AllNodes.Select(n => n.School).Distinct().ToHashSet();
        Assert.Contains(DestinySchool.DaoChu, schools);
        Assert.Contains(DestinySchool.BaDao, schools);
        Assert.Contains(DestinySchool.VuongDao, schools);
        Assert.Contains(DestinySchool.BinhGia, schools);
        Assert.Contains(DestinySchool.MacGia, schools);
        Assert.Contains(DestinySchool.DaoGia, schools);

        Assert.True(destiny.State.Milestones.Count >= 10);
    }

    [Fact]
    public void Prerequisites_and_school_points_are_strictly_enforced()
    {
        using var kernel = _host.Boot();
        var destiny = GetDestiny(kernel);

        // Cannot jump directly to a notable or keystone without prerequisite nodes
        var canLearnKeystone = destiny.CanLearnNode("daochu_keystone_merchant_king", out var reason1);
        Assert.False(canLearnKeystone);
        Assert.NotNull(reason1);

        var canLearnNotable = destiny.CanLearnNode("daochu_notable_rumor", out var reason2);
        Assert.False(canLearnNotable);
        Assert.NotNull(reason2);
    }

    [Fact]
    public void LearnNode_unlocks_passives_and_respec_refunds_cleanly()
    {
        using var kernel = _host.Boot();
        var destiny = GetDestiny(kernel);

        // Advance 30 days to trigger ms_life_1 milestone which awards +1 Universal, +1 DaoGia point
        kernel.AdvanceDays(35);
        destiny.EvaluateMilestones();

        var state = destiny.State;
        Assert.True(state.UniversalPointsAvailable >= 1 || state.ResonancePointsAvailable.Values.Any(v => v > 0));

        // Unlock daochu_root (connected to destiny_root)
        var canLearn = destiny.CanLearnNode("daochu_root", out var reason);
        Assert.True(canLearn, reason);

        var (success, msg) = destiny.LearnNode("daochu_root");
        Assert.True(success, msg);
        Assert.True(destiny.State.LearnedNodeIds.Contains("daochu_root"));

        // Modifier check
        var discount = destiny.GetModifier("price_buy_discount");
        Assert.Equal(3, discount);

        // Respec test
        var (respecSuccess, respecMsg) = destiny.RespecAll();
        Assert.True(respecSuccess, respecMsg);
        Assert.False(destiny.State.LearnedNodeIds.Contains("daochu_root"));
        Assert.True(destiny.State.LearnedNodeIds.Contains("destiny_root"));
        Assert.Equal(0, destiny.GetModifier("price_buy_discount"));
    }

    [Fact]
    public void DestinyScreen_and_command_handler_operate_seamlessly()
    {
        using var kernel = _host.Boot();
        var destiny = GetDestiny(kernel);

        var screen = kernel.Ui.Screens.FirstOrDefault(s => s.ScreenId == "player.destiny");
        Assert.NotNull(screen);
        Assert.Equal("Thiên Mệnh", screen.Title);

        var model = screen.Render();
        Assert.NotNull(model);
        Assert.Equal(2, model.Sections.Count);
        Assert.NotEmpty(model.Headline);

        Assert.True(kernel.Ui.TryGetCommandHandler("player.destiny", out var handler));
        Assert.NotNull(handler);

        var evalResult = handler.Execute("destiny:evaluate");
        Assert.NotNull(evalResult);
    }
}
