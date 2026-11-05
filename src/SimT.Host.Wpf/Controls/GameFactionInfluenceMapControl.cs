using System.Globalization;
using System.Windows;
using System.Windows.Media;
using SimT.Contracts.Playing;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Hardware-accelerated Strategic Faction Influence & Warlord Territory Canvas Control.
/// Visualizes the 5 historical macro-regions, citadel territorial allegiance spheres,
/// and warlord military influence densities via Direct3D DrawingContext.
/// </summary>
public sealed class GameFactionInfluenceMapControl : FrameworkElement
{
    public sealed record CitadelAllegiance(
        long CitadelId,
        string NameVi,
        string Region,
        double NormX,
        double NormY,
        long FactionId,
        string FactionColorHex,
        bool IsCapital);

    private readonly List<CitadelAllegiance> _citadels = new();
    private long _selectedFactionId;
    private Color _selectedFactionColor = Color.FromRgb(0xD9, 0xA4, 0x41);

    // Cached brushes & pens
    private static readonly Brush MapBgBrush = new SolidColorBrush(Color.FromRgb(0x10, 0x14, 0x1E));
    private static readonly Pen TerritoryLinePen = new(new SolidColorBrush(Color.FromRgb(0x22, 0x2C, 0x3D)), 1.2)
    {
        DashStyle = DashStyles.Dash
    };
    private static readonly Brush TextWhiteBrush = new SolidColorBrush(Color.FromRgb(0xEB, 0xF0, 0xF7));
    private static readonly Brush TextMutedBrush = new SolidColorBrush(Color.FromRgb(0x8A, 0x97, 0xA8));
    private static readonly Pen CapitalCrownPen = new(new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)), 2.0);

    static GameFactionInfluenceMapControl()
    {
        MapBgBrush.Freeze();
        TerritoryLinePen.Freeze();
        TextWhiteBrush.Freeze();
        TextMutedBrush.Freeze();
        CapitalCrownPen.Freeze();
    }

    public GameFactionInfluenceMapControl()
    {
        Height = 170;
        MinHeight = 140;
        ClipToBounds = true;
    }

    public void SetData(
        IEnumerable<CitadelAllegiance> citadels,
        long selectedFactionId,
        string selectedFactionColorHex)
    {
        _citadels.Clear();
        _citadels.AddRange(citadels);
        _selectedFactionId = selectedFactionId;

        try
        {
            _selectedFactionColor = (Color)ColorConverter.ConvertFromString(selectedFactionColorHex);
        }
        catch
        {
            _selectedFactionColor = Color.FromRgb(0xD9, 0xA4, 0x41);
        }

        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 100 || h <= 60) return;

        // Background terrain box
        dc.DrawRoundedRectangle(MapBgBrush, null, new Rect(0, 0, w, h), 8, 8);

        // Waterway / Yellow River & Yangtze River stylized curves
        DrawRiverCurves(dc, w, h);

        // 1. Draw connecting trade & military frontier lines between citadels
        for (var i = 0; i < _citadels.Count; i++)
        {
            var c1 = _citadels[i];
            var p1 = new Point(c1.NormX * (w - 60) + 30, c1.NormY * (h - 40) + 20);

            for (var j = i + 1; j < _citadels.Count; j++)
            {
                var c2 = _citadels[j];
                var p2 = new Point(c2.NormX * (w - 60) + 30, c2.NormY * (h - 40) + 20);

                var dist = (p1 - p2).Length;
                if (dist < w * 0.32)
                {
                    var isSameFaction = c1.FactionId == c2.FactionId && c1.FactionId == _selectedFactionId;
                    var pen = isSameFaction
                        ? new Pen(new SolidColorBrush(Color.FromArgb(0x80, _selectedFactionColor.R, _selectedFactionColor.G, _selectedFactionColor.B)), 2.0)
                        : TerritoryLinePen;
                    dc.DrawLine(pen, p1, p2);
                }
            }
        }

        // 2. Draw Citadels & Faction Territory Nodes
        foreach (var c in _citadels)
        {
            var pt = new Point(c.NormX * (w - 60) + 30, c.NormY * (h - 40) + 20);
            var isSelectedFaction = c.FactionId == _selectedFactionId;

            Color nodeColor;
            try
            {
                nodeColor = (Color)ColorConverter.ConvertFromString(c.FactionColorHex);
            }
            catch
            {
                nodeColor = Color.FromRgb(0x64, 0x74, 0x8B);
            }

            // Territory influence aura ring
            if (isSelectedFaction)
            {
                var auraBrush = new SolidColorBrush(Color.FromArgb(0x35, _selectedFactionColor.R, _selectedFactionColor.G, _selectedFactionColor.B));
                var auraPen = new Pen(new SolidColorBrush(Color.FromArgb(0x90, _selectedFactionColor.R, _selectedFactionColor.G, _selectedFactionColor.B)), 1.5);
                dc.DrawEllipse(auraBrush, auraPen, pt, 16, 16);
            }

            // Node Circle
            var fillBrush = new SolidColorBrush(nodeColor);
            var nodeRadius = c.IsCapital ? 7.5 : (isSelectedFaction ? 6.5 : 5.0);
            dc.DrawEllipse(fillBrush, null, pt, nodeRadius, nodeRadius);

            if (c.IsCapital)
            {
                dc.DrawEllipse(null, CapitalCrownPen, pt, 11, 11);
            }

            // Citadel Name Label
            var nameText = new FormattedText(c.NameVi,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, isSelectedFaction ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal),
                9.5,
                isSelectedFaction ? TextWhiteBrush : TextMutedBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            dc.DrawText(nameText, new Point(pt.X - (nameText.Width / 2.0), pt.Y + (c.IsCapital ? 12 : 8)));
        }

        // 3. Top-Right Strategic Legend
        var legendText = new FormattedText($"🏛️ Vùng Ảnh Hưởng Của Phe Phái",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            10.5,
            new SolidColorBrush(_selectedFactionColor),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(legendText, new Point(w - legendText.Width - 12, 8));
    }

    private static void DrawRiverCurves(DrawingContext dc, double w, double h)
    {
        var riverPen = new Pen(new SolidColorBrush(Color.FromArgb(0x28, 0x38, 0xBD, 0xF8)), 4.0);

        // Yellow River (Hoàng Hà)
        var p0 = new Point(10, h * 0.35);
        var p1 = new Point(w * 0.45, h * 0.20);
        var p2 = new Point(w * 0.70, h * 0.40);
        var p3 = new Point(w - 10, h * 0.25);

        var geom = new PathGeometry();
        var fig = new PathFigure { StartPoint = p0, IsClosed = false };
        fig.Segments.Add(new BezierSegment(p1, p2, p3, true));
        geom.Figures.Add(fig);
        dc.DrawGeometry(null, riverPen, geom);

        // Yangtze River (Trường Giang)
        var y0 = new Point(10, h * 0.75);
        var y1 = new Point(w * 0.40, h * 0.85);
        var y2 = new Point(w * 0.65, h * 0.65);
        var y3 = new Point(w - 10, h * 0.70);

        var yGeom = new PathGeometry();
        var yFig = new PathFigure { StartPoint = y0, IsClosed = false };
        yFig.Segments.Add(new BezierSegment(y1, y2, y3, true));
        yGeom.Figures.Add(yFig);
        dc.DrawGeometry(null, riverPen, yGeom);
    }
}
