using SimT.Contracts;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;

namespace SimT.Modules.Dlc.Character;

internal sealed partial class DialogueService
{
    private void AddDebateChoices(NpcDef npc, long currentCityId, List<SceneChoice> choices)
    {
        if (!_context.Capabilities.TryGet<IDebateService>(CapabilityIds.Debate, out var debate) || debate is null)
            return;

        if (IsMerchantForHaggle(npc, currentCityId))
        {
            var canHaggle = debate.CanDebate(npc.Id, DebateTopics.Haggle, out var reason);
            var waitDays = debate.DaysUntilDebateReady(npc.Id, DebateTopics.Haggle);
            choices.Add(new SceneChoice(
                "debate:haggle",
                "Mặc cả lô hàng",
                canHaggle ? "Lô từ 20 đơn vị · Biện luận" : $"Chờ {waitDays} ngày",
                Enabled: canHaggle,
                DisabledReasonVi: canHaggle ? null : reason));
        }

        if (_context.Capabilities.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social) &&
            social is not null &&
            social.GetRetainer(npc.Id) is null)
        {
            var req = social.GetRecruitRequirement(npc.Id);
            var rel = social.GetRelationship(npc.Id);
            if (!req.IsWarlord && rel.Affinity < req.RequiredAffinity)
            {
                var canRecruit = debate.CanDebate(npc.Id, DebateTopics.Recruit, out var reason);
                var waitDays = debate.DaysUntilDebateReady(npc.Id, DebateTopics.Recruit);
                choices.Add(new SceneChoice(
                    "debate:recruit",
                    "Thuyết phục làm gia thần",
                    canRecruit ? $"Hảo cảm {rel.Affinity}/{req.RequiredAffinity} · Giảm sính lễ" : $"Chờ {waitDays} ngày",
                    Enabled: canRecruit,
                    DisabledReasonVi: canRecruit ? null : reason));
            }
        }

