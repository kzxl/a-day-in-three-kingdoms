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
            {
                TryOpenCityScene();
            }
        };
    }

    public bool TryOpenCityScene(CityEventCard? eventCard = null)
    {
        if (!SceneOverlay.IsAvailable ||
            !_kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var playerState) ||
            playerState is null)
        {
            return false;
        }

        var cityId = playerState.CurrentCityId;
        var cityName = "Lạc Dương";
        var regionVi = "Trung Nguyên";
        IReadOnlyList<SublocationDef> sublocations = Array.Empty<SublocationDef>();
        if (_kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map) && map is not null)
        {
            var cit = map.Find(cityId);
            if (cit is not null)
            {
                cityName = cit.NameVi;
                regionVi = cit.Region;
            }
            sublocations = map.Sublocations(cityId);
        }

        var (seasonVi, weatherFx) = ResolveSeasonAndWeather(_kernel.Today.Month);
        var scene = new CityScene(
            SceneId: $"city:{cityId}",
            CityId: cityId,
            CityNameVi: cityName,
            CityNameHan: "城",
            RegionVi: regionVi,
            DateVi: GameDateCodec.ShortVi(_kernel.Today),
            Season: seasonVi,
            WeatherEffect: weatherFx,
            BackgroundPath: SceneArtCatalog.BackgroundForKey("city_street"),
            Hotspots: BuildCityHotspots(sublocations),
            Npcs: BuildCityNpcs(cityId),
            EventCard: eventCard,
            Choices:
            [
                new("stroll", "Du ngoạn phố", "Tản bộ tìm kỳ ngộ"),
                new("map", "Xem bản đồ", "Hành trình liên thành")
            ]);

        if (!SceneOverlay.ShowCityScene(scene))
            return false;

        StatusText.Text = eventCard is null
            ? $"Đang ở {cityName} ({seasonVi}) · bấm địa điểm hoặc nhân sĩ trong thành để tương tác."
            : $"[{eventCard.LocationVi} · {eventCard.TitleVi}] {eventCard.RewardVi}";
        return true;
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
            new("market.stall", "Chợ", MatchSub("Mua bán hàng hóa", "Chợ", "Thương", "Thị"), "market.stall", 20, 42),
            new("market.warehouse", "Kho", MatchSub("Trữ hàng bảo quản", "Khố", "Kho", "Xưởng"), "market.warehouse", 36, 48),
            new("politics.court", "Quan phủ", MatchSub("Nhậm chức chính sự", "Cung", "Phủ", "Điện", "Đàn"), "politics.court", 52, 28),
            new("military.arena", "Lôi đài", MatchSub("Tỷ võ binh nghiệp", "Lôi Đài", "Doanh", "Trường"), "military.arena", 72, 42),
            new("social.network", "Quán trọ", MatchSub("Đàm đạo nhân duyên", "Tửu", "Quán", "Trà", "Viện"), "social.network", 28, 60),
            new("world.map", "Cổng thành", MatchSub("Khởi hành viễn chinh", "Dịch", "Đạo", "Trại"), "world.map", 84, 56)
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
            var curCity = society?.GetNpcActivity(npc.Id)?.CurrentCitadelId ?? npc.CitadelId;
            if (curCity == cityId)
            {
                list.Add(npc);
                if (list.Count >= 6) break;
            }
        }

        double[] xs = [18, 31, 44, 57, 70, 82];
        double[] ys = [76, 73, 77, 74, 76, 73];
        var sprites = new List<CityNpcSprite>(list.Count);
        for (var i = 0; i < list.Count; i++)
        {
            var npc = list[i];
            var (_, facePath, spritePath) = SceneArtCatalog.ResolveNpcArt(npc.Code, npc.Role);
            sprites.Add(new CityNpcSprite(
                npc.Id,
                npc.Code,
                TrimSingleLine(npc.NameVi, 24),
                RoleTitleVi.Format(npc.Role),
                spritePath,
                facePath,
                xs[i],
                ys[i]));
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
        if (!SceneOverlay.IsAvailable ||
            !_kernel.Capabilities.TryGet<IDialogueModule>(CapabilityIds.Dialogue, out var dialogue) ||
            dialogue is null)
        {
            return false;
        }

        try
        {
            var scene = dialogue.BuildTalkScene(npcId, feedbackVi);
            if (!SceneOverlay.ShowTalkScene(scene))
                return false;

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
            !_kernel.Capabilities.TryGet<IMilitaryCommand>(CapabilityIds.MilitaryCommand, out var military) ||
            military is null ||
            !_kernel.Capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) ||
            attrs is null ||
            !_kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var playerState) ||
            playerState is null)
        {
            return false;
        }

        var oppBranch = military.ResolveNpcBranch(npc.Id);
        var pMaxHp = 100 + (attrs.Attributes.Con / 2);
        var oMaxHp = 100 + (npc.Attributes.Con / 2);
        var pSp = attrs.Attributes.Wil >= 80 ? 10 : 0;
        var oSp = npc.Attributes.Wil >= 80 ? 10 : 0;
        var (_, npcFace, npcSprite) = SceneArtCatalog.ResolveNpcArt(npc.Code, npc.Role);

        var playerFighter = new DuelFighterView(
            playerState.Name,
            $"Võ lực {attrs.Attributes.War} · {TroopBranchHelper.NameVi(playerBranch)}",
            SceneArtCatalog.PlayerPortraitFace,
            SceneArtCatalog.PlayerSprite,
            playerBranch, pMaxHp, pMaxHp, pSp, 100);

        var oppFighter = new DuelFighterView(
            npc.NameVi,
            $"Võ lực {npc.Attributes.War} · {TroopBranchHelper.NameVi(oppBranch)}",
            npcFace,
            npcSprite,
            oppBranch, oMaxHp, oMaxHp, oSp, 100);

        IReadOnlyList<SceneChoice> choices = result is null
            ?
            [
                new("branch:Infantry", "Xuất chiến Bộ Binh", "Khắc Cung · Kị Kỵ"),
                new("branch:Cavalry", "Xuất chiến Kỵ Binh", "Khắc Bộ · Kị Cung"),
                new("branch:Archers", "Xuất chiến Cung Binh", "Khắc Kỵ · Kị Bộ"),
                new("leave", "Rời lôi đài", "Cáo lui")
            ]
            :
            [
                new("rematch", "Tái đấu hiệp mới", "Chọn lại binh chủng"),
                new("talk", "Trò chuyện sau trận", $"Gặp {npc.NameVi}"),
                new("leave", "Rời lôi đài", "Quay lại màn hình")
            ];

        var scene = new DuelScene(
            $"duel:{npc.Id}",
            npc.Id,
            npc.Code,
            $"Tỷ võ cùng {npc.NameVi}",
            citadelName,
            SceneArtCatalog.BackgroundForKey("arena"),
            playerFighter,
            oppFighter,
            TroopBranchHelper.MatchupSummaryVi(playerBranch, oppBranch),
            result,
            choices);

        if (!SceneOverlay.ShowDuelScene(scene))
            return false;

        StatusText.Text = result is null
            ? $"Chuẩn bị tỷ võ cùng {npc.NameVi} tại lôi đài {citadelName}."
            : result.CombatSummaryVi;
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

        if (activeScene is CityScene city)
            HandleCitySceneMessage(type, message.Id ?? string.Empty, city);
        else if (activeScene is TalkScene talk)
            HandleTalkSceneMessage(type, message.Id ?? string.Empty, talk);
        else if (activeScene is DuelScene duel)
            HandleDuelSceneMessage(type, message.Id ?? string.Empty, duel);
    }

    private void HandleCitySceneMessage(string type, string id, CityScene _)
    {
        if (type == "navigate")
        {
            if (id.StartsWith("npc:", StringComparison.Ordinal) &&
                long.TryParse(id["npc:".Length..], out var npcId))
            {
                if (!TryOpenTalkScene(npcId))
                    MeetNpcFallback(npcId);
                return;
            }

            SelectScreen(id);
            return;
        }

        if (type == "choice")
        {
            if (id == "stroll")
                TriggerCityStroll();
            else if (id == "map")
                SelectScreen("world.map");
        }
    }

    private void HandleTalkSceneMessage(string type, string id, TalkScene talk)
    {
        if (type == "navigate" && id.StartsWith("npc:", StringComparison.Ordinal))
        {
            if (long.TryParse(id["npc:".Length..], out var targetNpcId))
                TryOpenTalkScene(targetNpcId);
            return;
        }

        if (type != "choice" ||
            !_kernel.Capabilities.TryGet<IDialogueModule>(CapabilityIds.Dialogue, out var dialogue) ||
            dialogue is null)
            return;

        var outcome = dialogue.HandleChoice(talk.NpcId, id);
        StatusText.Text = outcome.MessageVi;
        _kernel.Save();
        UpdateHeader();
        RefreshBadges();

        if (outcome.OpenDuel)
        {
            if (_kernel.Capabilities.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var reg) &&
                reg?.Find(talk.NpcId) is { } npc &&
                TryOpenDuelScene(npc, talk.LocationNameVi))
            {
                return;
            }

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

        if (outcome.UpdatedScene is not null)
            SceneOverlay.ShowTalkScene(outcome.UpdatedScene);
    }

    private void HandleDuelSceneMessage(string type, string id, DuelScene duel)
    {
        if (type != "choice")
            return;

        if (id == "leave")
        {
            SceneOverlay.HideScene();
            UpdateHeader();
            RefreshBadges();
            RenderSelected();
            return;
        }

        if (id == "talk")
        {
            TryOpenTalkScene(duel.NpcId);
            return;
        }

        if (!_kernel.Capabilities.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var reg) ||
            reg?.Find(duel.NpcId) is not { } npc)
            return;

        if (id == "rematch")
        {
            TryOpenDuelScene(npc, duel.CitadelNameVi);
            return;
        }

        if (id.StartsWith("branch:", StringComparison.Ordinal) &&
            Enum.TryParse<TroopBranch>(id["branch:".Length..], ignoreCase: true, out var branch) &&
            _kernel.Capabilities.TryGet<IMilitaryCommand>(CapabilityIds.MilitaryCommand, out var military) &&
            military is not null)
        {
            try
            {
                var result = military.ChallengeArena(npc.Id, branch);
                _kernel.Save();
                UpdateHeader();
                RefreshBadges();
                TryOpenDuelScene(npc, duel.CitadelNameVi, branch, result);
            }
            catch (Exception ex)
            {
                StatusText.Text = Describe(ex);
            }
        }
    }
}
