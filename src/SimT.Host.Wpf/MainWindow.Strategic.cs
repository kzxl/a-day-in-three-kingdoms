using System.Collections.ObjectModel;
using System.Windows;
using SimT.Contracts.Playing;
using SimT.Contracts.Ui;
using SimT.Host.Wpf.Controls;

namespace SimT.Host.Wpf;

public partial class MainWindow
{
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

        StrategicRibbon.ItemsSource = ribbonItems;
        StrategicRibbon.ItemSelected += (_, e) =>
        {
            var targetScreen = e.SubItem?.Id
                ?? (_lastScreenPerGroup.TryGetValue(e.Item.Id, out var lastScreen) ? lastScreen : e.Item.Tag as string);
            if (!string.IsNullOrEmpty(targetScreen))
            {
                SelectScreen(targetScreen);
            }
        };

        ScreenGroupTabs.SelectionChanged += (_, item) =>
        {
            if (!string.IsNullOrEmpty(item.Key) && item.Key != _screenId)
            {
                SelectScreen(item.Key);
            }
        };
    }

    public void SyncStrategicNavigation(string screenId)
    {
        if (StrategicRibbon?.ItemsSource is null || ScreenGroupTabs is null) return;

        var activeGroup = StrategicRibbon.ItemsSource
            .FirstOrDefault(g => g.SubItems.Any(s => string.Equals(s.Id, screenId, StringComparison.Ordinal)))
            ?? StrategicRibbon.ItemsSource[0];

        _lastScreenPerGroup[activeGroup.Id] = screenId;
        StrategicRibbon.SelectedItemId = activeGroup.Id;
        StrategicRibbon.SelectedSubItemId = screenId;

        var tabs = activeGroup.SubItems
            .Select(s => new GameTabItem(s.Id, s.Title))
            .ToList();

        int selectedIdx = tabs.FindIndex(t => string.Equals(t.Key, screenId, StringComparison.Ordinal));
        if (selectedIdx < 0) selectedIdx = 0;
        ScreenGroupTabs.SetTabs(tabs, selectedIdx);
    }

    private void RefreshBadges(bool force = false)
    {
        if (NavList.ItemsSource is not IEnumerable<NavEntry> entries)
            return;

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

        foreach (var group in StrategicRibbon.ItemsSource)
        {
            int groupTotal = 0;
            foreach (var sub in group.SubItems)
            {
                int count = counts.GetValueOrDefault(sub.Id);
                sub.BadgeText = count > 0 ? count.ToString() : null;
                groupTotal += count;
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

    private void RenderChronicle()
    {
        SceneOverlay.HideScene();
        ScreenTitle.Text = "Thiên Hạ Biên Niên Sử";
        ScreenStatus.Text = "Kỷ yếu thời cuộc · Nắm bắt biến động thiên hạ và đưa ra quyết sách can thiệp";
        UpdateRecommendationBanner("Theo dõi biến cố thiên hạ và chọn mục Chờ Quyết Sách để can thiệp kịp thời vào thời cuộc.");
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
        chronicleView.Bind(_kernel);
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
}