        if (_context.Capabilities.TryGet<IPoliticalCourt>(CapabilityIds.CourtOffice, out var court) &&
            court?.Governances.FirstOrDefault(g => g.CitadelId == currentCityId) is { } gov &&
            IsTaxPetitionTarget(npc, gov))
        {
            var canTax = debate.CanDebate(npc.Id, DebateTopics.Tax, out var reason);
            var waitDays = debate.DaysUntilDebateReady(npc.Id, DebateTopics.Tax);
            choices.Add(new SceneChoice(
                "debate:tax",
                "Xin quan phủ giảm thuế",
                canTax ? $"Thuế {gov.TaxRatePercent}% · Giảm 30 ngày" : $"Chờ {waitDays} ngày",
                Enabled: canTax,
                DisabledReasonVi: canTax ? null : reason));
        }
    }

    private bool IsMerchantForHaggle(NpcDef npc, long currentCityId)
    {
        if (string.Equals(RoleTitleVi.ToArchetypeKey(npc.Role), "merchant", StringComparison.OrdinalIgnoreCase))
            return true;

        var rawRole = (npc.Role ?? string.Empty).Trim().ToLowerInvariant();
        if (rawRole is "merchant" or "trader" or "shopkeeper" or "tavern" or "innkeeper")
            return true;

        if (_context.Capabilities.TryGet<INpcMarket>(CapabilityIds.NpcMarket, out var npcMarket) && npcMarket is not null)
        {
            if (npcMarket.StallsAt(currentCityId).Any(s => s.NpcId == npc.Id) ||
                npcMarket.OffersAt(currentCityId).Any(o => o.NpcId == npc.Id))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTaxPetitionTarget(NpcDef npc, CityGovernanceView gov)
    {
        if (gov.GovernorNpcId > 0 && npc.Id == gov.GovernorNpcId)
            return true;

        var raw = (npc.Role ?? string.Empty).Trim().ToLowerInvariant();
        if (raw is "official" or "governor" or "magistrate" or "scholar" or "strategist" or "advisor" or "general" or "commander" or "elder")
            return true;

        var arch = RoleTitleVi.ToArchetypeKey(npc.Role);
        return arch is "scholar" or "general" or "elder";
    }

    private TalkChoiceOutcome HandleDebateChoice(NpcDef npc, string choiceId)
    {
        if (!_context.Capabilities.TryGet<IDebateService>(CapabilityIds.Debate, out var debate) || debate is null)
            return new TalkChoiceOutcome(false, "Chưa mở được biện luận lúc này.", UpdatedScene: BuildTalkScene(npc.Id));

        var rawTopic = choiceId.StartsWith("debate:", StringComparison.OrdinalIgnoreCase)
            ? choiceId["debate:".Length..]
            : DebateTopics.Haggle;
        var topic = DebateTopics.Normalize(rawTopic);

        try
        {
            var session = debate.StartDebate(npc.Id, topic);
            var scene = BuildDebateSceneFromSession(_context, session);
            return new TalkChoiceOutcome(
                true,
                $"Bước vào cuộc biện luận ({session.TopicTitleVi}) cùng {npc.NameVi}.",
                OpenDebate: true,
                DebateScene: scene);
        }
        catch (SimException ex)
        {
            var msg = ClampLine90(ex.Message);
            return new TalkChoiceOutcome(false, msg, UpdatedScene: BuildTalkScene(npc.Id, msg));
        }
    }

    public DebateScene? BuildDebateScene(DebateSessionView? session) =>
        BuildDebateSceneFromSession(_context, session);

    internal static DebateScene? BuildDebateSceneFromSession(ModuleContext context, DebateSessionView? session)
    {
        if (session is null)
            return null;

        var npcs = context.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var map = context.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);
        var npc = npcs.Find(session.NpcId) ?? npcs.All.First();
        var citadel = map.Find(session.CityId);
        var locationVi = citadel?.NameVi ?? "Lạc Dương";
        var locationHan = ResolveCitadelHan(citadel?.Code);

        var (portraitFull, portraitFace, _) = SceneArtCatalog.ResolveNpcArt(npc);
        var (blinkPath, talkPath) = SceneArtCatalog.ResolvePortraitFrames(npc);
        var bgPath = ResolveTalkBackground(SceneArtCatalog.ClassifyArchetype(npc));

        context.Capabilities.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social);
        var psyche = social?.GetPsycheProfile(npc.Id);

        List<SceneChoice> choices;
        if (session.IsFinished)
        {
            choices =
            [
                new SceneChoice("leave", "Rời đi", "Khép lại cuộc biện luận")
            ];
        }
        else
        {
            choices = session.Arguments
                .Select(a => new SceneChoice(a.Id, a.LineVi, a.TagVi, a.Enabled, a.DisabledReasonVi))
                .ToList();
            choices.Add(new SceneChoice("concede", "Nhượng bộ", "Hòa nhẹ"));
        }

        return new DebateScene(
            SceneId: session.SessionId,
            NpcId: npc.Id,
            NpcCode: npc.Code,
            NpcNameVi: npc.NameVi,
            NpcNameHan: string.IsNullOrWhiteSpace(npc.NameHan) ? "辯" : npc.NameHan,
            StyleNameVi: npc.StyleNameVi,
            RoleVi: RoleTitleVi.Format(npc.Role),
            PersonalityVi: RoleTitleVi.FormatPersonality(npc.Personality),
            LocationNameVi: locationVi,
            LocationHan: locationHan,
            Topic: session.Topic,
            TopicTitleVi: session.TopicTitleVi,
            PortraitFull: portraitFull,
            PortraitFace: portraitFace,
            BackgroundPath: bgPath,
            Persuasion: session.Persuasion,
            Turn: session.Turn,
            MaxTurns: session.MaxTurns,
            IsFinished: session.IsFinished,
            OutcomeCode: session.Outcome.ToString().ToLowerInvariant(),
            OutcomeTitleVi: session.OutcomeTitleVi,
            LatestSpeakerVi: session.LatestSpeakerVi,
            LatestLineVi: session.LatestLineVi,
            PlayerLastLineVi: session.PlayerLastLineVi,
            ConsequenceVi: session.ConsequenceVi,
            Traits: session.Traits,
            Choices: choices,
            BlinkPath: blinkPath,
            TalkPath: talkPath,
            ArchetypeCode: psyche?.Archetype.ToString().ToLowerInvariant(),
            ArchetypeBadgeVi: psyche?.BadgeVi);
    }
}
