using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using SimT.Contracts;
using SimT.Contracts.Playing;
using SimT.Contracts.Products;
using SimT.Contracts.Scenes;
using SimT.Contracts.Time;
using SimT.Contracts.Ui;
using SimT.Core.Kernel;
using SimT.Host.Wpf.Controls;

namespace SimT.Host.Wpf;

/// <summary>
/// The shell: a rail for who you are, when it is and how much you hold; a page with headline
/// numbers, alerts, cards and a month footer; an inspector for the selected row. Every number
/// comes from a module's read model — the host knows no rules.
/// </summary>
public partial class MainWindow : Window
{
    private const string CityHubScreenId = "city.hub";
    private const string CitySceneScreenId = "city.scene";
    private const string LedgerScreenId = "core.ledger";

    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    private readonly SimKernel _kernel;
    private readonly SaveManager _saves;
    private readonly CampaignMeta _meta;
    private readonly bool _isSmoke;
    private bool _loaded;
    private bool _isNavigating;
    private string _screenId = string.Empty;
    private string? _detailKey;
    private GameTable? _firstGrid;
    private ScreenSection? _firstSection;
    private string _filter = string.Empty;

    public MainWindow(
        SimKernel kernel,
        SaveManager saves,
        CampaignMeta meta,
        bool isSmoke = false,
        LoadingProgressState? loadingState = null)
    {
        InitializeComponent();
        _kernel = kernel;
        _saves = saves;
        _meta = meta;
        _isSmoke = isSmoke;
        ToastHost.SuppressTimers = isSmoke;
        InitializeSceneHost(loadingState);

        CampaignText.Text = meta.Name;
        VersionText.Text = ShortVersion();
        VersionText.TextTrimming = TextTrimming.CharacterEllipsis;
        DarkTitleBar.Apply(this);
        PlayerHudCard.ProfileClicked += OpenPlayerProfileModal;
        RegisterModalPresenters();

        BuildStrategicRibbon();
        BuildRail();
        UpdateHeader();
        _loaded = true;
        RenderSelected();
        AttachDebugOverlayLoop();
    }

    private readonly List<NavGroupVm> _navGroups = [];

