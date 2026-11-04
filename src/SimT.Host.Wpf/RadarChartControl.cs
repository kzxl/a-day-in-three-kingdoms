using System.Globalization;
using System.Windows;
using System.Windows.Media;
using SimT.Contracts.Playing;

namespace SimT.Host.Wpf;

/// <summary>
/// High-performance WPF Radar / Spider Chart control for character profile visualization.
/// Adapted from ZeroCharts polar coordinate geometry algorithms.
/// Renders concentric octagonal grid lines, radial axis spokes, dynamic data polygons,
/// and value badges using Direct3D hardware-accelerated DrawingContext.
/// </summary>
public sealed class RadarChartControl : FrameworkElement
{
    public sealed record RadarAxisItem(string Code, string NameVi, double Value, double MaxValue)
    {
        public double Ratio => MaxValue > 0 ? Math.Clamp(Value / MaxValue, 0.05, 1.0) : 0.05;
    }

    private readonly List<RadarAxisItem> _axes = new();
    private Color _accentColor = Color.FromRgb(0xD9, 0xA4, 0x41); // Default Accent Gold

    // Cached pens and brushes for rendering
    private static readonly Brush GridBrush = new SolidColorBrush(Color.FromRgb(0x26, 0x30, 0x3D));
    private static readonly Pen GridPen = new(GridBrush, 1.0) { DashStyle = DashStyles.Dash };
    private static readonly Pen OuterGridPen = new(new SolidColorBrush(Color.FromRgb(0x36, 0x45, 0x5A)), 1.2);
    private static readonly Pen SpokePen = new(GridBrush, 1.0);
    private static readonly Brush LabelMutedBrush = new SolidColorBrush(Color.FromRgb(0x8A, 0x97, 0xA8));
    private static readonly Brush LabelHighBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
    private static readonly Brush DotCenterBrush = new SolidColorBrush(Color.FromRgb(0x0E, 0x11, 0x16));

    static RadarChartControl()
    {
        GridPen.Freeze();
        OuterGridPen.Freeze();
        SpokePen.Freeze();
        LabelMutedBrush.Freeze();
        LabelHighBrush.Freeze();
        DotCenterBrush.Freeze();
    }

    public RadarChartControl()
    {
        ClipToBounds = true;
    }

    public string ArchetypeTitle { get; private set; } = "NHÂN SĨ TAM QUỐC";

