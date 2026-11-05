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
        Assert.True(destiny.AllNodes.Count >= 43, $"Expected at least 43 nodes, found {destiny.AllNodes.Count}.");
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

    [Fact]
    public void Deep_mastery_nodes_enforce_keystone_and_school_thresholds_and_unlock_properly()
    {
        using var kernel = _host.Boot();
        var destiny = GetDestiny(kernel);

        // Cannot learn deep mastery directly
        var canLearn = destiny.CanLearnNode("daochu_mastery_monopoly", out var reason);
        Assert.False(canLearn);
        Assert.NotNull(reason);
        Assert.False(destiny.HasMastery("daochu_mastery_monopoly"));

        // Grant wealth and advance time to complete multiple milestones
        kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet)!.Pay(150000, "test", "test");
        kernel.AdvanceDays(65);
        destiny.EvaluateMilestones();

        Assert.True(destiny.State.UniversalPointsAvailable >= 5);

        // Unlock path: daochu_root -> daochu_minor_bargain -> daochu_notable_rumor -> daochu_keystone_merchant_king
        Assert.True(destiny.LearnNode("daochu_root").Success);
        Assert.True(destiny.LearnNode("daochu_minor_bargain").Success);
        Assert.True(destiny.LearnNode("daochu_minor_margin").Success);
        Assert.True(destiny.LearnNode("daochu_notable_rumor").Success);
        Assert.True(destiny.LearnNode("daochu_keystone_merchant_king").Success);
        Assert.True(destiny.HasKeystone("daochu_keystone_merchant_king"));

        // Now deep mastery is eligible!
        var canLearnMastery = destiny.CanLearnNode("daochu_mastery_monopoly", out var masteryReason);
        Assert.True(canLearnMastery, masteryReason);

        var (success, msg) = destiny.LearnNode("daochu_mastery_monopoly");
        Assert.True(success, msg);
        Assert.True(destiny.HasMastery("daochu_mastery_monopoly"));

        // Modifier check: daochu_root (3) + daochu_minor_bargain (3) + daochu_mastery_monopoly (5) = 11%
        var discount = destiny.GetModifier("price_buy_discount");
        Assert.Equal(11, discount);

        // Respec test resets masteries cleanly
        var (respecOk, _) = destiny.RespecAll();
        Assert.True(respecOk);
        Assert.False(destiny.HasMastery("daochu_mastery_monopoly"));
        Assert.False(destiny.HasKeystone("daochu_keystone_merchant_king"));
        Assert.Equal(0, destiny.GetModifier("price_buy_discount"));
    }
}
