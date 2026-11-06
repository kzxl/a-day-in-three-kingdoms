using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Character;

internal sealed class DialogueService : IDialogueModule
{
    private readonly ModuleContext _context;

    public DialogueService(ModuleContext context)
    {
        _context = context;
    }

    public IReadOnlyList<string> GetLinesForNpc(long npcId, string relationTierKey)
    {
        var npcs = _context.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var npc = npcs.Find(npcId) ?? npcs.All.First();
        var tier = NormalizeTierKey(relationTierKey);

        var lines = QueryLines("npc", npc.Code, tier);
        if (lines.Count == 0)
        {
            var roleGroup = MapToSeededRoleGroup(SceneArtCatalog.ClassifyArchetype(npc));
            lines = QueryLines("role", roleGroup, tier);
        }

        if (lines.Count == 0)
        {
            lines = QueryLines("role", "commoner", "stranger");
        }

        return lines.Select(ClampLine90).ToList();
    }

    public TalkScene BuildTalkScene(long npcId, string? feedbackVi = null)
    {
        var npcs = _context.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var map = _context.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);
        var playerState = _context.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var npc = npcs.Find(npcId) ?? npcs.All.First();
        var citadel = map.Find(npc.CitadelId);
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
        var choices = BuildChoices(npc, playerState.CurrentCityId);

        var candidateNpcs = new List<NpcDef> { npc };
        if (npcs.FindByCode("npc_0003") is { } gy && candidateNpcs.All(x => x.Id != gy.Id)) candidateNpcs.Add(gy);
        if (npcs.FindByCode("npc_0002") is { } zf && candidateNpcs.All(x => x.Id != zf.Id)) candidateNpcs.Add(zf);
        foreach (var local in npcs.FindByCitadel(npc.CitadelId).OrderBy(n => n.Id))
        {
            if (candidateNpcs.All(x => x.Id != local.Id)) candidateNpcs.Add(local);
        }

        var nearby = candidateNpcs
            .Take(5)
            .Select(n =>
            {
                var art = SceneArtCatalog.ResolveNpcArt(n);
                var shortName = n.NameVi.Contains('(')
                    ? n.NameVi[..n.NameVi.IndexOf('(')].Trim()
                    : n.NameVi;
                return new SceneNpcThumb(n.Id, shortName, art.PortraitFace, n.Id == npc.Id);
            })
            .ToList();

        return new TalkScene(
            SceneId: $"talk:{npc.Id}",
            NpcId: npc.Id,
            NpcCode: npc.Code,
            NameVi: npc.NameVi,
            NameHan: string.IsNullOrWhiteSpace(npc.NameHan) ? "漢" : npc.NameHan,
            StyleNameVi: npc.StyleNameVi,
            RoleVi: RoleTitleVi.Format(npc.Role),
            PersonalityVi: RoleTitleVi.FormatPersonality(npc.Personality),
            LocationNameVi: locationVi,
            LocationHan: locationHan,
            Affinity: affinity,
            RelationTierVi: tierVi,
            PortraitFull: portraitFull,
            PortraitFace: portraitFace,
            BackgroundPath: bgPath,
            Lines: lines,
            Choices: choices,
            NearbyNpcs: nearby,
            FeedbackVi: string.IsNullOrWhiteSpace(feedbackVi) ? null : ClampLine90(feedbackVi!),
            BlinkPath: blinkPath,
            TalkPath: talkPath);
    }

    public TalkChoiceOutcome HandleChoice(long npcId, string choiceId)
    {
        var npcs = _context.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var npc = npcs.Find(npcId);
        if (npc is null)
            return new TalkChoiceOutcome(false, "Không tìm thấy nhân vật.", CloseScene: true);

        RecordVisit(npc.Id);

        switch (choiceId.Trim().ToLowerInvariant())
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
                var bag = _context.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
                var firstRow = bag.Rows(playerState.CurrentCityId).FirstOrDefault(r => r.Quantity > 0);
                if (firstRow is null ||
                    !_context.Capabilities.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social) ||
                    social is null)
                {
                    var noGift = "Trong hành trang tại thành này hiện chưa có lễ vật.";
                    return new TalkChoiceOutcome(false, noGift, UpdatedScene: BuildTalkScene(npc.Id, noGift));
                }

                string msg;
                try
                {
                    var raw = social.GiftNpc(npc.Id, firstRow.ItemId);
                    msg = SummarizeFeedback(raw, $"{npc.NameVi} vui vẻ nhận lễ vật bạn trao tặng.");
                }
                catch (SimException ex)
                {
                    msg = ClampLine90(ex.Message);
                }

                var updated = BuildTalkScene(npc.Id, msg);
                return new TalkChoiceOutcome(true, msg, UpdatedScene: updated);
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
            var firstItem = bag.Rows(currentCityId).FirstOrDefault(r => r.Quantity > 0);
            if (firstItem is not null)
            {
                var items = _context.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);
                var itemName = items.Find(firstItem.ItemId)?.NameVi ?? "Lễ vật";
                list.Add(new SceneChoice("gift", "Tặng quà kết giao", Clamp40(itemName)));
            }
        }

        if (CanDuelNpc(npc))
        {
            list.Add(new SceneChoice("duel", "Đấu tay đôi", $"Võ lực {npc.Attributes.War}"));
        }

        list.Add(new SceneChoice("bye", "Tạm biệt", "Rời cuộc trò chuyện"));
        return list;
    }

    private bool CanDuelNpc(NpcDef npc)
    {
        if (!_context.Capabilities.TryGet<IMilitaryCommand>(CapabilityIds.MilitaryCommand, out var mil) || mil is null)
            return false;

        var playerState = _context.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var isVisitingHero = npc.Code is "npc_0002" or "npc_0003";
        if ((!isVisitingHero && playerState.CurrentCityId != npc.CitadelId) || npc.Attributes.War < 30)
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
        var map = _context.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);
        var cityName = map.Find(npc.CitadelId)?.NameVi ?? "thành này";
        if (!_context.Capabilities.TryGet<IMarketTrade>(CapabilityIds.MarketTrade, out var trade) || trade is null)
            return ClampLine90($"Chợ {cityName} hôm nay buôn bán bình ổn, lương thảo dồi dào.");

        var items = _context.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);
        var quotes = items.All
            .Take(3)
            .Select(i => $"{i.NameVi} {trade.QuoteBuy(npc.CitadelId, i.Id)} văn")
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
