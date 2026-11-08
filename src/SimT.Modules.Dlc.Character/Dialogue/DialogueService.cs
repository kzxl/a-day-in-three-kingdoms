using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Character;

internal sealed partial class DialogueService(ModuleContext context) : IDialogueModule
{
    private readonly ModuleContext _context = context;

    public IReadOnlyList<string> GetLinesForNpc(long npcId, string relationTierKey)
    {
        var npcs = _context.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var npc = npcs.Find(npcId) ?? npcs.All.First();
        var tier = NormalizeTierKey(relationTierKey);

        var lines = QueryLines("npc", npc.Code, tier);
        if (lines.Count == 0)
            lines = QueryLines("role", MapToSeededRoleGroup(SceneArtCatalog.ClassifyArchetype(npc)), tier);
        if (lines.Count == 0)
            lines = QueryLines("role", "commoner", "stranger");

        var result = lines.Select(ClampLine90).ToList();
        AppendTraitLinesForNpc(npc.Id, tier, result);
        return result;
    }

    public TalkScene BuildTalkScene(long npcId, string? feedbackVi = null) =>
        DecorateLordScene(BuildTalkSceneCore(npcId, feedbackVi, customChoices: null), feedbackVi);

    private TalkScene BuildTalkSceneCore(long npcId, string? feedbackVi, IReadOnlyList<SceneChoice>? customChoices)
    {
        var npcs = _context.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var map = _context.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);
        var playerState = _context.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var npc = npcs.Find(npcId) ?? npcs.All.First();
        var currentCityId = playerState.CurrentCityId;
        var citadel = map.Find(currentCityId);
        var locationVi = citadel?.NameVi ?? "Lạc Dương";
        var locationHan = ResolveCitadelHan(citadel?.Code);

        var (affinity, tierKey, tierVi) = ResolveRelationship(npc.Id);
        var lines = GetLinesForNpc(npc.Id, tierKey).ToList();
        if (!string.IsNullOrWhiteSpace(feedbackVi))
        {
            var cleanFeedback = ClampLine90(feedbackVi!);
            if (!lines.Contains(cleanFeedback, StringComparer.Ordinal))
                lines.Insert(0, cleanFeedback);
        }

        var (portraitFull, portraitFace, _) = SceneArtCatalog.ResolveNpcArt(npc);
        var (blinkPath, talkPath) = SceneArtCatalog.ResolvePortraitFrames(npc);
        var bgPath = ResolveTalkBackground(SceneArtCatalog.ClassifyArchetype(npc));
        var choices = customChoices ?? BuildChoices(npc, currentCityId);

        _context.Capabilities.TryGet<ISocietyChronicle>(CapabilityIds.SocietyChronicle, out var society);
        var nearby = new[] { npc }
            .Concat(npcs.All.Where(n => n.Id != npc.Id && n.CurrentCitadelId(society) == currentCityId))
            .Take(5)
            .Select(n => new SceneNpcThumb(
                n.Id,
                RoleTitleVi.ShortName(n.NameVi),
                SceneArtCatalog.ResolveNpcArt(n).PortraitFace,
                n.Id == npc.Id))
            .ToList();

