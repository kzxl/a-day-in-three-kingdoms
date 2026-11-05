using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using SimT.Contracts;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Core.Kernel;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Hardware-accelerated interactive Three Kingdoms World Map Control.
/// Visualizes the 15 citadels with ornate cartography, interstate highways,
/// regional ecosystems, player beacon, and strategic expedition dispatch console.
/// </summary>
public sealed class GameWorldMapControl : Border
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    private readonly IWorldMap _map;
    private readonly SimKernel? _kernel;
    private readonly long _currentCityId;
    private long _selectedCityId;
    private TravelMode _selectedMode = TravelMode.Walk;

    private readonly MapCanvas _canvas;

    // Dossier UI components (Left Column)
    private readonly TextBlock _destTitleText;
    private readonly Border _destPriceBadge;
    private readonly TextBlock _destPriceText;
    private readonly Border _destDangerBadge;
    private readonly TextBlock _destDangerText;
    private readonly TextBlock _destEcosystemText;
    private readonly TextBlock _destSpecialtyText;
    private readonly WrapPanel _destSublocationsPanel;
    private readonly TextBlock _destNpcText;

    // Expedition UI components (Right Column)
    private readonly Button _walkModeBtn;
    private readonly Button _horseModeBtn;
    private readonly Button _caravanModeBtn;
    private readonly TextBlock _routePathText;
    private readonly TextBlock _expeditionMetricsText;
    private readonly Button _departBtn;

    public event Action<long, TravelMode>? TravelRequested;

    public GameWorldMapControl(IWorldMap map, long currentCityId, SimKernel? kernel = null)
    {
        _map = map;
        _kernel = kernel;
        _currentCityId = currentCityId;

        // Default selection: first neighbor or another citadel
        _selectedCityId = map.RouteNeighbours(currentCityId).FirstOrDefault();
        if (_selectedCityId == 0)
        {
            _selectedCityId = map.Citadels.FirstOrDefault(c => c.Id != currentCityId)?.Id ?? currentCityId;
        }

        Background = (Brush)Application.Current.FindResource("Surface");
        BorderBrush = (Brush)Application.Current.FindResource("Line");
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(8);
        Padding = new Thickness(16, 14, 16, 14);
        Margin = new Thickness(0, 0, 0, 16);

        var mainStack = new StackPanel();

        // 1. Header & Region Legend
        var headerDock = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        var legendPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        legendPanel.Children.Add(CreateLegendBadge("🏛️ Kinh Đô", Color.FromRgb(0xC0, 0x39, 0x2B)));
        legendPanel.Children.Add(CreateLegendBadge("🌾 Hà Bắc", Color.FromRgb(0x27, 0xAE, 0x60)));
        legendPanel.Children.Add(CreateLegendBadge("🌊 Giang Đông", Color.FromRgb(0x29, 0x80, 0xB9)));
        legendPanel.Children.Add(CreateLegendBadge("⛵ Kinh Châu", Color.FromRgb(0x16, 0xA0, 0x85)));
        legendPanel.Children.Add(CreateLegendBadge("⛰️ Ba Thục", Color.FromRgb(0x8E, 0x44, 0xAD)));
        DockPanel.SetDock(legendPanel, Dock.Right);
        headerDock.Children.Add(legendPanel);

        var currentCitadel = map.Find(currentCityId);
        var currentEco = map.GetEcosystem(currentCityId);
        var titleStack = new StackPanel();
        titleStack.Children.Add(new TextBlock
        {
            Text = "BẢN ĐỒ CHIẾN LƯỢC THIÊN HẠ & MẠNG LƯỚI QUAN ĐẠO 15 ĐẠI THÀNH",
            Style = (Style)Application.Current.FindResource("Overline")
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = $"📍 Đang trú tại: {currentCitadel?.NameVi ?? "Lạc Dương"} ({currentEco.BiomeNameVi} · {currentEco.ClimateVi})",
            FontSize = 13.5,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)Application.Current.FindResource("Accent"),
            Margin = new Thickness(0, 2, 0, 0)
        });
        headerDock.Children.Add(titleStack);
        mainStack.Children.Add(headerDock);

        // 2. Interactive Ornate Visual Map Canvas
        _canvas = new MapCanvas(map, currentCityId, _selectedCityId);
        _canvas.NodeSelected += OnCanvasNodeSelected;
        mainStack.Children.Add(_canvas);

        // 3. Strategic Dossier & Expedition Planning Console (2 Columns)
        var consoleBorder = new Border
        {
            Background = (Brush)Application.Current.FindResource("SurfaceAlt"),
            BorderBrush = (Brush)Application.Current.FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 12, 0, 0)
        };

        var consoleGrid = new Grid();
        consoleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });
        consoleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) }); // Spacer
        consoleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });

        // Left Column: Selected Citadel Strategic Dossier
        var leftCard = new Border
        {
            Background = (Brush)Application.Current.FindResource("Rail"),
            BorderBrush = (Brush)Application.Current.FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10)
        };

        var leftStack = new StackPanel();

        var dossierHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var badgeStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        _destDangerBadge = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x33, 0xE5, 0xB5, 0x48)),
            BorderBrush = (Brush)Application.Current.FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 1, 6, 1),
            Margin = new Thickness(0, 0, 6, 0)
        };
        _destDangerText = new TextBlock { FontSize = 10, FontWeight = FontWeights.Bold, Foreground = (Brush)Application.Current.FindResource("Accent") };
        _destDangerBadge.Child = _destDangerText;
        badgeStack.Children.Add(_destDangerBadge);

        _destPriceBadge = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x33, 0x4E, 0xC9, 0x90)),
            BorderBrush = (Brush)Application.Current.FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 1, 6, 1)
        };
        _destPriceText = new TextBlock { FontSize = 10, FontWeight = FontWeights.Bold, Foreground = (Brush)Application.Current.FindResource("Gain") };
        _destPriceBadge.Child = _destPriceText;
        badgeStack.Children.Add(_destPriceBadge);

        DockPanel.SetDock(badgeStack, Dock.Right);
        dossierHeader.Children.Add(badgeStack);

        _destTitleText = new TextBlock
        {
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)Application.Current.FindResource("Text"),
            VerticalAlignment = VerticalAlignment.Center
        };
        dossierHeader.Children.Add(_destTitleText);
        leftStack.Children.Add(dossierHeader);

        _destEcosystemText = new TextBlock
        {
            FontSize = 11.5,
            Foreground = (Brush)Application.Current.FindResource("Text"),
            Margin = new Thickness(0, 0, 0, 4),
            TextWrapping = TextWrapping.Wrap
        };
        leftStack.Children.Add(_destEcosystemText);

        _destSpecialtyText = new TextBlock
        {
            FontSize = 11.5,
            Foreground = (Brush)Application.Current.FindResource("Gain"),
            Margin = new Thickness(0, 0, 0, 6),
            TextWrapping = TextWrapping.Wrap
        };
        leftStack.Children.Add(_destSpecialtyText);

        // Sublocations / POIs
        leftStack.Children.Add(new TextBlock
        {
            Text = "CÁC ĐỊA DANH & TỤ ĐIỂM NỔI BẬT TRONG THÀNH:",
            Style = (Style)Application.Current.FindResource("Caption"),
            Foreground = (Brush)Application.Current.FindResource("Faint"),
            Margin = new Thickness(0, 4, 0, 3)
        });
        _destSublocationsPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        leftStack.Children.Add(_destSublocationsPanel);

        // Prominent NPCs
        _destNpcText = new TextBlock
        {
            FontSize = 11,
            Foreground = (Brush)Application.Current.FindResource("Muted"),
            TextWrapping = TextWrapping.Wrap
        };
        leftStack.Children.Add(_destNpcText);

        leftCard.Child = leftStack;
        Grid.SetColumn(leftCard, 0);
        consoleGrid.Children.Add(leftCard);

        // Right Column: Expedition Planning & Dispatch
        var rightCard = new Border
        {
            Background = (Brush)Application.Current.FindResource("Rail"),
            BorderBrush = (Brush)Application.Current.FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10)
        };

        var rightStack = new StackPanel();

        rightStack.Children.Add(new TextBlock
        {
            Text = "PHƯƠNG THỨC VIỄN HÀNH & ĐIỀU ĐỘNG:",
            Style = (Style)Application.Current.FindResource("Overline"),
            Margin = new Thickness(0, 0, 0, 6)
        });

        // Mode toggles
        var modesGrid = new UniformGrid { Columns = 3, Margin = new Thickness(0, 0, 0, 8) };
        _walkModeBtn = CreateModeButton("🚶 Bộ Hành", "0 Lộ phí · Chuẩn", TravelMode.Walk);
        _horseModeBtn = CreateModeButton("⚡ Dịch Mã", "Nhanh x2 · Bạc", TravelMode.Horse);
        _caravanModeBtn = CreateModeButton("🛡️ Tiêu Cục", "Bảo Tiêu An Toàn", TravelMode.Caravan);
        modesGrid.Children.Add(_walkModeBtn);
        modesGrid.Children.Add(_horseModeBtn);
        modesGrid.Children.Add(_caravanModeBtn);
        rightStack.Children.Add(modesGrid);

        // Route path
        _routePathText = new TextBlock
        {
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.FindResource("Accent"),
            Margin = new Thickness(0, 0, 0, 4),
            TextWrapping = TextWrapping.Wrap
        };
        rightStack.Children.Add(_routePathText);

        // Metrics breakdown
        _expeditionMetricsText = new TextBlock
        {
            FontSize = 11.5,
            Foreground = (Brush)Application.Current.FindResource("Text"),
            Margin = new Thickness(0, 0, 0, 10),
            TextWrapping = TextWrapping.Wrap
        };
        rightStack.Children.Add(_expeditionMetricsText);

        // Action Depart button
        _departBtn = new Button
        {
            Content = "Khởi Hành Viễn Hành 🐎",
            Style = (Style)Application.Current.FindResource("PrimaryButton"),
            Padding = new Thickness(20, 8, 20, 8),
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _departBtn.Click += (_, _) =>
        {
            if (_selectedCityId != _currentCityId)
            {
                TravelRequested?.Invoke(_selectedCityId, _selectedMode);
            }
        };
        rightStack.Children.Add(_departBtn);

        rightCard.Child = rightStack;
        Grid.SetColumn(rightCard, 2);
        consoleGrid.Children.Add(rightCard);

        consoleBorder.Child = consoleGrid;
        mainStack.Children.Add(consoleBorder);

        Child = mainStack;

        UpdatePlanUI();
    }

    private Button CreateModeButton(string title, string subtitle, TravelMode mode)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.Bold,
            FontSize = 11.5,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        stack.Children.Add(new TextBlock
        {
            Text = subtitle,
            FontSize = 9.5,
            Foreground = (Brush)Application.Current.FindResource("Muted"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 1, 0, 0)
        });

        var btn = new Button
        {
            Content = stack,
            Style = (Style)Application.Current.FindResource("GhostButton"),
            Margin = new Thickness(2, 0, 2, 0),
            Padding = new Thickness(4, 5, 4, 5)
        };
        btn.Click += (_, _) =>
        {
            _selectedMode = mode;
            UpdateModeButtons();
            UpdatePlanUI();
        };
        return btn;
    }

    private void UpdateModeButtons()
    {
        _walkModeBtn.Style = (Style)Application.Current.FindResource(_selectedMode == TravelMode.Walk ? "PrimaryButton" : "GhostButton");
        _horseModeBtn.Style = (Style)Application.Current.FindResource(_selectedMode == TravelMode.Horse ? "PrimaryButton" : "GhostButton");
        _caravanModeBtn.Style = (Style)Application.Current.FindResource(_selectedMode == TravelMode.Caravan ? "PrimaryButton" : "GhostButton");
    }

    private void OnCanvasNodeSelected(long citadelId)
    {
        _selectedCityId = citadelId;
        _canvas.SetSelection(citadelId);
        UpdatePlanUI();
    }

    private void UpdatePlanUI()
    {
        UpdateModeButtons();

        var targetCitadel = _map.Find(_selectedCityId);
        if (targetCitadel is null) return;

        var targetEco = _map.GetEcosystem(_selectedCityId);

        // Update Dossier UI (Left Column)
        var regionName = GetRegionDisplayName(targetCitadel.Region);
        var rankBadge = GetCitadelRankTitle(targetCitadel.Id);
        _destTitleText.Text = $"{rankBadge} {targetCitadel.NameVi} · {regionName}";

        var pricePct = targetCitadel.PriceFactorPermille / 10.0;
        _destPriceText.Text = $"Vật giá: {pricePct:0.#}%";
        _destPriceBadge.Background = new SolidColorBrush(pricePct > 105
            ? Color.FromArgb(0x33, 0xE5, 0xB5, 0x48)
            : Color.FromArgb(0x33, 0x4E, 0xC9, 0x90));

        _destDangerText.Text = $"Hiểm trở: Cấp {targetEco.DangerLevel}";
        _destDangerBadge.Background = new SolidColorBrush(targetEco.DangerLevel >= 3
            ? Color.FromArgb(0x44, 0xC0, 0x39, 0x2B)
            : Color.FromArgb(0x33, 0x71, 0x82, 0x99));

        _destEcosystemText.Text = $"🌿 Địa hình: {targetEco.TopographyVi} | Khí hậu: {targetEco.ClimateVi}";
        _destSpecialtyText.Text = $"📦 Sản vật danh trấn: {targetEco.PrimarySpecialtyVi}, {targetEco.SecondarySpecialtyVi}";

        // Sublocations list
        _destSublocationsPanel.Children.Clear();
        var subs = _map.Sublocations(_selectedCityId);
        if (subs.Count > 0)
        {
            foreach (var s in subs)
            {
                var icon = GetSublocationIcon(s.Kind);
                var pill = new Border
                {
                    Background = (Brush)Application.Current.FindResource("SurfaceAlt"),
                    BorderBrush = (Brush)Application.Current.FindResource("Line"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(0, 0, 5, 4)
                };
                pill.Child = new TextBlock
                {
                    Text = $"{icon} {s.NameVi}",
                    FontSize = 10,
                    Foreground = (Brush)Application.Current.FindResource("Text")
                };
                _destSublocationsPanel.Children.Add(pill);
            }
        }
        else
        {
            _destSublocationsPanel.Children.Add(new TextBlock
            {
                Text = "Chợ Đông, Quan Nha, Dịch Trạm",
                FontSize = 10.5,
                Foreground = (Brush)Application.Current.FindResource("Faint")
            });
        }

        // Prominent NPCs in this citadel
        if (_kernel != null && _kernel.Capabilities.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var npcs) && npcs != null)
        {
            var residentNpcs = npcs.FindByCitadel(_selectedCityId);
            if (residentNpcs.Count > 0)
            {
                var names = string.Join(" · ", residentNpcs.Take(4).Select(n => $"{n.NameVi} ({n.Role})"));
                _destNpcText.Text = $"👥 Nhân sĩ đương thời: {names}{(residentNpcs.Count > 4 ? $" và {residentNpcs.Count - 4} vị khác..." : "")}";
            }
            else
            {
                _destNpcText.Text = "👥 Nhân sĩ đương thời: Thương nhân và bá tánh địa phương.";
            }
        }
        else
        {
            _destNpcText.Text = "👥 Nhân sĩ đương thời: Chưa có thám báo.";
        }

        // Update Expedition Planning (Right Column)
        if (_selectedCityId == _currentCityId)
        {
            _routePathText.Text = $"📍 Bạn hiện đang đóng quân tại {targetCitadel.NameVi}.";
            _expeditionMetricsText.Text = "Bấm chọn một đại thành khác trên bản đồ để hoạch định lộ trình viễn hành.";
            _departBtn.IsEnabled = false;
            _departBtn.Content = $"Đang Ở {targetCitadel.NameVi} 📍";
            return;
        }

        var quote = _map.QuoteTravel(_currentCityId, _selectedCityId, _selectedMode);
        var pathNames = string.Join(" ➔ ", quote.PathNodeIds.Select(id => _map.Find(id)?.NameVi ?? id.ToString()));

        _routePathText.Text = $"🗺️ Lộ trình ({quote.ActualDays} ngày): {pathNames}";

        var feeText = quote.TravelFee > 0 ? $"{quote.TravelFee} Quan Lộ Phí" : "Miễn phí lộ phí";
        _expeditionMetricsText.Text = $"✦ Tiêu hao: {quote.RationsNeeded} phần lương khô | +{quote.FatigueExpected}% mệt mỏi | {feeText}";

        _departBtn.IsEnabled = true;
        _departBtn.Content = $"Khởi Hành Đến {targetCitadel.NameVi} ({quote.ActualDays}N) 🐎";
    }

    private static string GetSublocationIcon(string kind) => kind switch
    {
        "market" => "🏪",
        "tavern" or "teahouse" => "🍵",
        "camp" or "barracks" => "⚔️",
        "dock" or "port" => "⛵",
        "gov" or "palace" => "🏛️",
        _ => "🏮"
    };

    private static string GetCitadelRankTitle(long id) => id switch
    {
        1 or 2 => "🏛️ [Đế Đô]",
        3 or 7 or 10 or 13 or 15 => "🏯 [Đại Đô]",
        6 or 9 or 11 or 12 => "⛵ [Thương Cảng]",
        _ => "🛡️ [Biên Ải]"
    };

    private static string GetRegionDisplayName(string region) => region switch
    {
        "trung_nguyen" => "Trung Nguyên",
        "quan_trung" => "Quan Trung",
        "ha_bac" => "Hà Bắc",
        "u_chau" => "U Châu",
        "thanh_chau" => "Thanh Châu",
        "du_chau" => "Dự Châu",
        "tu_chau" => "Từ Châu",
        "kinh_chau" => "Kinh Châu",
        "giang_dong" => "Giang Đông",
        "ba_thuc" or "ich_chau" => "Ba Thục",
        _ => "Thiên Hạ"
    };

    private static Border CreateLegendBadge(string label, Color color)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x33, color.R, color.G, color.B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x88, color.R, color.G, color.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(6, 0, 0, 0)
        };
        border.Child = new TextBlock
        {
            Text = label,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B))
        };
        return border;
    }

    /// <summary>
    /// Direct3D / DrawingContext Canvas that plots citadels, terrain mountains,
    /// river streams, graticules, player beacon, and floating reconnaissance HUD.
    /// </summary>
    private sealed class MapCanvas : FrameworkElement
    {
        private readonly IWorldMap _map;
        private readonly long _currentCityId;
        private long _selectedCityId;
        private long _hoveredCityId;
        private Point _mousePos;

        public event Action<long>? NodeSelected;

        // Frozen drawing assets for 60fps high performance (R_PERF)
        private static readonly Brush MapBgBrush = new LinearGradientBrush(
            Color.FromRgb(0x0A, 0x0E, 0x14),
            Color.FromRgb(0x13, 0x1A, 0x24),
            new Point(0, 0),
            new Point(1, 1));

        private static readonly Pen MapBorderPen = new(new SolidColorBrush(Color.FromRgb(0x28, 0x36, 0x48)), 1.2);
        private static readonly Pen CornerOrnamentPen = new(new SolidColorBrush(Color.FromRgb(0xA8, 0x86, 0x42)), 1.5);
        private static readonly Pen GridPen = new(new SolidColorBrush(Color.FromArgb(0x18, 0x8F, 0xB2, 0xD4)), 1.0);

        private static readonly Pen RouteNormalPen = new(new SolidColorBrush(Color.FromRgb(0x28, 0x38, 0x4D)), 1.8)
        {
            DashStyle = DashStyles.Dash
        };

        private static readonly Pen RouteAuraPen = new(new SolidColorBrush(Color.FromArgb(0x55, 0xE5, 0xB5, 0x48)), 7.0);
        private static readonly Pen RouteHighlightedPen = new(new SolidColorBrush(Color.FromRgb(0xFF, 0xCF, 0x4D)), 3.0);

        private static readonly Pen CurrentBeaconPen1 = new(new SolidColorBrush(Color.FromArgb(0xCC, 0x4E, 0xC9, 0x90)), 2.5);
        private static readonly Pen CurrentBeaconPen2 = new(new SolidColorBrush(Color.FromArgb(0x55, 0x4E, 0xC9, 0x90)), 1.5);
        private static readonly Pen SelectedReticlePen = new(new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00)), 2.5);

        private static readonly Brush TextWhiteBrush = new SolidColorBrush(Color.FromRgb(0xEB, 0xF0, 0xF8));
        private static readonly Brush TextGoldBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0xB5, 0x48));
        private static readonly Brush TextGreenBrush = new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0x90));
        private static readonly Brush TextFaintBrush = new SolidColorBrush(Color.FromRgb(0x71, 0x82, 0x99));
        private static readonly Brush TextMutedBrush = new SolidColorBrush(Color.FromRgb(0x9E, 0xAC, 0xBF));
        private static readonly Brush DayBadgeBg = new SolidColorBrush(Color.FromRgb(0x16, 0x20, 0x2D));

        // River & Terrain Brushes
        private static readonly Pen YellowRiverPen = new(new SolidColorBrush(Color.FromArgb(0x55, 0xBF, 0x8A, 0x30)), 9.0);
        private static readonly Pen YangtzeRiverPen = new(new SolidColorBrush(Color.FromArgb(0x55, 0x1E, 0x72, 0x9B)), 11.0);
        private static readonly Brush MountainBrush = new SolidColorBrush(Color.FromArgb(0x38, 0x42, 0x58, 0x6E));
        private static readonly Pen MountainPen = new(new SolidColorBrush(Color.FromArgb(0x55, 0x7E, 0x92, 0xAB)), 1.0);

        // Tooltip HUD Brushes
        private static readonly Brush HudBgBrush = new SolidColorBrush(Color.FromArgb(0xF2, 0x0F, 0x16, 0x22));
        private static readonly Pen HudBorderPen = new(new SolidColorBrush(Color.FromRgb(0xC5, 0xA0, 0x59)), 1.2);

        static MapCanvas()
        {
            MapBgBrush.Freeze();
            MapBorderPen.Freeze();
            CornerOrnamentPen.Freeze();
            GridPen.Freeze();
            RouteNormalPen.Freeze();
            RouteAuraPen.Freeze();
            RouteHighlightedPen.Freeze();
            CurrentBeaconPen1.Freeze();
            CurrentBeaconPen2.Freeze();
            SelectedReticlePen.Freeze();
            TextWhiteBrush.Freeze();
            TextGoldBrush.Freeze();
            TextGreenBrush.Freeze();
            TextFaintBrush.Freeze();
            TextMutedBrush.Freeze();
            DayBadgeBg.Freeze();
            YellowRiverPen.Freeze();
            YangtzeRiverPen.Freeze();
            MountainBrush.Freeze();
            MountainPen.Freeze();
            HudBgBrush.Freeze();
            HudBorderPen.Freeze();
        }

        public MapCanvas(IWorldMap map, long currentCityId, long selectedCityId)
        {
            _map = map;
            _currentCityId = currentCityId;
            _selectedCityId = selectedCityId;

            Height = 460;
            MinHeight = 400;
            ClipToBounds = true;
            Cursor = Cursors.Arrow;

            MouseMove += OnMouseMove;
            MouseLeave += (_, _) =>
            {
                _hoveredCityId = 0;
                InvalidateVisual();
            };
            MouseLeftButtonDown += OnMouseLeftButtonDown;
        }

        public void SetSelection(long selectedId)
        {
            _selectedCityId = selectedId;
            InvalidateVisual();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            _mousePos = e.GetPosition(this);
            var hit = HitTestCity(_mousePos);
            if (hit != _hoveredCityId)
            {
                _hoveredCityId = hit;
                Cursor = hit > 0 ? Cursors.Hand : Cursors.Arrow;
                InvalidateVisual();
            }
            else if (_hoveredCityId > 0)
            {
                InvalidateVisual(); // Redraw tooltip following cursor smoothly
            }
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(this);
            var hit = HitTestCity(pos);
            if (hit > 0)
            {
                _selectedCityId = hit;
                NodeSelected?.Invoke(hit);
                InvalidateVisual();
            }
        }

        private long HitTestCity(Point p)
        {
            var scaleX = ActualWidth > 0 ? ActualWidth / 750.0 : 1.0;
            var scaleY = ActualHeight > 0 ? ActualHeight / 480.0 : 1.0;

            foreach (var c in _map.Citadels)
            {
                var eco = _map.GetEcosystem(c.Id);
                var cx = eco.MapX * scaleX;
                var cy = eco.MapY * scaleY;
                var dx = p.X - cx;
                var dy = p.Y - cy;
                if (dx * dx + dy * dy <= 24 * 24)
                {
                    return c.Id;
                }
            }
            return 0;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            var w = ActualWidth;
            var h = ActualHeight;
            if (w <= 10 || h <= 10) return;

            // 1. Parchment Background & Graticule
            dc.DrawRoundedRectangle(MapBgBrush, MapBorderPen, new Rect(0, 0, w, h), 8, 8);
            DrawGraticuleAndBorderOrnaments(dc, w, h);

            // 2. Natural Geography: Ocean, Mountains & Great Rivers
            DrawNaturalGeography(dc, w, h);

            // 3. Ancient Cartography Ornaments: Compass Rose
            DrawCompassRose(dc, w, h);

            var scaleX = w / 750.0;
            var scaleY = h / 480.0;

            // 4. Calculate Selected Travel Route Path
            var pathSet = new HashSet<long>();
            var pathList = new List<long>();
            if (_selectedCityId > 0 && _selectedCityId != _currentCityId)
            {
                var quote = _map.QuoteTravel(_currentCityId, _selectedCityId, TravelMode.Walk);
                foreach (var id in quote.PathNodeIds)
                {
                    pathSet.Add(id);
                    pathList.Add(id);
                }
            }

            // 5. Draw Routes (Interstate Highways)
            foreach (var route in _map.Routes)
            {
                var eco1 = _map.GetEcosystem(route.FromCitadelId);
                var eco2 = _map.GetEcosystem(route.ToCitadelId);

                var p1 = new Point(eco1.MapX * scaleX, eco1.MapY * scaleY);
                var p2 = new Point(eco2.MapX * scaleX, eco2.MapY * scaleY);

                var isPathEdge = pathSet.Contains(route.FromCitadelId) && pathSet.Contains(route.ToCitadelId);

                if (isPathEdge)
                {
                    // Glowing outer aura + bright golden path
                    dc.DrawLine(RouteAuraPen, p1, p2);
                    dc.DrawLine(RouteHighlightedPen, p1, p2);

                    // Directional arrowhead
                    DrawRouteArrow(dc, p1, p2);
                }
                else
                {
                    dc.DrawLine(RouteNormalPen, p1, p2);
                }

                // Distance milestone badge in the middle
                var mid = new Point((p1.X + p2.X) * 0.5, (p1.Y + p2.Y) * 0.5);
                var badgeRect = new Rect(mid.X - 11, mid.Y - 8, 22, 16);
                dc.DrawRoundedRectangle(DayBadgeBg, isPathEdge ? RouteHighlightedPen : MapBorderPen, badgeRect, 4, 4);

                var dayText = new FormattedText(
                    $"{route.Days}N",
                    Vietnamese,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    8.5,
                    isPathEdge ? TextGoldBrush : TextFaintBrush,
                    1.0);
                dc.DrawText(dayText, new Point(mid.X - dayText.Width * 0.5, mid.Y - dayText.Height * 0.5));
            }

            // 6. Draw Citadel Nodes with Ornate Architecture
            foreach (var c in _map.Citadels)
            {
                var eco = _map.GetEcosystem(c.Id);
                var pt = new Point(eco.MapX * scaleX, eco.MapY * scaleY);

                var isCurrent = (c.Id == _currentCityId);
                var isSelected = (c.Id == _selectedCityId);
                var isHovered = (c.Id == _hoveredCityId);

                DrawCitadelNode(dc, c, eco, pt, isCurrent, isSelected, isHovered);
            }

            // 7. Floating Reconnaissance HUD Tooltip on Hover
            if (_hoveredCityId > 0)
            {
                DrawHoverReconnaissanceHud(dc, _hoveredCityId, _mousePos, w, h);
            }
        }

        private void DrawGraticuleAndBorderOrnaments(DrawingContext dc, double w, double h)
        {
            // Subtle graticule grid lines
            for (double x = 100; x < w - 20; x += 120)
            {
                dc.DrawLine(GridPen, new Point(x, 10), new Point(x, h - 10));
            }
            for (double y = 80; y < h - 20; y += 100)
            {
                dc.DrawLine(GridPen, new Point(10, y), new Point(w - 10, y));
            }

            // 4 Ornate corner brackets
            const double cSize = 14;
            // Top-Left
            dc.DrawLine(CornerOrnamentPen, new Point(8, 8 + cSize), new Point(8, 8));
            dc.DrawLine(CornerOrnamentPen, new Point(8, 8), new Point(8 + cSize, 8));
            // Top-Right
            dc.DrawLine(CornerOrnamentPen, new Point(w - 8 - cSize, 8), new Point(w - 8, 8));
            dc.DrawLine(CornerOrnamentPen, new Point(w - 8, 8), new Point(w - 8, 8 + cSize));
            // Bottom-Left
            dc.DrawLine(CornerOrnamentPen, new Point(8, h - 8 - cSize), new Point(8, h - 8));
            dc.DrawLine(CornerOrnamentPen, new Point(8, h - 8), new Point(8 + cSize, h - 8));
            // Bottom-Right
            dc.DrawLine(CornerOrnamentPen, new Point(w - 8 - cSize, h - 8), new Point(w - 8, h - 8));
            dc.DrawLine(CornerOrnamentPen, new Point(w - 8, h - 8), new Point(w - 8, h - 8 - cSize));
        }

        private void DrawNaturalGeography(DrawingContext dc, double w, double h)
        {
            var scaleX = w / 750.0;
            var scaleY = h / 480.0;

            // 1. Coastline & Bohai / East China Sea in the East
            var coastGeom = new StreamGeometry();
            using (var ctx = coastGeom.Open())
            {
                ctx.BeginFigure(new Point(630 * scaleX, 10), true, true);
                ctx.BezierTo(
                    new Point(610 * scaleX, 180 * scaleY),
                    new Point(680 * scaleX, 280 * scaleY),
                    new Point(650 * scaleX, h),
                    true, true);
                ctx.LineTo(new Point(w, h), true, false);
                ctx.LineTo(new Point(w, 10), true, false);
            }
            coastGeom.Freeze();
            var seaBrush = new SolidColorBrush(Color.FromArgb(0x18, 0x1E, 0x50, 0x78));
            seaBrush.Freeze();
            dc.DrawGeometry(seaBrush, null, coastGeom);

            var seaText = new FormattedText(
                "ĐÔNG HẢI",
                Vietnamese,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                11,
                new SolidColorBrush(Color.FromArgb(0x40, 0x5D, 0x8A, 0xAA)),
                1.0);
            dc.DrawText(seaText, new Point(w - 65, 230 * scaleY));

            // 2. Great Wall in the North
            var wallPen = new Pen(new SolidColorBrush(Color.FromArgb(0x45, 0x88, 0x98, 0xA8)), 2.5);
            wallPen.Freeze();
            var wallGeom = new StreamGeometry();
            using (var ctx = wallGeom.Open())
            {
                ctx.BeginFigure(new Point(430 * scaleX, 45 * scaleY), false, false);
                ctx.BezierTo(
                    new Point(500 * scaleX, 65 * scaleY),
                    new Point(560 * scaleX, 35 * scaleY),
                    new Point(630 * scaleX, 55 * scaleY),
                    true, true);
            }
            wallGeom.Freeze();
            dc.DrawGeometry(null, wallPen, wallGeom);

            var wallText = new FormattedText(
                "TRƯỜNG THÀNH BIÊN ẢI",
                Vietnamese,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                8.5,
                new SolidColorBrush(Color.FromArgb(0x44, 0x88, 0x98, 0xA8)),
                1.0);
            dc.DrawText(wallText, new Point(480 * scaleX, 42 * scaleY));

            // 3. Mountain Ranges (Qinling, Taihang, Wushan)
            DrawMountainPeaks(dc, new Point(230 * scaleX, 300 * scaleY), 4, 18 * scaleX, 16 * scaleY);
            DrawMountainPeaks(dc, new Point(420 * scaleX, 150 * scaleY), 3, 16 * scaleX, 14 * scaleY);
            DrawMountainPeaks(dc, new Point(340 * scaleX, 410 * scaleY), 3, 16 * scaleX, 14 * scaleY);

            // 4. Yellow River (Hoàng Hà)
            var yellowRiver = new StreamGeometry();
            using (var ctx = yellowRiver.Open())
            {
                ctx.BeginFigure(new Point(190 * scaleX, 215 * scaleY), false, false);
                ctx.BezierTo(
                    new Point(330 * scaleX, 195 * scaleY),
                    new Point(410 * scaleX, 260 * scaleY),
                    new Point(610 * scaleX, 185 * scaleY),
                    true, true);
            }
            yellowRiver.Freeze();
            dc.DrawGeometry(null, YellowRiverPen, yellowRiver);

            var yellowText = new FormattedText(
                "HOÀNG HÀ",
                Vietnamese,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Italic, FontWeights.Bold, FontStretches.Normal),
                9.5,
                new SolidColorBrush(Color.FromArgb(0x60, 0xE5, 0xB5, 0x48)),
                1.0);
            dc.DrawText(yellowText, new Point(310 * scaleX, 195 * scaleY));

            // 5. Yangtze River (Trường Giang)
            var yangtzeRiver = new StreamGeometry();
            using (var ctx = yangtzeRiver.Open())
            {
                ctx.BeginFigure(new Point(170 * scaleX, 415 * scaleY), false, false);
                ctx.BezierTo(
                    new Point(320 * scaleX, 400 * scaleY),
                    new Point(470 * scaleX, 445 * scaleY),
                    new Point(640 * scaleX, 395 * scaleY),
                    true, true);
            }
            yangtzeRiver.Freeze();
            dc.DrawGeometry(null, YangtzeRiverPen, yangtzeRiver);

            var yangtzeText = new FormattedText(
                "TRƯỜNG GIANG",
                Vietnamese,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Italic, FontWeights.Bold, FontStretches.Normal),
                10,
                new SolidColorBrush(Color.FromArgb(0x60, 0x48, 0x9B, 0xC4)),
                1.0);
            dc.DrawText(yangtzeText, new Point(480 * scaleX, 430 * scaleY));
        }

        private static void DrawMountainPeaks(DrawingContext dc, Point origin, int count, double peakW, double peakH)
        {
            for (int i = 0; i < count; i++)
            {
                var peakX = origin.X + (i * peakW * 0.85);
                var peakY = origin.Y + ((i % 2 == 1) ? 5 : -4);

                var triGeom = new StreamGeometry();
                using (var ctx = triGeom.Open())
                {
                    ctx.BeginFigure(new Point(peakX - peakW * 0.5, peakY + peakH), true, true);
                    ctx.LineTo(new Point(peakX, peakY), true, false);
                    ctx.LineTo(new Point(peakX + peakW * 0.5, peakY + peakH), true, false);
                }
                triGeom.Freeze();
                dc.DrawGeometry(MountainBrush, MountainPen, triGeom);
            }
        }

        private static void DrawCompassRose(DrawingContext dc, double w, double h)
        {
            var cx = w - 48;
            var cy = 48;
            const double r = 20;

            var compassPen = new Pen(new SolidColorBrush(Color.FromArgb(0x44, 0xA8, 0x86, 0x42)), 1.0);
            compassPen.Freeze();
            dc.DrawEllipse(null, compassPen, new Point(cx, cy), r, r);

            // Cardinal arrows
            dc.DrawLine(CornerOrnamentPen, new Point(cx, cy - r), new Point(cx, cy + r));
            dc.DrawLine(compassPen, new Point(cx - r, cy), new Point(cx + r, cy));

            var northText = new FormattedText(
                "BẮC",
                Vietnamese,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                8.5,
                TextGoldBrush,
                1.0);
            dc.DrawText(northText, new Point(cx - northText.Width * 0.5, cy - r - 12));
        }

        private static void DrawRouteArrow(DrawingContext dc, Point p1, Point p2)
        {
            var mid = new Point((p1.X + p2.X) * 0.5, (p1.Y + p2.Y) * 0.5);
            var dx = p2.X - p1.X;
            var dy = p2.Y - p1.Y;
            var len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 10) return;

            var ux = dx / len;
            var uy = dy / len;
            var perpX = -uy;
            var perpY = ux;

            const double arrowLen = 9.0;
            const double arrowWidth = 5.0;

            var tip = new Point(mid.X + ux * 6, mid.Y + uy * 6);
            var left = new Point(tip.X - ux * arrowLen + perpX * arrowWidth, tip.Y - uy * arrowLen + perpY * arrowWidth);
            var right = new Point(tip.X - ux * arrowLen - perpX * arrowWidth, tip.Y - uy * arrowLen - perpY * arrowWidth);

            var arrowGeom = new StreamGeometry();
            using (var ctx = arrowGeom.Open())
            {
                ctx.BeginFigure(tip, true, true);
                ctx.LineTo(left, true, false);
                ctx.LineTo(right, true, false);
            }
            arrowGeom.Freeze();
            dc.DrawGeometry(TextGoldBrush, null, arrowGeom);
        }

        private void DrawCitadelNode(DrawingContext dc, CitadelDef c, CitadelEcosystemDef eco, Point pt, bool isCurrent, bool isSelected, bool isHovered)
        {
            var baseColor = GetRegionColor(c.Region);
            var nodeBrush = new SolidColorBrush(baseColor);
            nodeBrush.Freeze();

            var isCapital = c.Id is 1 or 2;
            var radius = isCapital ? 14.0 : 10.0;

            // 1. Outer Auras & Beacon rings
            if (isCurrent)
            {
                // Animated-style double pulse beacon
                dc.DrawEllipse(null, CurrentBeaconPen2, pt, radius + 10, radius + 10);
                dc.DrawEllipse(null, CurrentBeaconPen1, pt, radius + 5, radius + 5);
            }
            else if (isSelected)
            {
                // Golden target reticle
                dc.DrawEllipse(null, SelectedReticlePen, pt, radius + 5, radius + 5);
                DrawTargetReticle(dc, pt, radius + 8);
            }
            else if (isHovered)
            {
                var hoverPen = new Pen(new SolidColorBrush(Color.FromArgb(0xDD, 0xFF, 0xFF, 0xFF)), 2.0);
                hoverPen.Freeze();
                dc.DrawEllipse(null, hoverPen, pt, radius + 4, radius + 4);
            }

            // 2. Citadel Base Circle with 3D Border
            dc.DrawEllipse(nodeBrush, isCurrent ? CurrentBeaconPen1 : MapBorderPen, pt, radius, radius);

            // 3. Iconic Glyph on Node
            var glyph = isCapital ? "🏛️" : (c.Id is 3 or 7 or 10 or 13 or 15 ? "🏯" : (c.Id is 6 or 9 or 11 or 12 ? "⛵" : "🛡️"));
            var glyphText = new FormattedText(
                glyph,
                Vietnamese,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI Emoji"),
                isCapital ? 11 : 9.5,
                TextWhiteBrush,
                1.0);
            dc.DrawText(glyphText, new Point(pt.X - glyphText.Width * 0.5, pt.Y - glyphText.Height * 0.5));

            // 4. Parchment Town Nameplate below node
            var labelText = isCurrent ? $"📍 {c.NameVi}" : c.NameVi;
            var textBrush = isCurrent ? TextGreenBrush : (isSelected ? TextGoldBrush : TextWhiteBrush);
            var fontWt = (isCurrent || isSelected || isHovered) ? FontWeights.Bold : FontWeights.SemiBold;

            var ft = new FormattedText(
                labelText,
                Vietnamese,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, fontWt, FontStretches.Normal),
                isCurrent ? 12 : 10.5,
                textBrush,
                1.0);

            var textPos = new Point(pt.X - ft.Width * 0.5, pt.Y + radius + 4);
            var labelBg = new Rect(textPos.X - 5, textPos.Y - 1, ft.Width + 10, ft.Height + 2);
            dc.DrawRoundedRectangle(DayBadgeBg, isCurrent ? CurrentBeaconPen1 : (isSelected ? SelectedReticlePen : MapBorderPen), labelBg, 4, 4);
            dc.DrawText(ft, textPos);
        }

        private static void DrawTargetReticle(DrawingContext dc, Point p, double size)
        {
            const double len = 6;
            // Top-left
            dc.DrawLine(SelectedReticlePen, new Point(p.X - size, p.Y - size + len), new Point(p.X - size, p.Y - size));
            dc.DrawLine(SelectedReticlePen, new Point(p.X - size, p.Y - size), new Point(p.X - size + len, p.Y - size));
            // Top-right
            dc.DrawLine(SelectedReticlePen, new Point(p.X + size - len, p.Y - size), new Point(p.X + size, p.Y - size));
            dc.DrawLine(SelectedReticlePen, new Point(p.X + size, p.Y - size), new Point(p.X + size, p.Y - size + len));
            // Bottom-left
            dc.DrawLine(SelectedReticlePen, new Point(p.X - size, p.Y + size - len), new Point(p.X - size, p.Y + size));
            dc.DrawLine(SelectedReticlePen, new Point(p.X - size, p.Y + size), new Point(p.X - size + len, p.Y + size));
            // Bottom-right
            dc.DrawLine(SelectedReticlePen, new Point(p.X + size - len, p.Y + size), new Point(p.X + size, p.Y + size));
            dc.DrawLine(SelectedReticlePen, new Point(p.X + size, p.Y + size), new Point(p.X + size, p.Y + size - len));
        }

        private void DrawHoverReconnaissanceHud(DrawingContext dc, long citadelId, Point mouse, double canvasW, double canvasH)
        {
            var c = _map.Find(citadelId);
            if (c is null) return;

            var eco = _map.GetEcosystem(citadelId);
            const double hudW = 265;
            const double hudH = 155;

            // Clamping position so it does not overflow canvas
            var posX = mouse.X + 16;
            var posY = mouse.Y + 16;
            if (posX + hudW > canvasW - 8) posX = mouse.X - hudW - 12;
            if (posY + hudH > canvasH - 8) posY = mouse.Y - hudH - 12;
            if (posX < 8) posX = 8;
            if (posY < 8) posY = 8;

            var hudRect = new Rect(posX, posY, hudW, hudH);
            dc.DrawRoundedRectangle(HudBgBrush, HudBorderPen, hudRect, 6, 6);

            var curY = posY + 8;
            var startX = posX + 10;

            // 1. Header: Name & Region
            var rank = GetCitadelRankTitle(c.Id);
            var headerFt = new FormattedText(
                $"{rank} {c.NameVi} · {GetRegionDisplayName(c.Region)}",
                Vietnamese,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                12.5,
                TextGoldBrush,
                1.0);
            dc.DrawText(headerFt, new Point(startX, curY));
            curY += headerFt.Height + 5;

            // Divider
            var divPen = new Pen(new SolidColorBrush(Color.FromArgb(0x44, 0xC5, 0xA0, 0x59)), 1.0);
            divPen.Freeze();
            dc.DrawLine(divPen, new Point(startX, curY), new Point(posX + hudW - 10, curY));
            curY += 6;

            // 2. Ecosystem & Climate
            var ecoFt = new FormattedText(
                $"🌿 {eco.BiomeNameVi} · {eco.ClimateVi}",
                Vietnamese,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                10.5,
                TextWhiteBrush,
                1.0);
            dc.DrawText(ecoFt, new Point(startX, curY));
            curY += ecoFt.Height + 4;

            // 3. Danger Level & Price factor
            var dangerStars = new string('★', Math.Clamp(eco.DangerLevel, 1, 5));
            var pricePct = c.PriceFactorPermille / 10.0;
            var statsFt = new FormattedText(
                $"⚠️ Hiểm trở: {dangerStars} (Cấp {eco.DangerLevel}) | 💰 Vật giá: {pricePct:0.#}%",
                Vietnamese,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                10,
                TextMutedBrush,
                1.0);
            dc.DrawText(statsFt, new Point(startX, curY));
            curY += statsFt.Height + 4;

            // 4. Specialties
            var specFt = new FormattedText(
                $"📦 Sản vật: {eco.PrimarySpecialtyVi}, {eco.SecondarySpecialtyVi}",
                Vietnamese,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                10,
                TextGreenBrush,
                1.0);
            dc.DrawText(specFt, new Point(startX, curY));
            curY += specFt.Height + 4;

            // 5. Distance from current location
            var distanceText = (citadelId == _currentCityId)
                ? "📍 Bạn hiện đang đóng quân tại thành này."
                : $"🐎 {_map.QuoteTravel(_currentCityId, citadelId, TravelMode.Walk).ActualDays} ngày đường viễn hành.";
            var distFt = new FormattedText(
                distanceText,
                Vietnamese,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Italic, FontWeights.SemiBold, FontStretches.Normal),
                10,
                (citadelId == _currentCityId) ? TextGreenBrush : TextGoldBrush,
                1.0);
            dc.DrawText(distFt, new Point(startX, curY));
            curY += distFt.Height + 4;

            // 6. Action Hint
            var hintFt = new FormattedText(
                "👉 Nhấp chuột để khảo sát & điều phối viễn hành",
                Vietnamese,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                9.5,
                TextFaintBrush,
                1.0);
            dc.DrawText(hintFt, new Point(startX, curY));
        }

        private static Color GetRegionColor(string region) => region switch
        {
            "trung_nguyen" or "quan_trung" or "du_chau" => Color.FromRgb(0xC0, 0x39, 0x2B),
            "ha_bac" or "ky_chau" or "u_chau" or "thanh_chau" => Color.FromRgb(0x27, 0xAE, 0x60),
            "giang_dong" or "duong_chau" or "tu_chau" => Color.FromRgb(0x29, 0x80, 0xB9),
            "kinh_chau" => Color.FromRgb(0x16, 0xA0, 0x85),
            _ => Color.FromRgb(0x8E, 0x44, 0xAD) // Ba Thục
        };
    }
}
