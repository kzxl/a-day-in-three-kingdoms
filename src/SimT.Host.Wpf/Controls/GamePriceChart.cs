using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// High-performance hardware-accelerated WPF Price Trend Chart control.
/// Renders price curves, area gradient fills, peak/valley support points,
/// and market trading signals using Direct3D DrawingContext.
/// </summary>
public sealed class GamePriceChart : FrameworkElement
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    private readonly List<long> _prices = new();
    private string _itemName = "Hàng Hóa";

    // Cached pens & brushes
    private static readonly Brush BgBrush = new SolidColorBrush(Color.FromRgb(0x13, 0x18, 0x20));
    private static readonly Pen BorderPen = new(new SolidColorBrush(Color.FromRgb(0x26, 0x30, 0x3D)), 1.0);
    private static readonly Pen GridLinePen = new(new SolidColorBrush(Color.FromRgb(0x1F, 0x27, 0x33)), 1.0) { DashStyle = DashStyles.Dash };
    private static readonly Pen TrendLinePen = new(new SolidColorBrush(Color.FromRgb(0xD9, 0xA4, 0x41)), 2.0);
    private static readonly Brush TextFaintBrush = new SolidColorBrush(Color.FromRgb(0x60, 0x6E, 0x82));
    private static readonly Brush TextTitleBrush = new SolidColorBrush(Color.FromRgb(0xE8, 0xED, 0xF5));
    private static readonly Brush PeakBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0x6C, 0x75));
    private static readonly Brush ValleyBrush = new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0x90));
    private static readonly Brush CurrentBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0xA4, 0x41));

    static GamePriceChart()
    {
        BgBrush.Freeze();
        BorderPen.Freeze();
        GridLinePen.Freeze();
        TrendLinePen.Freeze();
        TextFaintBrush.Freeze();
        TextTitleBrush.Freeze();
        PeakBrush.Freeze();
        ValleyBrush.Freeze();
        CurrentBrush.Freeze();
    }

    public GamePriceChart()
    {
        Height = 135;
        MinHeight = 110;
        ClipToBounds = true;
    }

    public void SetData(string itemName, IReadOnlyList<long> prices)
    {
        _itemName = itemName;
        _prices.Clear();
        _prices.AddRange(prices);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var w = ActualWidth;
        var h = ActualHeight;
        if (w < 40 || h < 40)
        {
            return;
        }

        // 1. Background & Border
        var rect = new Rect(0, 0, w, h);
        dc.DrawRoundedRectangle(BgBrush, BorderPen, rect, 6.0, 6.0);

        // 2. Header: Item name, current price, and trend label
        var currentPrice = _prices.Count > 0 ? _prices[^1] : 0;
        var minPrice = _prices.Count > 0 ? _prices.Min() : 0;
        var maxPrice = _prices.Count > 0 ? _prices.Max() : 0;

        var headerTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var subTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        // Title: "BIẾN ĐỘNG GIÁ · [Tên]"
        var titleFt = new FormattedText(
            $"📈 BIẾN ĐỘNG GIÁ · {_itemName.ToUpperInvariant()}",
            Vietnamese,
            FlowDirection.LeftToRight,
            headerTypeface,
            11,
            TextTitleBrush,
            1.0);
        dc.DrawText(titleFt, new Point(10, 8));

        // Subtitle / Signal: e.g. "Hiện tại: 42 Q"
        var trendDiff = _prices.Count >= 2 ? currentPrice - _prices[0] : 0;
        var trendPercent = _prices.Count >= 2 && _prices[0] > 0 ? (trendDiff * 100.0 / _prices[0]) : 0.0;
        var trendSignal = trendPercent switch
        {
            >= 5.0 => $"▲ +{trendPercent:0.#}% (Đang tăng)",
            <= -5.0 => $"▼ {trendPercent:0.#}% (Đang giảm)",
            _ => "≈ Ổn định"
        };
        var signalBrush = trendPercent >= 5.0 ? ValleyBrush : (trendPercent <= -5.0 ? PeakBrush : CurrentBrush);

        var priceFt = new FormattedText(
            $"Hiện tại: {currentPrice:N0} Q  ({trendSignal})",
            Vietnamese,
            FlowDirection.LeftToRight,
            subTypeface,
            10.5,
            signalBrush,
            1.0);
        dc.DrawText(priceFt, new Point(10, 24));

        if (_prices.Count < 2)
        {
            var noDataFt = new FormattedText(
                "Chưa có đủ mẫu giá lịch sử (Cần ít nhất 2 ngày giao dịch)",
                Vietnamese,
                FlowDirection.LeftToRight,
                subTypeface,
                11,
                TextFaintBrush,
                1.0);
            dc.DrawText(noDataFt, new Point(10, 60));
            return;
        }

        // 3. Coordinate mapping
        var chartTop = 46.0;
        var chartBottom = h - 16.0;
        var chartLeft = 14.0;
        var chartRight = w - 14.0;
        var chartHeight = Math.Max(10.0, chartBottom - chartTop);
        var chartWidth = Math.Max(10.0, chartRight - chartLeft);

        var range = Math.Max(1, maxPrice - minPrice);
        // Expand range by 15% top & bottom for breathing room
        var yMin = Math.Max(0, minPrice - (long)(range * 0.15));
        var yMax = maxPrice + (long)(range * 0.15);
        var yRange = Math.Max(1, yMax - yMin);

        // Draw horizontal grid lines (Max, Mid, Min)
        for (var step = 0; step <= 2; step++)
        {
            var yRatio = step / 2.0;
            var gy = chartBottom - (yRatio * chartHeight);
            dc.DrawLine(GridLinePen, new Point(chartLeft, gy), new Point(chartRight, gy));

            var priceLevel = yMin + (long)(yRatio * yRange);
            var gridPriceFt = new FormattedText(
                $"{priceLevel:N0}",
                Vietnamese,
                FlowDirection.LeftToRight,
                subTypeface,
                8.5,
                TextFaintBrush,
                1.0);
            dc.DrawText(gridPriceFt, new Point(chartRight - 28, gy - 11));
        }

        // Calculate points
        var points = new Point[_prices.Count];
        var stepX = chartWidth / (_prices.Count - 1);

        for (var i = 0; i < _prices.Count; i++)
        {
            var x = chartLeft + (i * stepX);
            var normY = (double)(_prices[i] - yMin) / yRange;
            var y = chartBottom - (normY * chartHeight);
            points[i] = new Point(x, y);
        }

        // 4. Area fill geometry
        var areaGeometry = new StreamGeometry();
        using (var ctx = areaGeometry.Open())
        {
            ctx.BeginFigure(new Point(points[0].X, chartBottom), isFilled: true, isClosed: true);
            ctx.LineTo(points[0], isStroked: false, isSmoothJoin: true);
            for (var i = 1; i < points.Length; i++)
            {
                ctx.LineTo(points[i], isStroked: false, isSmoothJoin: true);
            }
            ctx.LineTo(new Point(points[^1].X, chartBottom), isStroked: false, isSmoothJoin: false);
        }
        areaGeometry.Freeze();

        var areaBrush = new LinearGradientBrush(
            Color.FromArgb(0x40, 0xD9, 0xA4, 0x41),
            Color.FromArgb(0x02, 0xD9, 0xA4, 0x41),
            new Point(0, 0),
            new Point(0, 1));
        areaBrush.Freeze();
        dc.DrawGeometry(areaBrush, null, areaGeometry);

        // 5. Trend Line path
        for (var i = 0; i < points.Length - 1; i++)
        {
            dc.DrawLine(TrendLinePen, points[i], points[i + 1]);
        }

        // 6. Dots and Badges
        var minIndex = _prices.IndexOf(minPrice);
        var maxIndex = _prices.IndexOf(maxPrice);

        for (var i = 0; i < points.Length; i++)
        {
            var isCurrent = (i == points.Length - 1);
            var isMin = (i == minIndex && minPrice != maxPrice);
            var isMax = (i == maxIndex && minPrice != maxPrice);

            if (isCurrent)
            {
                dc.DrawEllipse(CurrentBrush, null, points[i], 4.0, 4.0);
                dc.DrawEllipse(null, new Pen(Brushes.White, 1.5), points[i], 5.5, 5.5);
            }
            else if (isMin)
            {
                dc.DrawEllipse(ValleyBrush, null, points[i], 3.5, 3.5);
            }
            else if (isMax)
            {
                dc.DrawEllipse(PeakBrush, null, points[i], 3.5, 3.5);
            }
            else
            {
                dc.DrawEllipse(CurrentBrush, null, points[i], 2.0, 2.0);
            }
        }
    }
}
