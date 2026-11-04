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

    [Fact]
    public void Host_banquet_charges_fee_and_boosts_affinity_for_local_attendees()
    {
        using var kernel = _host.Boot();
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);

        var balanceBefore = wallet.Balance;
        var msg = social.HostBanquet();

        Assert.Contains("Đại yến thành công", msg);
        Assert.Equal(balanceBefore - 150, wallet.Balance);

        // Check local attendees had relationships created/boosted
        var localNpcs = registry.FindByCitadel(1);
        Assert.NotEmpty(localNpcs);

        var rel = social.GetRelationship(localNpcs[0].Id);
        Assert.True(rel.Affinity >= 12);
        Assert.True(rel.Trust >= 55);

        var memories = social.GetMemories(localNpcs[0].Id);
        Assert.Contains(memories, m => m.Topic == "banquet");

        Assert.Empty(kernel.RunInvariants());
    }

    [Fact]
    public void Swear_brotherhood_locks_affinity_and_limits_to_two()
    {
        using var kernel = _host.Boot();
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        var attrs = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);

        var locals = registry.FindByCitadel(1).Take(3).ToList();
        Assert.True(locals.Count >= 3);

        // Boost affinity and trust with locals[0]
        for (var i = 0; i < 15; i++)
        {
            social.ChatWithNpc(locals[0].Id);
        }
        attrs.Rest(3);
        for (var i = 0; i < 15; i++)
        {
            social.ChatWithNpc(locals[0].Id);
        }

        var balanceBefore = wallet.Balance;
        var oathMsg1 = social.SwearBrotherhood(locals[0].Id);
        Assert.Contains("kết nghĩa huynh đệ kim lan", oathMsg1);
        Assert.Equal(balanceBefore - 100, wallet.Balance);

        var sworn = social.GetSwornBrothers();
        Assert.Single(sworn);
        Assert.Equal(locals[0].Id, sworn[0].NpcId);
        Assert.Equal("Nghĩa Huynh", sworn[0].BondTitle);

        var rel1 = social.GetRelationship(locals[0].Id);
        Assert.Equal(100, rel1.Affinity);
        Assert.Equal(100, rel1.Trust);
        Assert.Equal("Kết Nghĩa Huynh Đệ", rel1.Impression);

        // Boost and swear 2nd brother
        attrs.Rest(3);
        for (var i = 0; i < 15; i++)
        {
            social.ChatWithNpc(locals[1].Id);
        }
        attrs.Rest(3);
        for (var i = 0; i < 15; i++)
        {
            social.ChatWithNpc(locals[1].Id);
        }
        var oathMsg2 = social.SwearBrotherhood(locals[1].Id);
        Assert.Contains("kết nghĩa huynh đệ kim lan", oathMsg2);

        sworn = social.GetSwornBrothers();
        Assert.Equal(2, sworn.Count);
        Assert.Equal("Nghĩa Đệ", sworn[1].BondTitle);

        // Attempt 3rd brother: refused!
        attrs.Rest(3);
        for (var i = 0; i < 15; i++)
        {
            social.ChatWithNpc(locals[2].Id);
        }
        attrs.Rest(3);
        for (var i = 0; i < 15; i++)
        {
            social.ChatWithNpc(locals[2].Id);
        }
        var oathMsg3 = social.SwearBrotherhood(locals[2].Id);
        Assert.Contains("Đào Viên Tam Kết Nghĩa", oathMsg3);
        Assert.Equal(2, social.GetSwornBrothers().Count);

        Assert.Empty(kernel.RunInvariants());
    }
}
