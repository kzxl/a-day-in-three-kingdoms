using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SimT.Host.Wpf.Controls;

public enum GameButtonVariant
{
    Primary,
    Secondary,
    Danger,
    Icon
}

/// <summary>
/// Custom Tam Quốc button replacing the default WPF Button.
/// Supports Primary (cinnabar), Secondary (lacquer + bronze border), Danger, and Icon variants,
/// chamfered corner geometry, hover highlight, pressed sink, disabled state, and inline cost badge.
/// </summary>
public sealed class GameButton : Button
{
    public static readonly DependencyProperty VariantProperty =
        DependencyProperty.Register(
            nameof(Variant),
            typeof(GameButtonVariant),
            typeof(GameButton),
            new FrameworkPropertyMetadata(GameButtonVariant.Secondary, OnVisualStatePropertyChanged));

    public static readonly DependencyProperty CostTextProperty =
        DependencyProperty.Register(
            nameof(CostText),
            typeof(string),
            typeof(GameButton),
            new FrameworkPropertyMetadata(null, OnVisualStatePropertyChanged));

    public static readonly DependencyProperty IconPathDataProperty =
        DependencyProperty.Register(
            nameof(IconPathData),
            typeof(string),
            typeof(GameButton),
            new FrameworkPropertyMetadata(null, OnVisualStatePropertyChanged));

    public static readonly DependencyProperty IsCompactProperty =
        DependencyProperty.Register(
            nameof(IsCompact),
            typeof(bool),
            typeof(GameButton),
            new FrameworkPropertyMetadata(false, OnVisualStatePropertyChanged));

    private static readonly Brush PrimaryBg = Freeze(new SolidColorBrush(Color.FromRgb(0xA8, 0x32, 0x1F)));
    private static readonly Brush PrimaryHoverBg = Freeze(new SolidColorBrush(Color.FromRgb(0xBD, 0x3B, 0x26)));
    private static readonly Brush PrimaryPressedBg = Freeze(new SolidColorBrush(Color.FromRgb(0x8E, 0x28, 0x18)));
    private static readonly Brush PrimaryBorder = Freeze(new SolidColorBrush(Color.FromRgb(0xC4, 0xA0, 0x62)));
    private static readonly Brush PrimaryFg = Freeze(new SolidColorBrush(Color.FromRgb(0xF3, 0xE6, 0xCC)));

