using System.IO;
using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;
using SimT.Contracts.Time;

namespace SimT.Host.Wpf;

public partial class MainWindow
{
    private void InitializeSceneHost()
    {
        SceneOverlay.ClientMessageReceived += OnSceneClientMessageReceived;
        Loaded += async (_, _) =>
        {
            var scenesDir = Path.Combine(AppContext.BaseDirectory, "assets", "scenes");
            await SceneOverlay.InitializeWarmAsync(scenesDir, skipForSmoke: _isSmoke);
            if (_loaded && string.Equals(_screenId, CityHubScreenId, StringComparison.Ordinal))
                TryOpenCityScene();
        };
    }

    public bool TryOpenCityScene(CityEventCard? eventCard = null, IReadOnlyList<SceneChoice>? customChoices = null)
    {
        if (!SceneOverlay.IsAvailable ||
            !_kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var playerState) || playerState is null)
            return false;

        var cityId = playerState.CurrentCityId;
        var cityName = "Lạc Dương";
        var regionVi = "Trung Nguyên";
        IReadOnlyList<SublocationDef> sublocations = Array.Empty<SublocationDef>();
        if (_kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map) && map is not null)
        {
            if (map.Find(cityId) is { } cit)
            {
                cityName = cit.NameVi;
                regionVi = RoleTitleVi.FormatRegion(cit.Region);
            }
            sublocations = map.Sublocations(cityId);
        }

        var (seasonVi, weatherFx) = ResolveSeasonAndWeather(_kernel.Today.Month);
        IReadOnlyList<SceneChoice> choices = customChoices ??
        [
            new("briefing", "🏛️ Nhật trình", "Tiêu điểm & Báo cáo đầu ngày"),
            new("stroll", "Du ngoạn phố", "Tản bộ tìm kỳ ngộ"),
            new("stroll:log", "Sổ dạo phố", "Nhật ký kỳ ngộ"),
            new("map", "Xem bản đồ", "Hành trình liên thành")
        ];

        var scene = new CityScene(
            $"city:{cityId}", cityId, cityName, "城", regionVi,
            GameDateCodec.ShortVi(_kernel.Today), seasonVi, weatherFx,
            SceneArtCatalog.BackgroundForKey("city_street"),
            BuildCityHotspots(sublocations), BuildCityNpcs(cityId), eventCard, choices);

        if (!SceneOverlay.ShowCityScene(scene))
            return false;

        StatusText.Text = eventCard is null
            ? $"Đang ở {cityName} ({seasonVi}) · bấm địa điểm hoặc nhân sĩ trong thành để tương tác."
            : $"[{eventCard.LocationVi} · {eventCard.TitleVi}] {eventCard.RewardVi}";
        return true;
    }

    public void PresentStrollResultInCityScene(StrollResult result)
    {
        var rewardText = result.RequiresChoice ? "Hãy chọn hướng ứng xử tại thanh lựa chọn phía dưới." : result.RewardSummary;
        var card = new CityEventCard(result.Title, result.LocationName, result.Narrative, rewardText);
        List<SceneChoice>? strollChoices = null;

        if (result.RequiresChoice)
        {
            strollChoices = new List<SceneChoice>();
            if (result.MerchantOffers is { Count: > 0 })
            {
                foreach (var offer in result.MerchantOffers.Take(3))
                    strollChoices.Add(new SceneChoice($"stroll:buy:{offer.ItemId}", TrimSingleLine($"Mua {offer.ItemNameVi} ({offer.UnitPrice}q)", 38), offer.CategoryVi));
                strollChoices.Add(new SceneChoice("stroll:decline", "Cáo biệt không mua", "Từ chối"));
            }
            else if (result.Choices is { Count: > 0 })
            {
                foreach (var c in result.Choices.Take(4))
                {
                    var cleanLabel = c.LabelVi.Replace("💬", "", StringComparison.Ordinal).Replace("⚔️", "", StringComparison.Ordinal)
                        .Replace("🍶", "", StringComparison.Ordinal).Replace("👋", "", StringComparison.Ordinal).Trim();
                    strollChoices.Add(new SceneChoice($"stroll:choice:{c.Key}", TrimSingleLine(cleanLabel, 38), c.CostSummary ?? c.DescriptionVi));
                }
            }
        }

        TryOpenCityScene(card, strollChoices);
    }

    private static (string SeasonVi, string WeatherFx) ResolveSeasonAndWeather(int month) => month switch
    {
        >= 1 and <= 3 => ("Mùa Xuân", "cherry-blossoms"),
        >= 4 and <= 6 => ("Mùa Hạ", "none"),
        >= 7 and <= 9 => ("Mùa Thu", "falling-leaves"),
        _ => ("Mùa Đông", "snow")
    };

    private static IReadOnlyList<CityHotspot> BuildCityHotspots(IReadOnlyList<SublocationDef> subs)
    {
        string MatchSub(string fallback, params string[] keywords)
        {
            foreach (var sub in subs)
                foreach (var kw in keywords)
                    if (sub.NameVi.Contains(kw, StringComparison.OrdinalIgnoreCase))
                        return TrimSingleLine(sub.NameVi, 24);
            return fallback;
        }

        return
        [
            new("social.network", "Quán trọ", MatchSub("Đàm đạo nhân duyên", "Tửu", "Quán", "Trà", "Viện"), "social.network", 15, 34),
            new("market.stall", "Chợ", MatchSub("Mua bán hàng hóa", "Chợ", "Thương", "Thị"), "market.stall", 31, 44),
            new("market.warehouse", "Kho", MatchSub("Trữ hàng bảo quản", "Khố", "Kho", "Xưởng"), "market.warehouse", 38, 26),
            new("politics.court", "Quan phủ", MatchSub("Nhậm chức chính sự", "Cung", "Phủ", "Điện", "Đàn"), "politics.court", 52, 20),
            new("military.arena", "Lôi đài", MatchSub("Tỷ võ binh nghiệp", "Lôi Đài", "Doanh", "Trường"), "military.arena", 68, 30),
            new("world.map", "Cổng thành", MatchSub("Khởi hành viễn chinh", "Dịch", "Đạo", "Trại"), "world.map", 83, 44)
        ];
    }

    private IReadOnlyList<CityNpcSprite> BuildCityNpcs(long cityId)
    {
        if (!_kernel.Capabilities.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var reg) || reg is null)
            return Array.Empty<CityNpcSprite>();

        _kernel.Capabilities.TryGet<ISocietyChronicle>(CapabilityIds.SocietyChronicle, out var society);
        var list = new List<NpcDef>();
        foreach (var npc in reg.All)
        {
            if ((society?.GetNpcActivity(npc.Id)?.CurrentCitadelId ?? npc.CitadelId) != cityId) continue;
            list.Add(npc);
            if (list.Count >= 6) break;
        }

        double[] xs = [24, 41, 58, 74, 15, 84];
        double[] ys = [72, 74, 73, 76, 88, 89];
        var sprites = new List<CityNpcSprite>(list.Count);
        for (var i = 0; i < list.Count; i++)
        {
            var npc = list[i];
            var (_, facePath, spritePath) = SceneArtCatalog.ResolveNpcArt(npc);
            sprites.Add(new CityNpcSprite(npc.Id, npc.Code, TrimSingleLine(npc.NameVi, 24), RoleTitleVi.Format(npc.Role), spritePath, facePath, xs[i], ys[i]));
        }
        return sprites;
    }

    private static string TrimSingleLine(string text, int maxChars)
    {
        var clean = (text ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        return clean.Length <= maxChars ? clean : clean[..(maxChars - 1)] + "…";
    }

    public bool TryOpenTalkScene(long npcId, string? feedbackVi = null)
    {
        if (!SceneOverlay.IsAvailable || !_kernel.Capabilities.TryGet<IDialogueModule>(CapabilityIds.Dialogue, out var dialogue) || dialogue is null)
            return false;

        try
        {
            var scene = dialogue.BuildTalkScene(npcId, feedbackVi);
            if (!SceneOverlay.ShowTalkScene(scene)) return false;
            StatusText.Text = $"Đang diện kiến {scene.NameVi} ({scene.RoleVi}) tại {scene.LocationNameVi}.";
            return true;
        }
        catch (Exception ex)
        {
            StatusText.Text = Describe(ex);
            return false;
        }
    }

    public bool TryOpenDuelScene(NpcDef npc, string citadelName, TroopBranch playerBranch = TroopBranch.Infantry, DuelResult? result = null)
    {
        if (!SceneOverlay.IsAvailable ||
            !_kernel.Capabilities.TryGet<IMilitaryCommand>(CapabilityIds.MilitaryCommand, out var military) || military is null ||
            !_kernel.Capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) || attrs is null ||
            !_kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var playerState) || playerState is null)
            return false;

        var oppBranch = military.ResolveNpcBranch(npc.Id);
        var pMaxHp = 100 + (attrs.Attributes.Con / 2);
        var oMaxHp = 100 + (npc.Attributes.Con / 2);
        var pSp = attrs.Attributes.Wil >= 80 ? 10 : 0;
        var oSp = npc.Attributes.Wil >= 80 ? 10 : 0;
        var (_, npcFace, npcSprite) = SceneArtCatalog.ResolveNpcArt(npc);

        var playerFighter = new DuelFighterView(
            playerState.Name, $"Võ lực {attrs.Attributes.War} · {TroopBranchHelper.NameVi(playerBranch)}",
            SceneArtCatalog.PlayerPortraitFace, SceneArtCatalog.PlayerSprite, playerBranch, pMaxHp, pMaxHp, pSp, 100);

        var oppFighter = new DuelFighterView(
            npc.NameVi, $"Võ lực {npc.Attributes.War} · {TroopBranchHelper.NameVi(oppBranch)}",
            npcFace, npcSprite, oppBranch, oMaxHp, oMaxHp, oSp, 100);

        IReadOnlyList<SceneChoice> choices = result is null
            ? [
                new("branch:Infantry", "Xuất chiến Bộ Binh", "Khắc Cung · Kị Kỵ"),
                new("branch:Cavalry", "Xuất chiến Kỵ Binh", "Khắc Bộ · Kị Cung"),
                new("branch:Archers", "Xuất chiến Cung Binh", "Khắc Kỵ · Kị Bộ"),
                new("leave", "Rời lôi đài", "Cáo lui")
              ]
            : [
                new("rematch", "Tái đấu hiệp mới", "Chọn lại binh chủng"),
                new("talk", "Trò chuyện sau trận", $"Gặp {npc.NameVi}"),
                new("leave", "Rời lôi đài", "Quay lại màn hình")
              ];

        var cleanMatchup = TroopBranchHelper.MatchupSummaryVi(playerBranch, oppBranch)
            .Replace("🛡️", "", StringComparison.Ordinal).Replace("🐎", "", StringComparison.Ordinal)
            .Replace("🏹", "", StringComparison.Ordinal).Replace("  ", " ", StringComparison.Ordinal).Trim();

        var scene = new DuelScene(
            $"duel:{npc.Id}", npc.Id, npc.Code, $"Tỷ võ cùng {npc.NameVi}", citadelName,
            SceneArtCatalog.BackgroundForKey("arena"), playerFighter, oppFighter, cleanMatchup, result, choices);

        if (!SceneOverlay.ShowDuelScene(scene)) return false;
        StatusText.Text = result is null ? $"Chuẩn bị tỷ võ cùng {npc.NameVi} tại lôi đài {citadelName}." : result.CombatSummaryVi;
        return true;
    }

    private void OnSceneClientMessageReceived(SceneClientMessage message, object? activeScene)
    {
        var type = message.Type.Trim().ToLowerInvariant();
        if (type == "close")
        {
            UpdateHeader();
            RefreshBadges();
            RenderSelected();
            return;
        }

        if (activeScene is CityScene city) HandleCitySceneMessage(type, message.Id ?? string.Empty, city);
        else if (activeScene is TalkScene talk) HandleTalkSceneMessage(type, message.Id ?? string.Empty, talk);
        else if (activeScene is DuelScene duel) HandleDuelSceneMessage(type, message.Id ?? string.Empty, duel);
    }

    private void HandleCitySceneMessage(string type, string id, CityScene _)
    {
        if (type == "navigate")
        {
            if (id.StartsWith("npc:", StringComparison.Ordinal) && long.TryParse(id["npc:".Length..], out var npcId))
            {
                if (!TryOpenTalkScene(npcId)) MeetNpcFallback(npcId);
                return;
            }
            SelectScreen(id);
            return;
        }

        if (type != "choice") return;
        if (id == "briefing") SelectScreen(CityHubScreenId);
        else if (id == "stroll") TriggerCityStroll();
        else if (id == "stroll:log")
        {
            if (_kernel.Capabilities.TryGet<ICityStroll>(CapabilityIds.CityStroll, out var stroll) && stroll is not null &&
                _kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var player) && player is not null)
            {
                new CityStrollModalWindow(_kernel, stroll, player.CurrentCityId) { Owner = this }.ShowDialog();
                _kernel.Save();
                UpdateHeader();
                RenderSelected();
            }
        }
        else if (id.StartsWith("stroll:", StringComparison.Ordinal) &&
                 _kernel.Capabilities.TryGet<ICityStroll>(CapabilityIds.CityStroll, out var strollCap) && strollCap is not null &&
                 _kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var playerState) && playerState is not null)
        {
            var resolved = id switch
            {
                _ when id.StartsWith("stroll:buy:", StringComparison.Ordinal) => strollCap.ResolveChoice(playerState.CurrentCityId, "buy", id["stroll:buy:".Length..]),
                "stroll:decline" => strollCap.ResolveChoice(playerState.CurrentCityId, "decline"),
                _ => strollCap.ResolveChoice(playerState.CurrentCityId, id["stroll:choice:".Length..])
            };
            _kernel.Save();
            UpdateHeader();
            ShowNotification($"[{resolved.LocationName} · {resolved.Title}] {resolved.RewardSummary}");
            PresentStrollResultInCityScene(resolved);
        }
        else if (id == "map") SelectScreen("world.map");
    }

    private void HandleTalkSceneMessage(string type, string id, TalkScene talk)
    {
        if (type == "navigate" && id.StartsWith("npc:", StringComparison.Ordinal))
        {
            if (long.TryParse(id["npc:".Length..], out var targetNpcId)) TryOpenTalkScene(targetNpcId);
            return;
        }

        if (type != "choice" || !_kernel.Capabilities.TryGet<IDialogueModule>(CapabilityIds.Dialogue, out var dialogue) || dialogue is null)
            return;

        var outcome = dialogue.HandleChoice(talk.NpcId, id);
        StatusText.Text = outcome.MessageVi;
        _kernel.Save();
        UpdateHeader();
        RefreshBadges();

        if (outcome.OpenDuel)
        {
            if (_kernel.Capabilities.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var reg) &&
                reg?.Find(talk.NpcId) is { } npc && TryOpenDuelScene(npc, talk.LocationNameVi))
                return;
            SceneOverlay.HideScene();
            TryOpenDuelModal(talk.NpcId);
            return;
        }

        if (outcome.CloseScene)
        {
            SceneOverlay.HideScene();
            RenderSelected();
            return;
        }

        if (outcome.UpdatedScene is not null) SceneOverlay.ShowTalkScene(outcome.UpdatedScene);
    }

    private void HandleDuelSceneMessage(string type, string id, DuelScene duel)
    {
        if (type != "choice") return;
        if (id == "leave")
        {
            SceneOverlay.HideScene();
            UpdateHeader();
            RefreshBadges();
            RenderSelected();
            return;
        }
        if (id == "talk") { TryOpenTalkScene(duel.NpcId); return; }

        if (!_kernel.Capabilities.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var reg) || reg?.Find(duel.NpcId) is not { } npc)
            return;

        if (id == "rematch") { TryOpenDuelScene(npc, duel.CitadelNameVi); return; }

        if (id.StartsWith("branch:", StringComparison.Ordinal) &&
            Enum.TryParse<TroopBranch>(id["branch:".Length..], ignoreCase: true, out var branch) &&
            _kernel.Capabilities.TryGet<IMilitaryCommand>(CapabilityIds.MilitaryCommand, out var military) && military is not null)
        {
            try
            {
                var result = military.ChallengeArena(npc.Id, branch);
                _kernel.Save();
                UpdateHeader();
                RefreshBadges();
                TryOpenDuelScene(npc, duel.CitadelNameVi, branch, result);
            }
            catch (Exception ex) { StatusText.Text = Describe(ex); }
        }
    }
}
