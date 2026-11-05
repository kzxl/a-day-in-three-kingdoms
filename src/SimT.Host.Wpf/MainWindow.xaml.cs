using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using SimT.Contracts;
using SimT.Contracts.Playing;
using SimT.Contracts.Products;
using SimT.Contracts.Time;
using SimT.Contracts.Ui;
using SimT.Host.Wpf.Controls;
using SimT.Core.Kernel;

namespace SimT.Host.Wpf;

/// <summary>
/// The shell: a rail for who you are, when it is and how much you hold; a page with headline
/// numbers, alerts, cards and a month footer; an inspector for the selected row. Every number
/// comes from a module's read model — the host knows no rules.
/// </summary>
public partial class MainWindow : Window
{
    private const string LedgerScreenId = "core.ledger";

    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    private readonly SimKernel _kernel;
    private readonly SaveManager _saves;
    private readonly CampaignMeta _meta;
    private bool _loaded;
    private string _screenId = string.Empty;
    private string? _detailKey;
    private DataGrid? _firstGrid;
    private ScreenSection? _firstSection;
    private string _filter = string.Empty;

    public MainWindow(SimKernel kernel, SaveManager saves, CampaignMeta meta)
    {
        InitializeComponent();
        _kernel = kernel;
        _saves = saves;
        _meta = meta;

        CampaignText.Text = meta.Name;
        VersionText.Text = ShortVersion();
        VersionText.TextTrimming = TextTrimming.CharacterEllipsis;
        DarkTitleBar.Apply(this);
        PlayerHudCard.ProfileClicked += OpenPlayerProfileModal;
        RegisterModalPresenters();

        BuildRail();
        UpdateHeader();
        _loaded = true;
        RenderSelected();
    }

    // ---------------------------------------------------------------- rail

    private void BuildRail()
    {
        var entries = _kernel.Ui.Screens
            .Where(screen => screen.ScreenId != "content.item_hub")
            .Select(screen => new NavEntry(screen.ScreenId, screen.Title, GlyphFor(screen.ScreenId)))
            .ToList();

        entries.Add(new NavEntry(LedgerScreenId, "Sổ Cái", "≡"));

        NavList.ItemsSource = entries;
        NavList.SelectedIndex = 0;
    }

    private static string GlyphFor(string screenId) => screenId switch
    {
        "content.item_hub" => "📖",
        "player.reputation" => "🏆",
        "world.map" => "🗺️",
        "market.stall" => "◈",
        "market.player_stall" => "🎪",
        "market.warehouse" => "▤",
        "caravan.routes" => "⇢",
        "professions.trades" => "⚒",
        "cultivation.farm" => "❖",
        "lineage.family" => "⚭",
        "politics.court" => "⌂",
        "npc.directory" => "👤",
        "society.chronicle" => "📜",
        "social.network" => "🤝",
        "military.arena" => "⚔",
        _ => "•"
    };

    private void UpdateHeader()
    {
        var today = _kernel.Today;
        ChronoCard.Configure(today, _kernel.Turn, _kernel.Product.StartDate);

        var liquid = _kernel.Ledger.Liquid;
        var safe = _kernel.Ledger.Safe;
        var goods = GoodsValue();
        var netWorth = _kernel.Ledger.Balance + goods;
        TreasuryCard.Configure(liquid, safe, goods, netWorth);

        if (_kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var playerState) && playerState is not null)
        {
            var citadelName = "Lạc Dương";
            var regionName = "Trung Nguyên";
            if (_kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map) && map is not null)
            {
                var cit = map.Find(playerState.CurrentCityId);
                if (cit is not null)
                {
                    citadelName = cit.NameVi;
                    regionName = FormatRegionName(cit.Region);
                }
            }

