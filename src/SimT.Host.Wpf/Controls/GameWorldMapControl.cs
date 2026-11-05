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
public sealed partial class GameWorldMapControl : Border
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

        // 2. Interactive Ornate Visual Map Canvas with Floating RPG Zoom HUD
        var mapContainer = new Grid();
        _canvas = new MapCanvas(map, currentCityId, _selectedCityId);
        _canvas.NodeSelected += OnCanvasNodeSelected;
        mapContainer.Children.Add(_canvas);
        mapContainer.Children.Add(CreateMapZoomHud());
        mainStack.Children.Add(mapContainer);

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

    private FrameworkElement CreateMapZoomHud()
    {
        var hudBorder = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 14, 14),
            Background = new SolidColorBrush(Color.FromArgb(0xDD, 0x0F, 0x16, 0x22)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x88, 0xC5, 0xA0, 0x59)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(4)
        };

        var stack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        var zoomInBtn = CreateMiniButton("➕", "Phóng to bản đồ (Cuộn chuột lên)", () => _canvas.ZoomIn());
        var zoomOutBtn = CreateMiniButton("➖", "Thu nhỏ bản đồ (Cuộn chuột xuống)", () => _canvas.ZoomOut());
        var centerBtn = CreateMiniButton("🎯", "Định vị đại thành bản thân đang trú ngụ", () => _canvas.CenterOnCitadel(_currentCityId));
        var resetBtn = CreateMiniButton("⟲", "Toàn cảnh thiên hạ 15 châu quận", () => _canvas.ResetView());

        var zoomLabel = new TextBlock
        {
            Text = "100%",
            Width = 44,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE5, 0xB5, 0x48)),
            ToolTip = "Tỷ lệ phóng thu hiện tại"
        };

        _canvas.ZoomChanged += zoom =>
        {
            zoomLabel.Text = $"{(int)Math.Round(zoom * 100)}%";
        };

        stack.Children.Add(zoomInBtn);
        stack.Children.Add(zoomLabel);
        stack.Children.Add(zoomOutBtn);

        var separator = new Border
        {
            Width = 1,
            Height = 16,
            Background = new SolidColorBrush(Color.FromArgb(0x55, 0xC5, 0xA0, 0x59)),
            Margin = new Thickness(4, 0, 4, 0)
        };
        stack.Children.Add(separator);

        stack.Children.Add(centerBtn);
        stack.Children.Add(resetBtn);

        hudBorder.Child = stack;
        return hudBorder;
    }

    private static Button CreateMiniButton(string text, string tooltip, Action onClick)
    {
        var btn = new Button
        {
            Content = text,
            ToolTip = tooltip,
            Width = 28,
            Height = 28,
            Margin = new Thickness(2),
            Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x1A, 0x24, 0x33)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xEB, 0xF0, 0xF8)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0x51, 0x6B)),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            FontSize = 12,
            FontWeight = FontWeights.Bold
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }
}
