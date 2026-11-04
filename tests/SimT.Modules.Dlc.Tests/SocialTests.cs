using SimT.Contracts;
using SimT.Contracts.Data;
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

    [Fact]
    public void HasMet_returns_false_for_stranger_and_true_after_chat()
    {
        using var kernel = _host.Boot();
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);

        var npc = registry.All.First(n => n.CitadelId == 1);
        Assert.False(social.HasMet(npc.Id));

        social.ChatWithNpc(npc.Id);
        Assert.True(social.HasMet(npc.Id));
    }

    [Fact]
    public void Chatting_or_gifting_distant_npc_is_rejected_due_to_non_godview()
    {
        using var kernel = _host.Boot();
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var bag = kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        var items = kernel.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);

        // NPC at citadel 4 (different city than player starting at citadel 1)
        var distantNpc = registry.All.First(n => n.CitadelId != 1);

        var chatMsg = social.ChatWithNpc(distantNpc.Id);
        Assert.Contains("không có mặt tại cùng thành trì", chatMsg);

        var teaItem = items.All.First();
        bag.Add(1, teaItem.Id, 2);
        var ex = Assert.Throws<SimException>(() => social.GiftNpc(distantNpc.Id, teaItem.Id));
        Assert.Equal("social.not_in_same_city", ex.Code);
    }

    [Fact]
    public void Rank_S_officer_aloof_when_fame_is_low_and_debates_when_fame_rises()
    {
        using var kernel = _host.Boot();
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var attrs = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);

        var guanYu = registry.FindByCode("npc_0003")!;
        Assert.NotNull(guanYu);
        Assert.Equal(NpcRank.S, social.GetNpcRank(guanYu.Id));

        // Move player to Guan Yu's city (citadel 4 - Trác Quận)
        player.MoveTo(guanYu.CitadelId, kernel.Clock.Today);

        // Player starting fame is 100 (< 300 required for Rank S)
        Assert.True(attrs.Social.Fame < 300);

        var aloofMsg = social.ChatWithNpc(guanYu.Id);
        Assert.Contains("Uy danh < 300", aloofMsg);
        Assert.Equal(1, social.GetRelationship(guanYu.Id).Affinity);

        // Boost player fame to 350
        attrs.GainFame(250, "Chiến công hiển hách dẹp loạn khăn vàng");
        Assert.True(attrs.Social.Fame >= 300);

        attrs.Rest(1);
        var intimateMsg = social.ChatWithNpc(guanYu.Id);
        Assert.Contains("Đàm đạo cùng", intimateMsg);
        Assert.Contains("Phẩm S", intimateMsg);
        // Affinity grew with martial camaraderie
        Assert.True(social.GetRelationship(guanYu.Id).Affinity >= 3);
    }

    [Fact]
    public void Rank_S_officer_rejects_cheap_grain_commodity_gifts()
    {
        using var kernel = _host.Boot();
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var bag = kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        var items = kernel.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);

        var guanYu = registry.FindByCode("npc_0003")!;
        player.MoveTo(guanYu.CitadelId, kernel.Clock.Today);

        var riceItem = items.FindByCode("gao")!;
        var brocadeItem = items.FindByCode("gam")!;

        bag.Add(guanYu.CitadelId, riceItem.Id, 5);
        bag.Add(guanYu.CitadelId, brocadeItem.Id, 2);

        // Guan Yu rejects cheap rice (< 35 base price bulk grain)
        var ex = Assert.Throws<SimException>(() => social.GiftNpc(guanYu.Id, riceItem.Id));
        Assert.Equal("social.gift_rejected", ex.Code);
        Assert.Contains("lễ mọn", ex.Message);
        // Rice was NOT removed from bag
        Assert.Equal(5, bag.Quantity(guanYu.CitadelId, riceItem.Id));

        // High quality brocade (150 quan) is graciously accepted
        var giftResult = social.GiftNpc(guanYu.Id, brocadeItem.Id);
        Assert.Contains("Đã biếu tặng", giftResult);
        Assert.Contains("Phẩm S", giftResult);
        Assert.Equal(1, bag.Quantity(guanYu.CitadelId, brocadeItem.Id));

        // Affinity gain is tightly balanced (2 to 4) instead of runaway 30+
        var rel = social.GetRelationship(guanYu.Id);
        Assert.InRange(rel.Affinity, 2, 6);
    }

    [Fact]
    public void Recruiting_warlord_is_strictly_forbidden()
    {
        using var kernel = _host.Boot();
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);

        var liuBei = registry.FindByCode("npc_0001")!;
        player.MoveTo(liuBei.CitadelId, kernel.Clock.Today);

        var req = social.GetRecruitRequirement(liuBei.Id);
        Assert.True(req.IsWarlord);
        Assert.False(req.CanRecruit);

        var ex = Assert.Throws<SimException>(() => social.RecruitRetainer(liuBei.Id, "auto", 500));
        Assert.Equal("social.warlord_unrecruitable", ex.Code);
        Assert.Contains("chư hầu", ex.Message);
    }

    [Fact]
    public void Recruiting_rank_S_officer_enforces_tiered_fame_affinity_and_fee()
    {
        using var kernel = _host.Boot();
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        var attrs = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);

        var guanYu = registry.FindByCode("npc_0003")!;
        player.MoveTo(guanYu.CitadelId, kernel.Clock.Today);

        var req = social.GetRecruitRequirement(guanYu.Id);
        Assert.Equal(NpcRank.S, req.Rank);
        Assert.Equal(80, req.RequiredAffinity);
        Assert.Equal(1200, req.RequiredFame);
        Assert.Equal(1500, req.SigningFee);
        Assert.Equal(120, req.MinMonthlyStipend);

        // 1. Without affinity, throws affinity_insufficient
        var ex1 = Assert.Throws<SimException>(() => social.RecruitRetainer(guanYu.Id, "auto", 150));
        Assert.Equal("social.affinity_insufficient", ex1.Code);
    }

    [Fact]
    public void Advancing_month_generates_letters_from_high_affinity_friends_with_attached_gifts()
    {
        using var kernel = _host.Boot();
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var db = kernel.Db;

        var npc = registry.FindByCode("npc_0003")!; // Quan Vũ

        // Set affinity to 95 (sworn/high tier friend)
        db.Execute(
            "INSERT INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date) " +
            "VALUES (@npcId, 95, 90, 'Thâm giao tri kỷ', 5, '184-05-01')",
            DbArgs.One("@npcId", npc.Id));

        Assert.Equal(0, social.UnreadLetterCount());

        // Advance 30 days to close month
        kernel.AdvanceDays(30);

        var letters = social.GetLetters();
        Assert.NotEmpty(letters);
        Assert.True(social.UnreadLetterCount() > 0);

        var letter = letters.First(l => l.SenderNpcId == npc.Id);
        Assert.NotNull(letter);
        Assert.False(letter.IsRead);
        Assert.False(letter.IsClaimed);
        Assert.Contains("Quan Vũ", letter.SenderNameVi);
        Assert.False(string.IsNullOrWhiteSpace(letter.TitleVi));
        Assert.False(string.IsNullOrWhiteSpace(letter.ContentVi));
    }

    [Fact]
    public void Reading_letter_marks_read_and_claiming_gift_transfers_copper_and_items()
    {
        using var kernel = _host.Boot();
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        var bag = kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var items = kernel.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);
        var db = kernel.Db;

        var npc = registry.FindByCode("npc_0005")!; // Trương Thế Bình (merchant)
        var initialWallet = wallet.Liquid;
        var initialUnread = social.UnreadLetterCount();

        // High affinity with merchant
        db.Execute(
            "INSERT INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date) " +
            "VALUES (@npcId, 90, 85, 'Hảo hữu thân thiết', 4, '184-05-01')",
            DbArgs.One("@npcId", npc.Id));

        // Advance 30 days to generate monthly letter
        kernel.AdvanceDays(30);

        var letters = social.GetLetters();
        var letter = letters.FirstOrDefault(l => l.SenderNpcId == npc.Id);
        Assert.NotNull(letter);

        // Read letter
        var readPresentation = social.ReadLetter(letter.Id);
        Assert.Contains(letter.TitleVi, readPresentation);
        Assert.Contains(letter.SenderNameVi, readPresentation);

        // Now letter is marked read
        var readLetter = social.GetLetter(letter.Id)!;
        Assert.True(readLetter.IsRead);

        // Claim gift
        if (readLetter.GiftCopper > 0 || readLetter.GiftItemQuantity > 0)
        {
            var claimResult = social.ClaimLetterGift(letter.Id);
            Assert.Contains("Đã nhận lễ vật", claimResult);

            if (readLetter.GiftCopper > 0)
            {
                Assert.True(wallet.Liquid >= initialWallet + readLetter.GiftCopper);
            }

            if (readLetter.GiftItemId.HasValue)
            {
                Assert.True(bag.Quantity(player.CurrentCityId, readLetter.GiftItemId.Value) >= readLetter.GiftItemQuantity);
            }

            // Claimed letter status
            var claimedLetter = social.GetLetter(letter.Id)!;
            Assert.True(claimedLetter.IsClaimed);

            // Re-claiming returns already claimed notice
            var reClaimResult = social.ClaimLetterGift(letter.Id);
            Assert.Contains("trước đó", reClaimResult);
        }
    }

    [Fact]
    public void Bodyguard_retainer_and_sworn_brother_follow_protagonist_to_new_citadel()
    {
        using var kernel = _host.Boot();
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var db = kernel.Db;

        var npc = registry.FindByCode("npc_0002")!; // Trương Phi
        player.MoveTo(npc.CitadelId, kernel.Clock.Today);

        // Swear brotherhood with Trương Phi
        db.Execute(
            "INSERT INTO kz_sworn_brother (npc_id, oath_date, bond_title, assist_count) VALUES (@id, '184-05-01', 'Nghĩa Đệ', 0)",
            DbArgs.One("@id", npc.Id));
        db.Execute(
            "INSERT INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date) " +
            "VALUES (@id, 100, 100, 'Kết Nghĩa Huynh Đệ', 0, '184-05-01') " +
            "ON CONFLICT(npc_id) DO UPDATE SET affinity = 100, trust = 100",
            DbArgs.One("@id", npc.Id));

        // Move protagonist to Lạc Dương (city 1)
        player.MoveTo(1, kernel.Clock.Today);

        // Advance 30 days to simulate society monthly turn
        kernel.AdvanceDays(30);

        // Trương Phi accompanied protagonist to city 1!
        var currentZf = registry.Find(npc.Id)!;
        Assert.Equal(1, currentZf.CitadelId);

        var npcsInCity1 = registry.FindByCitadel(1);
        Assert.Contains(npcsInCity1, n => n.Id == npc.Id);
    }
}
