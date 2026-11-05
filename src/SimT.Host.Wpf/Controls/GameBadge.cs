using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SimT.Host.Wpf.Controls;

public enum BadgeVariant
{
    Default,
    Gain,
    Warning,
    Loss,
    Info,
    Purple,
    Gold,
    Rose
}

public enum BadgeSize
{
    Small,
    Medium,
    Large
}

/// <summary>
/// Hardware-efficient, zero-leak badge and status chip control.
/// Uses pre-frozen solid color brushes to eliminate GC allocations while
/// rendering compact status tags, ranks, and categories across the UI.
/// </summary>
public sealed class GameBadge : Border
{
    private static readonly Brush DefaultBg = Freeze(new SolidColorBrush(Color.FromRgb(0x1E, 0x24, 0x2C)));
    private static readonly Brush DefaultBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)));
    private static readonly Brush DefaultFg = Freeze(new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)));

    private static readonly Brush GainBg = Freeze(new SolidColorBrush(Color.FromRgb(0x0C, 0x2B, 0x1D)));
    private static readonly Brush GainBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69)));
    private static readonly Brush GainFg = Freeze(new SolidColorBrush(Color.FromRgb(0x34, 0xD3, 0x99)));

    private static readonly Brush WarningBg = Freeze(new SolidColorBrush(Color.FromRgb(0x28, 0x1B, 0x08)));
    private static readonly Brush WarningBorder = Freeze(new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06)));
    private static readonly Brush WarningFg = Freeze(new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)));

    private static readonly Brush LossBg = Freeze(new SolidColorBrush(Color.FromRgb(0x2E, 0x14, 0x18)));
    private static readonly Brush LossBorder = Freeze(new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)));
    private static readonly Brush LossFg = Freeze(new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71)));

    private static readonly Brush InfoBg = Freeze(new SolidColorBrush(Color.FromRgb(0x10, 0x26, 0x36)));
    private static readonly Brush InfoBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x02, 0x84, 0xC7)));
    private static readonly Brush InfoFg = Freeze(new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8)));

    private static readonly Brush PurpleBg = Freeze(new SolidColorBrush(Color.FromRgb(0x21, 0x19, 0x33)));
    private static readonly Brush PurpleBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x7C, 0x3A, 0xED)));
    private static readonly Brush PurpleFg = Freeze(new SolidColorBrush(Color.FromRgb(0xA7, 0x8B, 0xFA)));

    private static readonly Brush GoldBg = Freeze(new SolidColorBrush(Color.FromRgb(0x2A, 0x22, 0x0D)));
    private static readonly Brush GoldBorder = Freeze(new SolidColorBrush(Color.FromRgb(0xD9, 0xA4, 0x41)));
    private static readonly Brush GoldFg = Freeze(new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24)));

    private static readonly Brush RoseBg = Freeze(new SolidColorBrush(Color.FromRgb(0x2A, 0x16, 0x22)));
    private static readonly Brush RoseBorder = Freeze(new SolidColorBrush(Color.FromRgb(0xE1, 0x1D, 0x48)));
    private static readonly Brush RoseFg = Freeze(new SolidColorBrush(Color.FromRgb(0xFB, 0x71, 0x85)));

    private readonly TextBlock _textBlock;
    private readonly Border? _dot;
    private BadgeVariant _variant = BadgeVariant.Default;
    private BadgeSize _size = BadgeSize.Medium;

    public GameBadge() : this(string.Empty, BadgeVariant.Default) { }

    public GameBadge(string text, BadgeVariant variant = BadgeVariant.Default, bool hasDot = false)
    {
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(4);
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalAlignment = HorizontalAlignment.Left;

        _textBlock = new TextBlock
        {
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (hasDot)
        {
            var stack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _dot = new Border
            {
                Width = 6,
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(0, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            stack.Children.Add(_dot);
            stack.Children.Add(_textBlock);
            Child = stack;
        }
        else
        {
            Child = _textBlock;
        }

        Variant = variant;
        Size = BadgeSize.Medium;
        Text = text;
    }

    public string Text
    {
        get => _textBlock.Text;
        set => _textBlock.Text = value;
    }

    public BadgeVariant Variant
    {
        get => _variant;
        set
        {
            _variant = value;
            ApplyPalette();
        }
    }

    public BadgeSize Size
    {
        get => _size;
        set
        {
            _size = value;
            ApplySize();
        }
    }

    public bool IsPill
    {
        set => CornerRadius = value ? new CornerRadius(10) : new CornerRadius(4);
    }

    private void ApplyPalette()
    {
        var (bg, border, fg) = _variant switch
        {
            BadgeVariant.Gain => (GainBg, GainBorder, GainFg),
            BadgeVariant.Warning => (WarningBg, WarningBorder, WarningFg),
            BadgeVariant.Loss => (LossBg, LossBorder, LossFg),
            BadgeVariant.Info => (InfoBg, InfoBorder, InfoFg),
            BadgeVariant.Purple => (PurpleBg, PurpleBorder, PurpleFg),
            BadgeVariant.Gold => (GoldBg, GoldBorder, GoldFg),
            BadgeVariant.Rose => (RoseBg, RoseBorder, RoseFg),
            _ => (DefaultBg, DefaultBorder, DefaultFg)
        };

        Background = bg;
        BorderBrush = border;
        _textBlock.Foreground = fg;
        if (_dot is not null)
        {
            _dot.Background = fg;
        }
    }

    private void ApplySize()
    {
        switch (_size)
        {
            case BadgeSize.Small:
                Padding = new Thickness(5, 1, 5, 1);
                _textBlock.FontSize = 10.5;
                break;
            case BadgeSize.Large:
                Padding = new Thickness(9, 4, 9, 4);
                _textBlock.FontSize = 12.5;
                break;
            default:
                Padding = new Thickness(7, 2, 7, 2);
                _textBlock.FontSize = 11.5;
                break;
        }
    }

    private static Brush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
