using SimT.Contracts.Time;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;
using SimT.Contracts.Ui;
using SimT.Host.Wpf.Controls;

namespace SimT.Host.Wpf;

public partial class MainWindow
{
    private DispatcherOperation? _pendingNavOp;

    private void BuildStrategicRibbon()
    {
        var ribbonItems = new ObservableCollection<CommandRibbonItem>
        {
            new()
            {
                Id = "group.self",
                Title = "Đại Bản Doanh",
                Subtitle = "Nội vụ, mệnh cách & sổ cái",
                IconPathData = "M12,3 L2,12 L5,12 L5,20 L19,20 L19,12 L22,12 Z M10,14 L14,14 L14,20 L10,20 Z",
                Tag = CitySceneScreenId,
                SubItems =
                [
                    new() { Id = CitySceneScreenId, Title = "Trong Thành", Subtitle = "Dạo phố, gặp danh sĩ" },
                    new() { Id = CityHubScreenId, Title = "Nhật Trình", Subtitle = "Tiêu điểm & báo cáo ngày" },
                    new() { Id = "player.destiny", Title = "Mệnh Cách", Subtitle = "Cộng điểm thiên mệnh" },
                    new() { Id = "player.reputation", Title = "Danh Vọng", Subtitle = "Uy tín từng vùng" },
                    new() { Id = "content.item_hub", Title = "Đồ Giám", Subtitle = "Tra vật phẩm" },
                    new() { Id = LedgerScreenId, Title = "Sổ Cái", Subtitle = "Thu chi của bạn" }
                ]
            },
            new()
            {
                Id = "group.map",
                Title = "Thiên Hạ",
                Subtitle = "Quan đạo, thành trì & 13 châu",
                IconPathData = "M3,6 L9,3 L15,6 L21,3 L21,18 L15,21 L9,18 L3,21 Z M9,3 L9,18 M15,6 L15,21",
                Tag = "world.map",
                SubItems =
                [
                    new() { Id = "world.map", Title = "Thiên Hạ", Subtitle = "Chọn đường sang thành khác" }
                ]
            },
            new()
            {
                Id = "group.trade",
                Title = "Thương Đạo",
                Subtitle = "Chợ, kho bãi & ngân hiệu",
                IconPathData = "M3,7 L21,7 L19,12 L5,12 Z M5,12 L5,20 L19,20 L19,12",
                Tag = "market.stall",
                SubItems =
                [
                    new() { Id = "market.stall", Title = "Chợ Thành", Subtitle = "Mua bán theo giá từng thành" },
                    new() { Id = "market.player_stall", Title = "Sạp Hàng", Subtitle = "Mở tiệm, bày hàng của bạn" },
                    new() { Id = "market.warehouse", Title = "Kho Bãi", Subtitle = "Gửi hàng tránh hư hỏng" },
                    new() { Id = "market.bank", Title = "Tiền Trang", Subtitle = "Gửi tiết kiệm, vay vốn" },
                    new() { Id = "market.player_bank", Title = "Ngân Trang", Subtitle = "Mở ngân hiệu, vận ngân" },
                    new() { Id = "caravan.routes", Title = "Thương Đoàn", Subtitle = "Chở hàng sang thành khác" },
                    new() { Id = "cultivation.farm", Title = "Điền Trang", Subtitle = "Thuê ruộng, gieo, gặt" },
                    new() { Id = "professions.trades", Title = "Nghề Nghiệp", Subtitle = "Làm công, rèn, nấu, câu" }
                ]
            },
            new()
            {
                Id = "group.social",
                Title = "Nhân Duyên Hào Kiệt",
                Subtitle = "Danh sĩ, quan hệ, dòng họ",
                IconPathData = "M12,11 A4,4 0 1,0 12,3 A4,4 0 1,0 12,11 M5,20 C5,16 8,14 12,14 C16,14 19,16 19,20",
                Tag = "npc.directory",
                SubItems =
                [
                    new() { Id = "npc.directory", Title = "Danh Sĩ", Subtitle = "Danh bạ mọi nhân vật" },
                    new() { Id = "social.network", Title = "Nhân Duyên", Subtitle = "Kết giao, tuyệt kỹ, thư từ" },
                    new() { Id = "lineage.family", Title = "Gia Tộc", Subtitle = "Cưới hỏi, con cái" }
                ]
            },
            new()
            {
                Id = "group.power",
                Title = "Thế Lực Triều Đình",
                Subtitle = "Quan trường, niên giám & binh bị",
                IconPathData = "M3,20 L21,20 M5,20 L5,10 M9,20 L9,10 M15,20 L15,10 M19,20 L19,10 M2,10 L12,4 L22,10",
                Tag = "society.chronicle",
                SubItems =
                [
                    new() { Id = "society.chronicle", Title = "Thiên Hạ Niên Giám", Subtitle = "Biến động thời cuộc" },
                    new() { Id = "politics.court", Title = "Quan Trường", Subtitle = "Xin chức, lĩnh bổng" },
                    new() { Id = "military.arena", Title = "Binh Nghiệp", Subtitle = "Lôi đài, dẹp giặc" }
                ]
            }
        };

        LocalizeRibbonItems(ribbonItems);

        StrategicRibbon.ItemsSource = ribbonItems;
        StrategicRibbon.ItemSelected += (_, e) =>
        {
            var targetScreen = e.SubItem?.Id
                ?? (_lastScreenPerGroup.TryGetValue(e.Item.Id, out var lastScreen) ? lastScreen : e.Item.Tag as string);
            if (string.IsNullOrEmpty(targetScreen)) return;

            var startDayIdx = GameDate.Create(184, 1, 1).DayIndex;
            var daysPassed = (long)Math.Max(0, _kernel.Today.DayIndex - startDayIdx);
            var balance = _kernel.Ledger.Balance;
            var repTier = _kernel.Capabilities.TryGet<IPlayerReputation>(CapabilityIds.PlayerReputation, out var rep) && rep is not null
                ? rep.Standing.CurrentTier
                : 1;
            var gate = ProgressionGating.Check(targetScreen, daysPassed, balance, repTier);
            if (!gate.IsUnlocked)
            {
                UpdateRecommendationBanner($"🔒 [CHƯA MỞ KHÓA] {e.SubItem?.Title ?? targetScreen}: {gate.ConditionVi}. Hướng dẫn: {gate.AdviceVi}");
            }

            _lastScreenPerGroup[e.Item.Id] = targetScreen;
            StrategicRibbon.SelectedItemId = e.Item.Id;
            StrategicRibbon.SelectedSubItemId = targetScreen;

            _pendingNavOp?.Abort();
            _pendingNavOp = null;

            if (string.Equals(targetScreen, _screenId, StringComparison.Ordinal) &&
                SceneOverlay.ActiveScene is not (TalkScene or DuelScene))
                return;

            _pendingNavOp = Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() =>
                {
                    _pendingNavOp = null;
                    SelectScreen(targetScreen);
                }));
        };
    }

    public void SyncStrategicNavigation(string screenId)
    {
        if (StrategicRibbon?.ItemsSource is null) return;

        var activeGroup = StrategicRibbon.ItemsSource
            .FirstOrDefault(g => g.SubItems.Any(s => string.Equals(s.Id, screenId, StringComparison.Ordinal)))
            ?? StrategicRibbon.ItemsSource[0];

        _lastScreenPerGroup[activeGroup.Id] = screenId;
        StrategicRibbon.SelectedItemId = activeGroup.Id;
        StrategicRibbon.SelectedSubItemId = screenId;
    }

    private bool _badgeRefreshQueued;

    private void RefreshBadges(bool force = false)
    {
        if (NavList.ItemsSource is not IEnumerable<NavEntry> entries)
            return;

        if (!force && _badgesDirty && !_isSmoke && !IsLoaded)
        {
            if (!_badgeRefreshQueued)
            {
                _badgeRefreshQueued = true;
                Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                {
                    _badgeRefreshQueued = false;
                    RefreshBadges(force: true);
                }));
            }
            return;
        }

        if (force || _badgesDirty)
        {
            _cachedBadgeCounts.Clear();
            foreach (var screen in _kernel.Ui.Screens)
            {
                var alerts = screen.Render().Attention.Count;
                if (alerts > 0)
                    _cachedBadgeCounts[screen.ScreenId] = alerts;
            }
            _badgesDirty = false;
        }

        foreach (var entry in entries)
            entry.SetBadge(_cachedBadgeCounts.GetValueOrDefault(entry.ScreenId));

        foreach (var group in _navGroups)
            group.RefreshGroupBadge();

        RefreshRibbonBadges(_cachedBadgeCounts);
    }

    private void RefreshRibbonBadges(IReadOnlyDictionary<string, int> counts)
    {
        if (StrategicRibbon?.ItemsSource is null) return;

        var startDayIdx = GameDate.Create(184, 1, 1).DayIndex;
        var daysPassed = (long)Math.Max(0, _kernel.Today.DayIndex - startDayIdx);
        var balance = _kernel.Ledger.Balance;
        var repTier = _kernel.Capabilities.TryGet<IPlayerReputation>(CapabilityIds.PlayerReputation, out var rep) && rep is not null
            ? rep.Standing.CurrentTier
            : 1;

        foreach (var group in StrategicRibbon.ItemsSource)
        {
            int groupTotal = 0;
            foreach (var sub in group.SubItems)
            {
                var gate = ProgressionGating.Check(sub.Id, daysPassed, balance, repTier);
                if (!gate.IsUnlocked)
                {
                    sub.BadgeText = "🔒";
                }
                else
                {
                    int count = counts.GetValueOrDefault(sub.Id);
                    sub.BadgeText = count > 0 ? count.ToString() : null;
                    groupTotal += count;
                }
            }
            group.BadgeText = groupTotal > 0 ? groupTotal.ToString() : null;
        }
        StrategicRibbon.InvalidateVisual();
    }

    private void RenderCityHub()
    {
        SceneOverlay.HideScene();
        ScreenTitle.Text = "Nhật Trình Chiến Lược";
        ScreenStatus.Text = "Đại điện quân cơ · Nắm bắt thời cuộc, tiêu điểm ngày và cơ hội thương đạo";
        UpdateRecommendationBanner("Xem tổng quan cơ hội chênh lệch giá và danh sĩ tại thành, hoặc bấm [Trong Thành] để dạo phố tương tác trực tiếp.");
        KpiStrip.Children.Clear();
        AlertFeed.ItemsSource = null;
        GlobalCommands.Children.Clear();
        FilterBox.Visibility = Visibility.Collapsed;
        FilterHint.Visibility = Visibility.Collapsed;
        ContentPanel.Children.Clear();

        var briefing = new GameStrategicBriefingControl();
        briefing.RequestNavigate += targetScreen => SelectScreen(targetScreen);
        briefing.RequestOpenCityScene += () => SelectScreen(CitySceneScreenId);
        briefing.RequestRest += () => Rest_Click(this, new RoutedEventArgs());
        briefing.RequestStroll += () => TriggerCityStroll(preferModalWhenOutsideScene: true);
        briefing.RequestTalkNpc += npcId => TryOpenTalkScene(npcId);

        briefing.RequestWishDestination += OpenWishDestination;
        briefing.Bind(_kernel);
        ContentPanel.Children.Add(briefing);
    }

    private void RenderCityScene()
    {
        ScreenTitle.Text = "Trong Thành";
        ScreenStatus.Text = "Trung tâm thành trì · bấm địa điểm hoặc danh sĩ để tương tác";
        UpdateRecommendationBanner("Bấm vào danh sĩ trên phố để trò chuyện hoặc bấm các địa điểm (Chợ, Kho Hàng, Lôi Đài, Quan Phủ) để mở nhanh chức năng.");
        KpiStrip.Children.Clear();
        AlertFeed.ItemsSource = null;
        GlobalCommands.Children.Clear();
        FilterBox.Visibility = Visibility.Collapsed;
        FilterHint.Visibility = Visibility.Collapsed;
        ContentPanel.Children.Clear();
        ContentPanel.Children.Add(EmptyState("Cảnh Trong Thành đang hiển thị trên lớp phủ tương tác. Bấm nút [Nhật Trình] để xem bảng tổng hợp chiến lược."));
        TryOpenCityScene();
    }

    public void RenderChronicle(string? initialFilter = null)
    {
        SceneOverlay.HideScene();
        ScreenTitle.Text = "Thiên Hạ Biên Niên Sử";
        ScreenStatus.Text = "Kỷ yếu thời cuộc · Nắm bắt biến động thiên hạ và đưa ra quyết sách can thiệp";
        UpdateRecommendationBanner("Theo dõi biến cố thiên hạ và chọn mục Sử biến hoặc Chờ Quyết Sách để xem lại trang sử và can thiệp thời cuộc.");
        KpiStrip.Children.Clear();
        AlertFeed.ItemsSource = null;
        GlobalCommands.Children.Clear();
        FilterBox.Visibility = Visibility.Collapsed;
        FilterHint.Visibility = Visibility.Collapsed;
        ContentPanel.Children.Clear();

        var chronicleView = new GameChronicleView();
        chronicleView.EventInteracted += () =>
        {
            UpdateHeader();
            ShowNotification("Đã thực thi quyết sách dã sử thành công!");
        };
        chronicleView.HistoricalEventReplayRequested += eventCode =>
        {
            TryOpenHistoricalEventScene(eventCode, isReplay: true);
        };
        chronicleView.Bind(_kernel);
        if (!string.IsNullOrWhiteSpace(initialFilter))
        {
            chronicleView.SetFilter(initialFilter);
        }
        ContentPanel.Children.Add(chronicleView);
    }

    private void RenderWorldMapScreen(ScreenModel model)
    {
        ScreenTitle.Text = model.Title;
        ScreenStatus.Text = model.StatusLine;

        KpiStrip.Children.Clear();
        foreach (var metric in model.Headline)
            KpiStrip.Children.Add(BuildMetric(metric));

        AlertFeed.ItemsSource = model.Attention.Count == 0
            ? null
            : model.Attention.Select(alert => new AlertVm(alert)).ToList();

        GlobalCommands.Children.Clear();
        foreach (var command in model.Commands.Where(command => !command.RequiresQuantity))
            GlobalCommands.Children.Add(CommandButton(command, command.Id));

        FilterBox.Visibility = Visibility.Collapsed;
        FilterHint.Visibility = Visibility.Collapsed;
        ContentPanel.Children.Clear();

        if (_kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map) && map is not null &&
            _kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var player) && player is not null)
        {
            var mapCtrl = new GameWorldMapControl(map, player.CurrentCityId, _kernel);
            mapCtrl.TravelRequested += async (targetId, mode) =>
            {
                var modeStr = mode switch
                {
                    TravelMode.Horse => "horse",
                    TravelMode.Caravan => "caravan",
                    _ => "walk"
                };
                await ExecuteScreenCommandAsync($"travel:{targetId}:{modeStr}", "world.map");
            };
            ContentPanel.Children.Add(mapCtrl);
        }

        foreach (var section in model.Sections)
            ContentPanel.Children.Add(BuildCard(Filtered(section)));

        ContentPanel.Children.Add(MonthFooter());
        RestoreDetail(model);
        RefreshBadges();
        AnimatePageTransition();
    }

    private void RenderDestinyTreeScreen(ScreenModel model)
    {
        ScreenTitle.Text = model.Title;
        ScreenStatus.Text = model.StatusLine;

        KpiStrip.Children.Clear();
        foreach (var metric in model.Headline)
            KpiStrip.Children.Add(BuildMetric(metric));

        AlertFeed.ItemsSource = model.Attention.Count == 0
            ? null
            : model.Attention.Select(alert => new AlertVm(alert)).ToList();

        GlobalCommands.Children.Clear();
        foreach (var command in model.Commands.Where(command => !command.RequiresQuantity))
            GlobalCommands.Children.Add(CommandButton(command, command.Id));

        FilterBox.Visibility = Visibility.Collapsed;
        FilterHint.Visibility = Visibility.Collapsed;
        ContentPanel.Children.Clear();

        if (_kernel.Capabilities.TryGet<IPlayerDestiny>(CapabilityIds.PlayerDestiny, out var destiny) && destiny is not null)
        {
            var treeCtrl = new GameDestinyTreeControl(destiny);
            treeCtrl.LearnRequested += async nodeId => await ExecuteScreenCommandAsync($"destiny:learn:{nodeId}", "player.destiny");
            treeCtrl.RespecRequested += async () => await ExecuteScreenCommandAsync("destiny:respec", "player.destiny");
            treeCtrl.EvaluateRequested += async () => await ExecuteScreenCommandAsync("destiny:evaluate", "player.destiny");
            ContentPanel.Children.Add(treeCtrl);
        }

        var milestoneSection = model.Sections.FirstOrDefault(s => s.Heading.Contains("KỲ NGỘ", StringComparison.OrdinalIgnoreCase))
            ?? model.Sections.LastOrDefault();
        if (milestoneSection is not null)
            ContentPanel.Children.Add(BuildCard(Filtered(milestoneSection)));

        ContentPanel.Children.Add(MonthFooter());
        RestoreDetail(model);
        RefreshBadges();
        AnimatePageTransition();
    }

    private void RenderFarmFieldScreen(ScreenModel model)
    {
        ScreenTitle.Text = model.Title;
        ScreenStatus.Text = model.StatusLine;

        KpiStrip.Children.Clear();
        foreach (var metric in model.Headline)
            KpiStrip.Children.Add(BuildMetric(metric));

        AlertFeed.ItemsSource = model.Attention.Count == 0
            ? null
            : model.Attention.Select(alert => new AlertVm(alert)).ToList();

        GlobalCommands.Children.Clear();
        foreach (var command in model.Commands.Where(command => !command.RequiresQuantity))
            GlobalCommands.Children.Add(CommandButton(command, command.Id));

        FilterBox.Visibility = Visibility.Collapsed;
        FilterHint.Visibility = Visibility.Collapsed;
        ContentPanel.Children.Clear();

        if (_kernel.Capabilities.TryGet<ICultivationEstate>(CapabilityIds.CultivationEstate, out var estate) && estate is not null &&
            _kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map) && map is not null &&
            _kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var player) && player is not null)
        {
            var farmCtrl = new GameFarmFieldControl(estate, map, player.CurrentCityId, _kernel.Clock);
            farmCtrl.RentRequested += async plotId => await ExecuteScreenCommandAsync($"rent:{plotId}", "cultivation.farm");
            farmCtrl.PlantRequested += async (plotId, cropCode) => await ExecuteScreenCommandAsync($"plant:{plotId}:{cropCode}", "cultivation.farm");
            farmCtrl.HarvestRequested += async plotId => await ExecuteScreenCommandAsync($"harvest:{plotId}", "cultivation.farm");
            farmCtrl.FertilizeRequested += async plotId => await ExecuteScreenCommandAsync($"fertilize:{plotId}", "cultivation.farm");
            farmCtrl.BuySeedRequested += async (cropCode, qty) => await ExecuteScreenCommandAsync($"buy_seed:{player.CurrentCityId}:{cropCode}:{qty}", "cultivation.farm");
            farmCtrl.BuyFertilizerRequested += async qty => await ExecuteScreenCommandAsync($"buy_fertilizer:{player.CurrentCityId}:{qty}", "cultivation.farm");
            ContentPanel.Children.Add(farmCtrl);
        }

        foreach (var section in model.Sections.Where(s => !s.Heading.Contains("QUỸ ĐẤT", StringComparison.OrdinalIgnoreCase) && !s.Heading.StartsWith("KHU VỰC CANH TÁC", StringComparison.OrdinalIgnoreCase)))
            ContentPanel.Children.Add(BuildCard(Filtered(section)));

        ContentPanel.Children.Add(MonthFooter());
        RestoreDetail(model);
        RefreshBadges();
        AnimatePageTransition();
    }

    private void LocalizeRibbonItems(ObservableCollection<CommandRibbonItem> items)
    {
        var loc = _kernel.Localization;
        var groupKeyMap = new Dictionary<string, (string TitleKey, string SubKey)>(StringComparer.OrdinalIgnoreCase)
        {
            ["group.self"] = ("nav.headquarters", "nav.headquarters_sub"),
            ["group.map"] = ("nav.world", "nav.world_sub"),
            ["group.trade"] = ("nav.trade", "nav.trade_sub"),
            ["group.social"] = ("nav.social", "nav.social_sub"),
            ["group.power"] = ("nav.military", "nav.military_sub")
        };

        var subKeyMap = new Dictionary<string, (string TitleKey, string SubKey)>(StringComparer.OrdinalIgnoreCase)
        {
            [CitySceneScreenId] = ("nav.city_scene", "nav.city_scene_sub"),
            [CityHubScreenId] = ("nav.city_hub", "nav.city_hub_sub"),
            ["player.destiny"] = ("nav.destiny", "nav.destiny_sub"),
            ["player.reputation"] = ("nav.reputation", "nav.reputation_sub"),
            ["content.item_hub"] = ("nav.item_hub", "nav.item_hub_sub"),
            [LedgerScreenId] = ("nav.records", "nav.records_sub"),
            ["world.map"] = ("nav.world", "nav.world_sub"),
            ["market.stall"] = ("nav.market", "nav.market_sub"),
            ["market.player_stall"] = ("nav.player_stall", "nav.player_stall_sub"),
            ["market.warehouse"] = ("nav.warehouse", "nav.warehouse_sub"),
            ["market.bank"] = ("nav.bank", "nav.bank_sub"),
            ["market.player_bank"] = ("nav.player_bank", "nav.player_bank_sub"),
            ["caravan.routes"] = ("nav.caravan", "nav.caravan_sub"),
            ["cultivation.farm"] = ("nav.cultivation", "nav.cultivation_sub"),
            ["professions.trades"] = ("nav.professions", "nav.professions_sub"),
            ["npc.directory"] = ("nav.npc_dir", "nav.npc_dir_sub"),
            ["social.network"] = ("nav.bonds", "nav.bonds_sub"),
            ["lineage.family"] = ("nav.family", "nav.family_sub"),
            ["society.chronicle"] = ("nav.chronicle", "nav.chronicle_sub"),
            ["politics.court"] = ("nav.court", "nav.court_sub"),
            ["military.arena"] = ("nav.arena", "nav.arena_sub")
        };

        foreach (var item in items)
        {
            if (groupKeyMap.TryGetValue(item.Id, out var gk))
            {
                item.Title = loc.Get(gk.TitleKey);
                item.Subtitle = loc.Get(gk.SubKey);
            }

            foreach (var sub in item.SubItems)
            {
                if (subKeyMap.TryGetValue(sub.Id, out var sk))
                {
                    sub.Title = loc.Get(sk.TitleKey);
                    sub.Subtitle = loc.Get(sk.SubKey);
                }
            }
        }
    }

}