using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;

namespace SimT.Host.Wpf;

/// <summary>
/// Rich cell view model for SimT WPF DataGrid.
/// Intelligently parses cell contents to render enterprise badges,
/// dynamic durability/progress gauges, and formatted numeric values.
/// </summary>
public sealed class CellVm : IComparable
{
    private static readonly CultureInfo Vietnamese = new("vi-VN");
    private static readonly Regex RatioPattern = new(@"^(?:Cấp\s+)?(\d+)\s*/\s*(\d+)", RegexOptions.Compiled);

    // Cached brushes for high rendering throughput
    private static readonly Brush CommonBg = new SolidColorBrush(Color.FromRgb(0x1E, 0x24, 0x2C));
    private static readonly Brush CommonBorder = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55));
    private static readonly Brush CommonFg = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));

    private static readonly Brush FineBg = new SolidColorBrush(Color.FromRgb(0x0C, 0x2B, 0x1D));
    private static readonly Brush FineBorder = new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69));
    private static readonly Brush FineFg = new SolidColorBrush(Color.FromRgb(0x34, 0xD3, 0x99));

    private static readonly Brush MasterBg = new SolidColorBrush(Color.FromRgb(0x1A, 0x1B, 0x2E));
    private static readonly Brush MasterBorder = new SolidColorBrush(Color.FromRgb(0x63, 0x66, 0xF1));
    private static readonly Brush MasterFg = new SolidColorBrush(Color.FromRgb(0x81, 0x8C, 0xF8));

    private static readonly Brush LegendaryBg = new SolidColorBrush(Color.FromRgb(0x28, 0x1B, 0x08));
    private static readonly Brush LegendaryBorder = new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06));
    private static readonly Brush LegendaryFg = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));

    private static readonly Brush RoseBg = new SolidColorBrush(Color.FromRgb(0x2A, 0x16, 0x22));
    private static readonly Brush RoseBorder = new SolidColorBrush(Color.FromRgb(0xE1, 0x1D, 0x48));
    private static readonly Brush RoseFg = new SolidColorBrush(Color.FromRgb(0xFB, 0x71, 0x85));

    private static readonly Brush SkyBg = new SolidColorBrush(Color.FromRgb(0x10, 0x26, 0x36));
    private static readonly Brush SkyBorder = new SolidColorBrush(Color.FromRgb(0x02, 0x84, 0xC7));
    private static readonly Brush SkyFg = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8));

    private static readonly Brush PurpleBg = new SolidColorBrush(Color.FromRgb(0x21, 0x19, 0x33));
    private static readonly Brush PurpleBorder = new SolidColorBrush(Color.FromRgb(0x7C, 0x3A, 0xED));
    private static readonly Brush PurpleFg = new SolidColorBrush(Color.FromRgb(0xA7, 0x8B, 0xFA));

    private static readonly Brush RedBg = new SolidColorBrush(Color.FromRgb(0x2E, 0x14, 0x18));
    private static readonly Brush RedBorder = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
    private static readonly Brush RedFg = new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71));

    private static readonly Brush BlueBg = new SolidColorBrush(Color.FromRgb(0x15, 0x22, 0x38));
    private static readonly Brush BlueBorder = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB));
    private static readonly Brush BlueFg = new SolidColorBrush(Color.FromRgb(0x60, 0xA5, 0xFA));

    private static readonly Brush GainBrush = new SolidColorBrush(Color.FromRgb(0x5A, 0xC0, 0x8A));
    private static readonly Brush AccentBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0xA4, 0x41));
    private static readonly Brush LossBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0x6C, 0x75));
    private static readonly Brush TextBrush = new SolidColorBrush(Color.FromRgb(0xE8, 0xED, 0xF5));

    public string Text { get; }

    public string SortKey => Text;

    public bool IsNumeric { get; }

    public long NumericValue { get; }

    public TextAlignment TextAlignment => IsNumeric ? TextAlignment.Right : TextAlignment.Left;

    public FontFamily FontFamily { get; }

    public Brush TextForeground { get; }

    public bool IsBadge { get; }

    public Brush? BadgeBackground { get; }

    public Brush? BadgeBorder { get; }

    public Brush? BadgeForeground { get; }

    public Visibility BadgeVisibility => IsBadge ? Visibility.Visible : Visibility.Collapsed;

    public bool HasProgress { get; }

    public double ProgressValue { get; set; }

    public double ProgressMax { get; set; }

    public Brush? ProgressBrush { get; }

    public Visibility ProgressVisibility => HasProgress ? Visibility.Visible : Visibility.Collapsed;

    public Visibility NormalTextVisibility => (!IsBadge && !HasProgress) ? Visibility.Visible : Visibility.Collapsed;

    private CellVm(
        string text,
        bool isNumeric,
        long numericValue,
        FontFamily fontFamily,
        Brush textForeground,
        bool isBadge,
        Brush? badgeBackground,
        Brush? badgeBorder,
        Brush? badgeForeground,
        bool hasProgress,
        double progressValue,
        double progressMax,
        Brush? progressBrush)
    {
        Text = text;
        IsNumeric = isNumeric;
        NumericValue = numericValue;
        FontFamily = fontFamily;
        TextForeground = textForeground;
        IsBadge = isBadge;
        BadgeBackground = badgeBackground;
        BadgeBorder = badgeBorder;
        BadgeForeground = badgeForeground;
        HasProgress = hasProgress;
        ProgressValue = progressValue;
        ProgressMax = progressMax;
        ProgressBrush = progressBrush;
    }

    public static CellVm Create(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new CellVm(string.Empty, false, 0, new FontFamily("Segoe UI"), TextBrush, false, null, null, null, false, 0, 0, null);
        }

        var trimmed = text.Trim();

        // 1. Check Masterpiece Equipment Quality Ranks
        if (trimmed is "LEGENDARY" or "HUYỀN THOẠI" || trimmed.Contains("[Huyền Thoại]") || trimmed.Contains("[HUYỀN THOẠI]"))
        {
            return CreateBadge(trimmed, LegendaryBg, LegendaryBorder, LegendaryFg);
        }
        if (trimmed is "MASTER" or "ĐẠI SƯ" || trimmed.Contains("[Đại Sư]") || trimmed.Contains("[ĐẠI SƯ]"))
        {
            return CreateBadge(trimmed, MasterBg, MasterBorder, MasterFg);
        }
        if (trimmed is "FINE" or "TINH CHẾ" || trimmed.Contains("[Tinh Chế]") || trimmed.Contains("[TINH CHẾ]"))
        {
            return CreateBadge(trimmed, FineBg, FineBorder, FineFg);
        }
        if (trimmed is "COMMON" or "PHỔ THÔNG" || trimmed.Contains("[Phổ Thông]") || trimmed.Contains("[PHỔ THÔNG]"))
        {
            return CreateBadge(trimmed, CommonBg, CommonBorder, CommonFg);
        }

        // 2. Check Sworn Brotherhood & Social Kizuna Ranks
        if (trimmed.Contains("Đào Viên") || trimmed.Contains("Kết Nghĩa") || trimmed == "Kim Lan Tri Kỷ")
        {
            return CreateBadge(trimmed, RoseBg, RoseBorder, RoseFg);
        }
        if (trimmed == "Huynh Đệ" || trimmed.Contains("Gia thần"))
        {
            return CreateBadge(trimmed, SkyBg, SkyBorder, SkyFg);
        }
        if (trimmed == "Tri Kỷ")
        {
            return CreateBadge(trimmed, PurpleBg, PurpleBorder, PurpleFg);
        }
        if (trimmed == "Bằng Hữu" || trimmed == "Sẵn sàng giao đấu" || trimmed == "Có thể chiêu mộ" || trimmed == "Đã đầu quân")
        {
            return CreateBadge(trimmed, FineBg, FineBorder, FineFg);
        }
        if (trimmed is "Sơ giao" or "Sơ Giao")
        {
            return CreateBadge(trimmed, CommonBg, CommonBorder, CommonFg);
        }
        if (trimmed is "Bất Hòa" or "Thù Địch" or "Quá hạn")
        {
            return CreateBadge(trimmed, RedBg, RedBorder, RedFg);
        }

        // 3. Troop Branches & Military Stances
        if (trimmed.Contains("Bộ Binh") || trimmed.StartsWith("🛡️"))
        {
            return CreateBadge(trimmed, BlueBg, BlueBorder, BlueFg);
        }
        if (trimmed.Contains("Kỵ Binh") || trimmed.StartsWith("🐎"))
        {
            return CreateBadge(trimmed, LegendaryBg, LegendaryBorder, LegendaryFg);
        }
        if (trimmed.Contains("Cung Binh") || trimmed.StartsWith("🏹"))
        {
            return CreateBadge(trimmed, FineBg, FineBorder, FineFg);
        }

        // 4. Caravan & Transit Logistics
        if (trimmed.Contains("Có Tiêu Sư") || trimmed is "Đã hoàn tất" or "đã tới" or "Đủ hàng bàn giao")
        {
            return CreateBadge(trimmed, FineBg, FineBorder, FineFg);
        }
        if (trimmed.Contains("Không Tiêu Sư") || trimmed.StartsWith("-") && trimmed.Contains("lạc") || trimmed is "Còn thiếu hàng")
        {
            return CreateBadge(trimmed, RedBg, RedBorder, RedFg);
        }
        if (trimmed is "Đang áp tải" or "đang đi" or "Chờ nhận")
        {
            return CreateBadge(trimmed, SkyBg, SkyBorder, SkyFg);
        }

        // 5. Check Progress / Durability Gauges (e.g. 85/100, Cấp 3/5, 5/10)
        var ratioMatch = RatioPattern.Match(trimmed);
        if (ratioMatch.Success &&
            double.TryParse(ratioMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cur) &&
            double.TryParse(ratioMatch.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var max) &&
            max > 0 && cur <= max * 1.5)
        {
            var fraction = Math.Clamp(cur / max, 0.0, 1.0);
            var brush = fraction switch
            {
                >= 0.6 => GainBrush,
                >= 0.3 => AccentBrush,
                _ => LossBrush
            };

            return new CellVm(
                trimmed,
                isNumeric: false,
                numericValue: (long)cur,
                new FontFamily("Segoe UI"),
                TextBrush,
                isBadge: false,
                null,
                null,
                null,
                hasProgress: true,
                progressValue: cur,
                progressMax: max,
                progressBrush: brush);
        }

        // 6. Check Numeric / Currency Format
        if (long.TryParse(trimmed, NumberStyles.AllowLeadingSign | NumberStyles.AllowThousands, Vietnamese, out var numVal))
        {
            var isNegative = numVal < 0;
            var numBrush = isNegative ? LossBrush : TextBrush;
            return new CellVm(trimmed, true, numVal, new FontFamily("Consolas"), numBrush, false, null, null, null, false, 0, 0, null);
        }

        return new CellVm(trimmed, false, 0, new FontFamily("Segoe UI"), TextBrush, false, null, null, null, false, 0, 0, null);
    }

    private static CellVm CreateBadge(string text, Brush bg, Brush border, Brush fg) =>
        new(text, false, 0, new FontFamily("Segoe UI"), TextBrush, true, bg, border, fg, false, 0, 0, null);

    public int CompareTo(object? obj)
    {
        if (obj is CellVm other)
        {
            if (IsNumeric && other.IsNumeric)
            {
                return NumericValue.CompareTo(other.NumericValue);
            }
            if (HasProgress && other.HasProgress)
            {
                var r1 = ProgressValue / Math.Max(1.0, ProgressMax);
                var r2 = other.ProgressValue / Math.Max(1.0, other.ProgressMax);
                return r1.CompareTo(r2);
            }
            return string.Compare(SortKey, other.SortKey, StringComparison.CurrentCultureIgnoreCase);
        }
        return 0;
    }

    public override string ToString() => Text;
}
