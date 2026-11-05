using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SimT.Host.Wpf.Controls;

public enum GameItemRarity
{
    Common,
    Fine,
    Master,
    Legendary,
    Imperial
}

/// <summary>
/// Square Tam Quốc item slot with a rarity-colored border frame, vector item icon,
/// corner quantity badge, short label, and a rich multi-line tooltip.
/// Used for bag, warehouse, stall stock, and reward displays.
/// </summary>
public sealed class GameItemSlot : Border
{
    private static readonly Brush CommonBg = Freeze(new SolidColorBrush(Color.FromRgb(0x21, 0x19, 0x13)));
    private static readonly Brush CommonBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x52, 0x40, 0x30)));
    private static readonly Brush CommonAccent = Freeze(new SolidColorBrush(Color.FromRgb(0xB9, 0xAA, 0x8C)));

    private static readonly Brush FineBg = Freeze(new SolidColorBrush(Color.FromRgb(0x16, 0x24, 0x1D)));
    private static readonly Brush FineBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x3F, 0x6B, 0x55)));
    private static readonly Brush FineAccent = Freeze(new SolidColorBrush(Color.FromRgb(0x6C, 0xA8, 0x89)));

    private static readonly Brush MasterBg = Freeze(new SolidColorBrush(Color.FromRgb(0x17, 0x20, 0x29)));
    private static readonly Brush MasterBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x4A, 0x6B, 0x82)));
    private static readonly Brush MasterAccent = Freeze(new SolidColorBrush(Color.FromRgb(0x82, 0xB0, 0xCF)));

    private static readonly Brush LegendaryBg = Freeze(new SolidColorBrush(Color.FromRgb(0x2B, 0x20, 0x12)));
    private static readonly Brush LegendaryBorder = Freeze(new SolidColorBrush(Color.FromRgb(0xC4, 0xA0, 0x62)));
    private static readonly Brush LegendaryAccent = Freeze(new SolidColorBrush(Color.FromRgb(0xE8, 0xC6, 0x84)));

    private static readonly Brush ImperialBg = Freeze(new SolidColorBrush(Color.FromRgb(0x2B, 0x15, 0x12)));
    private static readonly Brush ImperialBorder = Freeze(new SolidColorBrush(Color.FromRgb(0xA8, 0x32, 0x1F)));
    private static readonly Brush ImperialAccent = Freeze(new SolidColorBrush(Color.FromRgb(0xF0, 0x86, 0x73)));

    private static readonly Brush QtyBg = Freeze(new SolidColorBrush(Color.FromArgb(0xDD, 0x15, 0x11, 0x0D)));
    private static readonly Brush NameFg = Freeze(new SolidColorBrush(Color.FromRgb(0xEA, 0xDF, 0xC6)));
    private static readonly FontFamily MonoFont = new("Consolas");

    public event Action? Clicked;

    public GameItemSlot(
        string nameVi,
        int quantity,
        GameItemRarity rarity = GameItemRarity.Common,
        string? categoryVi = null,
        IReadOnlyList<(string Label, string Value)>? stats = null,
        string? description = null,
        string? iconPathData = null)
    {
        var (bg, border, accent) = PaletteFor(rarity);

        Width = 74;
        Height = 74;
        Background = bg;
        BorderBrush = border;
        BorderThickness = new Thickness(1.5);
        CornerRadius = new CornerRadius(6);
        Margin = new Thickness(0, 0, 8, 8);
        Padding = new Thickness(5);
        Cursor = Cursors.Hand;
        SnapsToDevicePixels = true;

        var grid = new Grid();

        // Corner ornament frame
        var innerFrame = new Border
        {
            BorderBrush = accent,
            BorderThickness = new Thickness(0.6),
            Opacity = 0.28,
            CornerRadius = new CornerRadius(3),
            IsHitTestVisible = false
        };
        grid.Children.Add(innerFrame);

        // Vector icon in center-top
        var pathData = string.IsNullOrWhiteSpace(iconPathData)
            ? ResolveCategoryIconPath(nameVi, categoryVi)
            : iconPathData;

        Geometry geom;
        try
        {
            geom = Geometry.Parse(pathData);
        }
        catch
        {
            geom = Geometry.Empty;
        }

        var icon = new Path
        {
            Data = geom,
            Stroke = accent,
            StrokeThickness = 1.6,
            Width = 24,
            Height = 24,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 8, 0, 0)
        };
        grid.Children.Add(icon);

        // Quantity badge in top-right corner
        if (quantity > 0)
        {
            var qtyBadge = new Border
            {
                Background = QtyBg,
                BorderBrush = border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4, 1, 4, 1),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock
                {
                    Text = $"×{quantity}",
                    FontFamily = MonoFont,
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = accent
                }
            };
            grid.Children.Add(qtyBadge);
        }

        // Short item name at bottom
        var nameBlock = new TextBlock
        {
            Text = nameVi,
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = NameFg,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 64
        };
        grid.Children.Add(nameBlock);

        Child = grid;

        var rarityName = RarityLabelVi(rarity);
        var subtitle = string.IsNullOrWhiteSpace(categoryVi) ? rarityName : $"{rarityName} · {categoryVi}";
        ToolTip = new GameRichTooltip(nameVi, subtitle, stats, description);

        MouseEnter += (_, _) => BorderBrush = accent;
        MouseLeave += (_, _) => BorderBrush = border;
        MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            Clicked?.Invoke();
        };
    }

    public static GameItemRarity ParseRarity(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return GameItemRarity.Common;
        var u = text.ToUpperInvariant();
        if (u.Contains("IMPERIAL") || u.Contains("THẦN BINH") || u.Contains("BÁ VƯƠNG")) return GameItemRarity.Imperial;
        if (u.Contains("LEGEND") || u.Contains("HUYỀN THOẠI") || u.Contains("BẢO VẬT") || u.Contains("LỤA") || u.Contains("NGỌC")) return GameItemRarity.Legendary;
        if (u.Contains("MASTER") || u.Contains("ĐẠI SƯ") || u.Contains("VŨ KHÍ") || u.Contains("CHIẾN MÃ")) return GameItemRarity.Master;
        if (u.Contains("FINE") || u.Contains("TINH CHẾ") || u.Contains("TRÀ") || u.Contains("RƯỢU") || u.Contains("MUỐI")) return GameItemRarity.Fine;
        return GameItemRarity.Common;
    }

    public static string RarityLabelVi(GameItemRarity rarity) => rarity switch
    {
        GameItemRarity.Imperial => "Thần Phẩm Chu Sa",
        GameItemRarity.Legendary => "Cực Phẩm Hoàng Kim",
        GameItemRarity.Master => "Thượng Phẩm Danh Gia",
        GameItemRarity.Fine => "Tinh Chế Thanh Ngọc",
        _ => "Phổ Thông"
    };

    public static string ResolveCategoryIconPath(string nameVi, string? category = null)
    {
        var key = $"{nameVi} {category}".ToLowerInvariant();
        if (key.Contains("lụa") || key.Contains("gấm") || key.Contains("vải"))
            return "M4,6 C8,4 16,8 20,6 L20,18 C16,20 8,16 4,18 Z M8,7 L8,17 M16,7 L16,17";
        if (key.Contains("trà") || key.Contains("dược") || key.Contains("thảo"))
            return "M12,20 L12,9 M12,15 C7,15 5,10 6,5 C10,5 12,9 12,15 M12,12 C17,12 19,8 18,4 C14,4 12,7 12,12";
        if (key.Contains("rượu") || key.Contains("tửu") || key.Contains("bình"))
            return "M9,3 L15,3 M10,3 L10,7 L7,11 L7,19 C7,20.5 8.5,21 12,21 C15.5,21 17,20.5 17,19 L17,11 L14,7 L14,3";
        if (key.Contains("kiếm") || key.Contains("đao") || key.Contains("thương") || key.Contains("cung") || key.Contains("vũ khí") || key.Contains("sắt"))
            return "M4,20 L19,5 M14,5 L19,5 L19,10 M6,14 L10,18";
        if (key.Contains("ngựa") || key.Contains("mã"))
            return "M5,18 L5,11 L9,7 L15,5 L19,8 L17,12 L13,12 L13,18 M9,18 L9,14";
        if (key.Contains("gạo") || key.Contains("lương") || key.Contains("lúa") || key.Contains("muối"))
            return "M6,9 L18,9 L19,20 L5,20 Z M8,9 C8,5 16,5 16,9";
        return "M12,3 L20,7.5 L20,16.5 L12,21 L4,16.5 L4,7.5 Z M12,12 L20,7.5 M12,12 L4,7.5 M12,12 L12,21";
    }

    private static (Brush Bg, Brush Border, Brush Accent) PaletteFor(GameItemRarity rarity) => rarity switch
    {
        GameItemRarity.Imperial => (ImperialBg, ImperialBorder, ImperialAccent),
        GameItemRarity.Legendary => (LegendaryBg, LegendaryBorder, LegendaryAccent),
        GameItemRarity.Master => (MasterBg, MasterBorder, MasterAccent),
        GameItemRarity.Fine => (FineBg, FineBorder, FineAccent),
        _ => (CommonBg, CommonBorder, CommonAccent)
    };

    private static Brush Freeze(SolidColorBrush b)
    {
        b.Freeze();
        return b;
    }
}
