using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace SimT.Host.Wpf.Controls;

public enum SealShape
{
    Square,
    Round
}

/// <summary>
/// Strategic Seal Button inspired by Imperial seals and classic Three Kingdoms strategy titles.
/// Supports Square (imperial seal stamp) and Round shapes, with bronze relief frame,
/// cinnabar lacquer core, vector glyph, dynamic hover glow, pressed depth sink, and notification badge.
/// </summary>
public sealed class GameSealButton : Button
{
    public static readonly DependencyProperty SealShapeProperty =
        DependencyProperty.Register(
            nameof(Shape),
            typeof(SealShape),
            typeof(GameSealButton),
            new FrameworkPropertyMetadata(SealShape.Square, FrameworkPropertyMetadataOptions.AffectsRender));

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
            new FrameworkPropertyMetadata(76.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public SealShape Shape
    {
        get => (SealShape)GetValue(SealShapeProperty);
        set => SetValue(SealShapeProperty, value);
    }

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

    // Frozen brushes for zero-allocation rendering (R_PERF)
    private static readonly Brush OuterBronzeRing = Freeze(new LinearGradientBrush(
        Color.FromRgb(0x8A, 0x6E, 0x43),
        Color.FromRgb(0x42, 0x33, 0x1E),
        new Point(0, 0),
        new Point(1, 1)));

    private static readonly Brush OuterBronzeHoverRing = Freeze(new LinearGradientBrush(
        Color.FromRgb(0xD6, 0xB4, 0x75),
        Color.FromRgb(0x7D, 0x5C, 0x32),
        new Point(0, 0),
        new Point(1, 1)));

    private static readonly Brush InnerBronzeGroove = Freeze(new SolidColorBrush(Color.FromRgb(0x22, 0x18, 0x10)));

    private static readonly Brush CinnabarCore = Freeze(new RadialGradientBrush(
        Color.FromRgb(0xB8, 0x38, 0x22),
        Color.FromRgb(0x6A, 0x1E, 0x12)));

    private static readonly Brush CinnabarHoverCore = Freeze(new RadialGradientBrush(
        Color.FromRgb(0xD8, 0x48, 0x30),
        Color.FromRgb(0x82, 0x26, 0x18)));

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
        if (Shape == SealShape.Square)
        {
            double w = double.IsNaN(Width) || Width <= 0 ? 190.0 : Width;
            double h = double.IsNaN(Height) || Height <= 0 ? 60.0 : Height;
            return new Size(w, h);
        }

        double d = Diameter;
        return new Size(d, d);
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        if (Effect is DropShadowEffect shadow)
        {
            shadow.BlurRadius = 22;
            shadow.Opacity = 0.70;
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

        double width = ActualWidth > 0 ? ActualWidth : (Shape == SealShape.Square ? 190.0 : Diameter);
        double height = ActualHeight > 0 ? ActualHeight : (Shape == SealShape.Square ? 60.0 : Diameter);

        bool isEnabled = IsEnabled;
        double offsetY = _isPressed ? 1.5 : 0;

        if (Shape == SealShape.Square)
        {
            DrawSquareSeal(dc, width, height, isEnabled, offsetY);
        }
        else
        {
            DrawRoundSeal(dc, width, height, isEnabled, offsetY);
        }
    }

    private void DrawSquareSeal(DrawingContext dc, double w, double h, bool isEnabled, double offsetY)
    {
        Rect outerRect = new(0, offsetY, w, h);
        double r = 6.0;

        // 1. Outer bronze relief border
        Brush borderBrush = !isEnabled ? DisabledRing : (_isHovered ? OuterBronzeHoverRing : OuterBronzeRing);
        dc.DrawRoundedRectangle(borderBrush, null, outerRect, r, r);

        // 2. Decorative inner track
        Rect trackRect = new(outerRect.Left + 2.5, outerRect.Top + 2.5, Math.Max(0, w - 5), Math.Max(0, h - 5));
        Pen goldPen = new(!isEnabled ? DisabledRing : GoldRingPenBrush, 1.2);
        dc.DrawRoundedRectangle(null, goldPen, trackRect, r - 1.5, r - 1.5);

        // 3. Middle dark groove
        Rect grooveRect = new(outerRect.Left + 4.5, outerRect.Top + 4.5, Math.Max(0, w - 9), Math.Max(0, h - 9));
        dc.DrawRoundedRectangle(InnerBronzeGroove, null, grooveRect, r - 2.5, r - 2.5);

        // 4. Central Cinnabar Core
        Brush coreBrush = !isEnabled
            ? DisabledCore
            : (_isPressed ? CinnabarPressedCore : (_isHovered ? CinnabarHoverCore : CinnabarCore));
        Rect coreRect = new(outerRect.Left + 6.0, outerRect.Top + 6.0, Math.Max(0, w - 12), Math.Max(0, h - 12));
        dc.DrawRoundedRectangle(coreBrush, null, coreRect, r - 3.5, r - 3.5);

        // 5. Corner decorative studs
        if (isEnabled)
        {
            Brush studBrush = GoldIconBrush;
            dc.DrawEllipse(studBrush, null, new Point(coreRect.Left + 5, coreRect.Top + 5), 1.2, 1.2);
            dc.DrawEllipse(studBrush, null, new Point(coreRect.Right - 5, coreRect.Top + 5), 1.2, 1.2);
            dc.DrawEllipse(studBrush, null, new Point(coreRect.Left + 5, coreRect.Bottom - 5), 1.2, 1.2);
            dc.DrawEllipse(studBrush, null, new Point(coreRect.Right - 5, coreRect.Bottom - 5), 1.2, 1.2);
        }

        // 6. Typography & Center Icon
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        Point center = new(w / 2, (h / 2) + offsetY);

        // If width is wide, render in horizontal layout: Icon + Label + SubLabel
        if (w > 120)
        {
            // Center Play Icon on left
            double iconX = center.X - 44;
            DrawCenterIcon(dc, iconX, center.Y, isEnabled);

            // Text Panel
            FormattedText ft = new(
                SealLabel,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                SerifTypeface,
                14.0,
                isEnabled ? GoldTextBrush : SubTextBrush,
                dpi);

            double textX = iconX + 16;
            double titleY = string.IsNullOrEmpty(SubLabel) ? center.Y - (ft.Height / 2) : center.Y - 14;
            dc.DrawText(ft, new Point(textX, titleY));

            if (!string.IsNullOrEmpty(SubLabel))
            {
                FormattedText subFt = new(
                    SubLabel,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    SubTypeface,
                    11.0,
                    SubTextBrush,
                    dpi);

                dc.DrawText(subFt, new Point(textX, center.Y + 2));
            }
        }
        else
        {
            // Compact Square
            DrawCenterIcon(dc, center.X, center.Y - 14, isEnabled);

            FormattedText ft = new(
                SealLabel,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                SerifTypeface,
                12.5,
                isEnabled ? GoldTextBrush : SubTextBrush,
                dpi);

            dc.DrawText(ft, new Point(center.X - (ft.Width / 2), center.Y + 1));

            if (!string.IsNullOrEmpty(SubLabel))
            {
                FormattedText subFt = new(
                    SubLabel,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    SubTypeface,
                    10.0,
                    SubTextBrush,
                    dpi);

                dc.DrawText(subFt, new Point(center.X - (subFt.Width / 2), center.Y + 16));
            }
        }

        // Notification badge
        if (!string.IsNullOrEmpty(BadgeText))
        {
            DrawNotificationBadge(dc, w, BadgeText);
        }
    }

    private void DrawRoundSeal(DrawingContext dc, double w, double h, bool isEnabled, double offsetY)
    {
        double size = Math.Min(w, h);
        Point center = new(w / 2, (h / 2) + offsetY);
        double radius = (size / 2) - 2;

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

        // 5. Traditional circular decorative marks
        if (isEnabled)
        {
            double markDist = coreRadius - 3.5;
            Brush markBrush = GoldIconBrush;
            dc.DrawEllipse(markBrush, null, new Point(center.X, center.Y - markDist), 1.2, 1.2);
            dc.DrawEllipse(markBrush, null, new Point(center.X, center.Y + markDist), 1.2, 1.2);
            dc.DrawEllipse(markBrush, null, new Point(center.X - markDist, center.Y), 1.2, 1.2);
            dc.DrawEllipse(markBrush, null, new Point(center.X + markDist, center.Y), 1.2, 1.2);
        }

        // 6. Center Action Symbol
        double iconTop = center.Y - (string.IsNullOrEmpty(SubLabel) ? 14 : 18);
        DrawCenterIcon(dc, center.X, iconTop, isEnabled);

        // 7. Typography (Main label)
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
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
                dpi);

            double textY = center.Y + (string.IsNullOrEmpty(SubLabel) ? 3 : 0);
            dc.DrawText(ft, new Point(center.X - (ft.Width / 2), textY));
        }

        // 8. Sub label
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
                dpi);

            double subY = center.Y + 14;
            dc.DrawText(subFt, new Point(center.X - (subFt.Width / 2), subY));
        }

        // 9. Notification badge
        if (!string.IsNullOrEmpty(BadgeText))
        {
            DrawNotificationBadge(dc, size, BadgeText);
        }
    }

    private void DrawCenterIcon(DrawingContext dc, double cx, double cy, bool isEnabled)
    {
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
