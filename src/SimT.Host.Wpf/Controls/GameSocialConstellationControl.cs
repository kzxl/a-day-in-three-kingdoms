using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace SimT.Host.Wpf.Controls;

public enum SocialBondType
{
    SwornBrother, // Kim Lan Chi Giao 🤝
    Retainer,     // Môn Khách Gia Thần ⚔️
    Friend,       // Tri Kỷ Bằng Hữu 😊
    Rival,        // Túc Địch Cừu Nhân 💀
    Neutral       // Đồng Liêu Quen Biết 👤
}

public sealed record SocialConstellationNode(
    long NpcId,
    string NameVi,
    string TitleVi,
    int Affinity,
    SocialBondType BondType,
    string Glyph);

/// <summary>
/// Hardware-accelerated Radial Social Constellation Canvas.
/// Visualizes character relationships (Sworn Brothers, Retainers, Friends, Nemeses)
/// with celestial orbital rings, glowing relationship threads, and interactive node hit testing.
/// </summary>
public sealed class GameSocialConstellationControl : FrameworkElement
{
    private readonly List<SocialConstellationNode> _nodes = new();
    private string _protagonistName = "Chủ Công";
    private int _hoveredIndex = -1;

    // Direct3D Drawing Resources
    private static readonly Brush SpaceBgBrush;
    private static readonly Pen OrbitPen;
    private static readonly Pen InnerOrbitPen;
    private static readonly Pen RivalOrbitPen;
    private static readonly Brush ProtagonistBrush;
    private static readonly Pen ProtagonistAuraPen;
    private static readonly Brush SwornBrush;
    private static readonly Brush RetainerBrush;
    private static readonly Brush FriendBrush;
    private static readonly Brush RivalBrush;
    private static readonly Brush NeutralBrush;
    private static readonly Pen SwornLinePen;
    private static readonly Pen RetainerLinePen;
    private static readonly Pen FriendLinePen;
    private static readonly Pen RivalLinePen;
    private static readonly Brush TextWhiteBrush;
    private static readonly Brush TextMutedBrush;
    private static readonly Brush TooltipBgBrush;
    private static readonly Pen TooltipBorderPen;

    public event Action<SocialConstellationNode>? NodeSelected;

