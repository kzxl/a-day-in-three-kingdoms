using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace SimT.Host.Wpf.Controls;

public sealed record ArbitrageSpreadItem(
    string ItemCode,
    string ItemNameVi,
    string Glyph,
    int BuyPrice,
    int SellPrice,
    int TransportFee,
    int NetProfit,
    double ProfitMarginPct);

/// <summary>
/// Hardware-accelerated 2D Commodity Trade Arbitrage Matrix & Profit Spread Control.
/// Renders inter-city price disparities, transportation overheads, and color-coded profit spreads via Direct3D.
/// </summary>
public sealed class GameArbitrageMatrixControl : FrameworkElement
{
    private readonly List<ArbitrageSpreadItem> _items = new();
    private string _fromCityName = "Lạc Dương";
    private string _toCityName = "Hứa Xương";

    // Cached pens & brushes
    private static readonly Brush CardBgBrush = new SolidColorBrush(Color.FromRgb(0x13, 0x18, 0x22));
    private static readonly Brush RowAltBrush = new SolidColorBrush(Color.FromRgb(0x18, 0x20, 0x2C));
    private static readonly Pen RowBorderPen = new(new SolidColorBrush(Color.FromRgb(0x22, 0x2C, 0x3D)), 1.0);
    private static readonly Brush TextWhiteBrush = new SolidColorBrush(Color.FromRgb(0xEB, 0xF0, 0xF7));
    private static readonly Brush TextMutedBrush = new SolidColorBrush(Color.FromRgb(0x8A, 0x97, 0xA8));
    private static readonly Brush GainBrush = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
    private static readonly Brush LossBrush = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
    private static readonly Brush AmberBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
    private static readonly Brush TrackBgBrush = new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x37));

    static GameArbitrageMatrixControl()
    {
        CardBgBrush.Freeze();
        RowAltBrush.Freeze();
        RowBorderPen.Freeze();
        TextWhiteBrush.Freeze();
        TextMutedBrush.Freeze();
        GainBrush.Freeze();
        LossBrush.Freeze();
        AmberBrush.Freeze();
        TrackBgBrush.Freeze();
    }

    public GameArbitrageMatrixControl()
    {
        MinHeight = 160;
        ClipToBounds = true;
    }

    public void SetMatrix(string fromCity, string toCity, IEnumerable<ArbitrageSpreadItem> items)
    {
        _fromCityName = fromCity;
        _toCityName = toCity;
        _items.Clear();
        _items.AddRange(items);

        Height = Math.Max(160, 36 + _items.Count * 38);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 100 || h <= 40) return;

        dc.DrawRoundedRectangle(CardBgBrush, null, new Rect(0, 0, w, h), 8, 8);

        // Header Row
        var headerH = 34.0;
        dc.DrawRoundedRectangle(RowAltBrush, null, new Rect(0, 0, w, headerH), 8, 8);

        var colGood = 16.0;
        var colBuy = w * 0.35;
        var colSell = w * 0.52;
        var colProfit = w * 0.68;
        var colMargin = w * 0.85;

        DrawHeaderText(dc, "MẶT HÀNG BUÔN CHUYẾN", colGood, 9);
        DrawHeaderText(dc, $"MUA TẠI {_fromCityName.ToUpperInvariant()}", colBuy, 9);
        DrawHeaderText(dc, $"BÁN TẠI {_toCityName.ToUpperInvariant()}", colSell, 9);
        DrawHeaderText(dc, "LỢI NHUẬN RÒNG", colProfit, 9);
        DrawHeaderText(dc, "BIÊN ĐỘ LÃI", colMargin, 9);

        // Data Rows
        var rowH = 36.0;
        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            var y = headerH + (i * rowH);

            if (i % 2 == 1)
            {
                dc.DrawRectangle(RowAltBrush, null, new Rect(0, y, w, rowH));
            }
            dc.DrawLine(RowBorderPen, new Point(10, y + rowH), new Point(w - 10, y + rowH));

            // Glyph & Name
            var nameText = new FormattedText($"{item.Glyph} {item.ItemNameVi}",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                11.5,
                TextWhiteBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(nameText, new Point(colGood, y + 9));

            // Buy Price
            var buyText = new FormattedText($"{item.BuyPrice:N0}q",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                11,
                TextMutedBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(buyText, new Point(colBuy, y + 9));

            // Sell Price
            var sellText = new FormattedText($"{item.SellPrice:N0}q",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                11,
                TextWhiteBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(sellText, new Point(colSell, y + 9));

            // Net Profit (including fee)
            var isProfitable = item.NetProfit > 0;
            var profitBrush = isProfitable ? GainBrush : LossBrush;
            var profitPrefix = item.NetProfit > 0 ? "+" : "";
            var profitText = new FormattedText($"{profitPrefix}{item.NetProfit:N0} Quan",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                11,
                profitBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(profitText, new Point(colProfit, y + 9));

            // Margin Badge with Fill
            var marginColor = item.ProfitMarginPct >= 40.0 ? Color.FromRgb(0x10, 0xB9, 0x81) : (item.ProfitMarginPct > 15.0 ? Color.FromRgb(0xF5, 0x9E, 0x0B) : Color.FromRgb(0xEF, 0x44, 0x44));
            var badgeBrush = new SolidColorBrush(Color.FromArgb(0x28, marginColor.R, marginColor.G, marginColor.B));
            var badgePen = new Pen(new SolidColorBrush(marginColor), 1.0);
            dc.DrawRoundedRectangle(badgeBrush, badgePen, new Rect(colMargin - 2, y + 7, 65, 20), 4, 4);

            var marginText = new FormattedText($"{profitPrefix}{item.ProfitMarginPct:0.#}%",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                10.5,
                new SolidColorBrush(marginColor),
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(marginText, new Point(colMargin + (65 - marginText.Width) / 2.0 - 2, y + 10));
        }
    }

    private void DrawHeaderText(DrawingContext dc, string text, double x, double y)
    {
        var ft = new FormattedText(text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            9.5,
            AmberBrush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(ft, new Point(x, y));
    }
}