    public void SetCoreStats(CharacterAttributes a, NpcStatDelta? deltas = null)
    {
        _axes.Clear();

        var war = a.War + (deltas?.WarDelta ?? 0);
        var ldr = a.Ldr + (deltas?.LdrDelta ?? 0);
        var intl = a.Int + (deltas?.IntDelta ?? 0);
        var pol = a.Pol + (deltas?.PolDelta ?? 0);
        var dip = a.Dip + (deltas?.DipDelta ?? 0);
        var com = a.Com + (deltas?.ComDelta ?? 0);
        var log = a.Log + (deltas?.LogDelta ?? 0);
        var cra = a.Cra + (deltas?.CraDelta ?? 0);

        _axes.Add(new RadarAxisItem("WAR", "Võ Lực", war, 120.0));
        _axes.Add(new RadarAxisItem("LDR", "Thống Soái", ldr, 120.0));
        _axes.Add(new RadarAxisItem("INT", "Trí Tuệ", intl, 120.0));
        _axes.Add(new RadarAxisItem("POL", "Chính Trị", pol, 120.0));
        _axes.Add(new RadarAxisItem("DIP", "Ngoại Giao", dip, 120.0));
        _axes.Add(new RadarAxisItem("COM", "Thương Mại", com, 120.0));
        _axes.Add(new RadarAxisItem("LOG", "Hậu Cần", log, 120.0));
        _axes.Add(new RadarAxisItem("CRA", "Kỹ Nghệ", cra, 120.0));

        // Determine Archetype & Theme Color
        var maxStat = _axes.MaxBy(x => x.Value);
        if (maxStat is not null)
        {
            switch (maxStat.Code)
            {
                case "WAR":
                    _accentColor = Color.FromRgb(0xE0, 0x6C, 0x75); // Red
                    ArchetypeTitle = "VÕ TƯỚNG DŨNG QUÃNG · PHÁ TRẬN ĐOẠT KỲ";
                    break;
                case "LDR":
                    _accentColor = Color.FromRgb(0xF5, 0x9E, 0x0B); // Amber
                    ArchetypeTitle = "THỐNG SOÁI BA QUÂN · KỶ LUẬT THIẾT QUÂN";
                    break;
                case "INT":
                    _accentColor = Color.FromRgb(0x38, 0xBD, 0xF8); // Sky
                    ArchetypeTitle = "QUÂN SƯ MƯU LƯỢC · LIỆU ĐỊCH NHƯ THẦN";
                    break;
                case "POL":
                    _accentColor = Color.FromRgb(0x81, 0x8C, 0xF8); // Indigo
                    ArchetypeTitle = "TRỊ QUỐC AN DÂN · KINH BANG TẾ THẾ";
                    break;
                case "DIP":
                    _accentColor = Color.FromRgb(0xA7, 0x8B, 0xFA); // Purple
                    ArchetypeTitle = "DANH THUYẾT ĐẠI SỨ · TÂM LÒNG THU PHỤC";
                    break;
                case "COM":
                    _accentColor = Color.FromRgb(0x5A, 0xC0, 0x8A); // Gain Emerald
                    ArchetypeTitle = "ĐẠI THƯƠNG PHÚ HÀO · TÍCH TRỮ THIÊN HẠ";
                    break;
                case "LOG":
                    _accentColor = Color.FromRgb(0x34, 0xD3, 0x99); // Mint
                    ArchetypeTitle = "TRẤN THỦ LƯƠNG THẢO · VẬN TẢI THÔNG THƯƠNG";
                    break;
                case "CRA":
                    _accentColor = Color.FromRgb(0xFB, 0x92, 0x3C); // Orange
                    ArchetypeTitle = "THẦN CÔNG XẢO THỦ · CHẾ KHÍ TẠO VẬT";
                    break;
                default:
                    _accentColor = Color.FromRgb(0xD9, 0xA4, 0x41);
                    ArchetypeTitle = "NHÂN SĨ TAM QUỐC · HÀNH TRẠNG LỪNG DANH";
                    break;
            }
        }

        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var n = _axes.Count;
        if (n < 3 || ActualWidth <= 10 || ActualHeight <= 10) return;

        var cx = ActualWidth * 0.5;
        var cy = ActualHeight * 0.5;
        var maxRadius = Math.Min(cx, cy) - 34.0; // Reserve margin for axis text labels
        if (maxRadius <= 10) return;

        var angleStep = (2.0 * Math.PI) / n;
        var startAngle = -Math.PI * 0.5; // Start at top (WAR)

        // 1. Draw Concentric Octagonal Grid Webs (25%, 50%, 75%, 100%)
        for (var level = 1; level <= 4; level++)
        {
            var r = maxRadius * (level * 0.25);
            var isOuter = level == 4;
            var pen = isOuter ? OuterGridPen : GridPen;

            var webGeom = new StreamGeometry();
            using (var ctx = webGeom.Open())
            {
                var firstPt = new Point(cx + Math.Cos(startAngle) * r, cy + Math.Sin(startAngle) * r);
                ctx.BeginFigure(firstPt, isFilled: false, isClosed: true);

                for (var i = 1; i < n; i++)
                {
                    var angle = startAngle + (i * angleStep);
                    var pt = new Point(cx + Math.Cos(angle) * r, cy + Math.Sin(angle) * r);
                    ctx.LineTo(pt, isStroked: true, isSmoothJoin: true);
                }
            }
            webGeom.Freeze();
            dc.DrawGeometry(null, pen, webGeom);
        }

        // 2. Draw Radial Spokes & Axis Text Labels
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

        for (var i = 0; i < n; i++)
        {
            var angle = startAngle + (i * angleStep);
            var cos = Math.Cos(angle);
            var sin = Math.Sin(angle);

            var sx = cx + (cos * maxRadius);
            var sy = cy + (sin * maxRadius);
            dc.DrawLine(SpokePen, new Point(cx, cy), new Point(sx, sy));

            // Axis Label (e.g. "WAR 98" or "Võ Lực 98")
            var item = _axes[i];
            var isHigh = item.Value >= 80;
            var textBrush = isHigh ? LabelHighBrush : LabelMutedBrush;

            var labelText = $"{item.Code} {item.Value:N0}";
            var formatted = new FormattedText(
                labelText,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                11.0,
                textBrush,
                dpi);

            var lx = cx + (cos * (maxRadius + 18.0)) - (formatted.Width * 0.5);
            var ly = cy + (sin * (maxRadius + 18.0)) - (formatted.Height * 0.5);
            dc.DrawText(formatted, new Point(lx, ly));
        }

        // 3. Draw Character Data Polygon
        var dataPts = new Point[n];
        for (var i = 0; i < n; i++)
        {
            var angle = startAngle + (i * angleStep);
            var r = maxRadius * _axes[i].Ratio;
            dataPts[i] = new Point(cx + (Math.Cos(angle) * r), cy + (Math.Sin(angle) * r));
        }

        var dataGeom = new StreamGeometry();
        using (var ctx = dataGeom.Open())
        {
            ctx.BeginFigure(dataPts[0], isFilled: true, isClosed: true);
            for (var i = 1; i < n; i++)
            {
                ctx.LineTo(dataPts[i], isStroked: true, isSmoothJoin: true);
            }
        }
        dataGeom.Freeze();

        var fillBrush = new SolidColorBrush(Color.FromArgb(75, _accentColor.R, _accentColor.G, _accentColor.B));
        fillBrush.Freeze();

        var strokePen = new Pen(new SolidColorBrush(_accentColor), 2.0);
        strokePen.Freeze();

        dc.DrawGeometry(fillBrush, strokePen, dataGeom);

        // 4. Draw Glowing Data Points & Outer Ring
        var dotBrush = new SolidColorBrush(_accentColor);
        dotBrush.Freeze();
        var dotBorderPen = new Pen(DotCenterBrush, 1.2);
        dotBorderPen.Freeze();

        for (var i = 0; i < n; i++)
        {
            dc.DrawEllipse(dotBrush, dotBorderPen, dataPts[i], 3.5, 3.5);
        }

        // 5. Draw Center Point
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0x5E, 0x6B, 0x7C)), null, new Point(cx, cy), 2.5, 2.5);
    }
}
