using SimT.Contracts.Data;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Core.Kernel;
using Xunit;

namespace SimT.Modules.Dlc.Tests;

public sealed class MemoryAgencyAndSocialRippleTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void MemoryDisposition_grudged_npc_marks_up_prices_and_sanctions_military_goods()
    {
        using var kernel = _host.Boot();
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var npcMarket = kernel.Capabilities.Get<INpcMarket>(CapabilityIds.NpcMarket);
        var bag = kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        var items = kernel.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);

        var npc = registry.All.First(n => n.CitadelId == 1);
        var ironItem = items.FindByCode("sat") ?? items.All.First(i => i.Category == "material");

        // Record a betrayal memory and hostile relationship
        kernel.Db.Execute(
            "INSERT OR REPLACE INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date) " +
            "VALUES (@id, -50, 10, 'Thù địch', 0, '184-05-01')",
            DbArgs.One("@id", npc.Id));

        kernel.Db.Execute(
            "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) " +
            "VALUES (@id, '184-05-01', 'betrayal', 'negative', -40, 'Bội ước thương trường khiến đối phương căm hận')",
            DbArgs.One("@id", npc.Id));

        var disp = social.GetDisposition(npc.Id);
        Assert.True(disp.IsGrudged);
        Assert.False(disp.IsFavored);
        Assert.True(disp.PriceFactorPermille >= 1200);

        // Put iron in player bag and register a stall owned by this NPC
        bag.Add(1, ironItem.Id, 5);
        kernel.Db.Execute(
            "INSERT INTO mk_stall (id, citadel_id, sector, name_vi, owner_kind, npc_id, reputation) " +
            "VALUES (991, 1, 'material', 'Thiết Tiệm Cừu Nhân', 'npc', @npc, 50)",
            DbArgs.One("@npc", npc.Id));

        // Attempting to sell material/military goods to grudged NPC is sanctioned/rejected
        var result = npcMarket.SellToNpcStall(1, 991, ironItem.Id, 2);
        Assert.False(result.Ok);
        Assert.Contains("cự tuyệt", result.Message);
    }

    [Fact]
    public void MemoryDisposition_favored_npc_offers_discount()
    {
        using var kernel = _host.Boot();
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);

        var npc = registry.All.First(n => n.CitadelId == 1);

        // Record a sworn/favored bond relationship
        kernel.Db.Execute(
            "INSERT OR REPLACE INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date) " +
            "VALUES (@id, 80, 90, 'Tri kỷ', 5, '184-05-01')",
            DbArgs.One("@id", npc.Id));

        kernel.Db.Execute(
            "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) " +
            "VALUES (@id, '184-05-01', 'wish', 'positive', 25, 'Hoàn thành tâm nguyện cứu giúp đại nghiệp')",
            DbArgs.One("@id", npc.Id));

        var disp = social.GetDisposition(npc.Id);
        Assert.False(disp.IsGrudged);
        Assert.True(disp.IsFavored);
        Assert.True(disp.PriceFactorPermille < 1000);
        Assert.Contains("Tri kỷ", disp.SummaryVi);
    }

    [Fact]
    public void BondRipple_gifting_influential_npc_spreads_gratitude_to_allies_and_kin()
    {
        using var kernel = _host.Boot();
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var bag = kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        var items = kernel.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);

        var liu = registry.FindByCode("npc_0001")!;
        var guan = registry.FindByCode("npc_0003")!;
        var zhang = registry.FindByCode("npc_0002")!;

        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var liuCityId = liu.CurrentCitadelId(kernel.Capabilities);
        player.MoveTo(liuCityId, kernel.Today);

        var luxuryItem = items.FindByCode("tram_huong") ?? items.FindByCode("tra") ?? items.All.First();
        bag.Add(liuCityId, luxuryItem.Id, 2);

        var initialGuanAffinity = social.GetRelationship(guan.Id).Affinity;
        var initialZhangAffinity = social.GetRelationship(zhang.Id).Affinity;

        // Player gifts Lưu Bị
        var giftResult = social.GiftNpc(liu.Id, luxuryItem.Id);
        Assert.Contains("Đã biếu tặng", giftResult);

        // Check that Quan Vũ and Trương Phi received ripple positive affinity and gratitude memory
        var finalGuan = social.GetRelationship(guan.Id);
        var finalZhang = social.GetRelationship(zhang.Id);

        Assert.True(finalGuan.Affinity > initialGuanAffinity, "Quan Vũ should gain affinity when Lưu Bị is honored.");
        Assert.True(finalZhang.Affinity > initialZhangAffinity, "Trương Phi should gain affinity when Lưu Bị is honored.");

        var guanMemories = social.GetMemories(guan.Id);
        Assert.Contains(guanMemories, m => m.Topic == "bond_gratitude" && m.Sentiment == "positive");

        var zhangMemories = social.GetMemories(zhang.Id);
        Assert.Contains(zhangMemories, m => m.Topic == "bond_gratitude" && m.Sentiment == "positive");
    }

    [Fact]
    public void CaravanRoute_hostile_notables_impose_customs_toll_surcharge()
    {
        using var kernel = _host.Boot();
        var caravan = kernel.Capabilities.Get<ICaravanGuild>(CapabilityIds.CaravanGuild);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var bag = kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);

        var destCity = 2L;
        var destNpc = registry.All.First(n => n.CitadelId == destCity);

        // Load 10 grain in city 1 bag
        bag.Add(1, 1, 10);
        wallet.Pay(1000, "test", "Fund wallet for freight");

        // Record a hostile grudge at destination city
        kernel.Db.Execute(
            "INSERT OR REPLACE INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date) " +
            "VALUES (@id, -60, 5, 'Kẻ địch', 0, '184-05-01')",
            DbArgs.One("@id", destNpc.Id));
        kernel.Db.Execute(
            "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) " +
            "VALUES (@id, '184-05-01', 'feud', 'negative', -30, 'Thù sâu như biển')",
            DbArgs.One("@id", destNpc.Id));

        var sendResult = caravan.Send(1, destCity);
        Assert.True(sendResult.Ok);
        Assert.Contains("quan ải thù địch phụ thu cước", sendResult.Message);
    }
}