    private void BuildRail()
    {
        _navGroups.Clear();
        var gCity = new NavGroupVm("Trong thành", "城", "Đi lại, xem thân thế", isExpanded: true);
        var gBiz = new NavGroupVm("Kinh doanh", "商", "Mua bán, làm ra của");
        var gSocial = new NavGroupVm("Nhân duyên", "人", "Kết giao, gia đình");
        var gMartial = new NavGroupVm("Quan võ", "武", "Làm quan, đánh trận");
        var gLedger = new NavGroupVm("Sổ sách", "書", "Tra cứu");
        _navGroups.AddRange([gCity, gBiz, gSocial, gMartial, gLedger]);

        var screenLookup = _kernel.Ui.Screens.ToDictionary(s => s.ScreenId, StringComparer.Ordinal);
        var entries = new List<NavEntry>
        {
            new(CitySceneScreenId, "Trong thành", "Dạo phố, gặp người", "城", gCity, IconPathFor(CitySceneScreenId)),
            new(CityHubScreenId, "Nhật Trình", "Tiêu điểm & báo cáo ngày", "議", gCity, IconPathFor(CityHubScreenId))
        };

        var orderedSpecs = new (string ScreenId, string FallbackTitle, string Description, string SealHan, NavGroupVm Group)[]
        {
            ("world.map", "Bản Đồ", "Chọn đường sang thành khác", "圖", gCity),
            ("player.destiny", "Mệnh Cách", "Cộng điểm thiên mệnh", "命", gCity),
            ("player.reputation", "Danh Vọng", "Uy tín từng vùng", "名", gCity),
            ("market.stall", "Chợ", "Mua bán theo giá từng thành", "市", gBiz),
            ("market.player_stall", "Sạp Hàng", "Mở tiệm, bày hàng của bạn", "攤", gBiz),
            ("market.warehouse", "Kho Hàng", "Gửi hàng tránh hư hỏng", "倉", gBiz),
            ("market.bank", "Tiền Trang", "Gửi tiết kiệm, vay vốn", "錢", gBiz),
            ("market.player_bank", "Ngân Trang", "Mở ngân hiệu, vận ngân", "銀", gBiz),
            ("caravan.routes", "Thương Đoàn", "Chở hàng sang thành khác", "隊", gBiz),
            ("professions.trades", "Nghề Nghiệp", "Làm công, rèn, nấu, câu", "藝", gBiz),
            ("cultivation.farm", "Điền Trang", "Thuê ruộng, gieo, gặt", "田", gBiz),
            ("npc.directory", "Danh Sĩ", "Danh bạ mọi nhân vật", "士", gSocial),
            ("social.network", "Nhân Duyên", "Kết giao, tuyệt kỹ, thư từ", "緣", gSocial),
            ("lineage.family", "Gia Tộc", "Cưới hỏi, con cái", "家", gSocial),
            ("society.chronicle", "Thiên Hạ", "Tin tức khắp nơi", "世", gSocial),
            ("politics.court", "Quan Trường", "Xin chức, lĩnh bổng", "官", gMartial),
            ("military.arena", "Binh Nghiệp", "Lôi đài, dẹp giặc", "兵", gMartial),
            ("content.item_hub", "Đồ Giám", "Tra vật phẩm", "鑑", gLedger)
        };

        var added = new HashSet<string>(StringComparer.Ordinal) { CitySceneScreenId, CityHubScreenId, LedgerScreenId };
        foreach (var (id, fallbackTitle, desc, seal, group) in orderedSpecs)
        {
            var title = screenLookup.TryGetValue(id, out var screen) ? ShortRailTitle(id, screen.Title) : fallbackTitle;
            entries.Add(new NavEntry(id, title, desc, seal, group, IconPathFor(id)));
            added.Add(id);
        }

        foreach (var extra in _kernel.Ui.Screens.Where(s => !added.Contains(s.ScreenId)))
            entries.Add(new NavEntry(extra.ScreenId, extra.Title, "Tra cứu thông tin", "卷", gLedger, IconPathFor(extra.ScreenId)));

        entries.Add(new NavEntry(LedgerScreenId, "Sổ Cái", "Thu chi của bạn", "帳", gLedger, IconPathFor(LedgerScreenId)));

        var view = CollectionViewSource.GetDefaultView(entries);
        view.GroupDescriptions.Clear();
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(NavEntry.Group)));

        NavList.ItemsSource = entries;
        NavList.SelectedIndex = 0;
    }

    private void NavGroupHeader_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: NavGroupVm group })
        {
            group.IsExpanded = !group.IsExpanded;
            e.Handled = true;
        }
    }

    private void EnsureActiveGroupExpanded(NavEntry activeEntry) =>
        activeEntry.GroupInfo.IsExpanded = true;

    private static string ShortRailTitle(string screenId, string rawTitle) => screenId switch
    {
        "world.map" => "Bản Đồ",
        "player.destiny" => "Mệnh Cách",
        "player.reputation" => "Danh Vọng",
        "market.stall" => "Chợ",
        "market.player_stall" => "Sạp Hàng",
        "market.warehouse" => "Kho Hàng",
        "market.bank" => "Tiền Trang",
        "market.player_bank" => "Ngân Trang",
        "caravan.routes" => "Thương Đoàn",
        "professions.trades" => "Nghề Nghiệp",
        "cultivation.farm" => "Điền Trang",
        "npc.directory" => "Danh Sĩ",
        "social.network" => "Nhân Duyên",
        "lineage.family" => "Gia Tộc",
        "society.chronicle" => "Thiên Hạ",
        "politics.court" => "Quan Trường",
        "military.arena" => "Binh Nghiệp",
        "content.item_hub" => "Đồ Giám",
        _ => rawTitle
    };

    private static string IconPathFor(string screenId) => screenId switch
    {
        CitySceneScreenId or CityHubScreenId => "M3,10 L12,3 L21,10 M5,9 L5,20 L19,20 L19,9 M10,20 L10,14 L14,14 L14,20",
        "world.map" => "M3,6 L9,3 L15,6 L21,3 L21,18 L15,21 L9,18 L3,21 Z M9,3 L9,18 M15,6 L15,21",
        "player.destiny" => "M12,2 L15,9 L22,12 L15,15 L12,22 L9,15 L2,12 L9,9 Z",
        "player.reputation" => "M6,4 L18,4 L18,9 C18,13 15,15 12,15 C9,15 6,13 6,9 Z M9,20 L15,20 M12,15 L12,20",
        "market.stall" => "M3,7 L21,7 L19,12 L5,12 Z M5,12 L5,20 L19,20 L19,12",
        "market.player_stall" => "M4,6 L20,6 M6,6 L6,19 L18,19 L18,6 M9,11 L15,11",
        "market.warehouse" => "M3,9 L12,4 L21,9 L21,20 L3,20 Z M7,13 L17,13 M7,17 L17,17",
        "market.bank" or "market.player_bank" => "M3,9 L12,4 L21,9 M5,9 L5,19 M9,9 L9,19 M15,9 L15,19 M19,9 L19,19 M3,19 L21,19",
        "caravan.routes" => "M3,15 L17,15 L20,10 L6,10 Z M7,18 A2,2 0 1,0 7,18.1 M16,18 A2,2 0 1,0 16,18.1",
        "professions.trades" => "M14,4 L20,10 L10,20 L4,14 Z M7,11 L13,17",
        "cultivation.farm" => "M12,20 L12,8 M12,13 C8,13 6,9 6,6 C9,6 12,8 12,13 M12,11 C16,11 18,7 18,4 C15,4 12,6 12,11",
        "npc.directory" => "M12,11 A4,4 0 1,0 12,3 A4,4 0 1,0 12,11 M5,20 C5,16 8,14 12,14 C16,14 19,16 19,20",
        "social.network" => "M8,10 A3,3 0 1,0 8,4 A3,3 0 1,0 8,10 M16,10 A3,3 0 1,0 16,4 A3,3 0 1,0 16,10 M3,19 C3,16 5.5,14 8.5,14 M15.5,14 C18.5,14 21,16 21,19",
        "lineage.family" => "M12,4 A2.5,2.5 0 1,0 12,9 A2.5,2.5 0 1,0 12,4 M6,15 A2,2 0 1,0 6,19 A2,2 0 1,0 6,15 M18,15 A2,2 0 1,0 18,19 A2,2 0 1,0 18,15 M12,9 L12,13 M6,15 L12,13 L18,15",
        "society.chronicle" => "M6,4 L18,4 L18,20 L6,20 Z M9,8 L15,8 M9,12 L15,12 M9,16 L13,16",
        "politics.court" => "M3,20 L21,20 M5,20 L5,10 M9,20 L9,10 M15,20 L15,10 M19,20 L19,10 M2,10 L12,4 L22,10",
        "military.arena" => "M4,20 L20,4 M15,4 L20,4 L20,9 M20,20 L4,4 M9,4 L4,4 L4,9",
        "content.item_hub" => "M12,3 L20,7.5 L20,16.5 L12,21 L4,16.5 L4,7.5 Z M12,12 L20,7.5 M12,12 L4,7.5 M12,12 L12,21",
        LedgerScreenId => "M4,5 L20,5 M4,10 L20,10 M4,15 L20,15 M4,20 L14,20",
        _ => "M5,12 L19,12 M12,5 L12,19"
    };

    private void UpdateHeader()
    {
        var today = _kernel.Today;
        ChronoCard.Configure(today, _kernel.Turn, _kernel.Product.StartDate);
        var (liquid, safe, goods) = (_kernel.Ledger.Liquid, _kernel.Ledger.Safe, GoodsValue());
        TreasuryCard.Configure(liquid, safe, goods, _kernel.Ledger.Balance + goods);
        CompactDateText.Text = GameDateCodec.ShortVi(today);
        CompactMoneyText.Text = $"{liquid.ToString("N0", Vietnamese)} quan";

        if (_kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var playerState) && playerState is not null)
        {
            var (citadelName, regionName) = ("Lạc Dương", "Trung Nguyên");
            if (_kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map) && map?.Find(playerState.CurrentCityId) is { } cit)
                (citadelName, regionName) = (cit.NameVi, RoleTitleVi.FormatRegion(cit.Region));

            _kernel.Capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs);
            var vitals = attrs?.Vitals;
            string? fameStanding = null;
            var shortStanding = "Bách Tính";
            if (_kernel.Capabilities.TryGet<IPlayerReputation>(CapabilityIds.PlayerReputation, out var rep) && rep is not null)
                (shortStanding, fameStanding) = (rep.Standing.TitleVi, $"{rep.Standing.TitleVi} · {rep.Standing.Fame:N0} Uy Danh");

            CompactPlayerNameText.Text = playerState.Name;
            CompactLocationText.Text = citadelName;
            CompactVitalsText.Text = vitals is not null ? $"Thể lực {vitals.CurrentStamina}/{vitals.MaxStamina} · Mệt {vitals.Fatigue}% · {shortStanding}" : shortStanding;

            if (vitals is not null)
            {
                VisualStaminaFill.Width = Math.Clamp((vitals.MaxStamina > 0 ? (double)vitals.CurrentStamina / vitals.MaxStamina : 1.0) * 65.0, 2.0, 65.0);
                VisualStaminaText.Text = $"{vitals.CurrentStamina}/{vitals.MaxStamina}";
                VisualFatigueFill.Width = Math.Clamp(Math.Clamp(vitals.Fatigue / 100.0, 0.0, 1.0) * 55.0, 2.0, 55.0);
                VisualFatigueText.Text = $"{vitals.Fatigue}%";
                VisualFatigueFill.Background = vitals.Fatigue > 75 ? (Brush)FindResource("Loss") : new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
            }
            VisualStandingText.Text = shortStanding;
            PlayerHudCard.Configure(playerState.Name, citadelName, regionName, vitals, fameStanding);
        }

        RenderCalendar(today);
        UpdateStrategicState();
    }

    private long GoodsValue() =>
        _kernel.Capabilities.TryGet<IMarketPortfolio>(CapabilityIds.MarketPortfolio, out var portfolio) && portfolio is not null ? portfolio.InventoryValue() : 0;

    private void RenderCalendar(GameDate today) => CalendarBar.SetDate(today, _kernel.Turn);

    private void UpdateHeaderDateOnly(GameDate today)
    {
        ChronoCard.Configure(today, _kernel.Turn, _kernel.Product.StartDate);
        CompactDateText.Text = GameDateCodec.ShortVi(today);
    }

    private bool _badgesDirty = true;
    private readonly Dictionary<string, int> _cachedBadgeCounts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _lastScreenPerGroup = new(StringComparer.Ordinal);

    public void InvalidateBadges() => _badgesDirty = true;

    private void Nav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loaded && !_isNavigating)
            RenderSelected();
    }

    public void ShowNotification(string message, GameToastKind? kind = null)
    {
        StatusText.Text = message;
        ToastHost.ShowToast(message, kind);
    }

    public bool SelectScreen(string screenId)
    {
        _pendingNavOp?.Abort(); _pendingNavOp = null;
        if (string.Equals(screenId, "finance.ledger", StringComparison.Ordinal))
            screenId = LedgerScreenId;

        if (string.Equals(screenId, UiGalleryScreenId, StringComparison.Ordinal))
        {
            _screenId = UiGalleryScreenId;
            SceneOverlay.HideScene();
            DismissStartupOverlay(immediate: true);
            RenderUiGalleryScreen();
            return true;
        }
        if (NavList.ItemsSource is not IEnumerable<NavEntry> entries)
            return false;

        var index = entries
            .Select((entry, position) => (entry, position))
            .Where(pair => string.Equals(pair.entry.ScreenId, screenId, StringComparison.Ordinal))
            .Select(pair => pair.position)
            .DefaultIfEmpty(-1)
            .First();

        if (index < 0) return false;
        if (NavList.SelectedIndex == index && string.Equals(_screenId, screenId, StringComparison.Ordinal) && SceneOverlay.ActiveScene is not (TalkScene or DuelScene or EventScene or DebateScene))
            return true;

        _isNavigating = true;
        try { NavList.SelectedIndex = index; }
        finally { _isNavigating = false; }
        RenderSelected();
        if (!IsWebSceneScreen(_screenId)) DismissStartupOverlay(immediate: true);
        return true;
    }

    public bool SelectFirstRow()
    {
        if (_firstSection is null || _firstSection.Rows.Count == 0) return false;
        UpdateLayout();
        if (_firstGrid is not null) _firstGrid.SelectedIndex = 0;
        ShowInspector(_firstSection, _firstSection.Rows[0]);
        return true;
    }

    public void RunCommandForDemo(string commandId) =>
        RowCommand_Click(new Button { Tag = commandId }, new RoutedEventArgs());

    public void AdvanceOneDayForDemo() => AdvanceSync(1);

    public int RenderAllScreens()
    {
        var rendered = 0;
        for (var index = 0; index < NavList.Items.Count; index++)
        {
            NavList.SelectedIndex = index;
            rendered++;
        }
        SelectScreen(UiGalleryScreenId);
        UpdateLayout();
        return rendered + 1;
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (HandleLoadingScreenKeyDown(e))
            return;

        if (e.OriginalSource is TextBox && e.Key != System.Windows.Input.Key.Escape) return;
        if (e.OriginalSource is ButtonBase && e.Key is System.Windows.Input.Key.Space or System.Windows.Input.Key.Enter) return;

        if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control && e.Key == System.Windows.Input.Key.F)
        {
            if (FilterBox.Visibility == Visibility.Visible)
            {
                FilterBox.Focus();
                FilterBox.SelectAll();
                e.Handled = true;
            }
            return;
        }

        switch (e.Key)
        {
            case System.Windows.Input.Key.F3:
                DebugOverlay.ToggleVisibility();
                e.Handled = true;
                break;
            case System.Windows.Input.Key.Space:
                _ = AdvanceAsync(1); e.Handled = true; break;
            case System.Windows.Input.Key.W:
                _ = AdvanceAsync(7); e.Handled = true; break;
            case System.Windows.Input.Key.Enter:
                if (_firstGrid?.SelectedRowVm is RowVm selectedRow)
                {
                    ShowInspector(selectedRow.Section, selectedRow.Row);
                    e.Handled = true;
                }
                break;
            case System.Windows.Input.Key.Escape:
                if (SceneOverlay.ActiveScene is MapScene or SocialScene or MarketScene or DossierScene) SelectScreen(CitySceneScreenId);
                else if (SceneOverlay.ActiveScene is TalkScene or DuelScene or EventScene or DebateScene) { SceneOverlay.HideScene(); RenderSelected(); }
                else CloseInspector();
                e.Handled = true; break;
            case >= System.Windows.Input.Key.D1 and <= System.Windows.Input.Key.D9:
                var index = e.Key - System.Windows.Input.Key.D1;
                if (index < NavList.Items.Count) { NavList.SelectedIndex = index; e.Handled = true; }
                break;
        }
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        DismissStartupOverlay(immediate: true);
        SceneOverlay.Dispose();
        _kernel.Save();
        _saves.Touch(_meta, GameDateCodec.Format(_kernel.Today));
        _kernel.Dispose();
    }
}
