using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Hardware-accelerated Visual Caravan Route & Ambush Risk Trail Canvas Control.
/// Plots the connecting trade route between origin and destination citadels,
/// renders animated marching caravan carriage, route distance, and dangerous ambush hotspots.
/// </summary>
public sealed class GameCaravanTrailControl : FrameworkElement
{
    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(
            nameof(Progress),
            typeof(double),
            typeof(GameCaravanTrailControl),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    private string _fromCityName = "Lạc Dương";
    private string _toCityName = "Hứa Xương";
    private int _travelDays = 3;
    private int _ambushRisk = 15;
    private string _fleetName = "Xa Mã Tiêu Đoàn";

    // Cached brushes & pens
    private static readonly Brush BgTrackBrush = new SolidColorBrush(Color.FromRgb(0x13, 0x18, 0x22));
    private static readonly Pen PathPen = new(new SolidColorBrush(Color.FromRgb(0x3B, 0x48, 0x5D)), 3.0)
    {
        DashStyle = DashStyles.Dash
    };
    private static readonly Pen ActivePathPen = new(new SolidColorBrush(Color.FromRgb(0xD9, 0xA4, 0x41)), 3.0);
    private static readonly Brush CityNodeBrush = new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x37));
    private static readonly Pen CityNodePen = new(new SolidColorBrush(Color.FromRgb(0xD9, 0xA4, 0x41)), 2.0);
    private static readonly Brush TextLightBrush = new SolidColorBrush(Color.FromRgb(0xEB, 0xF0, 0xF7));
    private static readonly Brush TextMutedBrush = new SolidColorBrush(Color.FromRgb(0x8A, 0x97, 0xA8));
    private static readonly Brush WarningBrush = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));

    static GameCaravanTrailControl()
    {
        BgTrackBrush.Freeze();
        PathPen.Freeze();
        ActivePathPen.Freeze();
        CityNodeBrush.Freeze();
        CityNodePen.Freeze();
        TextLightBrush.Freeze();
        TextMutedBrush.Freeze();
        WarningBrush.Freeze();
    }

    public GameCaravanTrailControl()
    {
        Height = 120;
        MinHeight = 100;
        ClipToBounds = true;

        Loaded += (_, _) => StartCaravanAnimation();
    }

    public void SetRoute(string fromCity, string toCity, int travelDays, int ambushRisk, string fleetName)
    {
        _fromCityName = fromCity;
        _toCityName = toCity;
        _travelDays = travelDays;
        _ambushRisk = ambushRisk;
        _fleetName = fleetName;
        InvalidateVisual();
    }

    private void StartCaravanAnimation()
    {
        var anim = new System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, TimeSpan.FromSeconds(3.5))
        {
            RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
            AutoReverse = true,
            EasingFunction = new System.Windows.Media.Animation.SineEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut }
        };
        BeginAnimation(ProgressProperty, anim);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 80 || h <= 40) return;

        // Background track rounded card
        dc.DrawRoundedRectangle(BgTrackBrush, null, new Rect(0, 0, w, h), 8, 8);

        var startX = 60.0;
        var endX = w - 60.0;
        var centerY = h / 2.0;

        // 1. Draw Winding S-curve trail
        var midX = (startX + endX) / 2.0;
        var ctrlY1 = centerY - 24.0;
        var ctrlY2 = centerY + 24.0;

        var pathGeom = new PathGeometry();
        var figure = new PathFigure { StartPoint = new Point(startX, centerY), IsClosed = false };
        figure.Segments.Add(new BezierSegment(
            new Point(midX - 30, ctrlY1),
            new Point(midX + 30, ctrlY2),
            new Point(endX, centerY),
            true));
        pathGeom.Figures.Add(figure);
        dc.DrawGeometry(null, PathPen, pathGeom);

        // 2. Compute animated caravan position along Bezier
        var t = Math.Clamp(Progress, 0.0, 1.0);
        var p0 = new Point(startX, centerY);
        var p1 = new Point(midX - 30, ctrlY1);
        var p2 = new Point(midX + 30, ctrlY2);
        var p3 = new Point(endX, centerY);

        // Cubic Bezier interpolation: B(t) = (1-t)^3 P0 + 3(1-t)^2 t P1 + 3(1-t) t^2 P2 + t^3 P3
        var u = 1.0 - t;
        var tt = t * t;
        var uu = u * u;
        var uuu = uu * u;
        var ttt = tt * t;

        var carX = uuu * p0.X + 3 * uu * t * p1.X + 3 * u * tt * p2.X + ttt * p3.X;
        var carY = uuu * p0.Y + 3 * uu * t * p1.Y + 3 * u * tt * p2.Y + ttt * p3.Y;

        // Draw animated caravan carriage marker 🐎
        var carriageBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
        dc.DrawEllipse(carriageBrush, null, new Point(carX, carY), 14, 14);
        var horseText = new FormattedText("🐎",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI Emoji"),
            13,
            Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(horseText, new Point(carX - 7, carY - 8));

        // 3. Draw Ambush Risk Hotspot in middle
        if (_ambushRisk > 0)
        {
            var riskColor = _ambushRisk >= 25 ? Color.FromRgb(0xEF, 0x44, 0x44) : Color.FromRgb(0xF5, 0x9E, 0x0B);
            var riskPen = new Pen(new SolidColorBrush(riskColor), 1.2);
            var riskBrush = new SolidColorBrush(Color.FromArgb(0x40, riskColor.R, riskColor.G, riskColor.B));
            dc.DrawRoundedRectangle(riskBrush, riskPen, new Rect(midX - 52, centerY - 44, 104, 22), 4, 4);

            var riskText = new FormattedText($"⚠️ Nguy Cơ {_ambushRisk}%",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                10.5,
                new SolidColorBrush(riskColor),
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(riskText, new Point(midX - 44, centerY - 40));
        }

        // 4. Draw Fleet Badge below middle
        var fleetText = new FormattedText($"🛡️ {_fleetName} · {_travelDays} ngày đường",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            11,
            TextMutedBrush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(fleetText, new Point(midX - (fleetText.Width / 2.0), centerY + 28));

        // 5. Draw City Endpoints (Origin & Destination Nodes)
        DrawCityNode(dc, new Point(startX, centerY), _fromCityName, "Khởi Hành 📍", isOrigin: true);
        DrawCityNode(dc, new Point(endX, centerY), _toCityName, "Đích Đến 🏁", isOrigin: false);
    }

    private void DrawCityNode(DrawingContext dc, Point pt, string cityName, string roleName, bool isOrigin)
    {
        // Outer ring
        dc.DrawEllipse(CityNodeBrush, CityNodePen, pt, 18, 18);

        var iconText = new FormattedText(isOrigin ? "🏛️" : "🏯",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI Emoji"),
            13,
            Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(iconText, new Point(pt.X - 7, pt.Y - 8));

        // City Label below
        var cityText = new FormattedText(cityName,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            12,
            TextLightBrush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(cityText, new Point(pt.X - (cityText.Width / 2.0), pt.Y + 22));

        // Role Caption above
        var roleText = new FormattedText(roleName,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
            9.5,
            TextMutedBrush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(roleText, new Point(pt.X - (roleText.Width / 2.0), pt.Y - 32));
    }
}
