using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;
using SimT.Contracts.Time;

namespace SimT.Host.Wpf;

public partial class MainWindow
{
    public bool TryOpenFeatureScene(string screenId, string? recommendation = null) => screenId switch
    {
        "world.map" => TryOpenMapScene(recommendation),
        "social.network" => TryOpenSocialScene(recommendation),
        "market.stall" => TryOpenMarketScene(recommendation),
        "npc.directory" => TryOpenDossierScene(recommendation),
        _ => false
    };

    public bool TryOpenSocialScene(string? recommendation = null, long selectedNpcId = 0, long worldNpcId = 0, string worldSearch = "")
    {
        if (!SceneOverlay.IsAvailable ||
            !_kernel.Capabilities.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social) || social is null ||
            !_kernel.Capabilities.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var reg) || reg is null ||
            !_kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map) || map is null ||
            !_kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var playerState) || playerState is null)
            return false;

        var currentCityId = playerState.CurrentCityId;
        var currentCityName = map.Find(currentCityId)?.NameVi ?? "Lạc Dương";
        _kernel.Capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs);
        _kernel.Capabilities.TryGet<IPlayerReputation>(CapabilityIds.PlayerReputation, out var rep);
        _kernel.Capabilities.TryGet<IBag>(CapabilityIds.PlayerBag, out var bag);
        _kernel.Capabilities.TryGet<IItemCatalog>(CapabilityIds.ItemCatalog, out var items);

        var relMap = social.AllRelationships().ToDictionary(r => r.NpcId);
        var retainers = social.GetRetainers();
        var swornIds = social.GetSwornBrothers().Select(s => s.NpcId).ToHashSet();

        var candidateNpcs = reg.FindByCitadel(currentCityId)
            .OrderByDescending(n => relMap.TryGetValue(n.Id, out var r) ? r.Affinity + 1000 : 0)
            .Take(8).ToList();
        foreach (var r in social.AllRelationships().OrderByDescending(x => x.Affinity))
        {
            if (candidateNpcs.Count >= 8) break;
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
            var req = social.GetRecruitRequirement(npc.Id);
            var rankCode = req.Rank.ToString();
            var (_, facePath, _) = SceneArtCatalog.ResolveNpcArt(npc);
            var firstSkill = npc.Skills.FirstOrDefault();
            var tierTitle = aff >= 80 ? "Tri kỷ" : aff >= 60 ? "Thân thiết" : aff >= 35 ? "Quen biết" : "Sơ giao";
            var tierKey = aff >= 80 ? "sworn" : aff >= 60 ? "trusted" : aff >= 35 ? "friend" : "known";
            var angle = (i * (360.0 / Math.Max(1, candidateNpcs.Count)) - 90.0 + 360.0) % 360.0;

            nodeViews.Add(new SocialNodeView(
                npc.Id, npc.Code, RoleTitleVi.ShortName(npc.NameVi), RoleTitleVi.Format(npc.Role), rankCode,
                map.Find(npc.CitadelId)?.NameVi ?? currentCityName,
                npc.CitadelId == currentCityId, aff, rel?.Trust ?? 0, req.RequiredAffinity, req.SigningFee,
                tierKey, tierTitle, facePath, SceneArtCatalog.ResolveProfileArt(npc),
                firstSkill?.Code ?? string.Empty, firstSkill?.NameVi ?? "—", rankCode,
                firstSkill?.EffectSummary ?? "—",
                firstSkill is not null && learnedCodes.Contains(firstSkill.Code),
                $"{req.SigningFee} quan", req.CanRecruit,
                retainers.Any(r => r.NpcId == npc.Id), swornIds.Contains(npc.Id), angle));
        }

        var skillViews = SocialScene.BuildSkillViews(reg, progresses, candidateNpcs);
        var letterViews = social.GetLetters(limit: 12).Select(l =>
        {
            var (_, face, _) = reg.Find(l.SenderNpcId) is { } sender
                ? SceneArtCatalog.ResolveNpcArt(sender)
                : ("", "art/portraits/scholar/face.webp", "");
            var giftText = l.GiftCopper > 0
                ? $"{l.GiftCopper} quan"
                : l.GiftItemQuantity > 0 ? $"{l.GiftItemQuantity} {l.GiftItemNameVi}" : "Thư thăm hỏi";
            var cleanTitle = l.TitleVi.Length > 2 && !char.IsLetterOrDigit(l.TitleVi[0]) &&
                             l.TitleVi.IndexOf(' ') is var sp and > 0 and <= 4
                ? l.TitleVi[(sp + 1)..].Trim() : l.TitleVi;
            return new SocialLetterView(
                l.Id, RoleTitleVi.ShortName(l.SenderNameVi), face, GameDateCodec.ShortVi(l.SentDate),
                cleanTitle, l.ContentVi, giftText, l.IsRead,
                !l.IsClaimed && (l.GiftCopper > 0 || l.GiftItemQuantity > 0));
        }).ToList();

        var retainerViews = retainers.Select(r =>
        {
            var npc = reg.Find(r.NpcId);
            var (_, face, _) = npc is not null
                ? SceneArtCatalog.ResolveNpcArt(npc)
                : ("", "art/portraits/general/face.webp", "");
            var bonus = r.RoleAssigned switch
            {
                "accountant" => "Giảm chi phí sổ sách & giao thương",
                "bodyguard" => "Hộ vệ an toàn trên thương lộ",
                "advisor" => "Tăng hiệu quả mưu lược & đàm đạo",
                _ => "Quản lý sản nghiệp & kho bãi"
            };
            return new SocialRetainerView(
                r.NpcId, RoleTitleVi.ShortName(npc?.NameVi ?? $"#{r.NpcId}"),
                RoleTitleVi.Format(r.RoleAssigned), social.GetNpcRank(r.NpcId).ToString(),
                face, r.MonthlyStipend, bonus, r.Loyalty);
        }).ToList();

        var giftItems = new List<SocialGiftItemView>();
        if (bag is not null && items is not null)
            foreach (var row in bag.Rows(currentCityId))
                if (row.Quantity > 0 && items.Find(row.ItemId) is { } item)
                    giftItems.Add(new SocialGiftItemView(item.Id, item.NameVi, row.Quantity));

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
            nodeViews, skillViews, letterViews, retainerViews, giftItems,
            _kernel.Capabilities.TryGet<INpcBonds>(INpcBonds.CapabilityId, out var bonds) && bonds is not null
                ? NpcBondScene.Build(bonds, reg, worldNpcId > 0 ? worldNpcId : selectedNpcId > 0 ? selectedNpcId : candidateNpcs[0].Id, worldSearch) : null));
    }

    public bool TryOpenMarketScene(string? recommendation = null, long selectedItemId = 0)
    {
        if (!SceneOverlay.IsAvailable ||
            !_kernel.Capabilities.TryGet<IMarketTrade>(CapabilityIds.MarketTrade, out var trade) || trade is null ||
            !_kernel.Capabilities.TryGet<IItemCatalog>(CapabilityIds.ItemCatalog, out var items) || items is null ||
            !_kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map) || map is null ||
            !_kernel.Capabilities.TryGet<IBag>(CapabilityIds.PlayerBag, out var bag) || bag is null ||
            !_kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var playerState) || playerState is null)
            return false;

        var cityId = playerState.CurrentCityId;
        var city = map.Find(cityId);
        var cityName = city?.NameVi ?? "Lạc Dương";
        var regionVi = RoleTitleVi.FormatRegion(city?.Region);
        var (seasonVi, _) = ResolveSeasonAndWeather(_kernel.Today.Month);

        var otherCities = map.Citadels
            .Where(c => c.Id != cityId)
            .Select(c => (Citadel: c, Days: map.QuoteTravel(cityId, c.Id, TravelMode.Walk).BaseDays))
            .ToList();
        var itemViews = new List<MarketItemView>(items.All.Count);
        string? bestArbitrageTip = null;
        long bestArbitrageGain = 0;

        foreach (var item in items.All)
        {
            var buyPrice = trade.QuoteBuy(cityId, item.Id);
            var sellPrice = trade.QuoteSell(cityId, item.Id);
            var stock = trade.StallStock(cityId, item.Id);
            var held = bag.Quantity(cityId, item.Id);
            var hist = trade.PriceHistory(cityId, item.Id, 10).ToList();
            if (hist.Count == 0 || hist[^1] != buyPrice)
                hist.Add(buyPrice);

            var bestCities = otherCities
                .Select(x =>
                {
                    var estSell = trade.QuoteSell(x.Citadel.Id, item.Id);
                    return new MarketArbitrageView(x.Citadel.Id, x.Citadel.NameVi, x.Days, estSell, estSell - buyPrice);
                })
                .OrderByDescending(x => x.DiffPerUnit).Take(3).ToList();

            if (bestCities.Count > 0 && bestCities[0].DiffPerUnit > bestArbitrageGain && stock > 0)
            {
                bestArbitrageGain = bestCities[0].DiffPerUnit;
                var carryQty = Math.Min(stock, TravelLuggage.WalkCapacity);
                var walkProfit = carryQty * bestCities[0].DiffPerUnit;
                bestArbitrageTip = $"{item.NameVi} mua {buyPrice} quan, sang {bestCities[0].CityNameVi} bán ~{bestCities[0].EstimatedSellPrice} quan (+{bestCities[0].DiffPerUnit} quan/đv; đi bộ mang tối đa {carryQty} đv lãi ~{walkProfit} quan, chở nhiều dùng Thương Đoàn).";
            }

            itemViews.Add(new MarketItemView(
                item.Id, item.Code, item.NameVi, ItemHanziFor(item.Category, item.NameVi),
                RoleTitleVi.FormatItemCategory(item.Category), regionVi,
                buyPrice, sellPrice, item.BasePrice, stock, held, item.ShelfLifeDays,
                string.Empty, hist, bestCities));
        }

        var recText = bestArbitrageTip ?? recommendation ?? $"Tại chợ {cityName}, đi bộ chỉ mang tối đa {TravelLuggage.WalkCapacity} đơn vị; muốn chở hàng số lượng lớn hãy dùng Thương Đoàn.";
        return SceneOverlay.ShowMarketScene(new MarketScene(
            cityId, cityName, regionVi, seasonVi, city?.PriceFactorPermille ?? 1000,
            GameDateCodec.ShortVi(_kernel.Today), _kernel.Ledger.Liquid,
            bag.TotalQuantity(cityId), 200, "art/backgrounds/market.png",
            recText, selectedItemId, itemViews));
    }

    public static string ItemHanziFor(string category, string nameVi) =>
        RoleTitleVi.FormatItemHanzi(category, nameVi);

    private static readonly string[] FeaturedProfileCodes =
        ["npc_0223", "npc_0205", "npc_0206", "npc_0207", "npc_0221", "npc_0222", "npc_0224", "npc_0225"];

    public bool TryOpenDossierScene(string? recommendation = null, long selectedNpcId = 0)
    {
        if (!SceneOverlay.IsAvailable ||
            !_kernel.Capabilities.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var reg) || reg is null ||
            !_kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map) || map is null ||
            !_kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var playerState) || playerState is null)
            return false;

        var cityId = playerState.CurrentCityId;
        var cityName = map.Find(cityId)?.NameVi ?? "Lạc Dương";
        _kernel.Capabilities.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social);
        _kernel.Capabilities.TryGet<ISocietyChronicle>(CapabilityIds.SocietyChronicle, out var society);
        _kernel.Capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs);

        var relMap = social?.AllRelationships().ToDictionary(r => r.NpcId) ?? new Dictionary<long, RelationshipDef>();
        var ordered = new List<NpcDef>();
        var seen = new HashSet<long>();

        foreach (var code in FeaturedProfileCodes)
            if (reg.FindByCode(code) is { } pNpc && seen.Add(pNpc.Id)) ordered.Add(pNpc);
        foreach (var lNpc in reg.All.Where(n => n.CurrentCitadelId(society) == cityId))
            if (seen.Add(lNpc.Id)) ordered.Add(lNpc);
        foreach (var aNpc in reg.All)
            if (seen.Add(aNpc.Id)) ordered.Add(aNpc);

        var views = ordered.Select(npc =>
        {
            relMap.TryGetValue(npc.Id, out var rel);
            var aff = rel?.Affinity ?? 0;
            var (_, face, _) = SceneArtCatalog.ResolveNpcArt(npc);
            var profile = SceneArtCatalog.ResolveProfileArt(npc);
            var sk = npc.Skills.FirstOrDefault();
            var a = npc.Attributes;
            var isDeceased = npc.IsDeceased(society);
            var tier = isDeceased
                ? "Đã mất"
                : aff >= 80 ? "Tri kỷ" : aff >= 60 ? "Thân thiết" : aff >= 35 ? "Quen biết" : rel is not null ? "Đã gặp" : "Chưa gặp";
            var npcCityId = npc.CurrentCitadelId(society);
            var isRetainer = social?.GetRetainer(npc.Id) is not null;
            var isLocal = !isDeceased && (npcCityId == cityId || isRetainer);
            var bioText = string.IsNullOrWhiteSpace(npc.BioVi)
                ? "Nhân sĩ danh tiếng thời Tam Quốc, hành trạng được dân gian ca tụng."
                : npc.BioVi;
            if (isDeceased && !bioText.Contains("Đã mất", StringComparison.OrdinalIgnoreCase))
                bioText = $"[Đã mất] {bioText}";
            return new DossierNpcView(
                npc.Id, npc.Code, npc.NameVi, npc.NameHan ?? string.Empty, npc.StyleNameVi ?? "—",
                RoleTitleVi.FormatPersonality(npc.Personality), "Quần Hùng", RoleTitleVi.Format(npc.Role),
                isDeceased ? $"{map.Find(npcCityId)?.NameVi ?? cityName} (Đã mất)" : map.Find(npcCityId)?.NameVi ?? cityName,
                isLocal, rel is not null,
                aff, tier, face, profile, sk?.NameVi ?? "Nội Chính Tinh Thông", "A",
                sk?.EffectSummary ?? "Gia tăng hiệu suất công việc và uy vọng tại thành trì.",
                bioText,
                a.War, a.Ldr, a.Int, a.Pol, a.Dip, a.Com, a.Log, a.Cra,
                social?.GetVisibleTraits(npc.Id));
        }).ToList();

        var prc = attrs?.EffectivePrc ?? 50;
        var rec = recommendation ?? $"Đang tại {cityName}: chọn danh sĩ để xem ảnh chân dung riêng, biểu đồ bát diện năng lực, bái phỏng hoặc so tài.";
        return SceneOverlay.ShowDossierScene(new DossierScene(
            cityName, relMap.Count, reg.All.Count, $"{prc} điểm", rec, selectedNpcId, views));
    }

    private async void HandleMapSceneMessage(string type, string id, MapScene _)
    {
        if (type != "choice" || !id.StartsWith("travel:", StringComparison.Ordinal)) return;
        await ExecuteScreenCommandAsync(id, "world.map");
        TryOpenMapScene(StatusText.Text);
    }

    private async void HandleSocialSceneMessage(string type, string id, SocialScene _)
    {
        if (type == "navigate" && id.StartsWith("npc:", StringComparison.Ordinal) && long.TryParse(id["npc:".Length..], out var talkNpcId))
        {
            TryOpenTalkScene(talkNpcId);
            return;
        }
        if (type != "choice") return;
        if (id.StartsWith("world:", StringComparison.Ordinal) && long.TryParse(id[6..], out var center))
        { TryOpenSocialScene(worldNpcId: center); return; }
        if (id.StartsWith("search:", StringComparison.Ordinal))
        { TryOpenSocialScene(worldNpcId: _.World?.CenterId ?? 0, worldSearch: id[7..]); return; }
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

    private bool TryBlockRemoteDossierNpc(long npcId)
    {
        if (!_kernel.Capabilities.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var reg) ||
            reg?.Find(npcId) is not { } npc ||
            !_kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var ps) ||
            ps is null)
            return false;

        _kernel.Capabilities.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social);
        if (social?.GetRetainer(npc.Id) is not null)
            return false;

        var npcCityId = npc.CurrentCitadelId(_kernel.Capabilities);
        if (npcCityId == ps.CurrentCityId)
            return false;

        _kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map);
        var cityName = map?.Find(npcCityId)?.NameVi ?? "thành khác";
        var msg = $"{RoleTitleVi.ShortName(npc.NameVi)} hiện đang ở {cityName}, chưa thể tương tác từ xa.";
        StatusText.Text = $"Đang ở {cityName}";
        TryOpenDossierScene(msg, npc.Id);
        return true;
    }

    private async void HandleDossierSceneMessage(string type, string id, DossierScene _)
    {
        if (type == "navigate" && id.StartsWith("npc:", StringComparison.Ordinal) && long.TryParse(id["npc:".Length..], out var talkId))
        {
            if (!TryBlockRemoteDossierNpc(talkId))
                TryOpenTalkScene(talkId);
            return;
        }
        if (type != "choice") return;
        if (id.StartsWith("visit:", StringComparison.Ordinal) && long.TryParse(id["visit:".Length..], out var visitId))
        {
            if (TryBlockRemoteDossierNpc(visitId)) return;
            await ExecuteScreenCommandAsync($"chat:{visitId}", "social.network");
            TryOpenDossierScene(selectedNpcId: visitId);
        }
        else if (id.StartsWith("spar:", StringComparison.Ordinal) && long.TryParse(id["spar:".Length..], out var sparId) &&
                 _kernel.Capabilities.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var reg) && reg?.Find(sparId) is { } npc)
        {
            if (TryBlockRemoteDossierNpc(sparId)) return;
            var cityId = _kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var ps) && ps is not null
                ? ps.CurrentCityId
                : npc.CurrentCitadelId(_kernel.Capabilities);
            var city = _kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map)
                ? map?.Find(cityId)?.NameVi ?? "Lạc Dương"
                : "Lạc Dương";
            TryOpenDuelScene(npc, city);
        }
    }
}
