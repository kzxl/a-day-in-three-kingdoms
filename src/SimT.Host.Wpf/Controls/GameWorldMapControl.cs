using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimT.Contracts.Playing;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Hardware-accelerated interactive Three Kingdoms World Map Control.
/// Visualizes the 15 citadels, interstate highways, regional ecosystems,
/// player location, and interactive travel departure planning.
/// </summary>
public sealed class GameWorldMapControl : Border
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    private readonly IWorldMap _map;
    private readonly long _currentCityId;
    private long _selectedCityId;
    private TravelMode _selectedMode = TravelMode.Walk;

    private readonly MapCanvas _canvas;
    private readonly TextBlock _routeSummaryText;
    private readonly TextBlock _destEcosystemText;
    private readonly TextBlock _destSpecialtyText;
    private readonly TextBlock _quoteDetailsText;
    private readonly Button _walkModeBtn;
    private readonly Button _horseModeBtn;
    private readonly Button _caravanModeBtn;
    private readonly Button _departBtn;

    public event Action<long, TravelMode>? TravelRequested;

    public GameWorldMapControl(IWorldMap map, long currentCityId)
    {
        _map = map;
        _currentCityId = currentCityId;
        _selectedCityId = map.RouteNeighbours(currentCityId).FirstOrDefault();
        if (_selectedCityId == 0) _selectedCityId = map.Citadels.FirstOrDefault(c => c.Id != currentCityId)?.Id ?? currentCityId;

        Background = (Brush)Application.Current.FindResource("Surface");
        BorderBrush = (Brush)Application.Current.FindResource("Line");
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(8);
        Padding = new Thickness(16, 14, 16, 14);
        Margin = new Thickness(0, 0, 0, 16);

        var mainStack = new StackPanel();

        // 1. Header & Legend
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
            Text = "BẢN ĐỒ THIÊN HẠ & MẠNG LƯỚI QUAN ĐẠO 15 ĐẠI THÀNH",
            Style = (Style)Application.Current.FindResource("Overline")
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = $"📍 Đang trú tại: {currentCitadel?.NameVi ?? "Lạc Dương"} ({currentEco.BiomeNameVi} · {currentEco.ClimateVi})",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.FindResource("Accent"),
            Margin = new Thickness(0, 2, 0, 0)
        });
        headerDock.Children.Add(titleStack);
        mainStack.Children.Add(headerDock);

        // 2. Interactive Visual Map Canvas
        _canvas = new MapCanvas(map, currentCityId, _selectedCityId);
        _canvas.NodeSelected += OnCanvasNodeSelected;
        mainStack.Children.Add(_canvas);

        // 3. Travel Planning & Departure Panel
        var planCard = new Border
        {
            Background = (Brush)Application.Current.FindResource("Rail"),
            BorderBrush = (Brush)Application.Current.FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 12, 0, 0)
        };

        var planStack = new StackPanel();

        var planHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        _routeSummaryText = new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)Application.Current.FindResource("Ink"),
            VerticalAlignment = VerticalAlignment.Center
        };
        planHeader.Children.Add(_routeSummaryText);
        planStack.Children.Add(planHeader);

        // Ecosystem & Specialty details of target
        var ecoRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        _destEcosystemText = new TextBlock
        {
            FontSize = 11.5,
            Foreground = (Brush)Application.Current.FindResource("Faint"),
            Margin = new Thickness(0, 0, 16, 4)
        };
        _destSpecialtyText = new TextBlock
        {
            FontSize = 11.5,
            Foreground = (Brush)Application.Current.FindResource("Faint"),
            Margin = new Thickness(0, 0, 16, 4)
        };
        ecoRow.Children.Add(_destEcosystemText);
        ecoRow.Children.Add(_destSpecialtyText);
        planStack.Children.Add(ecoRow);

        // Mode toggles & action button
        var actionDock = new DockPanel();

        _departBtn = new Button
        {
            Content = "Khởi Hành Viễn Hành 🐎",
            Style = (Style)Application.Current.FindResource("PrimaryButton"),
            Padding = new Thickness(16, 6, 16, 6),
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };
        _departBtn.Click += (_, _) =>
        {
            if (_selectedCityId != _currentCityId)
            {
                TravelRequested?.Invoke(_selectedCityId, _selectedMode);
            }
        };
        DockPanel.SetDock(_departBtn, Dock.Right);
        actionDock.Children.Add(_departBtn);

        var modesPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        _walkModeBtn = CreateModeButton("🚶 Bộ Hành", TravelMode.Walk);
        _horseModeBtn = CreateModeButton("⚡ Dịch Mã", TravelMode.Horse);
        _caravanModeBtn = CreateModeButton("🛡️ Tiêu Cục", TravelMode.Caravan);

        modesPanel.Children.Add(_walkModeBtn);
        modesPanel.Children.Add(_horseModeBtn);
        modesPanel.Children.Add(_caravanModeBtn);

        _quoteDetailsText = new TextBlock
        {
            FontSize = 11,
            Foreground = (Brush)Application.Current.FindResource("Accent"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 0, 0)
        };
        modesPanel.Children.Add(_quoteDetailsText);

        actionDock.Children.Add(modesPanel);
        planStack.Children.Add(actionDock);

        planCard.Child = planStack;
        mainStack.Children.Add(planCard);

        Child = mainStack;

        UpdatePlanUI();
    }

    private Button CreateModeButton(string label, TravelMode mode)
    {
        var btn = new Button
        {
            Content = label,
            Style = (Style)Application.Current.FindResource("GhostButton"),
            Margin = new Thickness(0, 0, 6, 0),
            Padding = new Thickness(10, 4, 10, 4),
            FontSize = 11.5
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

        if (_selectedCityId == _currentCityId)
        {
            _routeSummaryText.Text = $"📍 Bạn hiện đang có mặt tại {targetCitadel.NameVi}. Hãy bấm chọn một thành khác trên bản đồ để lên kế hoạch viễn hành!";
            _destEcosystemText.Text = $"🌿 Hệ sinh thái: {targetEco.BiomeNameVi} · {targetEco.ClimateVi}";
            _destSpecialtyText.Text = $"📦 Đặc sản: {targetEco.PrimarySpecialtyVi}, {targetEco.SecondarySpecialtyVi}";
            _quoteDetailsText.Text = "Không cần di chuyển.";
            _departBtn.IsEnabled = false;
            return;
        }

        var quote = _map.QuoteTravel(_currentCityId, _selectedCityId, _selectedMode);
        var pathNames = string.Join(" ➔ ", quote.PathNodeIds.Select(id => _map.Find(id)?.NameVi ?? id.ToString()));

        _routeSummaryText.Text = $"🗺️ Lộ trình: {pathNames} ({quote.ActualDays} ngày đường)";
        _destEcosystemText.Text = $"🌿 {targetEco.BiomeNameVi} · {targetEco.ClimateVi} (Hiểm trở: Cấp {targetEco.DangerLevel})";
        _destSpecialtyText.Text = $"📦 Đặc sản: {targetEco.PrimarySpecialtyVi}, {targetEco.SecondarySpecialtyVi} · Vật giá: {targetCitadel.PriceFactorPermille / 10.0:0.#}%";

        var feeText = quote.TravelFee > 0 ? $"{quote.TravelFee} Quan lộ phí" : "Miễn phí lộ phí";
        _quoteDetailsText.Text = $"Tiêu hao dự kiến: {quote.ActualDays} ngày · {quote.RationsNeeded} lương khô · +{quote.FatigueExpected} mệt mỏi · {feeText}";
        _departBtn.IsEnabled = true;
    }

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
    /// Direct3D / DrawingContext Canvas that plots citadels and connects routes.
    /// </summary>
    private sealed class MapCanvas : FrameworkElement
    {
        private readonly IWorldMap _map;
        private readonly long _currentCityId;
        private long _selectedCityId;
        private long _hoveredCityId;

        public event Action<long>? NodeSelected;

        // Frozen drawing assets
        private static readonly Brush MapBgBrush = new SolidColorBrush(Color.FromRgb(0x0E, 0x13, 0x1B));
        private static readonly Pen MapBorderPen = new(new SolidColorBrush(Color.FromRgb(0x23, 0x2E, 0x3D)), 1.0);
        private static readonly Pen RouteNormalPen = new(new SolidColorBrush(Color.FromRgb(0x28, 0x36, 0x47)), 1.5);
        private static readonly Pen RouteHighlightedPen = new(new SolidColorBrush(Color.FromRgb(0xE5, 0xB5, 0x48)), 2.8);
        private static readonly Pen CurrentRingPen = new(new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0x90)), 2.5);
        private static readonly Pen SelectedRingPen = new(new SolidColorBrush(Color.FromRgb(0xE5, 0xB5, 0x48)), 2.5);

        private static readonly Brush TextWhiteBrush = new SolidColorBrush(Color.FromRgb(0xEB, 0xF0, 0xF8));
        private static readonly Brush TextGoldBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0xB5, 0x48));
        private static readonly Brush TextFaintBrush = new SolidColorBrush(Color.FromRgb(0x71, 0x82, 0x99));
        private static readonly Brush DayBadgeBg = new SolidColorBrush(Color.FromRgb(0x18, 0x22, 0x2E));

        static MapCanvas()
        {
            MapBgBrush.Freeze();
            MapBorderPen.Freeze();
            RouteNormalPen.Freeze();
            RouteHighlightedPen.Freeze();
            CurrentRingPen.Freeze();
            SelectedRingPen.Freeze();
            TextWhiteBrush.Freeze();
            TextGoldBrush.Freeze();
            TextFaintBrush.Freeze();
            DayBadgeBg.Freeze();
        }

        public MapCanvas(IWorldMap map, long currentCityId, long selectedCityId)
        {
            _map = map;
            _currentCityId = currentCityId;
            _selectedCityId = selectedCityId;

            Height = 440;
            MinHeight = 380;
            ClipToBounds = true;
            Cursor = Cursors.Arrow;

            MouseMove += OnMouseMove;
            MouseLeave += (_, _) => { _hoveredCityId = 0; InvalidateVisual(); };
            MouseLeftButtonDown += OnMouseLeftButtonDown;
        }

        public void SetSelection(long selectedId)
        {
            _selectedCityId = selectedId;
            InvalidateVisual();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(this);
            var hit = HitTestCity(pos);
            if (hit != _hoveredCityId)
            {
                _hoveredCityId = hit;
                Cursor = hit > 0 ? Cursors.Hand : Cursors.Arrow;
                InvalidateVisual();
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
                if (dx * dx + dy * dy <= 22 * 22)
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

            // 1. Background
            dc.DrawRoundedRectangle(MapBgBrush, MapBorderPen, new Rect(0, 0, w, h), 6, 6);

            // Subtle terrain contours / water bodies
            DrawMapFeatures(dc, w, h);

            var scaleX = w / 750.0;
            var scaleY = h / 480.0;

            // Highlighted path nodes
            var pathSet = new HashSet<long>();
            if (_selectedCityId > 0 && _selectedCityId != _currentCityId)
            {
                var quote = _map.QuoteTravel(_currentCityId, _selectedCityId, TravelMode.Walk);
                foreach (var id in quote.PathNodeIds) pathSet.Add(id);
            }

            // 2. Draw Routes (edges)
            foreach (var route in _map.Routes)
            {
                var eco1 = _map.GetEcosystem(route.FromCitadelId);
                var eco2 = _map.GetEcosystem(route.ToCitadelId);

                var p1 = new Point(eco1.MapX * scaleX, eco1.MapY * scaleY);
                var p2 = new Point(eco2.MapX * scaleX, eco2.MapY * scaleY);

                var isPathEdge = pathSet.Contains(route.FromCitadelId) && pathSet.Contains(route.ToCitadelId);
                var pen = isPathEdge ? RouteHighlightedPen : RouteNormalPen;

                dc.DrawLine(pen, p1, p2);

                // Small distance badge in the middle
                var mid = new Point((p1.X + p2.X) * 0.5, (p1.Y + p2.Y) * 0.5);
                var badgeRect = new Rect(mid.X - 9, mid.Y - 7, 18, 14);
                dc.DrawRoundedRectangle(DayBadgeBg, isPathEdge ? RouteHighlightedPen : MapBorderPen, badgeRect, 3, 3);

                var dayText = new FormattedText(
                    $"{route.Days}d",
                    Vietnamese,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    8.5,
                    isPathEdge ? TextGoldBrush : TextFaintBrush,
                    1.0);
                dc.DrawText(dayText, new Point(mid.X - dayText.Width * 0.5, mid.Y - dayText.Height * 0.5));
            }

            // 3. Draw Citadel Nodes
            foreach (var c in _map.Citadels)
            {
                var eco = _map.GetEcosystem(c.Id);
                var pt = new Point(eco.MapX * scaleX, eco.MapY * scaleY);

                var isCurrent = (c.Id == _currentCityId);
                var isSelected = (c.Id == _selectedCityId);
                var isHovered = (c.Id == _hoveredCityId);

                var baseColor = GetRegionColor(c.Region);
                var nodeBrush = new SolidColorBrush(baseColor);
                nodeBrush.Freeze();

                var radius = isCurrent ? 12.0 : (isSelected || isHovered ? 10.0 : 8.0);

                // Outer rings
                if (isCurrent)
                {
                    dc.DrawEllipse(null, CurrentRingPen, pt, radius + 5, radius + 5);
                }
                else if (isSelected)
                {
                    dc.DrawEllipse(null, SelectedRingPen, pt, radius + 4, radius + 4);
                }
                else if (isHovered)
                {
                    var hoverPen = new Pen(new SolidColorBrush(Color.FromArgb(0xAA, 0xFF, 0xFF, 0xFF)), 1.5);
                    hoverPen.Freeze();
                    dc.DrawEllipse(null, hoverPen, pt, radius + 3, radius + 3);
                }

                // Inner filled circle
                dc.DrawEllipse(nodeBrush, MapBorderPen, pt, radius, radius);

                // Label
                var labelText = isCurrent ? $"★ {c.NameVi}" : c.NameVi;
                var textBrush = isCurrent ? TextGoldBrush : (isSelected ? TextGoldBrush : TextWhiteBrush);
                var fontWt = (isCurrent || isSelected || isHovered) ? FontWeights.Bold : FontWeights.Normal;

                var ft = new FormattedText(
                    labelText,
                    Vietnamese,
                    FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, fontWt, FontStretches.Normal),
                    isCurrent ? 12 : 10.5,
                    textBrush,
                    1.0);

                var textPos = new Point(pt.X - ft.Width * 0.5, pt.Y + radius + 3);
                // Background shadow for label legibility
                var labelBg = new Rect(textPos.X - 3, textPos.Y - 1, ft.Width + 6, ft.Height + 2);
                dc.DrawRoundedRectangle(DayBadgeBg, null, labelBg, 3, 3);
                dc.DrawText(ft, textPos);
            }
        }

        private static void DrawMapFeatures(DrawingContext dc, double w, double h)
        {
            // Stylized Yangtze & Yellow river flow lines
            var riverPen = new Pen(new SolidColorBrush(Color.FromArgb(0x22, 0x29, 0x80, 0xB9)), 12.0);
            riverPen.Freeze();

            var scaleX = w / 750.0;
            var scaleY = h / 480.0;

            // Yellow River approximation
            var yellowStream = new StreamGeometry();
            using (var ctx = yellowStream.Open())
            {
                ctx.BeginFigure(new Point(200 * scaleX, 230 * scaleY), false, false);
                ctx.BezierTo(
                    new Point(350 * scaleX, 210 * scaleY),
                    new Point(430 * scaleX, 260 * scaleY),
                    new Point(600 * scaleX, 200 * scaleY),
                    true, true);
            }
            yellowStream.Freeze();
            dc.DrawGeometry(null, riverPen, yellowStream);

            // Yangtze River approximation
            var yangtzeStream = new StreamGeometry();
            using (var ctx = yangtzeStream.Open())
            {
                ctx.BeginFigure(new Point(180 * scaleX, 420 * scaleY), false, false);
                ctx.BezierTo(
                    new Point(350 * scaleX, 410 * scaleY),
                    new Point(500 * scaleX, 450 * scaleY),
                    new Point(650 * scaleX, 410 * scaleY),
                    true, true);
            }
            yangtzeStream.Freeze();
            dc.DrawGeometry(null, riverPen, yangtzeStream);
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
