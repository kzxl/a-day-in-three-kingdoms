using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;
using SimT.Contracts.Time;

namespace SimT.Host.Wpf;

public partial class MainWindow
{
    private void TryOpenFeatureSceneForCurrentScreen()
    {
        switch (_screenId)
        {
            case "world.map":
                TryOpenMapScene();
                break;
            case "social.network":
                TryOpenSocialScene();
                break;
            case "market.stall":
                TryOpenMarketScene();
                break;
            case "npc.directory":
                TryOpenDossierScene();
                break;
        }
    }

    public bool TryOpenMapScene(string? recommendation = null)
    {
        if (!SceneOverlay.IsAvailable ||
            !_kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map) || map is null ||
            !_kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var playerState) || playerState is null)
        {
            return false;
        }

        var currentCityId = playerState.CurrentCityId;
        var currentCity = map.Find(currentCityId);
        var currentCityName = currentCity?.NameVi ?? "Lạc Dương";
        var fatigue = _kernel.Capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) && attrs is not null
            ? attrs.Vitals.Fatigue
            : 0;

        var cities = new List<MapCityView>(map.Citadels.Count);
        foreach (var c in map.Citadels)
        {
            var eco = map.GetEcosystem(c.Id);
            var quotes = new List<MapTravelQuoteView>();
            if (c.Id != currentCityId)
            {
                var qWalk = map.QuoteTravel(currentCityId, c.Id, TravelMode.Walk);
                var qHorse = map.QuoteTravel(currentCityId, c.Id, TravelMode.Horse);
                var qCaravan = map.QuoteTravel(currentCityId, c.Id, TravelMode.Caravan);
                quotes.Add(new MapTravelQuoteView("walk", "Bộ hành", "Không tốn tiền, mệt nhất", qWalk.BaseDays, qWalk.ActualDays, qWalk.TravelFee, qWalk.RationsNeeded, qWalk.FatigueExpected, qWalk.PathNodeIds));
                quotes.Add(new MapTravelQuoteView("horse", "Dịch mã", "Nhanh nhất", qHorse.BaseDays, qHorse.ActualDays, qHorse.TravelFee, qHorse.RationsNeeded, qHorse.FatigueExpected, qHorse.PathNodeIds));
                quotes.Add(new MapTravelQuoteView("caravan", "Tiêu cục", "Không gặp cướp đường", qCaravan.BaseDays, qCaravan.ActualDays, qCaravan.TravelFee, qCaravan.RationsNeeded, qCaravan.FatigueExpected, qCaravan.PathNodeIds));
            }

            cities.Add(new MapCityView(
                c.Id, c.Code, c.NameVi, RoleTitleVi.FormatRegion(c.Region),
                eco.MapX, eco.MapY, c.PriceFactorPermille, eco.DangerLevel,
                eco.BiomeNameVi, eco.ClimateVi, eco.TopographyVi, eco.PrimarySpecialtyVi, eco.SecondarySpecialtyVi, quotes));
        }

        var routes = map.Routes.Select(r => new MapRouteView(r.FromCitadelId, r.ToCitadelId, r.Days)).ToList();
        var recText = recommendation ??
                      $"Từ {currentCityName}, bấm một thành trên bản đồ để xem lộ trình ngắn nhất, chi phí và sản vật trước khi lên đường.";

        return SceneOverlay.ShowMapScene(new MapScene(
            currentCityId, currentCityName, GameDateCodec.ShortVi(_kernel.Today),
            _kernel.Ledger.Liquid, fatigue, "art/map/thien_ha.webp",
            SceneArtCatalog.PlayerPortraitFace, recText, cities, routes));
    }

    public bool TryOpenSocialScene(string? recommendation = null, long selectedNpcId = 0)
    {
        if (!SceneOverlay.IsAvailable ||
            !_kernel.Capabilities.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social) || social is null ||
            !_kernel.Capabilities.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var reg) || reg is null ||
            !_kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map) || map is null ||
            !_kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var playerState) || playerState is null)
        {
            return false;
        }

        var currentCityId = playerState.CurrentCityId;
        var currentCityName = map.Find(currentCityId)?.NameVi ?? "Lạc Dương";
        _kernel.Capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs);
        _kernel.Capabilities.TryGet<IPlayerReputation>(CapabilityIds.PlayerReputation, out var rep);
        _kernel.Capabilities.TryGet<IBag>(CapabilityIds.PlayerBag, out var bag);
        _kernel.Capabilities.TryGet<IItemCatalog>(CapabilityIds.ItemCatalog, out var items);

        var relMap = social.AllRelationships().ToDictionary(r => r.NpcId);
        var retainers = social.GetRetainers();
        var swornIds = social.GetSwornBrothers().Select(s => s.NpcId).ToHashSet();

        var candidateNpcs = reg.FindByCitadel(currentCityId).ToList();
        foreach (var r in social.AllRelationships().OrderByDescending(x => x.Affinity))
        {
            if (candidateNpcs.Count >= 10) break;
            if (candidateNpcs.All(n => n.Id != r.NpcId) && reg.Find(r.NpcId) is { } metNpc)
                candidateNpcs.Add(metNpc);
        }
        if (candidateNpcs.Count == 0)
            candidateNpcs.AddRange(reg.All.Take(8));

        var progresses = attrs?.SkillProgresses ?? Array.Empty<PlayerSkillProgressDef>();
        var learnedCodes = progresses.Select(p => p.SkillCode).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var nodeViews = new List<SocialNodeView>(candidateNpcs.Count);
        for (var i = 0; i < candidateNpcs.Count; i++)
        {
            var npc = candidateNpcs[i];
            relMap.TryGetValue(npc.Id, out var rel);
            var aff = rel?.Affinity ?? 0;
            var trust = rel?.Trust ?? 0;
            var req = social.GetRecruitRequirement(npc.Id);
            var rankCode = req.Rank.ToString();
            var (_, facePath, _) = SceneArtCatalog.ResolveNpcArt(npc);
            var profilePath = SceneArtCatalog.ResolveProfileArt(npc);
            var firstSkill = npc.Skills.FirstOrDefault();
            var tierTitle = aff >= 80 ? "Tri kỷ" : aff >= 60 ? "Thân thiết" : aff >= 35 ? "Quen biết" : "Sơ giao";
            var tierKey = aff >= 80 ? "sworn" : aff >= 60 ? "trusted" : aff >= 35 ? "friend" : "known";
            var angle = (i * (360.0 / Math.Max(1, candidateNpcs.Count)) + 25.0) % 360.0;

            nodeViews.Add(new SocialNodeView(
                npc.Id, npc.Code, npc.NameVi, RoleTitleVi.Format(npc.Role), rankCode,
                map.Find(npc.CitadelId)?.NameVi ?? currentCityName,
                npc.CitadelId == currentCityId, aff, trust, req.RequiredAffinity, req.SigningFee,
                tierKey, tierTitle, facePath, profilePath,
                firstSkill?.Code ?? string.Empty, firstSkill?.NameVi ?? "—", rankCode,
                firstSkill?.EffectSummary ?? "—",
                firstSkill is not null && learnedCodes.Contains(firstSkill.Code),
                $"{req.SigningFee} quan", req.CanRecruit,
                retainers.Any(r => r.NpcId == npc.Id), swornIds.Contains(npc.Id), angle));
        }

        var skillViews = BuildSocialSkillViews(reg, progresses);
        var letterViews = social.GetLetters(limit: 12).Select(l =>
        {
            var sender = reg.Find(l.SenderNpcId);
            var (_, face, _) = sender is not null ? SceneArtCatalog.ResolveNpcArt(sender) : ("", "art/portraits/scholar/face.webp", "");
            var giftText = l.GiftCopper > 0 ? $"{l.GiftCopper} quan" : l.GiftItemQuantity > 0 ? $"{l.GiftItemQuantity} {l.GiftItemNameVi}" : "Thư thăm hỏi";
            return new SocialLetterView(l.Id, l.SenderNameVi, face, GameDateCodec.ShortVi(l.SentDate), l.TitleVi, l.ContentVi, giftText, l.IsRead, !l.IsClaimed && (l.GiftCopper > 0 || l.GiftItemQuantity > 0));
        }).ToList();

        var retainerViews = retainers.Select(r =>
        {
            var npc = reg.Find(r.NpcId);
            var (_, face, _) = npc is not null ? SceneArtCatalog.ResolveNpcArt(npc) : ("", "art/portraits/general/face.webp", "");
            var rank = social.GetNpcRank(r.NpcId).ToString();
            return new SocialRetainerView(r.NpcId, npc?.NameVi ?? $"#{r.NpcId}", r.RoleAssigned, rank, face, r.MonthlyStipend, $"Trung thành {r.Loyalty}/100", r.Loyalty);
        }).ToList();

        var giftItems = new List<SocialGiftItemView>();
        if (bag is not null && items is not null)
        {
            foreach (var row in bag.Rows(currentCityId))
            {
                if (row.Quantity > 0 && items.Find(row.ItemId) is { } item)
                    giftItems.Add(new SocialGiftItemView(item.Id, item.NameVi, row.Quantity));
            }
        }

        var bestGapNode = nodeViews
            .Where(n => !n.IsRetainer && n.RequiredAffinity > n.Affinity)
            .OrderBy(n => n.RequiredAffinity - n.Affinity)
            .FirstOrDefault();
        var recText = recommendation ?? (bestGapNode is not null
            ? $"Đàm đạo với {bestGapNode.NameVi}: còn {bestGapNode.RequiredAffinity - bestGapNode.Affinity} hảo cảm nữa là đủ mốc chiêu mộ."
            : $"Đàm đạo và tặng quà cho danh sĩ tại {currentCityName} để lĩnh hội tuyệt kỹ.");

        return SceneOverlay.ShowSocialScene(new SocialScene(
            currentCityName, GameDateCodec.ShortVi(_kernel.Today),
            _kernel.Ledger.Liquid, rep?.Standing.Fame ?? 0, attrs?.Vitals.Fatigue ?? 0,
            SceneArtCatalog.PlayerPortraitFace, recText, selectedNpcId,
            nodeViews, skillViews, letterViews, retainerViews, giftItems));
    }

    private static IReadOnlyList<SocialSkillView> BuildSocialSkillViews(INpcRegistry reg, IReadOnlyList<PlayerSkillProgressDef> progresses)
    {
        var list = new List<SocialSkillView>();
        var skillOwners = new Dictionary<string, (NpcDef Npc, NpcSkillDef Skill)>(StringComparer.OrdinalIgnoreCase);
        foreach (var npc in reg.All)
            foreach (var sk in npc.Skills)
                skillOwners.TryAdd(sk.Code, (npc, sk));

        foreach (var p in progresses)
        {
            skillOwners.TryGetValue(p.SkillCode, out var owner);
            var name = owner.Skill?.NameVi ?? p.SkillCode;
            var rank = p.Tier switch { >= 5 => "S", 4 => "A", 3 => "B", _ => "C" };
            list.Add(new SocialSkillView(
                p.SkillCode, name, name[..1], rank, owner.Npc?.Id ?? 0, owner.Npc?.NameVi ?? "Danh sư",
                owner.Skill?.EffectSummary ?? "Gia tăng năng lực", "Đã lĩnh hội",
                true, p.IsEquipped, p.EquippedSlot ?? 0, p.Level, $"Cấp {p.Level}"));
        }

        foreach (var (code, (npc, sk)) in skillOwners.Take(12))
        {
            if (list.Any(x => string.Equals(x.SkillCode, code, StringComparison.OrdinalIgnoreCase)))
                continue;
            var rank = npc.Attributes.War >= 92 || npc.Attributes.Int >= 92 ? "S"
                : npc.Attributes.War >= 82 || npc.Attributes.Int >= 82 ? "A" : "B";
            var reqAff = rank == "S" ? 80 : rank == "A" ? 60 : 45;
            list.Add(new SocialSkillView(
                code, sk.NameVi, sk.NameVi[..1], rank, npc.Id, npc.NameVi,
                sk.EffectSummary, $"Học từ {npc.NameVi}, cần hảo cảm {reqAff}",
                false, false, 0, 1, "Chưa lĩnh hội"));
            if (list.Count >= 9) break;
        }

        return list;
    }

    public bool TryOpenMarketScene(string? recommendation = null, long selectedItemId = 0)
    {
        if (!SceneOverlay.IsAvailable ||
            !_kernel.Capabilities.TryGet<IMarketTrade>(CapabilityIds.MarketTrade, out var trade) || trade is null ||
            !_kernel.Capabilities.TryGet<IItemCatalog>(CapabilityIds.ItemCatalog, out var items) || items is null ||
            !_kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map) || map is null ||
            !_kernel.Capabilities.TryGet<IBag>(CapabilityIds.PlayerBag, out var bag) || bag is null ||
            !_kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var playerState) || playerState is null)
        {
            return false;
        }

        var cityId = playerState.CurrentCityId;
        var city = map.Find(cityId);
        var cityName = city?.NameVi ?? "Lạc Dương";
        var regionVi = RoleTitleVi.FormatRegion(city?.Region);
        var (seasonVi, _) = ResolveSeasonAndWeather(_kernel.Today.Month);

        var itemViews = new List<MarketItemView>();
        string? bestArbitrageTip = null;
        long bestArbitrageGain = 0;

        foreach (var item in items.All)
        {
            var buyPrice = trade.QuoteBuy(cityId, item.Id);
            var sellPrice = trade.QuoteSell(cityId, item.Id);
            var stock = trade.StallStock(cityId, item.Id);
            var held = bag.Quantity(cityId, item.Id);
            var hist = trade.PriceHistory(cityId, item.Id, 10);

            var bestCities = map.Citadels
                .Where(c => c.Id != cityId)
                .Select(c =>
                {
                    var estSell = trade.QuoteSell(c.Id, item.Id);
                    var diff = estSell - buyPrice;
                    var days = map.QuoteTravel(cityId, c.Id, TravelMode.Walk).BaseDays;
                    return new MarketArbitrageView(c.Id, c.NameVi, days, estSell, diff);
                })
                .OrderByDescending(x => x.DiffPerUnit)
                .Take(3)
                .ToList();

            if (bestCities.Count > 0 && bestCities[0].DiffPerUnit > bestArbitrageGain && stock > 0)
            {
                bestArbitrageGain = bestCities[0].DiffPerUnit;
                bestArbitrageTip = $"{item.NameVi} tại {cityName} mua {buyPrice} quan, chở sang {bestCities[0].CityNameVi} bán khoảng {bestCities[0].EstimatedSellPrice} quan (lãi +{bestCities[0].DiffPerUnit} quan/đv).";
            }

            itemViews.Add(new MarketItemView(
                item.Id, item.Code, item.NameVi, ItemHanziFor(item.Category, item.NameVi),
                RoleTitleVi.FormatItemCategory(item.Category), regionVi,
                buyPrice, sellPrice, item.BasePrice, stock, held, item.ShelfLifeDays,
                string.Empty, hist, bestCities));
        }

        var recText = bestArbitrageTip ?? recommendation ?? $"Tại chợ {cityName}, mua mặt hàng đang dồi dào tồn kho rồi chở sang thành giá cao để hưởng chênh lệch.";
        return SceneOverlay.ShowMarketScene(new MarketScene(
            cityId, cityName, regionVi, seasonVi, city?.PriceFactorPermille ?? 1000,
            GameDateCodec.ShortVi(_kernel.Today), _kernel.Ledger.Liquid,
            bag.TotalQuantity(cityId), 200, "art/backgrounds/market.png",
            recText, selectedItemId, itemViews));
    }

    private static string ItemHanziFor(string category, string nameVi)
    {
        var n = nameVi.ToLowerInvariant();
        if (n.Contains("gạo") || n.Contains("lúa")) return "米";
        if (n.Contains("muối")) return "鹽";
        if (n.Contains("rượu")) return "酒";
        if (n.Contains("trà")) return "茶";
        if (n.Contains("lụa") || n.Contains("gấm")) return "絲";
        if (n.Contains("sâm")) return "參";
        if (n.Contains("thuốc") || n.Contains("dược")) return "藥";
        if (n.Contains("sắt") || n.Contains("đồng")) return "鐵";
        return category.ToLowerInvariant() switch
        {
            "grain" => "米",
            "medicine" => "藥",
            "luxury" => "寶",
            "material" => "鐵",
            "seed" => "種",
            "tool" => "器",
            _ => "貨"
        };
    }

    public bool TryOpenDossierScene(string? recommendation = null, long selectedNpcId = 0) => false;

    private async void HandleMapSceneMessage(string type, string id, MapScene _)
    {
        if (type != "choice" || !id.StartsWith("travel:", StringComparison.Ordinal)) return;
        await ExecuteScreenCommandAsync(id, "world.map");
        TryOpenMapScene();
    }

    private async void HandleSocialSceneMessage(string type, string id, SocialScene _)
    {
        if (type == "navigate" && id.StartsWith("npc:", StringComparison.Ordinal) && long.TryParse(id["npc:".Length..], out var talkNpcId))
        {
            TryOpenTalkScene(talkNpcId);
            return;
        }
        if (type != "choice") return;
        await ExecuteScreenCommandAsync(id, "social.network");
        TryOpenSocialScene();
    }

    private async void HandleMarketSceneMessage(string type, string id, MarketScene _)
    {
        if (type != "choice") return;
        var parts = id.Split(':');
        var itemId = parts.Length >= 2 && long.TryParse(parts[1], out var parsedId) ? parsedId : 0;
        await ExecuteScreenCommandAsync(id, "market.stall");
        TryOpenMarketScene(selectedItemId: itemId);
    }

    private void HandleDossierSceneMessage(string type, string id, DossierScene _)
    {
    }
}
