using SimT.Contracts.Playing;
using SimT.Core.Kernel;

namespace SimT.Modules.Dlc.Tests;

public sealed class MilitaryTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void Strongholds_seeded_for_citadels()
    {
        using var kernel = _host.Boot();
        var military = kernel.Capabilities.Get<IMilitaryCommand>(CapabilityIds.MilitaryCommand);

        var all = military.AllStrongholds();
        Assert.Equal(17, all.Count);

        var local = military.StrongholdsAt(1);
        Assert.Equal(2, local.Count);
        Assert.Contains(local, s => s.NameVi.Contains("Mang Sơn"));
        Assert.Contains(local, s => s.NameVi.Contains("Lục Lâm"));
    }

    [Fact]
    public void Arena_duel_resolves_and_accrues_fatigue_and_exp()
    {
        using var kernel = _host.Boot();
        var military = kernel.Capabilities.Get<IMilitaryCommand>(CapabilityIds.MilitaryCommand);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var attributes = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);

        var opponent = registry.All.First(n => n.CitadelId == 1);
        var initialFatigue = attributes.Vitals.Fatigue;

        var result = military.ChallengeArena(opponent.Id);
        Assert.NotNull(result);
        Assert.True(result.Rounds >= 1);
        Assert.Contains(opponent.NameVi, result.OpponentName);
        Assert.NotEmpty(result.CombatSummaryVi);

        // Fatigue accrued from martial exertion
        Assert.True(attributes.Vitals.Fatigue > initialFatigue);

        var failures = kernel.RunInvariants();
        Assert.Empty(failures);
    }

    [Fact]
    public void Assaulting_stronghold_resolves_combat_and_awards_loot()
    {
        using var kernel = _host.Boot();
        var military = kernel.Capabilities.Get<IMilitaryCommand>(CapabilityIds.MilitaryCommand);
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);

        // Stronghold 2 has low danger level (2) -> suitable for early combat
        var localStrongholds = military.StrongholdsAt(1);
        var target = localStrongholds.First(s => s.DangerLevel == 2);

        var balanceBefore = wallet.Balance;
        var summary = military.AssaultStronghold(target.Id);

        Assert.NotEmpty(summary);
        if (summary.Contains("Đại Thắng"))
        {
            Assert.True(wallet.Balance > balanceBefore);
        }

        var failures = kernel.RunInvariants();
        Assert.Empty(failures);
    }

    [Fact]
    public void Fulfilling_supply_requisition_deducts_bag_and_pays_commissary()
    {
        using var kernel = _host.Boot();
        var military = kernel.Capabilities.Get<IMilitaryCommand>(CapabilityIds.MilitaryCommand);
        var bag = kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        var items = kernel.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);

        var reqs = military.ActiveRequisitions(1);
        Assert.NotEmpty(reqs);

        var req = reqs.First();
        var targetItem = items.FindByCode(req.TargetItemCode);
        Assert.NotNull(targetItem);

        // Stock required supplies into city bag
        bag.Add(1, targetItem.Id, req.QuantityNeeded + 2);
        var bagBefore = bag.Quantity(1, targetItem.Id);
        var walletBefore = wallet.Balance;

        var result = military.FulfillRequisition(req.Id);
        Assert.Contains("Đã giao nộp", result);

        // Bag deducted exactly needed quantity
        Assert.Equal(bagBefore - req.QuantityNeeded, bag.Quantity(1, targetItem.Id));

        // Wallet credited
        var expectedReward = req.QuantityNeeded * req.RewardPerUnit;
        Assert.Equal(walletBefore + expectedReward, wallet.Balance);

        // Requisition no longer listed in active
        var activeAfter = military.ActiveRequisitions(1);
        Assert.DoesNotContain(activeAfter, r => r.Id == req.Id);

        var failures = kernel.RunInvariants();
        Assert.Empty(failures);
    }
    [Fact]
    public void Exhaustion_gate_blocks_arena_and_assault_until_rested()
    {
        using var kernel = _host.Boot();
        var military = kernel.Capabilities.Get<IMilitaryCommand>(CapabilityIds.MilitaryCommand);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var attributes = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);

        var opponent = registry.All.First(n => n.CitadelId == 1);
        var dateBefore = kernel.Today;

        // Challenge arena advances 1 day and consumes fatigue
        var firstDuel = military.ChallengeArena(opponent.Id);
        Assert.NotNull(firstDuel);
        Assert.Equal(dateBefore.AddDays(1), kernel.Today);

        // Exhaust player to limit (95+ fatigue)
        attributes.ApplyFatigue(95);
        Assert.True(attributes.Vitals.Fatigue >= 95);

        // Arena is blocked by exhaustion gate
        var blockedDuel = military.ChallengeArena(opponent.Id);
        Assert.False(blockedDuel.Won);
        Assert.Contains("Thể lực đã cạn kiệt", blockedDuel.CombatSummaryVi);

        // Stronghold assault is blocked
        var localStrongholds = military.StrongholdsAt(1);
        var blockedAssault = military.AssaultStronghold(localStrongholds[0].Id);
        Assert.Contains("Thể lực đã cạn kiệt", blockedAssault);

        // Resting restores vitals
        attributes.Rest(2);
        kernel.AdvanceDays(2);
        Assert.True(attributes.Vitals.Fatigue < 95);

        // Arena duel can be performed again
        var restedDuel = military.ChallengeArena(opponent.Id);
        Assert.True(restedDuel.Rounds >= 1);
    }
}
