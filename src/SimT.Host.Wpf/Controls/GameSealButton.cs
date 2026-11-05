using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Strategic Round Seal Button inspired by classic Three Kingdoms strategy titles (ROTK).
/// Features a circular bronze relief frame, cinnabar/lacquer core, vector icon,
/// dynamic hover aura, pressed depth sink, and optional notification badge.
/// Designed for key actions like "Bước sang ngày mới" (Advance Turn) or "Chiến dịch".
/// </summary>
public sealed class GameSealButton : Button
{
    public static readonly DependencyProperty SealLabelProperty =
        DependencyProperty.Register(
            nameof(SealLabel),
            typeof(string),
            typeof(GameSealButton),
            new FrameworkPropertyMetadata("QUA NGÀY", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SubLabelProperty =
        DependencyProperty.Register(
            nameof(SubLabel),
            typeof(string),
            typeof(GameSealButton),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BadgeTextProperty =
        DependencyProperty.Register(
            nameof(BadgeText),
            typeof(string),
            typeof(GameSealButton),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DiameterProperty =
        DependencyProperty.Register(
            nameof(Diameter),
            typeof(double),
            typeof(GameSealButton),
            new FrameworkPropertyMetadata(84.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsEmphasizedProperty =
        DependencyProperty.Register(
            nameof(IsEmphasized),
            typeof(bool),
            typeof(GameSealButton),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public string SealLabel
    {
        get => (string)GetValue(SealLabelProperty);
        set => SetValue(SealLabelProperty, value);
    }

    public string? SubLabel
    {
        get => (string?)GetValue(SubLabelProperty);
        set => SetValue(SubLabelProperty, value);
    }

    public string? BadgeText
    {
        get => (string?)GetValue(BadgeTextProperty);
        set => SetValue(BadgeTextProperty, value);
    }

    public double Diameter
    {
        get => (double)GetValue(DiameterProperty);
        set => SetValue(DiameterProperty, value);
    }

    public bool IsEmphasized
    {
        get => (bool)GetValue(IsEmphasizedProperty);
        set => SetValue(IsEmphasizedProperty, value);
    }

    // Frozen brushes for zero-allocation rendering
    private static readonly Brush OuterBronzeRing = Freeze(new LinearGradientBrush(
        Color.FromRgb(0x8A, 0x6E, 0x43),
        Color.FromRgb(0x42, 0x33, 0x1E),
        new Point(0, 0),
        new Point(1, 1)));

    private static readonly Brush OuterBronzeHoverRing = Freeze(new LinearGradientBrush(
        Color.FromRgb(0xC4, 0xA0, 0x62),
        Color.FromRgb(0x6E, 0x54, 0x30),
        new Point(0, 0),
        new Point(1, 1)));

    private static readonly Brush InnerBronzeGroove = Freeze(new SolidColorBrush(Color.FromRgb(0x24, 0x1A, 0x13)));

    private static readonly Brush CinnabarCore = Freeze(new RadialGradientBrush(
        Color.FromRgb(0xB8, 0x38, 0x22),
        Color.FromRgb(0x6A, 0x1E, 0x12)));

    private static readonly Brush CinnabarHoverCore = Freeze(new RadialGradientBrush(
        Color.FromRgb(0xD2, 0x46, 0x2E),
        Color.FromRgb(0x7D, 0x24, 0x16)));

    private static readonly Brush CinnabarPressedCore = Freeze(new RadialGradientBrush(
        Color.FromRgb(0x55, 0x17, 0x0E),
        Color.FromRgb(0x3B, 0x10, 0x0A)));

    private static readonly Brush DisabledCore = Freeze(new SolidColorBrush(Color.FromRgb(0x2B, 0x25, 0x20)));
    private static readonly Brush DisabledRing = Freeze(new SolidColorBrush(Color.FromRgb(0x42, 0x3A, 0x30)));

    private static readonly Brush GoldIconBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xF5, 0xDF, 0xB0)));
    private static readonly Brush GoldTextBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xFA, 0xEF, 0xD4)));
    private static readonly Brush SubTextBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xD4, 0xC0, 0x9A)));
    private static readonly Brush GoldRingPenBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xD6, 0xB4, 0x75)));

    private static readonly Brush BadgeBg = Freeze(new SolidColorBrush(Color.FromRgb(0xD9, 0x38, 0x1E)));
    private static readonly Brush BadgeBorder = Freeze(new SolidColorBrush(Color.FromRgb(0xF5, 0xDF, 0xB0)));
    private static readonly Brush BadgeFg = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)));

    private static readonly Typeface SerifTypeface = new(
        new FontFamily("Noto Serif, Georgia, SimSun, Segoe UI"),
        FontStyles.Normal,
        FontWeights.Bold,
        FontStretches.Normal);

    private static readonly Typeface SubTypeface = new(
        new FontFamily("Segoe UI"),
        FontStyles.Normal,
        FontWeights.SemiBold,
        FontStretches.Normal);

    private static readonly Typeface BadgeTypeface = new(
        new FontFamily("Segoe UI"),
        FontStyles.Normal,
        FontWeights.Bold,
        FontStretches.Normal);

    private bool _isHovered;
    private bool _isPressed;

    static GameSealButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(GameSealButton),
            new FrameworkPropertyMetadata(typeof(GameSealButton)));
    }

    public GameSealButton()
    {
        Focusable = true;
        Cursor = Cursors.Hand;
        Width = Diameter;
        Height = Diameter;

        // Subtle glow effect
        Effect = new DropShadowEffect
        {
            Color = Color.FromRgb(0xC4, 0xA0, 0x62),
            BlurRadius = 14,
            ShadowDepth = 2,
            Opacity = 0.35,
            Direction = 270
        };
    }

    protected override Size MeasureOverride(Size constraint)
    {
        double d = Diameter;
        return new Size(d, d);
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        if (Effect is DropShadowEffect shadow)
        {
            shadow.BlurRadius = 20;
            shadow.Opacity = 0.65;
            shadow.Color = Color.FromRgb(0xE5, 0xC4, 0x83);
        }
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        _isPressed = false;
        if (Effect is DropShadowEffect shadow)
        {
            shadow.BlurRadius = 14;
            shadow.Opacity = 0.35;
            shadow.Color = Color.FromRgb(0xC4, 0xA0, 0x62);
        }
        InvalidateVisual();
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.ChangedButton == MouseButton.Left)
        {
            _isPressed = true;
            InvalidateVisual();
        }
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.ChangedButton == MouseButton.Left)
        {
            _isPressed = false;
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0)
        {
            size = Diameter;
        }

        Point center = new(size / 2, size / 2);
        double radius = size / 2 - 2;

        if (_isPressed)
        {
            center.Y += 1.5;
        }

        bool isEnabled = IsEnabled;

        // 1. Outer bronze relief ring
        Brush ringBrush = !isEnabled ? DisabledRing : (_isHovered ? OuterBronzeHoverRing : OuterBronzeRing);
        dc.DrawEllipse(ringBrush, null, center, radius, radius);

        // 2. Decorative outer border track
        Pen ringPen = new(!isEnabled ? DisabledRing : GoldRingPenBrush, 1.2);
        dc.DrawEllipse(null, ringPen, center, radius - 2, radius - 2);

        // 3. Middle groove
        dc.DrawEllipse(InnerBronzeGroove, null, center, radius - 4.5, radius - 4.5);

        // 4. Central Core Disk (Cinnabar seal)
        Brush coreBrush = !isEnabled
            ? DisabledCore
            : (_isPressed ? CinnabarPressedCore : (_isHovered ? CinnabarHoverCore : CinnabarCore));
        double coreRadius = radius - 6.5;
        dc.DrawEllipse(coreBrush, null, center, coreRadius, coreRadius);

        // 5. Traditional circular decorative marks (4 cardinal marks)
        if (isEnabled)
        {
            double markDist = coreRadius - 3.5;
            Brush markBrush = GoldIconBrush;
            dc.DrawEllipse(markBrush, null, new Point(center.X, center.Y - markDist), 1.2, 1.2);
            dc.DrawEllipse(markBrush, null, new Point(center.X, center.Y + markDist), 1.2, 1.2);
            dc.DrawEllipse(markBrush, null, new Point(center.X - markDist, center.Y), 1.2, 1.2);
            dc.DrawEllipse(markBrush, null, new Point(center.X + markDist, center.Y), 1.2, 1.2);
        }

        // 6. Center Action Symbol (Curved play chevron / ancient cloud curl)
        double iconTop = center.Y - (string.IsNullOrEmpty(SubLabel) ? 14 : 18);
        DrawCenterIcon(dc, center.X, iconTop, isEnabled);

        // 7. Typography (Main label)
        if (!string.IsNullOrEmpty(SealLabel))
        {
            double fontSize = Math.Clamp(size * 0.13, 10, 13);
            FormattedText ft = new(
                SealLabel,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                SerifTypeface,
                fontSize,
                isEnabled ? GoldTextBrush : SubTextBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            double textY = center.Y + (string.IsNullOrEmpty(SubLabel) ? 3 : 0);
            dc.DrawText(ft, new Point(center.X - (ft.Width / 2), textY));
        }

        // 8. Sub label if provided (e.g. "+1 Ngày" or "Lượt kế")
        if (!string.IsNullOrEmpty(SubLabel))
        {
            double subFontSize = Math.Clamp(size * 0.105, 9, 11);
            FormattedText subFt = new(
                SubLabel,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                SubTypeface,
                subFontSize,
                SubTextBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            double subY = center.Y + 14;
            dc.DrawText(subFt, new Point(center.X - (subFt.Width / 2), subY));
        }

        // 9. Notification badge (Top-right corner)
        if (!string.IsNullOrEmpty(BadgeText))
        {
            DrawNotificationBadge(dc, size, BadgeText);
        }
    }

    private void DrawCenterIcon(DrawingContext dc, double cx, double cy, bool isEnabled)
    {
        // Classical forward tri-prong chevron
        StreamGeometry geom = new();
        using (StreamGeometryContext ctx = geom.Open())
        {
            ctx.BeginFigure(new Point(cx - 6, cy - 7), isFilled: true, isClosed: true);
            ctx.LineTo(new Point(cx + 6, cy), isStroked: true, isSmoothJoin: false);
            ctx.LineTo(new Point(cx - 6, cy + 7), isStroked: true, isSmoothJoin: false);
        }
        geom.Freeze();

        Brush iconBrush = isEnabled ? GoldIconBrush : SubTextBrush;
        dc.DrawGeometry(iconBrush, null, geom);
    }

    private void DrawNotificationBadge(DrawingContext dc, double size, string text)
    {
        FormattedText bft = new(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            BadgeTypeface,
            10,
            BadgeFg,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        double badgeWidth = Math.Max(18, bft.Width + 8);
        double badgeHeight = 16;
        double bx = size - badgeWidth - 2;
        double by = 2;

        Rect badgeRect = new(bx, by, badgeWidth, badgeHeight);
        dc.DrawRoundedRectangle(BadgeBg, new Pen(BadgeBorder, 1.2), badgeRect, 8, 8);
        dc.DrawText(bft, new Point(bx + (badgeWidth - bft.Width) / 2, by + 1));
    }

    private static Brush Freeze(Brush brush)
    {
        if (brush.CanFreeze)
        {
            brush.Freeze();
        }
        return brush;
    }
}