    private static readonly Brush SecondaryBg = Freeze(new SolidColorBrush(Color.FromRgb(0x24, 0x1A, 0x13)));
    private static readonly Brush SecondaryHoverBg = Freeze(new SolidColorBrush(Color.FromRgb(0x30, 0x23, 0x19)));
    private static readonly Brush SecondaryPressedBg = Freeze(new SolidColorBrush(Color.FromRgb(0x1B, 0x13, 0x0E)));
    private static readonly Brush SecondaryBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x6E, 0x56, 0x34)));
    private static readonly Brush SecondaryHoverBorder = Freeze(new SolidColorBrush(Color.FromRgb(0xC4, 0xA0, 0x62)));
    private static readonly Brush SecondaryFg = Freeze(new SolidColorBrush(Color.FromRgb(0xEA, 0xDF, 0xC6)));

    private static readonly Brush DangerBg = Freeze(new SolidColorBrush(Color.FromRgb(0x2B, 0x15, 0x12)));
    private static readonly Brush DangerHoverBg = Freeze(new SolidColorBrush(Color.FromRgb(0x3D, 0x1C, 0x18)));
    private static readonly Brush DangerPressedBg = Freeze(new SolidColorBrush(Color.FromRgb(0x21, 0x0F, 0x0D)));
    private static readonly Brush DangerBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x8C, 0x2D, 0x22)));
    private static readonly Brush DangerFg = Freeze(new SolidColorBrush(Color.FromRgb(0xF2, 0xA4, 0x99)));

    private static readonly Brush AccentBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xC4, 0xA0, 0x62)));

    private readonly TranslateTransform _pressTransform = new(0, 0);
    private string _baseLabel = string.Empty;

    public GameButton()
    {
        Cursor = Cursors.Hand;
        FocusVisualStyle = null;
        MinHeight = 36;
        Padding = new Thickness(13, 6, 13, 6);
        FontSize = 12.5;
        FontWeight = FontWeights.SemiBold;
        RenderTransform = _pressTransform;
        SnapsToDevicePixels = true;

        Loaded += (_, _) => ApplyVisualState();
        IsEnabledChanged += (_, _) => Opacity = IsEnabled ? 1.0 : 0.42;
    }

    public GameButton(
        string label,
        GameButtonVariant variant = GameButtonVariant.Secondary,
        string? costText = null,
        string? iconPathData = null,
        bool isCompact = false) : this()
    {
        _baseLabel = label;
        Variant = variant;
        CostText = costText;
        IconPathData = iconPathData;
        IsCompact = isCompact;
        RebuildInnerContent();
        ApplyVisualState();
    }

    public GameButtonVariant Variant
    {
        get => (GameButtonVariant)GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public string? CostText
    {
        get => (string?)GetValue(CostTextProperty);
        set => SetValue(CostTextProperty, value);
    }

    public string? IconPathData
    {
        get => (string?)GetValue(IconPathDataProperty);
        set => SetValue(IconPathDataProperty, value);
    }

    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    public string Label
    {
        get => _baseLabel;
        set
        {
            _baseLabel = value;
            RebuildInnerContent();
        }
    }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        if (newContent is string str && str != _baseLabel)
        {
            _baseLabel = str;
            RebuildInnerContent();
        }
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        Clip = CreateChamferGeometry(sizeInfo.NewSize.Width, sizeInfo.NewSize.Height, IsCompact ? 3.0 : 4.5);
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        ApplyVisualState();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _pressTransform.Y = 0;
        ApplyVisualState();
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        if (IsEnabled)
        {
            _pressTransform.Y = 1.5;
            ApplyVisualState(pressed: true);
        }
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        _pressTransform.Y = 0;
        ApplyVisualState();
    }

    private static void OnVisualStatePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GameButton btn)
        {
            btn.RebuildInnerContent();
            btn.ApplyVisualState();
        }
    }

    private void RebuildInnerContent()
    {
        if (Content is UIElement && string.IsNullOrEmpty(_baseLabel) && string.IsNullOrEmpty(IconPathData))
        {
            return;
        }

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        if (!string.IsNullOrWhiteSpace(IconPathData))
        {
            Geometry geom;
            try
            {
                geom = Geometry.Parse(IconPathData);
            }
            catch
            {
                geom = Geometry.Empty;
            }

            var iconBrush = Variant == GameButtonVariant.Primary ? PrimaryFg : AccentBrush;
            var path = new Path
            {
                Data = geom,
                Stroke = iconBrush,
                StrokeThickness = 1.5,
                Width = IsCompact ? 11.5 : 13,
                Height = IsCompact ? 11.5 : 13,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = string.IsNullOrWhiteSpace(_baseLabel) ? new Thickness(0) : new Thickness(0, 0, 6, 0)
            };
            row.Children.Add(path);
        }

        if (!string.IsNullOrWhiteSpace(_baseLabel))
        {
            var displayText = string.IsNullOrWhiteSpace(CostText)
                ? _baseLabel
                : $"{_baseLabel} · {CostText}";

            row.Children.Add(new TextBlock
            {
                Text = displayText,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        }

        base.Content = row;
    }

    private void ApplyVisualState(bool pressed = false)
    {
        if (IsCompact)
        {
            MinHeight = 28;
            Padding = Variant == GameButtonVariant.Icon ? new Thickness(7, 4, 7, 4) : new Thickness(10, 4, 10, 4);
            FontSize = 11.5;
        }
        else
        {
            MinHeight = 36;
            Padding = Variant == GameButtonVariant.Icon ? new Thickness(9, 6, 9, 6) : new Thickness(13, 6, 13, 6);
            FontSize = 12.5;
        }

        var hover = IsMouseOver;
        switch (Variant)
        {
            case GameButtonVariant.Primary:
                Background = pressed ? PrimaryPressedBg : (hover ? PrimaryHoverBg : PrimaryBg);
                BorderBrush = PrimaryBorder;
                Foreground = PrimaryFg;
                break;
            case GameButtonVariant.Danger:
                Background = pressed ? DangerPressedBg : (hover ? DangerHoverBg : DangerBg);
                BorderBrush = hover ? PrimaryBorder : DangerBorder;
                Foreground = DangerFg;
                break;
            default:
                Background = pressed ? SecondaryPressedBg : (hover ? SecondaryHoverBg : SecondaryBg);
                BorderBrush = hover ? SecondaryHoverBorder : SecondaryBorder;
                Foreground = SecondaryFg;
                break;
        }
    }

    private static Geometry CreateChamferGeometry(double w, double h, double c)
    {
        if (w <= c * 2 || h <= c * 2)
        {
            return new RectangleGeometry(new Rect(0, 0, Math.Max(0, w), Math.Max(0, h)));
        }

        var sg = new StreamGeometry();
        using (var ctx = sg.Open())
        {
            ctx.BeginFigure(new Point(c, 0), isFilled: true, isClosed: true);
            ctx.LineTo(new Point(w - c, 0), isStroked: true, isSmoothJoin: false);
            ctx.LineTo(new Point(w, c), isStroked: true, isSmoothJoin: false);
            ctx.LineTo(new Point(w, h - c), isStroked: true, isSmoothJoin: false);
            ctx.LineTo(new Point(w - c, h), isStroked: true, isSmoothJoin: false);
            ctx.LineTo(new Point(c, h), isStroked: true, isSmoothJoin: false);
            ctx.LineTo(new Point(0, h - c), isStroked: true, isSmoothJoin: false);
            ctx.LineTo(new Point(0, c), isStroked: true, isSmoothJoin: false);
        }
        sg.Freeze();
        return sg;
    }

    private static Brush Freeze(SolidColorBrush b)
    {
        b.Freeze();
        return b;
    }
}
