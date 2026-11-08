using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;
using SimT.Modules.Dlc.Character;
using Xunit;

namespace SimT.Modules.Dlc.Tests;

public sealed class DebateTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void Debate_is_deterministic_by_seed()
    {
        using var hostA = new DlcTestHost();
        using var hostB = new DlcTestHost();
        using var kernelA = hostA.Boot();
        using var kernelB = hostB.Boot();

        var debateA = kernelA.Capabilities.Get<IDebateService>(CapabilityIds.Debate);
        var debateB = kernelB.Capabilities.Get<IDebateService>(CapabilityIds.Debate);
        var npcs = kernelA.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var targetNpc = npcs.FindByCode("thuong_nhan_a")!;

        var sessionA = debateA.StartDebate(targetNpc.Id, DebateTopics.Haggle);
        var sessionB = debateB.StartDebate(targetNpc.Id, DebateTopics.Haggle);

        Assert.Equal(50, sessionA.Persuasion);
        Assert.Equal(sessionA.Persuasion, sessionB.Persuasion);
        Assert.Equal(3, sessionA.Arguments.Count);
        Assert.Equal(
            sessionA.Arguments.Select(a => (a.Id, a.Kind, a.LineVi, a.CostCopper)).ToArray(),
            sessionB.Arguments.Select(a => (a.Id, a.Kind, a.LineVi, a.CostCopper)).ToArray());

        while (!sessionA.IsFinished && !sessionB.IsFinished)
        {
            var pick = sessionA.Arguments.First(a => a.Enabled).Id;
            sessionA = debateA.StepDebate(pick);
            sessionB = debateB.StepDebate(pick);

            Assert.Equal(sessionA.Turn, sessionB.Turn);
            Assert.Equal(sessionA.Persuasion, sessionB.Persuasion);
            Assert.Equal(sessionA.Outcome, sessionB.Outcome);
            Assert.Equal(sessionA.LatestLineVi, sessionB.LatestLineVi);
            Assert.Equal(
                sessionA.Arguments.Select(a => (a.Id, a.LineVi)).ToArray(),
                sessionB.Arguments.Select(a => (a.Id, a.LineVi)).ToArray());
        }
    }

    [Fact]
    public void Trait_multipliers_shift_push_power_in_right_direction()
    {
        using var kernel = _host.Boot();
        var traitsCap = kernel.Capabilities.Get<INpcTraits>(CapabilityIds.NpcTraits);
        var allDefs = traitsCap.GetAllTraitDefs();
        NpcTraitDef Trait(string code) => allDefs.First(t => t.Code == code);

        var baseAttrs = new CharacterAttributes(60, 60, 75, 65, 75, 70, 60, 60, 60, 65, 60, 65);
        var stat = DebateRules.GetPlayerBaseStat(DebateArgumentKind.Loi, baseAttrs, fame: 100, loiCopperCost: 40);

        var neutralPush = DebateRules.ComputePlayerPush(
            stat,
            [Trait(NpcTraitCodes.DiemTinh)],
            DebateArgumentKind.Loi,
            out var neutralMult,
            out var neutralDispleased);

        var greedyPush = DebateRules.ComputePlayerPush(
            stat,
            [Trait(NpcTraitCodes.ThamTai)],
            DebateArgumentKind.Loi,
            out var greedyMult,
            out var greedyDispleased);

        var incorruptiblePush = DebateRules.ComputePlayerPush(
            stat,
            [Trait(NpcTraitCodes.ThanhLiem)],
            DebateArgumentKind.Loi,
            out var incorruptibleMult,
            out var incorruptibleDispleased);

        Assert.Equal(1.0, neutralMult, precision: 3);
        Assert.False(neutralDispleased);
        Assert.Equal(1.5, greedyMult, precision: 3);
        Assert.False(greedyDispleased);
        Assert.Equal(0.3, incorruptibleMult, precision: 3);
        Assert.True(incorruptibleDispleased);
        Assert.True(greedyPush > neutralPush, $"Expected Tham tài + Lợi ({greedyPush}) > neutral ({neutralPush})");
        Assert.True(incorruptiblePush < neutralPush, $"Expected Thanh liêm + Lợi ({incorruptiblePush}) < neutral ({neutralPush})");

        // Verify remaining cells in the spec table
        Assert.Equal(0.5, DebateRules.GetSingleTraitMultiplier(NpcTraitCodes.TrungNghia, DebateArgumentKind.Loi), 3);
        Assert.Equal(1.4, DebateRules.GetSingleTraitMultiplier(NpcTraitCodes.TrungNghia, DebateArgumentKind.Tinh), 3);
        Assert.Equal(0.6, DebateRules.GetSingleTraitMultiplier(NpcTraitCodes.DaNghi, DebateArgumentKind.Tinh), 3);
        Assert.Equal(1.4, DebateRules.GetSingleTraitMultiplier(NpcTraitCodes.DaNghi, DebateArgumentKind.Ly), 3);
        Assert.Equal(0.4, DebateRules.GetSingleTraitMultiplier(NpcTraitCodes.KieuNgao, DebateArgumentKind.Uy), 3);
        Assert.Equal(1.3, DebateRules.GetSingleTraitMultiplier(NpcTraitCodes.KhiemCung, DebateArgumentKind.Uy), 3);

        var hotTemperedPush = DebateRules.ComputePlayerPush(
            stat,
            [Trait(NpcTraitCodes.NongNay)],
            DebateArgumentKind.Uy,
            out var hotMult,
            out var hotDispleased);
        Assert.Equal(-0.5, hotMult, 3);
        Assert.False(hotDispleased);
        Assert.True(hotTemperedPush < 0, "Expected Nóng nảy + Uy to backfire with negative push");
    }

    [Fact]
    public void Profit_argument_deducts_real_copper_from_wallet()
    {
        using var kernel = _host.Boot();
        var debate = kernel.Capabilities.Get<IDebateService>(CapabilityIds.Debate);
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        var npcs = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var merchant = npcs.FindByCode("thuong_nhan_a")!;

        var session = debate.StartDebate(merchant.Id, DebateTopics.Haggle);
        var profitArg = session.Arguments.FirstOrDefault(a => a.Kind == DebateArgumentKind.Loi);

        Assert.NotNull(profitArg);
        Assert.True(profitArg!.CostCopper > 0);
        Assert.True(profitArg.Enabled);

        var balanceBefore = wallet.Balance;
        var nextSession = debate.StepDebate(profitArg.Id);
        var balanceAfter = wallet.Balance;

        Assert.Equal(balanceBefore - profitArg.CostCopper, balanceAfter);
        Assert.NotNull(nextSession.PlayerLastLineVi);
    }

    [Fact]
    public void Winning_haggle_reduces_price_and_losing_increases_price()
    {
        using var kernel = _host.Boot();
        var debate = kernel.Capabilities.Get<IDebateService>(CapabilityIds.Debate);
        var npcMarket = kernel.Capabilities.Get<INpcMarket>(CapabilityIds.NpcMarket);
        var market = kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade);
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        var items = kernel.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);
        var npcs = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);

        var attrs = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);
        var greedyNpc = npcs.FindByCode("npc_0013")!; // Tả Phong: Tham tài + Keo kiệt
        var silk = items.FindByCode("lua")!;

        // Boost player INT, DIP, Fame via capability so player wins decisively
        attrs.AddExp("int", 3500);
        attrs.AddExp("dip", 3500);
        attrs.GainFame(300, "test_fame");
        wallet.Pay(2000, "test.fund", "Nạp quỹ kiểm thử");

        var baseQuote = npcMarket.QuoteNpcOfferUnitPrice(1, greedyNpc.Id, silk.Id, discountPermille: 50);

        var session = debate.StartDebate(greedyNpc.Id, DebateTopics.Haggle);
        while (!session.IsFinished)
        {
            var bestArg = session.Arguments.FirstOrDefault(a => a.Kind == DebateArgumentKind.Loi && a.Enabled)
                          ?? session.Arguments.First(a => a.Enabled);
            session = debate.StepDebate(bestArg.Id);
        }

        Assert.True(
            session.Outcome is DebateOutcomeKind.MajorWin or DebateOutcomeKind.MinorWin,
            $"Expected haggle win, got {session.Outcome} (persuasion={session.Persuasion})");

        var discountMod = debate.GetHagglePriceModifierPercent(1, greedyNpc.Id);
        Assert.InRange(discountMod, -15, -5);

        var discountedQuote = npcMarket.QuoteNpcOfferUnitPrice(1, greedyNpc.Id, silk.Id, discountPermille: 50);
        Assert.True(discountedQuote < baseQuote, $"Expected discounted quote ({discountedQuote}) < base ({baseQuote})");

        // Bulk market purchase (>= 20 units) at city 1 also applies the haggle discount
        var rice = items.All.First(i => i.BasePrice <= 20);
        var unitPrice = market.QuoteBuy(1, rice.Id);
        var fullBulkCost = unitPrice * 20L;
        var balBeforeBulk = wallet.Balance;
        market.Buy(1, rice.Id, 20);
        var paidBulk = balBeforeBulk - wallet.Balance;
        Assert.True(paidBulk < fullBulkCost, $"Expected bulk cost ({paidBulk}) < full cost ({fullBulkCost})");

        // Now test losing haggle against an NPC with Nóng nảy (npc_0023 Đổng Trác) when repeatedly using Uy (backfires -50%)
        var hotTemperedNpc = npcs.FindByCode("npc_0023")!;
        var baseHotQuote = npcMarket.QuoteNpcOfferUnitPrice(1, hotTemperedNpc.Id, silk.Id, discountPermille: 50);

        var loseSession = debate.StartDebate(hotTemperedNpc.Id, DebateTopics.Haggle);
        while (!loseSession.IsFinished)
        {
            loseSession = debate.ChooseArgument(DebateArgumentKind.Uy);
        }

        Assert.True(loseSession.Outcome is DebateOutcomeKind.MajorLoss or DebateOutcomeKind.MinorLoss);
        Assert.Equal(5, debate.GetHagglePriceModifierPercent(1, hotTemperedNpc.Id));

        var penalizedQuote = npcMarket.QuoteNpcOfferUnitPrice(1, hotTemperedNpc.Id, silk.Id, discountPermille: 50);
        Assert.True(penalizedQuote > baseHotQuote, $"Expected penalized quote ({penalizedQuote}) > base ({baseHotQuote})");
    }

    [Fact]
    public void Seven_day_cooldown_applies_per_npc_and_topic()
    {
        using var kernel = _host.Boot();
        var debate = kernel.Capabilities.Get<IDebateService>(CapabilityIds.Debate);
        var npcs = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var npcA = npcs.FindByCode("thuong_nhan_a")!;
        var npcB = npcs.FindByCode("npc_0013")!;

        Assert.True(debate.CanDebate(npcA.Id, DebateTopics.Haggle, out _));
        debate.StartDebate(npcA.Id, DebateTopics.Haggle);
        debate.Concede();

        // Blocked on same NPC + same topic for 7 days
        Assert.False(debate.CanDebate(npcA.Id, DebateTopics.Haggle, out var reason));
        Assert.False(string.IsNullOrWhiteSpace(reason));
        Assert.Equal(7, debate.DaysUntilDebateReady(npcA.Id, DebateTopics.Haggle));
        Assert.Throws<SimException>(() => debate.StartDebate(npcA.Id, DebateTopics.Haggle));

        // Allowed on same NPC + different topic, and on different NPC + same topic
        Assert.True(debate.CanDebate(npcA.Id, DebateTopics.Recruit, out _));
        Assert.True(debate.CanDebate(npcB.Id, DebateTopics.Haggle, out _));

        // Advance 6 days -> still 1 day left
        kernel.AdvanceDays(6);
        Assert.False(debate.CanDebate(npcA.Id, DebateTopics.Haggle, out _));
        Assert.Equal(1, debate.DaysUntilDebateReady(npcA.Id, DebateTopics.Haggle));

        // Advance 1 more day (total 7 days) -> ready again
        kernel.AdvanceDays(1);
        Assert.True(debate.CanDebate(npcA.Id, DebateTopics.Haggle, out _));
        Assert.Equal(0, debate.DaysUntilDebateReady(npcA.Id, DebateTopics.Haggle));
    }

    [Fact]
    public void Conceding_ends_immediately_without_extra_penalty()
    {
        using var kernel = _host.Boot();
        var debate = kernel.Capabilities.Get<IDebateService>(CapabilityIds.Debate);
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var attrs = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);
        var npcs = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var npc = npcs.FindByCode("thuong_nhan_a")!;

        var copperBefore = wallet.Balance;
        var affinityBefore = social.GetRelationship(npc.Id).Affinity;
        var fameBefore = attrs.Social.Fame;

        var started = debate.StartDebate(npc.Id, DebateTopics.Haggle);
        Assert.False(started.IsFinished);

        var conceded = debate.Concede();
        Assert.True(conceded.IsFinished);
        Assert.Equal(DebateOutcomeKind.Conceded, conceded.Outcome);
        Assert.Equal(copperBefore, wallet.Balance);
        Assert.Equal(affinityBefore, social.GetRelationship(npc.Id).Affinity);
        Assert.Equal(fameBefore, attrs.Social.Fame);
        Assert.Equal(0, debate.GetHagglePriceModifierPercent(1, npc.Id));
    }

    [Fact]
    public void Recruit_and_tax_debates_apply_expected_consequences_and_scene_guard_validates()
    {
        using var kernel = _host.Boot();
        var debate = kernel.Capabilities.Get<IDebateService>(CapabilityIds.Debate);
        var dialogue = kernel.Capabilities.Get<IDialogueModule>(CapabilityIds.Dialogue);
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var court = kernel.Capabilities.Get<IPoliticalCourt>(CapabilityIds.CourtOffice);
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        var npcs = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);

        var attrs = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);
        wallet.Pay(5000, "test.recruit_tax", "Nạp quỹ kiểm thử");
        attrs.AddExp("int", 3500);
        attrs.AddExp("dip", 3500);
        attrs.GainFame(300, "test_fame");

        // 1. Recruit debate when affinity < requiredAffinity
        var candidate = npcs.All.First(n => !social.GetRecruitRequirement(n.Id).IsWarlord);
        var baseReq = social.GetRecruitRequirement(candidate.Id);
        Assert.True(social.GetRelationship(candidate.Id).Affinity < baseReq.RequiredAffinity);

        var talkOutcome = dialogue.HandleChoice(candidate.Id, "debate:recruit");
        Assert.True(talkOutcome.Ok);
        Assert.True(talkOutcome.OpenDebate);
        Assert.NotNull(talkOutcome.DebateScene);

        var activeScene = talkOutcome.DebateScene!;
        var firstChoice = activeScene.Choices.First(c => c.Id.StartsWith("arg:", StringComparison.Ordinal) && c.Enabled);
        Assert.True(SceneTrustGuard.IsAllowedMessage(
            new SceneClientMessage("choice", activeScene.SceneId, firstChoice.Id),
            activeScene));

        var session = debate.ActiveSession!;
        while (!session.IsFinished)
        {
            var pick = session.Arguments.First(a => a.Enabled && a.Kind != DebateArgumentKind.Loi);
            session = debate.StepDebate(pick.Id);
        }

        Assert.True(session.Outcome is DebateOutcomeKind.MajorWin or DebateOutcomeKind.MinorWin);
        Assert.NotNull(social.GetRetainer(candidate.Id));

        // 2. Tax debate reduces city TaxRatePercent for 30 days
        var cityId = 1L;
        var baseTax = court.Governances.First(g => g.CitadelId == cityId).TaxRatePercent;
        var official = npcs.FindByCode("npc_0035")!; // Tuân Úc

        var taxSession = debate.StartDebate(official.Id, DebateTopics.Tax);
        while (!taxSession.IsFinished)
        {
            var pick = taxSession.Arguments.First(a => a.Enabled && a.Kind != DebateArgumentKind.Loi);
            taxSession = debate.StepDebate(pick.Id);
        }

        Assert.True(taxSession.Outcome is DebateOutcomeKind.MajorWin or DebateOutcomeKind.MinorWin);
        var reducedTax = court.Governances.First(g => g.CitadelId == cityId).TaxRatePercent;
        Assert.True(reducedTax < baseTax, $"Expected reduced tax ({reducedTax}%) < base tax ({baseTax}%)");
    }
}
