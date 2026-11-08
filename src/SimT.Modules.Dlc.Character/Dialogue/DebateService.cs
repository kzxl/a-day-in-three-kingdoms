using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;

namespace SimT.Modules.Dlc.Character;

internal sealed class DebateService(ModuleContext context, SocialService social) : IDebateService
{
    private readonly ModuleContext _context = context;
    private readonly SocialService _social = social;
    private DebateSessionView? _activeSession;

    public DebateSessionView? ActiveSession => _activeSession;

    public int DaysUntilDebateReady(long npcId, string topic)
    {
        var normTopic = DebateTopics.Normalize(topic);
        var lastDay = _context.Db.ScalarLong(
            "SELECT COALESCE((SELECT last_day_index FROM dg_debate_cooldown WHERE npc_id = @npc AND topic = @topic), -1)",
            new DbArgs().Add("@npc", npcId).Add("@topic", normTopic));
        if (lastDay < 0)
            return 0;

        var elapsed = (int)(_context.Clock.Today.DayIndex - lastDay);
        return elapsed >= IDebateService.CooldownDays ? 0 : IDebateService.CooldownDays - elapsed;
    }

    public bool CanDebate(long npcId, string topic, out string reasonVi)
    {
        var rem = DaysUntilDebateReady(npcId, topic);
        if (rem > 0)
        {
            reasonVi = $"Cần chờ thêm {rem} ngày nữa mới có thể biện luận việc này cùng đối phương.";
            return false;
        }

        reasonVi = string.Empty;
        return true;
    }