        _context.Capabilities.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var socialService);
        var psyche = socialService?.GetPsycheProfile(npc.Id);
        var introduction = socialService?.GetMemories(npc.Id,100).FirstOrDefault(m => m.Topic == "introduction")?.SummaryVi
            ?? socialService?.GetRelationship(npc.Id).Impression;
        if (introduction?.StartsWith("Bạn của ", StringComparison.Ordinal) == true) lines.Insert(0, introduction);

        return new TalkScene(
            SceneId: $"talk:{npc.Id}", NpcId: npc.Id, NpcCode: npc.Code,
            NameVi: npc.NameVi, NameHan: string.IsNullOrWhiteSpace(npc.NameHan) ? "漢" : npc.NameHan,
            StyleNameVi: npc.StyleNameVi, RoleVi: RoleTitleVi.Format(npc.Role),
            PersonalityVi: RoleTitleVi.FormatPersonality(npc.Personality),
            LocationNameVi: locationVi, LocationHan: locationHan,
            Affinity: affinity, RelationTierVi: tierVi,
            PortraitFull: portraitFull, PortraitFace: portraitFace, BackgroundPath: bgPath,
            Lines: lines, Choices: choices, NearbyNpcs: nearby,
            FeedbackVi: string.IsNullOrWhiteSpace(feedbackVi) ? null : ClampLine90(feedbackVi!),
            BlinkPath: blinkPath, TalkPath: talkPath,
            ArchetypeCode: psyche?.Archetype.ToString().ToLowerInvariant(),
            ArchetypeBadgeVi: psyche?.BadgeVi,
            Traits: socialService?.GetVisibleTraits(npc.Id));
    }

    public TalkChoiceOutcome HandleChoice(long npcId, string choiceId)
    {
        var npcs = _context.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var npc = npcs.Find(npcId);
        if (npc is null)
            return new TalkChoiceOutcome(false, "Không tìm thấy nhân vật.", CloseScene: true);

        if (choiceId.StartsWith("lord:", StringComparison.Ordinal)) return HandleLordChoice(npc, choiceId);

        if (choiceId.StartsWith("wish:", StringComparison.Ordinal)) return HandleWishChoice(npc, choiceId);

        if (choiceId.StartsWith("debate:", StringComparison.OrdinalIgnoreCase)) return HandleDebateChoice(npc, choiceId);

        RecordVisit(npc.Id);
        var normalized = choiceId.Trim().ToLowerInvariant();
        if (normalized == "gift:cancel")
        {
            var cancelMsg = $"Bạn cất lại hành trang, tiếp tục đàm đạo cùng {RoleTitleVi.ShortName(npc.NameVi)}.";
            return new TalkChoiceOutcome(true, cancelMsg, UpdatedScene: BuildTalkScene(npc.Id, cancelMsg));
        }

        if (normalized.StartsWith("gift:", StringComparison.Ordinal) &&
            long.TryParse(normalized["gift:".Length..], out var giftItemId))
        {
            return ExecuteGiftItem(npc, giftItemId);
        }

        switch (normalized)
        {
            case "chat":
            {
                string msg;
                if (_context.Capabilities.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social) && social is not null)
                {
                    try
                    {
                        var raw = social.ChatWithNpc(npc.Id);
                        msg = SummarizeFeedback(raw, $"{npc.NameVi} gật đầu đàm đạo chuyện trong thành.");
                    }
                    catch (SimException ex)
                    {
                        msg = ClampLine90(ex.Message);
                    }
                }
                else
                {
                    msg = $"{npc.NameVi} mỉm cười trò chuyện cùng bạn về phong tục bản địa.";
                }

                var updated = BuildTalkScene(npc.Id, msg);
                return new TalkChoiceOutcome(true, msg, UpdatedScene: updated);
            }

            case "price":
            {
                var quoteLine = BuildMarketQuoteLine(npc);
                var updated = BuildTalkScene(npc.Id, quoteLine);
                return new TalkChoiceOutcome(true, quoteLine, UpdatedScene: updated);
            }

            case "gift":
            {
                var playerState = _context.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
                var giftChoices = BuildGiftChoices(playerState.CurrentCityId);
                if (giftChoices.Count == 0 ||
                    !_context.Capabilities.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social) ||
                    social is null)
                {
                    var noGift = "Trong hành trang tại thành này hiện chưa có lễ vật.";
                    return new TalkChoiceOutcome(false, noGift, UpdatedScene: BuildTalkScene(npc.Id, noGift));
                }

                var prompt = ClampLine90($"Chọn lễ vật trong hành trang để biếu tặng {RoleTitleVi.ShortName(npc.NameVi)}.");
                return new TalkChoiceOutcome(true, prompt, UpdatedScene: BuildTalkSceneCore(npc.Id, prompt, giftChoices));
            }

            case "duel":
            {
                if (!CanDuelNpc(npc))
                {
                    var msg = "Chưa đủ điều kiện tỷ võ lúc này.";
                    return new TalkChoiceOutcome(false, msg, UpdatedScene: BuildTalkScene(npc.Id, msg));
                }

                return new TalkChoiceOutcome(true, $"Bước lên lôi đài so tài cùng {npc.NameVi}!", OpenDuel: true);
            }

            case "bye":
            default:
                return new TalkChoiceOutcome(true, $"Đã cáo từ {npc.NameVi}.", CloseScene: true);
        }
    }

    private TalkChoiceOutcome ExecuteGiftItem(NpcDef npc, long itemId)
    {
        if (!_context.Capabilities.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social) || social is null)
        {
            const string noSocial = "Không thể tặng lễ vật lúc này.";
            return new TalkChoiceOutcome(false, noSocial, UpdatedScene: BuildTalkScene(npc.Id, noSocial));
        }

        string msg;
        var ok = true;
        try
        {
            var raw = social.GiftNpc(npc.Id, itemId);
            msg = SummarizeFeedback(raw, $"{npc.NameVi} vui vẻ nhận lễ vật bạn trao tặng.");
        }
        catch (SimException ex)
        {
            msg = ClampLine90(ex.Message);
            ok = false;
        }

        return new TalkChoiceOutcome(ok, msg, UpdatedScene: BuildTalkScene(npc.Id, msg));
    }

    private IReadOnlyList<SceneChoice> BuildGiftChoices(long currentCityId)
    {
        var bag = _context.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        var items = _context.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);
        var list = bag.Rows(currentCityId)
            .Where(r => r.Quantity > 0)
            .Select(r => (Row: r, Item: items.Find(r.ItemId)))
            .Where(x => x.Item is not null)
            .OrderByDescending(x => x.Item!.BasePrice)
            .ThenBy(x => x.Item!.Id)
            .Take(5)
            .Select(x => new SceneChoice(
                $"gift:{x.Item!.Id}",
                Clamp40($"Tặng {x.Item.NameVi} (còn {x.Row.Quantity})"),
                $"{x.Item.BasePrice} quan"))
            .ToList();

        if (list.Count > 0)
            list.Add(new SceneChoice("gift:cancel", "Thôi", "Quay lại"));

        return list;
    }

    private IReadOnlyList<SceneChoice> BuildChoices(NpcDef npc, long currentCityId)
    {
        var list = new List<SceneChoice>
        {
            new("chat", "Hỏi chuyện thành này", "Đàm đạo +Hảo cảm"),
            new("price", "Hỏi giá hàng", "Thăm dò chợ địa phương")
        };

        if (_context.Capabilities.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social) && social is not null)
        {
            var bag = _context.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
            var heldRows = bag.Rows(currentCityId).Where(r => r.Quantity > 0).ToList();
            if (heldRows.Count > 0)
            {
                var items = _context.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);
                var topName = heldRows
                    .Select(r => items.Find(r.ItemId))
                    .Where(i => i is not null)
                    .OrderByDescending(i => i!.BasePrice)
                    .FirstOrDefault()?.NameVi ?? "Lễ vật";
                var hint = heldRows.Count == 1 ? Clamp40(topName) : $"{heldRows.Count} loại lễ vật";
                list.Add(new SceneChoice("gift", "Tặng quà kết giao", hint));
            }
        }

        if (CanDuelNpc(npc))
        {
            list.Add(new SceneChoice("duel", "Đấu tay đôi", $"Võ lực {npc.Attributes.War}"));
        }

        AddWishChoices(npc, list);
        AddDebateChoices(npc, currentCityId, list);
        list.Add(new SceneChoice("bye", "Tạm biệt", "Rời cuộc trò chuyện"));
        return list;
    }

    private bool CanDuelNpc(NpcDef npc)
    {
        if (!_context.Capabilities.TryGet<IMilitaryCommand>(CapabilityIds.MilitaryCommand, out var mil) || mil is null)
            return false;

        var playerState = _context.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var npcCityId = npc.CurrentCitadelId(_context.Capabilities);
        var isRetainer = _context.Capabilities.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social) &&
                         social?.GetRetainer(npc.Id) is not null;
        if ((!isRetainer && playerState.CurrentCityId != npcCityId) || npc.Attributes.War < 30)
            return false;

        if (_context.Capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) && attrs is not null)
        {
            if (attrs.Vitals.CurrentStamina < 18 || attrs.Vitals.Fatigue >= 95)
                return false;
        }

        return true;
    }

    private string BuildMarketQuoteLine(NpcDef npc)
    {
        var cityId = _context.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState).CurrentCityId;
        var map = _context.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);
        var cityName = map.Find(cityId)?.NameVi ?? "thành này";
        if (!_context.Capabilities.TryGet<IMarketTrade>(CapabilityIds.MarketTrade, out var trade) || trade is null)
            return ClampLine90($"Chợ {cityName} hôm nay buôn bán bình ổn, lương thảo dồi dào.");

        var items = _context.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);
        var quotes = items.All
            .Take(3)
            .Select(i => $"{i.NameVi} {trade.QuoteBuy(cityId, i.Id)} văn")
            .ToList();

        return ClampLine90($"Giá chợ {cityName}: {string.Join(", ", quotes)}.");
    }

    private (int Affinity, string TierKey, string TierVi) ResolveRelationship(long npcId)
    {
        if (_context.Capabilities.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social) && social is not null)
        {
            var rel = social.GetRelationship(npcId);
            var affinity = rel.Affinity;
            var tierKey = affinity >= 60 ? "trusted" : affinity >= 20 ? "acquaintance" : "stranger";
            var tierVi = affinity >= 60 ? "Tri kỷ" : affinity >= 20 ? "Bằng hữu" : "Sơ giao";
            return (affinity, tierKey, tierVi);
        }

        return (0, "stranger", "Sơ giao");
    }

    private IReadOnlyList<string> QueryLines(string targetKind, string targetCode, string tierKey) =>
        _context.Db.Query(
            """
            SELECT line_vi
            FROM dg_dialogue_line
            WHERE target_kind = @kind AND target_code = @code AND tier_key = @tier
            ORDER BY seq_order ASC;
            """,
            reader => reader.GetString(0),
            new DbArgs().Add("@kind", targetKind).Add("@code", targetCode).Add("@tier", tierKey));

    private void RecordVisit(long npcId)
    {
        var dateStr = GameDateCodec.Format(_context.Clock.Today);
        _context.Db.Execute(
            """
            INSERT INTO dg_dialogue_visit (npc_id, visit_count, last_date_str)
            VALUES (@id, 1, @date)
            ON CONFLICT(npc_id) DO UPDATE SET
                visit_count = visit_count + 1,
                last_date_str = @date;
            """,
            new DbArgs().Add("@id", npcId).Add("@date", dateStr));
    }

    internal static string MapToSeededRoleGroup(string? rawRole)
    {
        var arch = RoleTitleVi.ToArchetypeKey(rawRole);
        return arch switch
        {
            "general" or "archer" or "rebel" => "general",
            "scholar" => "scholar",
            "merchant" => "merchant",
            "lady" or "swordswoman" => "lady",
            "elder" => "elder",
            _ => "commoner"
        };
    }

    internal static string NormalizeTierKey(string? raw) => (raw ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "trusted" or "tri_ky" or "sworn" or "friend" => "trusted",
        "acquaintance" or "than_thiet" or "known" => "acquaintance",
        _ => "stranger"
    };

    private static string ResolveTalkBackground(string? rawRole) =>
        MapToSeededRoleGroup(rawRole) switch
        {
            "merchant" => SceneArtCatalog.BackgroundForKey("market"),
            "general" => SceneArtCatalog.BackgroundForKey("arena"),
            "scholar" => SceneArtCatalog.BackgroundForKey("hall"),
            _ => SceneArtCatalog.BackgroundForKey("street")
        };

    private static string ResolveCitadelHan(string? code) => (code ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "luoyang" or "lac_duong" => "洛陽",
        "changan" or "truong_an" => "長安",
        "xuchang" or "hu_xuong" => "許昌",
        "chengdu" or "thanh_do" => "成都",
        "jianye" or "kien_nghiep" => "建業",
        "ye" or "nghiep_thanh" => "鄴城",
        "xiangyang" or "tuong_duong" => "襄陽",
        _ => "城池"
    };

    private static string SummarizeFeedback(string raw, string fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return ClampLine90(fallback);

        var firstLine = raw
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? fallback;
        return ClampLine90(firstLine);
    }

    private static string ClampLine90(string text)
    {
        var clean = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return clean.Length <= 90 ? clean : clean[..89].TrimEnd() + "…";
    }

    private static string Clamp40(string text)
    {
        var clean = text.Trim();
        return clean.Length <= 40 ? clean : clean[..39].TrimEnd() + "…";
    }
}