            CharacterVitals? vitals = null;
            if (_kernel.Capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) && attrs is not null)
            {
                vitals = attrs.Vitals;
            }

            string? fameStanding = null;
            if (_kernel.Capabilities.TryGet<IPlayerReputation>(CapabilityIds.PlayerReputation, out var rep) && rep is not null)
            {
                var s = rep.Standing;
                fameStanding = $"★ {s.TitleVi} · {s.Fame:N0} Uy Danh";
            }

            PlayerHudCard.Configure(playerState.Name, citadelName, regionName, vitals, fameStanding);
        }

        RenderCalendar(today);
    }

    /// <summary>Goods at sell prices; zero when the market module is disabled.</summary>
    private long GoodsValue() =>
        _kernel.Capabilities.TryGet<IMarketPortfolio>(CapabilityIds.MarketPortfolio, out var portfolio) && portfolio is not null
            ? portfolio.InventoryValue()
            : 0;

    /// <summary>
    /// The month as thirty cells: past days fade, today is solid, day 30 is the close marker.
    /// A management sim always shows the clock you are spending.
    /// </summary>
    private void RenderCalendar(GameDate today)
    {
        CalendarBar.SetDate(today, _kernel.Turn);
    }

    private void UpdateHeaderDateOnly(GameDate today)
    {
        ChronoCard.Configure(today, _kernel.Turn, _kernel.Product.StartDate);
    }

    // ---------------------------------------------------------- rail badges

    /// <summary>Counts attention items per screen so the rail shows where the trouble is.</summary>
    private void RefreshBadges()
    {
        if (NavList.ItemsSource is not IEnumerable<NavEntry> entries)
        {
            return;
        }

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var screen in _kernel.Ui.Screens)
        {
            var alerts = screen.Render().Attention.Count;
            if (alerts > 0)
            {
                counts[screen.ScreenId] = alerts;
            }
        }

        foreach (var entry in entries)
        {
            entry.SetBadge(counts.GetValueOrDefault(entry.ScreenId));
        }
    }

    // ------------------------------------------------------------- screens

    private void Nav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loaded)
        {
            RenderSelected();
        }
    }

    /// <summary>Selects a screen by id; used by the demo launcher and future shortcuts.</summary>
    public bool SelectScreen(string screenId)
    {
        if (NavList.ItemsSource is not IEnumerable<NavEntry> entries)
        {
            return false;
        }

        var index = entries
            .Select((entry, position) => (entry, position))
            .Where(pair => string.Equals(pair.entry.ScreenId, screenId, StringComparison.Ordinal))
            .Select(pair => pair.position)
            .DefaultIfEmpty(-1)
            .First();

        if (index < 0)
        {
            return false;
        }

        NavList.SelectedIndex = index;
        RenderSelected();
        return true;
    }

    /// <summary>Selects the first row of the first card; used by the demo launcher.</summary>
    public bool SelectFirstRow()
    {
        if (_firstSection is null || _firstSection.Rows.Count == 0)
        {
            return false;
        }

        UpdateLayout();
        if (_firstGrid is not null)
        {
            _firstGrid.SelectedIndex = 0;
        }

        ShowInspector(_firstSection, _firstSection.Rows[0]);
        return true;
    }

    /// <summary>Runs a command through the normal path; used by the demo launcher.</summary>
    public void RunCommandForDemo(string commandId) =>
        RowCommand_Click(new Button { Tag = commandId }, new RoutedEventArgs());

    /// <summary>Runs one day through the real button path; used by the demo launcher.</summary>
    public void AdvanceOneDayForDemo() => AdvanceSync(1);

    public int RenderAllScreens()
    {
        var rendered = 0;
        for (var index = 0; index < NavList.Items.Count; index++)
        {
            NavList.SelectedIndex = index;
            rendered++;
        }

        UpdateLayout();
        return rendered;
    }

    /// <summary>Space advances a day, W a week, digits jump between screens, Esc closes the inspector.</summary>
    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.OriginalSource is TextBox && e.Key != System.Windows.Input.Key.Escape)
        {
            return;
        }

        if (e.OriginalSource is ButtonBase && e.Key is System.Windows.Input.Key.Space or System.Windows.Input.Key.Enter)
        {
            return;
        }

        if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control
            && e.Key == System.Windows.Input.Key.F)
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
            case System.Windows.Input.Key.Space:
                _ = AdvanceAsync(1);
                e.Handled = true;
                break;
            case System.Windows.Input.Key.W:
                _ = AdvanceAsync(7);
                e.Handled = true;
                break;
            case System.Windows.Input.Key.Enter:
                if (_firstGrid?.SelectedItem is RowVm selectedRow)
                {
                    ShowInspector(selectedRow.Section, selectedRow.Row);
                    e.Handled = true;
                }

                break;
            case System.Windows.Input.Key.Escape:
                CloseInspector();
                e.Handled = true;
                break;
            case System.Windows.Input.Key.D1:
            case System.Windows.Input.Key.D2:
            case System.Windows.Input.Key.D3:
            case System.Windows.Input.Key.D4:
            case System.Windows.Input.Key.D5:
            case System.Windows.Input.Key.D6:
            case System.Windows.Input.Key.D7:
            case System.Windows.Input.Key.D8:
            case System.Windows.Input.Key.D9:
                var index = e.Key - System.Windows.Input.Key.D1;
                if (index < NavList.Items.Count)
                {
                    NavList.SelectedIndex = index;
                    e.Handled = true;
                }

                break;
        }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _kernel.Save();
        _saves.Touch(_meta, GameDateCodec.Format(_kernel.Today));
        _kernel.Dispose();
    }

    private static string Describe(Exception error) => error switch
    {
        SimException sim => $"[{sim.Code}] {sim.Message}",
        _ => $"[internal] {error.GetType().Name}: {error.Message}"
    };

    private static string ShortVersion()
    {
        var full = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "unknown";

        var plus = full.IndexOf('+');
        if (plus < 0)
        {
            return $"SimT {full}";
        }

        var hash = full[(plus + 1)..];
        return $"SimT {full[..plus]} · {hash[..Math.Min(7, hash.Length)]}";
    }

    private static string FormatRegionName(string? regionCode)
    {
        if (string.IsNullOrWhiteSpace(regionCode))
        {
            return string.Empty;
        }

        return regionCode.Trim().ToLowerInvariant() switch
        {
            "trung_nguyen" => "Trung Nguyên",
            "quan_trung" => "Quan Trung",
            "ha_bac" => "Hà Bắc",
            "u_chau" => "U Châu",
            "ky_chau" => "Ký Châu",
            "thanh_chau" => "Thanh Châu",
            "du_chau" => "Dự Châu",
            "tu_chau" => "Từ Châu",
            "kinh_chau" => "Kinh Châu",
            "kinh_so" => "Kinh Sở",
            "giang_dong" => "Giang Đông",
            "duong_chau" => "Dương Châu",
            "ich_chau" => "Ích Châu",
            "ba_thuc" => "Ba Thục",
            _ => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(regionCode.Replace('_', ' '))
        };
    }
}