    public DebateSessionView StartDebate(
        long npcId,
        string topic,
        long itemId = 0,
        int quantity = IDebateService.MinHaggleLotQuantity)
    {
        var normTopic = DebateTopics.Normalize(topic);
        if (!CanDebate(npcId, normTopic, out var reasonVi))
            throw new SimException("debate.cooldown", reasonVi);

        var npcs = _context.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var npc = npcs.Find(npcId) ?? throw new SimException("debate.npc_not_found", $"Không tìm thấy nhân sĩ #{npcId}.");
        var playerState = _context.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var map = _context.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);
        var items = _context.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);

        var cityId = playerState.CurrentCityId;
        var cityNameVi = map.Find(cityId)?.NameVi ?? "Lạc Dương";
        var effectiveQty = normTopic == DebateTopics.Haggle
            ? Math.Max(IDebateService.MinHaggleLotQuantity, quantity)
            : Math.Max(1, quantity);

        var resolvedItemId = ResolveTargetItemId(cityId, npc.Id, itemId, items);
        var itemNameVi = items.Find(resolvedItemId)?.NameVi ?? "Lụa";
        var shortNpcName = RoleTitleVi.ShortName(npc.NameVi);

        RecordCooldown(npc.Id, normTopic, "ongoing");

        var args = DrawTurnArguments(1, normTopic, cityId, cityNameVi, shortNpcName, itemNameVi);
        var visibleTraits = _social.GetVisibleTraits(npc.Id);
        var openingLine = normTopic switch
        {
            DebateTopics.Recruit => "“Thiên hạ rộng lớn, vì sao tại hạ phải rời chốn tự do để đầu quân dưới trướng các hạ?”",
            DebateTopics.Tax => $"“Thuế khóa {cityNameVi} đều chi dùng cho việc quân quốc, các hạ lấy cớ gì xin khoan giảm?”",
            _ => $"“Lô {itemNameVi} ({effectiveQty} đơn vị) này giá đã định sẵn, các hạ muốn bàn giá thế nào?”"
        };

        _activeSession = new DebateSessionView(
            SessionId: $"debate:{npc.Id}:{normTopic}:{_context.Clock.Today.DayIndex}",
            NpcId: npc.Id, NpcNameVi: npc.NameVi, Topic: normTopic, TopicTitleVi: DebateTopics.TitleVi(normTopic),
            CityId: cityId, TargetItemId: resolvedItemId, TargetQuantity: effectiveQty,
            Turn: 1, MaxTurns: IDebateService.MaxTurns, Persuasion: IDebateService.StartPersuasion,
            Outcome: DebateOutcomeKind.Ongoing, IsFinished: false,
            LatestSpeakerVi: npc.NameVi, LatestLineVi: openingLine, PlayerLastLineVi: null, OpponentLastReplyVi: openingLine,
            LastPlayerPush: 0, LastOpponentCounter: 0, LastTraitMultiplier: 1.0, OpponentDispleased: false,
            OutcomeTitleVi: null, ConsequenceVi: null, PriceModifierPercent: 0, TaxReliefPercent: 0, RecruitFeeDiscountPercent: 0,
            Arguments: args, Traits: visibleTraits);

        return _activeSession;
    }

    public DebateSessionView Concede()
    {
        var session = RequireActiveSession();
        if (session.IsFinished) return session;

        RecordCooldown(session.NpcId, session.Topic, "conceded");
        var shortName = RoleTitleVi.ShortName(session.NpcNameVi);
        var finalLine = $"{shortName} chắp tay đáp lễ: “Các hạ đã chủ động nhường một bước, chuyện hôm nay coi như hòa.”";

        _activeSession = session with
        {
            Outcome = DebateOutcomeKind.Conceded, IsFinished = true,
            LatestSpeakerVi = session.NpcNameVi, LatestLineVi = finalLine,
            OutcomeTitleVi = "Nhượng bộ · Hòa nhẹ",
            ConsequenceVi = "Kết thúc hòa nhẹ, không mất thêm tiền bạc, danh vọng hay thiện cảm.",
            Arguments = Array.Empty<DebateArgumentView>()
        };
        return _activeSession;
    }

    public DebateSessionView ChooseArgument(DebateArgumentKind kind)
    {
        var session = RequireActiveSession();
        if (session.IsFinished)
            return session;

        var matched = session.Arguments.FirstOrDefault(a => a.Kind == kind);
        if (matched is not null)
            return ExecuteArgumentTurn(session, matched);

        var map = _context.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);
        var items = _context.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);
        var cityNameVi = map.Find(session.CityId)?.NameVi ?? "Lạc Dương";
        var itemNameVi = items.Find(session.TargetItemId)?.NameVi ?? "Lụa";
        var fallback = BuildSingleArgument(0, kind, 0, session.Topic, cityNameVi, RoleTitleVi.ShortName(session.NpcNameVi), itemNameVi);
        return ExecuteArgumentTurn(session, fallback);
    }

    public DebateSessionView StepDebate(string choiceId)
    {
        var session = RequireActiveSession();
        var norm = (choiceId ?? string.Empty).Trim().ToLowerInvariant();
        if (norm is "concede" or "nhuong_bo")
            return Concede();
        if (session.IsFinished)
            return session;

        var arg = session.Arguments.FirstOrDefault(a => string.Equals(a.Id, norm, StringComparison.OrdinalIgnoreCase));
        if (arg is null && norm.StartsWith("arg:", StringComparison.Ordinal))
        {
            arg = norm["arg:".Length..] switch
            {
                "ly" => session.Arguments.FirstOrDefault(a => a.Kind == DebateArgumentKind.Ly),
                "tinh" => session.Arguments.FirstOrDefault(a => a.Kind == DebateArgumentKind.Tinh),
                "loi" => session.Arguments.FirstOrDefault(a => a.Kind == DebateArgumentKind.Loi),
                "uy" => session.Arguments.FirstOrDefault(a => a.Kind == DebateArgumentKind.Uy),
                _ => null
            };
        }

        if (arg is null)
            throw new SimException("debate.invalid_choice", $"Lựa chọn biện luận '{choiceId}' không hợp lệ.");

        return ExecuteArgumentTurn(session, arg);
    }

    private DebateSessionView ExecuteArgumentTurn(DebateSessionView session, DebateArgumentView arg)
    {
        if (!arg.Enabled)
            throw new SimException("debate.argument_disabled", arg.DisabledReasonVi ?? "Lý lẽ này hiện không thể dùng.");

        var npcs = _context.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var npc = npcs.Find(session.NpcId) ?? throw new SimException("debate.npc_not_found", "Không tìm thấy đối phương.");
        var attrs = _context.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);
        var wallet = _context.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        var bag = _context.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        var map = _context.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);
        var items = _context.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);

        if (arg.Kind == DebateArgumentKind.Loi)
        {
            if (arg.CostCopper > 0)
            {
                wallet.Charge(arg.CostCopper, "character.debate_loi", $"Chi phí lý lẽ Lợi khi biện luận cùng {npc.NameVi}");
            }
            else if (arg.CostItemId > 0 && arg.CostItemQty > 0 && !bag.Remove(session.CityId, arg.CostItemId, arg.CostItemQty))
            {
                throw new SimException("debate.item_short", "Không đủ hàng hóa trong túi để dùng lý lẽ Lợi.");
            }
        }

        var fullTraits = _social.GetTraits(npc.Id);
        var playerStat = DebateRules.GetPlayerBaseStat(arg.Kind, attrs.Attributes, attrs.Social.Fame, Math.Max(DebateRules.DefaultLoiCopperCost, arg.CostCopper));
        var playerPush = DebateRules.ComputePlayerPush(playerStat, fullTraits, arg.Kind, out var multiplier, out var displeased);

        var useDip = _context.Rng.Next(0, 2) == 1;
        var counterRoll = _context.Rng.Next(0, 3);
        var replyVariant = _context.Rng.Next(0, 2);
        var oppCounter = DebateRules.ComputeOpponentCounter(npc.Attributes, useDip, displeased, counterRoll);

        var newPersuasion = Math.Clamp(session.Persuasion + playerPush - oppCounter, 0, 100);
        var cityNameVi = map.Find(session.CityId)?.NameVi ?? "Lạc Dương";
        var shortNpc = RoleTitleVi.ShortName(npc.NameVi);
        var oppReply = DebateRules.BuildOpponentReply(session.Topic, shortNpc, cityNameVi, multiplier, displeased, replyVariant);

        var outcome = DebateOutcomeKind.Ongoing;
        if (newPersuasion >= IDebateService.WinThreshold)
            outcome = DebateOutcomeKind.MajorWin;
        else if (newPersuasion <= IDebateService.LossThreshold)
            outcome = DebateOutcomeKind.MajorLoss;
        else if (session.Turn >= session.MaxTurns)
            outcome = newPersuasion >= IDebateService.StartPersuasion ? DebateOutcomeKind.MinorWin : DebateOutcomeKind.MinorLoss;

        var isFinished = outcome != DebateOutcomeKind.Ongoing;
        var nextTurn = isFinished ? session.Turn : session.Turn + 1;
        var itemNameVi = items.Find(session.TargetItemId)?.NameVi ?? "Lụa";
        var nextArgs = isFinished
            ? Array.Empty<DebateArgumentView>()
            : DrawTurnArguments(nextTurn, session.Topic, session.CityId, cityNameVi, shortNpc, itemNameVi);

        string? outcomeTitle = null, consequenceVi = null;
        int priceMod = 0, taxRelief = 0, recruitDiscount = 0;
        if (isFinished)
            (outcomeTitle, consequenceVi, priceMod, taxRelief, recruitDiscount) = ApplyOutcomeConsequences(session, npc, outcome, newPersuasion, cityNameVi);

        _activeSession = session with
        {
            Turn = nextTurn, Persuasion = newPersuasion, Outcome = outcome, IsFinished = isFinished,
            LatestSpeakerVi = npc.NameVi, LatestLineVi = oppReply, PlayerLastLineVi = arg.LineVi, OpponentLastReplyVi = oppReply,
            LastPlayerPush = playerPush, LastOpponentCounter = oppCounter, LastTraitMultiplier = multiplier, OpponentDispleased = displeased,
            OutcomeTitleVi = outcomeTitle, ConsequenceVi = consequenceVi,
            PriceModifierPercent = priceMod, TaxReliefPercent = taxRelief, RecruitFeeDiscountPercent = recruitDiscount,
            Arguments = nextArgs, Traits = _social.GetVisibleTraits(npc.Id)
        };
        return _activeSession;
    }

    private (string TitleVi, string ConsequenceVi, int PriceMod, int TaxRelief, int RecruitDiscount) ApplyOutcomeConsequences(
        DebateSessionView session, NpcDef npc, DebateOutcomeKind outcome, int finalPersuasion, string cityNameVi)
    {
        var today = _context.Clock.Today.DayIndex;
        var isWin = outcome is DebateOutcomeKind.MajorWin or DebateOutcomeKind.MinorWin;
        var titleVi = outcome switch
        {
            DebateOutcomeKind.MajorWin => "Thắng cuộc · Áp đảo lý lẽ",
            DebateOutcomeKind.MinorWin => "Thắng nhỏ · Chiếm thượng phong",
            DebateOutcomeKind.MinorLoss => "Thua nhỏ · Đuối lý",
            _ => "Thua cuộc · Bất đồng quan điểm"
        };

        RecordCooldown(npc.Id, session.Topic, outcome.ToString().ToLowerInvariant());

        switch (session.Topic)
        {
            case DebateTopics.Recruit:
            {
                if (isWin)
                {
                    var discount = DebateRules.ComputeRecruitFeeDiscountPercent(outcome);
                    SaveEffect(DebateTopics.Recruit, session.CityId, npc.Id, 0, -discount, today, today + 30);
                    _social.CompleteDebateRecruit(npc.Id, discount, out var feePaid, out var roleVi);
                    return (titleVi, $"{npc.NameVi} tâm phục khẩu phục, nhận lời làm {roleVi} với phí chiêu mộ giảm {discount}% (đã trao {feePaid} quan).", 0, 0, discount);
                }

                var affDelta = outcome == DebateOutcomeKind.MajorLoss ? -5 : -3;
                _social.AdjustAffinityFromDebate(npc.Id, affDelta, $"Biện luận thuyết phục gia thần bất thành ({affDelta} Hảo cảm).");
                return (titleVi, $"Thuyết phục bất thành, thiện cảm của {npc.NameVi} giảm nhẹ ({affDelta}).", 0, 0, 0);
            }
            case DebateTopics.Tax:
            {
                if (isWin)
                {
                    var relief = DebateRules.ComputeTaxReliefPercent(outcome);
                    SaveEffect(DebateTopics.Tax, session.CityId, npc.Id, 0, -relief, today, today + IDebateService.TaxReliefDurationDays);
                    return (titleVi, $"Quan phủ {cityNameVi} chuẩn tấu, giảm {relief}% thuế suất cho bạn trong {IDebateService.TaxReliefDurationDays} ngày.", 0, relief, 0);
                }

                var fameLoss = outcome == DebateOutcomeKind.MajorLoss ? -5 : -3;
                _context.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes)
                    .GainFame(fameLoss, $"Biện luận xin quan phủ {cityNameVi} giảm thuế thất bại.");
                return (titleVi, $"Quan phủ bác lời thỉnh cầu, bạn mất {Math.Abs(fameLoss)} điểm danh vọng.", 0, 0, 0);
            }
            default:
            {
                var modPct = DebateRules.ComputeHaggleModifierPercent(outcome, finalPersuasion);
                SaveEffect(DebateTopics.Haggle, session.CityId, npc.Id, 0, modPct, today, today + IDebateService.CooldownDays);
                var msg = modPct < 0
                    ? $"Mặc cả thành công: giá lô hàng giảm {Math.Abs(modPct)}% trong {IDebateService.CooldownDays} ngày."
                    : $"Mặc cả thất bại: thương nhân tăng giá thêm {modPct}% trong {IDebateService.CooldownDays} ngày.";
                return (titleVi, msg, modPct, 0, 0);
            }
        }
    }

    private IReadOnlyList<DebateArgumentView> DrawTurnArguments(
        int turn, string topic, long cityId, string cityNameVi, string npcShortNameVi, string itemNameVi)
    {
        DebateArgumentKind[] pool = [DebateArgumentKind.Ly, DebateArgumentKind.Tinh, DebateArgumentKind.Loi, DebateArgumentKind.Uy];
        int[] nonLoi = [0, 1, 3];
        var excludeIdx = turn == 1
            ? nonLoi[_context.Rng.Next(0, nonLoi.Length)]
            : _context.Rng.Next(0, pool.Length);

        var chosen = new List<DebateArgumentKind>(3);
        for (var i = 0; i < pool.Length; i++)
        {
            if (i != excludeIdx)
                chosen.Add(pool[i]);
        }

        for (var i = chosen.Count - 1; i > 0; i--)
        {
            var swapIdx = _context.Rng.Next(0, i + 1);
            (chosen[i], chosen[swapIdx]) = (chosen[swapIdx], chosen[i]);
        }

        var list = new List<DebateArgumentView>(3);
        for (var i = 0; i < chosen.Count; i++)
        {
            var variant = _context.Rng.Next(0, 3);
            list.Add(BuildSingleArgument(i, chosen[i], variant, topic, cityNameVi, npcShortNameVi, itemNameVi));
        }
        return list;
    }

    private DebateArgumentView BuildSingleArgument(
        int index, DebateArgumentKind kind, int variant, string topic, string cityNameVi, string npcShortNameVi, string itemNameVi)
    {
        var wallet = _context.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        var costCopper = kind == DebateArgumentKind.Loi ? DebateRules.DefaultLoiCopperCost : 0;
        var enabled = true;
        string? disabledReason = null;

        if (kind == DebateArgumentKind.Loi)
        {
            if (wallet.Liquid <= 0)
            {
                enabled = false;
                disabledReason = "Không đủ ngân lượng để dùng lý lẽ Lợi.";
            }
            else if (wallet.Liquid < costCopper)
            {
                costCopper = (int)wallet.Liquid;
            }
        }

        return new DebateArgumentView(
            Id: $"arg:{index}",
            Kind: kind,
            KindLabelVi: DebateRules.KindLabelVi(kind),
            LineVi: DebateRules.BuildArgumentLine(topic, kind, variant, cityNameVi, npcShortNameVi, itemNameVi, costCopper),
            TagVi: DebateRules.BuildArgumentTagVi(kind, costCopper),
            CostCopper: costCopper,
            Enabled: enabled,
            DisabledReasonVi: disabledReason);
    }

    public DebateScene? BuildDebateScene(DebateSessionView? session = null) =>
        DialogueService.BuildDebateSceneFromSession(_context, session ?? _activeSession);

    public int GetHagglePriceModifierPercent(long cityId, long npcId, long itemId = 0)
    {
        var day = _context.Clock.Today.DayIndex;
        return (int)_context.Db.ScalarLong(
            "SELECT COALESCE((SELECT modifier_percent FROM dg_debate_effect WHERE topic = 'haggle' AND city_id = @city AND (npc_id = @npc OR @npc = 0) AND (item_id = @item OR item_id = 0 OR @item = 0) AND start_day <= @day AND end_day >= @day ORDER BY id DESC LIMIT 1), 0)",
            new DbArgs().Add("@city", cityId).Add("@npc", npcId).Add("@item", itemId).Add("@day", day));
    }

    public int GetCityBulkHaggleModifierPercent(long cityId, long itemId = 0) => GetHagglePriceModifierPercent(cityId, 0, itemId);
    public int GetRecruitFeeDiscountPercent(long npcId) => _social.GetActiveDebateRecruitDiscountPercent(npcId);
    public bool HasPersuadedRetainer(long npcId) => GetRecruitFeeDiscountPercent(npcId) > 0 || _social.GetRetainer(npcId) is not null;

    public int GetTaxReliefPercent(long cityId)
    {
        var day = _context.Clock.Today.DayIndex;
        var mod = _context.Db.ScalarLong(
            "SELECT COALESCE((SELECT modifier_percent FROM dg_debate_effect WHERE topic = 'tax' AND city_id = @city AND start_day <= @day AND end_day >= @day ORDER BY id DESC LIMIT 1), 0)",
            new DbArgs().Add("@city", cityId).Add("@day", day));
        return mod < 0 ? (int)Math.Abs(mod) : 0;
    }

    private long ResolveTargetItemId(long cityId, long npcId, long itemId, IItemCatalog items)
    {
        if (itemId > 0 && items.Find(itemId) is not null) return itemId;
        if (_context.Capabilities.TryGet<INpcMarket>(CapabilityIds.NpcMarket, out var npcMkt) && npcMkt is not null)
        {
            var offer = npcMkt.OffersAt(cityId).FirstOrDefault(o => o.NpcId == npcId) ?? npcMkt.OffersAt(cityId).FirstOrDefault();
            if (offer is not null) return offer.ItemId;
        }
        return (items.FindByCode("lua") ?? items.All.First()).Id;
    }

    private void RecordCooldown(long npcId, string topic, string outcome) =>
        _context.Db.Execute(
            "INSERT INTO dg_debate_cooldown (npc_id, topic, last_day_index, last_outcome) VALUES (@npc, @topic, @day, @out) ON CONFLICT(npc_id, topic) DO UPDATE SET last_day_index = excluded.last_day_index, last_outcome = excluded.last_outcome",
            new DbArgs().Add("@npc", npcId).Add("@topic", topic).Add("@day", _context.Clock.Today.DayIndex).Add("@out", outcome));

    private void SaveEffect(string topic, long cityId, long npcId, long itemId, int modPct, long startDay, long endDay) =>
        _context.Db.Execute(
            "INSERT INTO dg_debate_effect (topic, city_id, npc_id, item_id, modifier_percent, start_day, end_day) VALUES (@topic, @city, @npc, @item, @mod, @start, @end)",
            new DbArgs().Add("@topic", topic).Add("@city", cityId).Add("@npc", npcId).Add("@item", itemId).Add("@mod", modPct).Add("@start", startDay).Add("@end", endDay));

    private DebateSessionView RequireActiveSession() =>
        _activeSession ?? throw new SimException("debate.no_active_session", "Hiện không có cuộc biện luận nào đang diễn ra.");
}
