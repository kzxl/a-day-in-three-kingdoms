using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SimT.Contracts.Playing;

namespace SimT.Host.Wpf.Controls;

public sealed partial class GameWorldMapControl
{
    /// <summary>
    /// Direct3D / DrawingContext Canvas that plots citadels, terrain mountains,
    /// river streams, graticules, player beacon, and floating reconnaissance HUD.
    /// Supports RPG-style Zoom In/Out (0.7x - 2.5x) anchored to mouse and Click-Drag Panning.
    /// </summary>
    private sealed class MapCanvas : FrameworkElement
    {
        private readonly IWorldMap _map;
        private readonly long _currentCityId;
        private long _selectedCityId;
        private long _hoveredCityId;
        private Point _mousePos;

        // Pan and Zoom state
        private double _panX;
        private double _panY;
        private double _zoom = 1.0;
        private bool _isDragging;
        private Point _lastDragPos;

        public event Action<long>? NodeSelected;
        public event Action<double>? ZoomChanged;

        public double Zoom => _zoom;

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
                if (_isDragging && !Mouse.LeftButton.HasFlag(MouseButtonState.Pressed) && !Mouse.RightButton.HasFlag(MouseButtonState.Pressed))
                {
                    _isDragging = false;
                    ReleaseMouseCapture();
                }
                InvalidateVisual();
            };
            LostMouseCapture += (_, _) =>
            {
                _isDragging = false;
                Cursor = Cursors.Arrow;
            };

