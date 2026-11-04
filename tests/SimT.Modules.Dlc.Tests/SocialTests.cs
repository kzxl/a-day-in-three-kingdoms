using SimT.Contracts;
using SimT.Contracts.Playing;
using SimT.Core.Kernel;

namespace SimT.Modules.Dlc.Tests;

public sealed class SocialTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void Chatting_with_npc_increases_affinity_and_records_memory()
    {
        using var kernel = _host.Boot();
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var attributes = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);

        var npc = registry.All.First(n => n.CitadelId == 1);
        var initialFatigue = attributes.Vitals.Fatigue;

        var message = social.ChatWithNpc(npc.Id);
        Assert.Contains("Hảo cảm", message);

        var rel = social.GetRelationship(npc.Id);
        Assert.True(rel.Affinity > 0);
        Assert.True(rel.Trust >= 50);

        var memories = social.GetMemories(npc.Id);
        Assert.NotEmpty(memories);
        Assert.Equal("chat", memories[0].Topic);

        // Player engaged in conversation -> fatigue accrued
        Assert.True(attributes.Vitals.Fatigue > initialFatigue);
    }

    [Fact]
    public void Gifting_npc_consumes_item_from_bag_and_boosts_affinity()
    {
        using var kernel = _host.Boot();
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var bag = kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        var items = kernel.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);

        var npc = registry.All.First(n => n.CitadelId == 1);
        var teaItem = items.FindByCode("tra") ?? items.All.First();

        // Stock tea into player's city bag
        bag.Add(1, teaItem.Id, 2);
        Assert.Equal(2, bag.Quantity(1, teaItem.Id));

        var result = social.GiftNpc(npc.Id, teaItem.Id);
        Assert.Contains("Đã biếu tặng", result);

        // Bag deducted
        Assert.Equal(1, bag.Quantity(1, teaItem.Id));

        var rel = social.GetRelationship(npc.Id);
        Assert.True(rel.Affinity >= 6);
        Assert.Equal(1, rel.GiftCount);

        var memories = social.GetMemories(npc.Id);
        Assert.Contains(memories, m => m.Topic == "gift");
    }

    [Fact]
    public void Recruiting_retainer_requires_affinity_and_pays_signing_fee()
    {
        using var kernel = _host.Boot();
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);

        var npc = registry.All.First(n => n.CitadelId == 1);

        // Attempting recruitment without enough affinity throws
        var ex = Assert.Throws<SimException>(() => social.RecruitRetainer(npc.Id, "auto", 50));
        Assert.Equal("social.affinity_insufficient", ex.Code);

        // Chat 10 times to build affinity >= 35
        for (var i = 0; i < 10; i++)
        {
            social.ChatWithNpc(npc.Id);
        }

        var rel = social.GetRelationship(npc.Id);
        Assert.True(rel.Affinity >= 35);

        var balanceBefore = wallet.Balance;
        var recruitMsg = social.RecruitRetainer(npc.Id, "auto", 60);
        Assert.Contains("Đã chiêu mộ", recruitMsg);

        // Signing fee deducted
        Assert.Equal(balanceBefore - 250, wallet.Balance);

        // Appears in retainers list
        var retainers = social.GetRetainers();
        Assert.Contains(retainers, r => r.NpcId == npc.Id);
        Assert.Equal(60, retainers.First(r => r.NpcId == npc.Id).MonthlyStipend);

        // Invariants pass cleanly
        var failures = kernel.RunInvariants();
        Assert.Empty(failures);
    }

    [Fact]
    public void Retainer_stipend_charged_on_month_closed()
    {
        using var kernel = _host.Boot();
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);

        var npc = registry.All.First(n => n.CitadelId == 1);

        for (var i = 0; i < 10; i++)
        {
            social.ChatWithNpc(npc.Id);
        }

        social.RecruitRetainer(npc.Id, "auto", 50);

        var balanceBeforeMonth = wallet.Balance;

        // Advance 30 days to close month
        kernel.AdvanceDays(30);

        // Stipend was charged
        Assert.True(wallet.Balance < balanceBeforeMonth);

        var failures = kernel.RunInvariants();
        Assert.Empty(failures);
    }
}
