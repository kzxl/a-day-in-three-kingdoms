using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Hardware-accelerated Visual Crop Growth Cycle & Soil Fertility Gauge Control.
/// Renders 4 growth stages (Gieo Mầm, Nảy Lộc, Trổ Đòng, Chín Vàng) with circular progress arc
/// and live days countdown indicator via Direct3D DrawingContext.
/// </summary>
public sealed class GameCropGrowthVisualizer : FrameworkElement
{
    private string _cropName = "Lúa Nước Kinh Sở";
    private string _cropStatus = "GROWING";
    private int _remainingDays = 12;
    private int _totalDays = 30;
    private int _soilFertility = 85;
    private int _irrigationLevel = 1;

    // Cached brushes
    private static readonly Brush BgCardBrush = new SolidColorBrush(Color.FromRgb(0x13, 0x18, 0x22));
    private static readonly Pen TrackPen = new(new SolidColorBrush(Color.FromRgb(0x26, 0x30, 0x3D)), 5.0);
    private static readonly Pen ArcPen = new(new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)), 5.0);
    private static readonly Pen GoldArcPen = new(new SolidColorBrush(Color.FromRgb(0xD9, 0xA4, 0x41)), 5.0);
    private static readonly Brush StageActiveBrush = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
    private static readonly Brush StageInactiveBrush = new SolidColorBrush(Color.FromRgb(0x26, 0x30, 0x3D));
    private static readonly Brush TextLightBrush = new SolidColorBrush(Color.FromRgb(0xEB, 0xF0, 0xF7));
    private static readonly Brush TextMutedBrush = new SolidColorBrush(Color.FromRgb(0x8A, 0x97, 0xA8));

    static GameCropGrowthVisualizer()
    {
        BgCardBrush.Freeze();
        TrackPen.Freeze();
        ArcPen.Freeze();
        GoldArcPen.Freeze();
        StageActiveBrush.Freeze();
        StageInactiveBrush.Freeze();
        TextLightBrush.Freeze();
        TextMutedBrush.Freeze();
    }

    public GameCropGrowthVisualizer()
    {
        Height = 130;
        MinHeight = 110;
        ClipToBounds = true;
    }

    public void SetStatus(string cropName, string? cropStatus, int remainingDays, int totalDays, int soilFertility, int irrigationLevel)
    {
        _cropName = cropName;
        _cropStatus = cropStatus ?? "EMPTY";
        _remainingDays = Math.Max(0, remainingDays);
        _totalDays = Math.Max(1, totalDays);
        _soilFertility = Math.Clamp(soilFertility, 0, 100);
        _irrigationLevel = irrigationLevel;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 80 || h <= 50) return;

        dc.DrawRoundedRectangle(BgCardBrush, null, new Rect(0, 0, w, h), 8, 8);

        // 1. Draw Left Radial Growth Arc
        var arcCenterX = 56.0;
        var arcCenterY = h / 2.0;
        var radius = 34.0;

        // Base circle
        dc.DrawEllipse(null, TrackPen, new Point(arcCenterX, arcCenterY), radius, radius);

        var isReady = _cropStatus == "READY";
        var isEmpty = _cropStatus == "EMPTY" || string.IsNullOrEmpty(_cropStatus);

        double ratio;
        if (isEmpty)
        {
            ratio = 0.0;
        }
        else if (isReady)
        {
            ratio = 1.0;
        }
        else
        {
            var elapsed = Math.Max(0, _totalDays - _remainingDays);
            ratio = Math.Clamp((double)elapsed / _totalDays, 0.05, 0.99);
        }

        if (ratio > 0.01)
        {
            var activePen = isReady ? GoldArcPen : ArcPen;
            var startAngle = -Math.PI / 2.0;
            var sweepAngle = ratio * Math.PI * 2.0;
            var endAngle = startAngle + sweepAngle;

            var pStart = new Point(arcCenterX + radius * Math.Cos(startAngle), arcCenterY + radius * Math.Sin(startAngle));
            var pEnd = new Point(arcCenterX + radius * Math.Cos(endAngle), arcCenterY + radius * Math.Sin(endAngle));

            var arcGeom = new PathGeometry();
            var fig = new PathFigure { StartPoint = pStart, IsClosed = false };
            fig.Segments.Add(new ArcSegment(pEnd, new Size(radius, radius), 0, ratio > 0.5, SweepDirection.Clockwise, true));
            arcGeom.Figures.Add(fig);
            dc.DrawGeometry(null, activePen, arcGeom);
        }

        // Center emoji & percent text
        var centerGlyph = isEmpty ? "🍂" : (isReady ? "🌽" : "🌾");
        var glyphText = new FormattedText(centerGlyph,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI Emoji"),
            18,
            Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(glyphText, new Point(arcCenterX - 10, arcCenterY - 18));

        var pctText = new FormattedText(isEmpty ? "Chưa Gieo" : $"{ratio * 100:0.#}%",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            9.5,
            isReady ? GoldArcPen.Brush : TextLightBrush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(pctText, new Point(arcCenterX - (pctText.Width / 2.0), arcCenterY + 4));

        // 2. Draw Right 4-Stage Growth Pipeline
        var stages = new[]
        {
            ("Gieo Mầm 🌱", ratio >= 0.1),
            ("Nảy Lộc 🌿", ratio >= 0.4),
            ("Trổ Đòng 🌾", ratio >= 0.75),
            ("Chín Vàng 🌽", isReady || ratio >= 1.0)
        };

        var pipelineStartX = 115.0;
        var pipelineW = w - pipelineStartX - 16;
        var stepW = pipelineW / 4.0;
        var stepY = 35.0;

        // Title and Status
        var titleText = new FormattedText(isEmpty ? "ĐẤT ĐANG ĐỂ TRỐNG · CHỜ GIEO HẠT" : $"{_cropName.ToUpperInvariant()} · {_cropStatus}",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            11.5,
            isReady ? GoldArcPen.Brush : TextLightBrush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(titleText, new Point(pipelineStartX, 12));

        for (var i = 0; i < stages.Length; i++)
        {
            var sx = pipelineStartX + i * stepW + (stepW / 2.0);
            var (sName, active) = stages[i];

            // Node Circle
            var nodeBrush = active ? (i == 3 ? GoldArcPen.Brush : StageActiveBrush) : StageInactiveBrush;
            dc.DrawEllipse(nodeBrush, null, new Point(sx, stepY), 7, 7);

            // Connecting pipe to next node
            if (i < stages.Length - 1)
            {
                var nextX = pipelineStartX + (i + 1) * stepW + (stepW / 2.0);
                var pipePen = active && stages[i + 1].Item2
                    ? new Pen(StageActiveBrush, 2.5)
                    : new Pen(StageInactiveBrush, 2.5);
                dc.DrawLine(pipePen, new Point(sx + 7, stepY), new Point(nextX - 7, stepY));
            }

            // Stage Label
            var stageLabel = new FormattedText(sName,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, active ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal),
                10,
                active ? TextLightBrush : TextMutedBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(stageLabel, new Point(sx - (stageLabel.Width / 2.0), stepY + 12));
        }

        // 3. Environmental Sub-Stats Bar at bottom
        var bottomY = h - 28.0;
        var soilColor = _soilFertility >= 60 ? Color.FromRgb(0x10, 0xB9, 0x81) : (_soilFertility >= 30 ? Color.FromRgb(0xF5, 0x9E, 0x0B) : Color.FromRgb(0xEF, 0x44, 0x44));
        var soilBadge = new FormattedText($"🌱 Độ Phì Đất: {_soilFertility}/100",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            10.5,
            new SolidColorBrush(soilColor),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(soilBadge, new Point(pipelineStartX, bottomY));

        var irrigBadge = new FormattedText($"💧 Thủy Lợi: Bậc {_irrigationLevel}/2",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            10.5,
            new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8)),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(irrigBadge, new Point(pipelineStartX + 140, bottomY));

        var harvestCountdown = isEmpty ? "Chưa canh tác" : (isReady ? "⚡ Đã chín, gặt ngay!" : $"⏳ Còn {_remainingDays} ngày chín");
        var timeBadge = new FormattedText(harvestCountdown,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            10.5,
            isReady ? GoldArcPen.Brush : TextMutedBrush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(timeBadge, new Point(w - timeBadge.Width - 16, bottomY));
    }
}
