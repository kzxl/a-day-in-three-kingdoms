using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Core.Kernel;
using Xunit;

namespace SimT.Modules.Dlc.Tests;

public sealed class NpcTraitTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void Catalog_defines_all_16_traits_with_descriptions_and_dialogue_lines()
    {
        using var kernel = _host.Boot();
        var traitsCap = kernel.Capabilities.Get<INpcTraits>(CapabilityIds.NpcTraits);
        var allTraits = traitsCap.GetAllTraitDefs();

        Assert.Equal(16, allTraits.Count);
        foreach (var t in allTraits)
        {
            Assert.False(string.IsNullOrWhiteSpace(t.Code));
            Assert.False(string.IsNullOrWhiteSpace(t.NameVi));
            Assert.False(string.IsNullOrWhiteSpace(t.DescriptionVi));

            var lines = SimT.Modules.Dlc.Character.DialogueService.GetTraitLines(t.Code);
            Assert.True(lines.Count >= 2, $"Trait {t.Code} must have at least 2 dialogue lines.");
            Assert.All(lines, line => Assert.True(line.Length <= 90, $"Trait line > 90 chars: {line}"));
        }
    }

    [Fact]
    public void Npc_traits_are_deterministic_by_seed_and_have_no_mutually_exclusive_conflicts()
    {
        using var hostA = new DlcTestHost();
        using var hostB = new DlcTestHost();
        using var kernelA = hostA.Boot();
        using var kernelB = hostB.Boot();

        var socialA = kernelA.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var socialB = kernelB.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var registry = kernelA.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);

        Assert.NotEmpty(registry.All);

        foreach (var npc in registry.All)
        {
            var traitsA = socialA.GetTraits(npc.Id);
            var traitsB = socialB.GetTraits(npc.Id);

            Assert.InRange(traitsA.Count, 1, 3);
            Assert.Equal(
                traitsA.Select(t => t.Code).ToArray(),
                traitsB.Select(t => t.Code).ToArray());

            // Verify no mutually exclusive pairs on the same NPC
            for (var i = 0; i < traitsA.Count; i++)
            {
                for (var j = i + 1; j < traitsA.Count; j++)
                {
                    Assert.False(
                        SimT.Modules.Dlc.Character.NpcTraitCatalog.AreMutuallyExclusive(traitsA[i].Code, traitsA[j].Code),
                        $"NPC {npc.Code} ({npc.NameVi}) has conflicting traits {traitsA[i].Code} and {traitsA[j].Code}");
                }
            }
        }
    }

    [Fact]
    public void Famous_historical_characters_have_canonical_fixed_traits()
    {
        using var kernel = _host.Boot();
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);

        void AssertHasTraits(string npcCode, params string[] expectedTraitCodes)
        {
            var npc = registry.FindByCode(npcCode);
            Assert.NotNull(npc);
            var actualCodes = social.GetTraits(npc!.Id).Select(t => t.Code).ToList();
            foreach (var expected in expectedTraitCodes)
            {
                Assert.Contains(expected, actualCodes);
            }
        }

        // Prompt examples & key Three Kingdoms figures
        AssertHasTraits("npc_0003", NpcTraitCodes.TrungNghia, NpcTraitCodes.KieuNgao); // Quan Vũ
        AssertHasTraits("npc_0002", NpcTraitCodes.NongNay, NpcTraitCodes.MeRuou);      // Trương Phi
        AssertHasTraits("npc_0022", NpcTraitCodes.DaNghi, NpcTraitCodes.XaoQuyet);     // Tào Tháo
        AssertHasTraits("npc_0001", NpcTraitCodes.NhanHau, NpcTraitCodes.KhiemCung);   // Lưu Bị
        AssertHasTraits("npc_0025", NpcTraitCodes.DiemTinh, NpcTraitCodes.HieuHoc);    // Gia Cát Lượng
        AssertHasTraits("npc_0024", NpcTraitCodes.TrungNghia, NpcTraitCodes.DiemTinh); // Triệu Vân
        AssertHasTraits("npc_0049", NpcTraitCodes.HaoSang, NpcTraitCodes.TrongChuTin); // Lỗ Túc
        AssertHasTraits("npc_0057", NpcTraitCodes.MeVo, NpcTraitCodes.KieuNgao);       // Lữ Bố
        AssertHasTraits("npc_0023", NpcTraitCodes.ThamTai, NpcTraitCodes.NongNay);     // Đổng Trác
        AssertHasTraits("npc_0035", NpcTraitCodes.ThanhLiem, NpcTraitCodes.TrungNghia);// Tuân Úc
    }

    [Fact]
    public void Npc_market_prices_and_stock_shift_correctly_for_tham_tai_hao_sang_keo_kiet_and_trong_chu_tin()
    {
        using var kernel = _host.Boot();
        var npcMarket = kernel.Capabilities.Get<INpcMarket>(CapabilityIds.NpcMarket);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var items = kernel.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);

        var honestNpc = registry.FindByCode("thuong_nhan_a")!; // Trọng chữ tín
        var generousNpc = registry.FindByCode("npc_0230")!;    // Tô Song: Hào sảng + Mê rượu
        var greedyStingyNpc = registry.FindByCode("npc_0013")!;// Tả Phong: Tham tài + Keo kiệt

        var silk = items.FindByCode("lua")!;

        // 1. Selling to player (Offer Unit Price)
        var baselineSellToPlayer = npcMarket.QuoteNpcOfferUnitPrice(1, honestNpc.Id, silk.Id, discountPermille: 50);
        var greedySellToPlayer = npcMarket.QuoteNpcOfferUnitPrice(1, greedyStingyNpc.Id, silk.Id, discountPermille: 50);
        var generousSellToPlayer = npcMarket.QuoteNpcOfferUnitPrice(1, generousNpc.Id, silk.Id, discountPermille: 50);

        Assert.True(greedySellToPlayer > baselineSellToPlayer,
            $"Tham tài sell price ({greedySellToPlayer}) should be higher than baseline ({baselineSellToPlayer})");
        Assert.True(generousSellToPlayer < baselineSellToPlayer,
            $"Hào sảng sell price ({generousSellToPlayer}) should be lower than baseline ({baselineSellToPlayer})");

        // 2. Buying from player (Buy Bid Price)
        var baselineBuyFromPlayer = npcMarket.QuoteNpcBuyBidUnitPrice(1, honestNpc.Id, silk.Id, bidMarkupPermille: 100);
        var greedyBuyFromPlayer = npcMarket.QuoteNpcBuyBidUnitPrice(1, greedyStingyNpc.Id, silk.Id, bidMarkupPermille: 100);
        var generousBuyFromPlayer = npcMarket.QuoteNpcBuyBidUnitPrice(1, generousNpc.Id, silk.Id, bidMarkupPermille: 100);

        Assert.True(greedyBuyFromPlayer < baselineBuyFromPlayer,
            $"Tham tài buy bid ({greedyBuyFromPlayer}) should be lower than baseline ({baselineBuyFromPlayer})");
        Assert.True(generousBuyFromPlayer > baselineBuyFromPlayer,
            $"Hào sảng buy bid ({generousBuyFromPlayer}) should be higher than baseline ({baselineBuyFromPlayer})");

        // 3. Keo kiệt reduces stock
        var normalStock = npcMarket.AdjustNpcOfferStock(honestNpc.Id, 20);
        var stingyStock = npcMarket.AdjustNpcOfferStock(greedyStingyNpc.Id, 20);
        Assert.Equal(20, normalStock);
        Assert.True(stingyStock < normalStock, $"Keo kiệt stock ({stingyStock}) should be less than normal ({normalStock})");
    }

    [Fact]
    public void Gifting_wine_to_me_ruou_grants_bonus_affinity_and_thanh_liem_penalizes_expensive_bribes()
    {
        using var kernel = _host.Boot();
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var bag = kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        var items = kernel.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);

        var wine = items.FindByCode("ruou")!;
        var brocade = items.FindByCode("gam")!; // BasePrice 150 >= 120

        // Compare gifting wine at Luoyang (city 1) to npc_0230 (Tô Song: Mê rượu) vs thuong_nhan_a (not Mê rượu)
        var wineLover = registry.FindByCode("npc_0230")!;
        var nonWineNpc = registry.FindByCode("thuong_nhan_a")!;
        player.MoveTo(1, kernel.Clock.Today);

        bag.Add(1, wine.Id, 4);
        var msgWineLover = social.GiftNpc(wineLover.Id, wine.Id);
        var msgNormal = social.GiftNpc(nonWineNpc.Id, wine.Id);

        var wineLoverAff = social.GetRelationship(wineLover.Id).Affinity;
        var normalAff = social.GetRelationship(nonWineNpc.Id).Affinity;

        Assert.Contains("Mê rượu", msgWineLover);
        Assert.True(wineLoverAff > normalAff,
            $"Wine lover affinity gain ({wineLoverAff}) should exceed non-wine NPC gain ({normalAff})");

        // Compare gifting expensive brocade to npc_0224 (Hoa Hân: Thanh liêm) vs thuong_nhan_a (not Thanh liêm)
        var incorruptibleNpc = registry.FindByCode("npc_0224")!;
        bag.Add(1, brocade.Id, 2);
        var msgBribe = social.GiftNpc(incorruptibleNpc.Id, brocade.Id);
        Assert.Contains("Thanh liêm", msgBribe);
        var incorruptibleGain = social.GetRelationship(incorruptibleNpc.Id).Affinity;
        Assert.True(incorruptibleGain < normalAff + 4);
    }

    [Fact]
    public void Traits_are_masked_when_affinity_is_low_and_fully_revealed_when_affinity_is_high()
    {
        using var kernel = _host.Boot();
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var dialogue = kernel.Capabilities.Get<SimT.Contracts.Scenes.IDialogueModule>(CapabilityIds.Dialogue);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);

        var zhangFei = registry.FindByCode("npc_0002")!; // Trương Phi: Nóng nảy, Mê rượu, Mê võ (3 traits)
        var fullTraits = social.GetTraits(zhangFei.Id);
        Assert.Equal(3, fullTraits.Count);

        // 1. At 0 affinity (unmet / stranger): 1st trait visible, remaining 2 masked as "???"
        var lowVisible = social.GetVisibleTraits(zhangFei.Id);
        Assert.Equal(3, lowVisible.Count);
        Assert.False(lowVisible[0].Hidden);
        Assert.Equal(fullTraits[0].NameVi, lowVisible[0].NameVi);
        Assert.True(lowVisible[1].Hidden);
        Assert.Equal("???", lowVisible[1].NameVi);
        Assert.True(lowVisible[2].Hidden);
        Assert.Equal("???", lowVisible[2].NameVi);

        var talkStranger = dialogue.BuildTalkScene(zhangFei.Id);
        Assert.NotNull(talkStranger.Traits);
        Assert.Equal("???", talkStranger.Traits![1].NameVi);

        // 2. Boost affinity >= 20 (Bằng hữu): all 3 traits revealed
        kernel.Db.Execute(
            "INSERT INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date) " +
            "VALUES (@id, 35, 65, 'Bằng hữu', 1, '184-05-01') " +
            "ON CONFLICT(npc_id) DO UPDATE SET affinity = 35",
            DbArgs.One("@id", zhangFei.Id));

        var highVisible = social.GetVisibleTraits(zhangFei.Id);
        Assert.All(highVisible, t => Assert.False(t.Hidden));
        Assert.DoesNotContain(highVisible, t => t.NameVi == "???");
        Assert.Equal(fullTraits.Select(t => t.NameVi).ToArray(), highVisible.Select(t => t.NameVi).ToArray());
    }

    [Fact]
    public void Duel_modifiers_reflect_nong_nay_and_diem_tinh_and_winning_against_me_vo_grants_bonus_affinity()
    {
        using var kernel = _host.Boot();
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);

        var zhangFei = registry.FindByCode("npc_0002")!;   // Nóng nảy, Mê rượu, Mê võ
        var zhaoYun = registry.FindByCode("npc_0024")!;    // Trung nghĩa, Điềm tĩnh, Khiêm cung

        var zfMods = SimT.Modules.Dlc.Power.TacticalDuelEngine.ResolveTraitCombatModifiers(social.GetTraits(zhangFei.Id));
        var zyMods = SimT.Modules.Dlc.Power.TacticalDuelEngine.ResolveTraitCombatModifiers(social.GetTraits(zhaoYun.Id));

        // Nóng nảy: higher attack (> 1.0), lower defense (incoming damage > 1.0)
        Assert.True(zfMods.AttackMultiplier > 1.0);
        Assert.True(zfMods.IncomingDamageMultiplier > 1.0);

        // Điềm tĩnh: lower attack (< 1.0), higher defense (incoming damage < 1.0)
        Assert.True(zyMods.AttackMultiplier < 1.0);
        Assert.True(zyMods.IncomingDamageMultiplier < 1.0);

        // Winning duel against Mê võ NPC grants bonus affinity
        var affBefore = social.GetRelationship(zhangFei.Id).Affinity;
        var bonus = social.RecordDuelVictoryBonus(zhangFei.Id);
        var affAfter = social.GetRelationship(zhangFei.Id).Affinity;

        Assert.True(bonus > 0);
        Assert.Equal(affBefore + bonus, affAfter);
    }
}