            MouseLeftButtonDown += OnMouseLeftButtonDown;
            MouseLeftButtonUp += OnMouseLeftButtonUp;
            MouseRightButtonDown += OnMouseRightButtonDown;
            MouseRightButtonUp += OnMouseRightButtonUp;
            MouseWheel += OnMouseWheel;
        }

        public void SetSelection(long selectedId)
        {
            _selectedCityId = selectedId;
            InvalidateVisual();
        }

        public void ZoomIn()
        {
            ApplyZoom(Math.Clamp(_zoom * 1.20, 0.70, 2.50));
        }

        public void ZoomOut()
        {
            ApplyZoom(Math.Clamp(_zoom / 1.20, 0.70, 2.50));
        }

        public void ResetView()
        {
            _panX = 0;
            _panY = 0;
            _zoom = 1.0;
            InvalidateVisual();
            ZoomChanged?.Invoke(_zoom);
        }

        public void CenterOnCitadel(long citadelId)
        {
            var eco = _map.GetEcosystem(citadelId);
            var w = ActualWidth > 0 ? ActualWidth : 800.0;
            var h = ActualHeight > 0 ? ActualHeight : 640.0;
            var scaleX = w / 800.0;
            var scaleY = h / 640.0;

            var targetWorldX = eco.MapX * scaleX;
            var targetWorldY = eco.MapY * scaleY;
            var cx = w * 0.5;
            var cy = h * 0.5;

            // Offset from center scaled by zoom
            _panX = -(targetWorldX - cx) * _zoom;
            _panY = -(targetWorldY - cy) * _zoom;

            InvalidateVisual();
        }

        private void ApplyZoom(double targetZoom)
        {
            var ratio = targetZoom / _zoom;
            _panX *= ratio;
            _panY *= ratio;
            _zoom = targetZoom;
            InvalidateVisual();
            ZoomChanged?.Invoke(_zoom);
        }

        private TransformGroup GetWorldTransform(double w, double h)
        {
            var cx = w * 0.5;
            var cy = h * 0.5;
            var group = new TransformGroup();
            group.Children.Add(new TranslateTransform(-cx, -cy));
            group.Children.Add(new ScaleTransform(_zoom, _zoom));
            group.Children.Add(new TranslateTransform(cx + _panX, cy + _panY));
            return group;
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!IsMouseOver)
            {
                if (IsMouseCaptured)
                {
                    _isDragging = false;
                    ReleaseMouseCapture();
                }
                return;
            }

            var mouse = e.GetPosition(this);
            if (mouse.X < 0 || mouse.X > ActualWidth || mouse.Y < 0 || mouse.Y > ActualHeight)
            {
                return;
            }

            var zoomDelta = e.Delta > 0 ? 1.15 : 0.87;
            var targetZoom = Math.Clamp(_zoom * zoomDelta, 0.70, 2.50);

            var cx = ActualWidth * 0.5;
            var cy = ActualHeight * 0.5;

            // Anchor zoom towards mouse cursor position
            _panX = (_panX - (mouse.X - cx)) * (targetZoom / _zoom) + (mouse.X - cx);
            _panY = (_panY - (mouse.Y - cy)) * (targetZoom / _zoom) + (mouse.Y - cy);
            _zoom = targetZoom;

            InvalidateVisual();
            ZoomChanged?.Invoke(_zoom);
            e.Handled = true;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(this);
            _mousePos = pos;

            if (_isDragging)
            {
                if (e.LeftButton != MouseButtonState.Pressed && e.RightButton != MouseButtonState.Pressed)
                {
                    _isDragging = false;
                    ReleaseMouseCapture();
                    Cursor = Cursors.Arrow;
                    return;
                }

                _panX += pos.X - _lastDragPos.X;
                _panY += pos.Y - _lastDragPos.Y;
                _lastDragPos = pos;
                InvalidateVisual();
                return;
            }

            var hit = HitTestCity(pos);
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
            else
            {
                // Drag pan on empty canvas space
                _isDragging = true;
                _lastDragPos = pos;
                CaptureMouse();
                Cursor = Cursors.SizeAll;
            }
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                ReleaseMouseCapture();
                Cursor = HitTestCity(e.GetPosition(this)) > 0 ? Cursors.Hand : Cursors.Arrow;
            }
        }

        private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _lastDragPos = e.GetPosition(this);
            CaptureMouse();
            Cursor = Cursors.SizeAll;
        }

        private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                ReleaseMouseCapture();
                Cursor = HitTestCity(e.GetPosition(this)) > 0 ? Cursors.Hand : Cursors.Arrow;
            }
        }

        private long HitTestCity(Point p)
        {
            var w = ActualWidth > 0 ? ActualWidth : 800.0;
            var h = ActualHeight > 0 ? ActualHeight : 640.0;
            var scaleX = w / 800.0;
            var scaleY = h / 640.0;
            var transform = GetWorldTransform(w, h);

            foreach (var c in _map.Citadels)
            {
                var eco = _map.GetEcosystem(c.Id);
                var worldPt = new Point(eco.MapX * scaleX, eco.MapY * scaleY);
                var screenPt = transform.Transform(worldPt);

                var dx = p.X - screenPt.X;
                var dy = p.Y - screenPt.Y;
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

            // 2. Push World Transform for Zoom & Pan
            var worldTransform = GetWorldTransform(w, h);
            dc.PushTransform(worldTransform);

            // Natural Geography: Ocean, Mountains & Great Rivers
            DrawNaturalGeography(dc, w, h);

            var scaleX = w / 800.0;
            var scaleY = h / 640.0;

            // Calculate Selected Travel Route Path
            var pathSet = new HashSet<long>();
            if (_selectedCityId > 0 && _selectedCityId != _currentCityId)
            {
                var quote = _map.QuoteTravel(_currentCityId, _selectedCityId, TravelMode.Walk);
                foreach (var id in quote.PathNodeIds)
                {
                    pathSet.Add(id);
                }
            }

            // Draw Routes (Interstate Highways)
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

            // Draw Citadel Nodes with Ornate Architecture
            foreach (var c in _map.Citadels)
            {
                var eco = _map.GetEcosystem(c.Id);
                var pt = new Point(eco.MapX * scaleX, eco.MapY * scaleY);

                var isCurrent = (c.Id == _currentCityId);
                var isSelected = (c.Id == _selectedCityId);
                var isHovered = (c.Id == _hoveredCityId);

                DrawCitadelNode(dc, c, eco, pt, isCurrent, isSelected, isHovered);
            }

            // POP World Transform
            dc.Pop();

            // 3. Screen-Space Overlays (Compass Rose, HUD Tooltip)
            DrawCompassRose(dc, w, h);

            // Floating Reconnaissance HUD Tooltip on Hover
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
            var scaleX = w / 800.0;
            var scaleY = h / 640.0;

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
            var glyph = isCapital ? "🏛️" : (c.Id is 3 or 7 or 10 or 13 or 15 or 17 or 19 ? "🏯" : (c.Id is 6 or 9 or 11 or 12 or 20 ? "⛵" : "🛡️"));
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
            "tinh_chau" => Color.FromRgb(0x48, 0xBB, 0x78),
            "luong_chau" => Color.FromRgb(0xD9, 0x77, 0x06),
            "giao_chau" => Color.FromRgb(0x0D, 0x94, 0x88),
            _ => Color.FromRgb(0x8E, 0x44, 0xAD) // Ba Thục
        };
    }
}