    static GameSocialConstellationControl()
    {
        SpaceBgBrush = new RadialGradientBrush(
            Color.FromRgb(0x16, 0x1F, 0x30),
            Color.FromRgb(0x0C, 0x10, 0x17))
        {
            Center = new Point(0.5, 0.5),
            GradientOrigin = new Point(0.5, 0.5),
            RadiusX = 0.7,
            RadiusY = 0.7
        };
        SpaceBgBrush.Freeze();

        OrbitPen = new Pen(new SolidColorBrush(Color.FromArgb(0x35, 0x4A, 0x90, 0xE2)), 1.2)
        {
            DashStyle = DashStyles.Dash
        };
        OrbitPen.Freeze();

        InnerOrbitPen = new Pen(new SolidColorBrush(Color.FromArgb(0x45, 0xF5, 0x9E, 0x0B)), 1.2)
        {
            DashStyle = DashStyles.Dot
        };
        InnerOrbitPen.Freeze();

        RivalOrbitPen = new Pen(new SolidColorBrush(Color.FromArgb(0x45, 0xEF, 0x44, 0x44)), 1.0)
        {
            DashStyle = DashStyles.DashDot
        };
        RivalOrbitPen.Freeze();

        ProtagonistBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
        ProtagonistBrush.Freeze();

        ProtagonistAuraPen = new Pen(new SolidColorBrush(Color.FromArgb(0x60, 0xF5, 0x9E, 0x0B)), 3.0);
        ProtagonistAuraPen.Freeze();

        SwornBrush = new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24)); // Gold
        SwornBrush.Freeze();

        RetainerBrush = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)); // Emerald
        RetainerBrush.Freeze();

        FriendBrush = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8)); // Sky Blue
        FriendBrush.Freeze();

        RivalBrush = new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71)); // Crimson
        RivalBrush.Freeze();

        NeutralBrush = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)); // Slate
        NeutralBrush.Freeze();

        SwornLinePen = new Pen(new SolidColorBrush(Color.FromArgb(0x90, 0xFB, 0xBF, 0x24)), 2.0);
        SwornLinePen.Freeze();

        RetainerLinePen = new Pen(new SolidColorBrush(Color.FromArgb(0x80, 0x10, 0xB9, 0x81)), 1.6);
        RetainerLinePen.Freeze();

        FriendLinePen = new Pen(new SolidColorBrush(Color.FromArgb(0x70, 0x38, 0xBD, 0xF8)), 1.4);
        FriendLinePen.Freeze();

        RivalLinePen = new Pen(new SolidColorBrush(Color.FromArgb(0x70, 0xF8, 0x71, 0x71)), 1.2)
        {
            DashStyle = DashStyles.Dash
        };
        RivalLinePen.Freeze();

        TextWhiteBrush = new SolidColorBrush(Color.FromRgb(0xEB, 0xF0, 0xF7));
        TextWhiteBrush.Freeze();

        TextMutedBrush = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
        TextMutedBrush.Freeze();

        TooltipBgBrush = new SolidColorBrush(Color.FromArgb(0xEE, 0x14, 0x1A, 0x24));
        TooltipBgBrush.Freeze();

        TooltipBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)), 1.2);
        TooltipBorderPen.Freeze();
    }

    public GameSocialConstellationControl()
    {
        MinHeight = 320;
        ClipToBounds = true;
        Cursor = Cursors.Hand;

        MouseMove += OnCanvasMouseMove;
        MouseLeave += OnCanvasMouseLeave;
        MouseDown += OnCanvasMouseDown;
    }

    public void SetConstellation(string protagonistName, IEnumerable<SocialConstellationNode> nodes)
    {
        _protagonistName = protagonistName;
        _nodes.Clear();
        _nodes.AddRange(nodes);
        InvalidateVisual();
    }

    private (Point Position, double Radius) CalculateNodeGeometry(int index, int total, double cx, double cy, double maxRadius)
    {
        var node = _nodes[index];
        double ringRadius;
        switch (node.BondType)
        {
            case SocialBondType.SwornBrother:
                ringRadius = maxRadius * 0.42;
                break;
            case SocialBondType.Retainer:
                ringRadius = maxRadius * 0.62;
                break;
            case SocialBondType.Friend:
                ringRadius = maxRadius * 0.78;
                break;
            case SocialBondType.Rival:
                ringRadius = maxRadius * 0.92;
                break;
            default:
                ringRadius = maxRadius * 0.70;
                break;
        }

        // Distribute angle evenly with phase offset per bond
        var angle = (2.0 * Math.PI / total) * index - Math.PI / 2.0;
        var x = cx + ringRadius * Math.Cos(angle);
        var y = cy + ringRadius * Math.Sin(angle);
        var r = node.BondType == SocialBondType.SwornBrother ? 18.0 : 15.0;

        return (new Point(x, y), r);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 100 || h <= 100) return;

        var cx = w / 2.0;
        var cy = h / 2.0;
        var maxR = Math.Min(cx, cy) - 30.0;

        // Background
        dc.DrawRoundedRectangle(SpaceBgBrush, null, new Rect(0, 0, w, h), 8, 8);

        // Orbital Resonance Rings
        dc.DrawEllipse(null, InnerOrbitPen, new Point(cx, cy), maxR * 0.42, maxR * 0.42);
        dc.DrawEllipse(null, OrbitPen, new Point(cx, cy), maxR * 0.62, maxR * 0.62);
        dc.DrawEllipse(null, OrbitPen, new Point(cx, cy), maxR * 0.78, maxR * 0.78);
        dc.DrawEllipse(null, RivalOrbitPen, new Point(cx, cy), maxR * 0.92, maxR * 0.92);

        // Constellation Lines from Center to Orbiting Nodes
        for (var i = 0; i < _nodes.Count; i++)
        {
            var (pos, _) = CalculateNodeGeometry(i, _nodes.Count, cx, cy, maxR);
            var linePen = _nodes[i].BondType switch
            {
                SocialBondType.SwornBrother => SwornLinePen,
                SocialBondType.Retainer => RetainerLinePen,
                SocialBondType.Friend => FriendLinePen,
                SocialBondType.Rival => RivalLinePen,
                _ => OrbitPen
            };
            dc.DrawLine(linePen, new Point(cx, cy), pos);
        }

        // Center Protagonist Node
        dc.DrawEllipse(ProtagonistBrush, ProtagonistAuraPen, new Point(cx, cy), 22, 22);
        DrawCenteredText(dc, "👑", 16, cx, cy - 10, Brushes.White);
        DrawCenteredText(dc, _protagonistName, 10, cx, cy + 13, TextWhiteBrush, bold: true);

        // Orbiting Relationship Nodes
        for (var i = 0; i < _nodes.Count; i++)
        {
            var node = _nodes[i];
            var (pos, r) = CalculateNodeGeometry(i, _nodes.Count, cx, cy, maxR);
            var isHovered = (i == _hoveredIndex);

            var (nodeBrush, glowPen) = node.BondType switch
            {
                SocialBondType.SwornBrother => (SwornBrush, SwornLinePen),
                SocialBondType.Retainer => (RetainerBrush, RetainerLinePen),
                SocialBondType.Friend => (FriendBrush, FriendLinePen),
                SocialBondType.Rival => (RivalBrush, RivalLinePen),
                _ => (NeutralBrush, OrbitPen)
            };

            var drawRadius = isHovered ? r + 4 : r;
            dc.DrawEllipse(nodeBrush, glowPen, pos, drawRadius, drawRadius);

            // Glyph inside node
            DrawCenteredText(dc, node.Glyph, 11, pos.X, pos.Y - 7, Brushes.White);

            // Name & Affinity below node
            DrawCenteredText(dc, node.NameVi, 10, pos.X, pos.Y + drawRadius + 4, TextWhiteBrush, bold: true);
            var affText = node.Affinity >= 0 ? $"+{node.Affinity}" : $"{node.Affinity}";
            var affColor = node.Affinity >= 50 ? GainBrushStatic : (node.Affinity < 0 ? LossBrushStatic : TextMutedBrush);
            DrawCenteredText(dc, affText, 9, pos.X, pos.Y + drawRadius + 16, affColor);
        }

        // Draw Interactive Tooltip on Hover
        if (_hoveredIndex >= 0 && _hoveredIndex < _nodes.Count)
        {
            var node = _nodes[_hoveredIndex];
            var (pos, _) = CalculateNodeGeometry(_hoveredIndex, _nodes.Count, cx, cy, maxR);
            DrawNodeTooltip(dc, node, pos, w, h);
        }
    }

    private static readonly Brush GainBrushStatic = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
    private static readonly Brush LossBrushStatic = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));

    private void DrawNodeTooltip(DrawingContext dc, SocialConstellationNode node, Point anchor, double w, double h)
    {
        var boxW = 160.0;
        var boxH = 68.0;
        var tx = anchor.X + 20;
        var ty = anchor.Y - 34;

        if (tx + boxW > w - 10) tx = anchor.X - boxW - 20;
        if (ty + boxH > h - 10) ty = h - boxH - 10;
        if (ty < 10) ty = 10;

        var boxRect = new Rect(tx, ty, boxW, boxH);
        dc.DrawRoundedRectangle(TooltipBgBrush, TooltipBorderPen, boxRect, 6, 6);

        var bondTitle = node.BondType switch
        {
            SocialBondType.SwornBrother => "🤝 Kim Lan Kết Nghĩa",
            SocialBondType.Retainer => "⚔️ Môn Khách Thân Tín",
            SocialBondType.Friend => "😊 Bằng Hữu Tri Kỷ",
            SocialBondType.Rival => "💀 Túc Địch Cừu Thị",
            _ => "👤 Đồng Liêu Giao Tế"
        };

        DrawText(dc, node.NameVi, 12, tx + 10, ty + 8, TextWhiteBrush, bold: true);
        DrawText(dc, bondTitle, 10, tx + 10, ty + 26, SwornBrush);
        DrawText(dc, $"Hảo Cảm: {node.Affinity} · {node.TitleVi}", 9.5, tx + 10, ty + 44, TextMutedBrush);
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        var pt = e.GetPosition(this);
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        var cx = w / 2.0;
        var cy = h / 2.0;
        var maxR = Math.Min(cx, cy) - 30.0;
        var oldHover = _hoveredIndex;
        _hoveredIndex = -1;

        for (var i = 0; i < _nodes.Count; i++)
        {
            var (pos, r) = CalculateNodeGeometry(i, _nodes.Count, cx, cy, maxR);
            var distSq = (pos.X - pt.X) * (pos.X - pt.X) + (pos.Y - pt.Y) * (pos.Y - pt.Y);
            if (distSq <= (r + 5) * (r + 5))
            {
                _hoveredIndex = i;
                break;
            }
        }

        if (oldHover != _hoveredIndex)
        {
            InvalidateVisual();
        }
    }

    private void OnCanvasMouseLeave(object sender, MouseEventArgs e)
    {
        if (_hoveredIndex != -1)
        {
            _hoveredIndex = -1;
            InvalidateVisual();
        }
    }

    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_hoveredIndex >= 0 && _hoveredIndex < _nodes.Count)
        {
            NodeSelected?.Invoke(_nodes[_hoveredIndex]);
        }
    }

    private void DrawCenteredText(DrawingContext dc, string text, double size, double cx, double cy, Brush brush, bool bold = false)
    {
        var ft = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal),
            size,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        dc.DrawText(ft, new Point(cx - ft.Width / 2.0, cy - ft.Height / 2.0));
    }

    private void DrawText(DrawingContext dc, string text, double size, double x, double y, Brush brush, bool bold = false)
    {
        var ft = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal),
            size,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        dc.DrawText(ft, new Point(x, y));
    }
}
