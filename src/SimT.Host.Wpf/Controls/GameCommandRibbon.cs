using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Data item representing a strategic category or command tab on the ribbon.
/// </summary>
public sealed class CommandRibbonItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? IconPathData { get; set; }
    public string? BadgeText { get; set; }
    public object? Tag { get; set; }
    public List<CommandRibbonSubItem> SubItems { get; set; } = new();
}

public sealed class CommandRibbonSubItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? IconPathData { get; set; }
    public string? BadgeText { get; set; }
    public object? Tag { get; set; }
}

public class CommandRibbonItemSelectedEventArgs : RoutedEventArgs
{
    public CommandRibbonItem Item { get; }
    public CommandRibbonSubItem? SubItem { get; }

    public CommandRibbonItemSelectedEventArgs(RoutedEvent routedEvent, CommandRibbonItem item, CommandRibbonSubItem? subItem = null)
        : base(routedEvent)
    {
        Item = item;
        SubItem = subItem;
    }
}

/// <summary>
/// Strategic Vertical Command Ribbon inspired by classic strategy UI.
/// Organizes deep menus into elegant lacquered silk tabs with subtle bronze borders,
/// status pips, badge indicators, and responsive flyout sub-options.
/// </summary>
public sealed class GameCommandRibbon : Control
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(ObservableCollection<CommandRibbonItem>),
            typeof(GameCommandRibbon),
            new FrameworkPropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty SelectedItemIdProperty =
        DependencyProperty.Register(
            nameof(SelectedItemId),
            typeof(string),
            typeof(GameCommandRibbon),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemIdChanged));

    public static readonly RoutedEvent ItemSelectedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(ItemSelected),
            RoutingStrategy.Bubble,
            typeof(EventHandler<CommandRibbonItemSelectedEventArgs>),
            typeof(GameCommandRibbon));

    public ObservableCollection<CommandRibbonItem> ItemsSource
    {
        get => (ObservableCollection<CommandRibbonItem>)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public string? SelectedItemId
    {
        get => (string?)GetValue(SelectedItemIdProperty);
        set => SetValue(SelectedItemIdProperty, value);
    }

    public event EventHandler<CommandRibbonItemSelectedEventArgs> ItemSelected
    {
        add => AddHandler(ItemSelectedEvent, value);
        remove => RemoveHandler(ItemSelectedEvent, value);
    }

    private static readonly Brush RailBackground = Freeze(new SolidColorBrush(Color.FromRgb(0x1B, 0x14, 0x0E)));
    private static readonly Brush ItemNormalBg = Freeze(new LinearGradientBrush(
        Color.FromRgb(0x27, 0x1D, 0x15),
        Color.FromRgb(0x1E, 0x17, 0x10),
        new Point(0, 0),
        new Point(1, 0)));

    private static readonly Brush ItemHoverBg = Freeze(new LinearGradientBrush(
        Color.FromRgb(0x3B, 0x2A, 0x1C),
        Color.FromRgb(0x2A, 0x1E, 0x14),
        new Point(0, 0),
        new Point(1, 0)));

    private static readonly Brush ItemSelectedBg = Freeze(new LinearGradientBrush(
        Color.FromRgb(0x52, 0x36, 0x20),
        Color.FromRgb(0x32, 0x22, 0x15),
        new Point(0, 0),
        new Point(1, 0)));

    private static readonly Brush BorderBronze = Freeze(new SolidColorBrush(Color.FromRgb(0x4A, 0x38, 0x24)));
    private static readonly Brush BorderGoldActive = Freeze(new SolidColorBrush(Color.FromRgb(0xC4, 0xA0, 0x62)));
    private static readonly Brush GoldAccent = Freeze(new SolidColorBrush(Color.FromRgb(0xD6, 0xB4, 0x75)));
    private static readonly Brush TextInk = Freeze(new SolidColorBrush(Color.FromRgb(0xF3, 0xE6, 0xCC)));
    private static readonly Brush TextMuted = Freeze(new SolidColorBrush(Color.FromRgb(0xB9, 0xAA, 0x8C)));
    private static readonly Brush TextFaint = Freeze(new SolidColorBrush(Color.FromRgb(0x8F, 0x7F, 0x62)));

    private static readonly Brush BadgeBg = Freeze(new SolidColorBrush(Color.FromRgb(0x8E, 0x28, 0x18)));
    private static readonly Brush BadgeFg = Freeze(new SolidColorBrush(Color.FromRgb(0xFA, 0xEF, 0xD4)));

    private static readonly Typeface TitleTypeface = new(
        new FontFamily("Noto Serif, Georgia, SimSun, Segoe UI"),
        FontStyles.Normal,
        FontWeights.SemiBold,
        FontStretches.Normal);

    private static readonly Typeface SubtitleTypeface = new(
        new FontFamily("Segoe UI"),
        FontStyles.Normal,
        FontWeights.Normal,
        FontStretches.Normal);

    private static readonly Typeface BadgeTypeface = new(
        new FontFamily("Segoe UI"),
        FontStyles.Normal,
        FontWeights.Bold,
        FontStretches.Normal);

    private const double TabHeight = 52.0;
    private const double TabSpacing = 9.0;
    private const double LeftCurveWidth = 6.0;

    private int _hoveredIndex = -1;

    static GameCommandRibbon()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(GameCommandRibbon),
            new FrameworkPropertyMetadata(typeof(GameCommandRibbon)));
    }

    public GameCommandRibbon()
    {
        Focusable = true;
        Cursor = Cursors.Hand;
        Width = 200;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GameCommandRibbon ribbon)
        {
            ribbon.InvalidateMeasure();
            ribbon.InvalidateVisual();
        }
    }

    private static void OnSelectedItemIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GameCommandRibbon ribbon)
        {
            ribbon.InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size constraint)
    {
        int count = ItemsSource?.Count ?? 0;
        if (count == 0)
        {
            return new Size(Width, 0);
        }

        double totalHeight = (count * TabHeight) + ((count - 1) * TabSpacing) + 8;
        return new Size(Width, totalHeight);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Point pos = e.GetPosition(this);
        int newHovered = HitTestIndex(pos.Y);
        if (newHovered != _hoveredIndex)
        {
            _hoveredIndex = newHovered;
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hoveredIndex = -1;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Point pos = e.GetPosition(this);
        int index = HitTestIndex(pos.Y);
        if (ItemsSource != null && index >= 0 && index < ItemsSource.Count)
        {
            CommandRibbonItem item = ItemsSource[index];
            SelectedItemId = item.Id;
            RaiseEvent(new CommandRibbonItemSelectedEventArgs(ItemSelectedEvent, item));
            InvalidateVisual();
        }
    }

    private int HitTestIndex(double y)
    {
        if (y < 4 || ItemsSource == null || ItemsSource.Count == 0) return -1;
        double currentY = 4;
        for (int i = 0; i < ItemsSource.Count; i++)
        {
            if (y >= currentY && y <= currentY + TabHeight)
            {
                return i;
            }
            currentY += TabHeight + TabSpacing;
        }
        return -1;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (ItemsSource == null || ItemsSource.Count == 0)
        {
            return;
        }

        double width = ActualWidth;
        double currentY = 4;
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        for (int i = 0; i < ItemsSource.Count; i++)
        {
            CommandRibbonItem item = ItemsSource[i];
            bool isSelected = string.Equals(item.Id, SelectedItemId, StringComparison.OrdinalIgnoreCase);
            bool isHovered = (i == _hoveredIndex);

            Rect tabRect = new(0, currentY, width, TabHeight);
            DrawTab(dc, tabRect, item, isSelected, isHovered, dpi);

            currentY += TabHeight + TabSpacing;
        }
    }

    private void DrawTab(DrawingContext dc, Rect rect, CommandRibbonItem item, bool isSelected, bool isHovered, double dpi)
    {
        // 1. Tab Body Background with chamfered right end
        Brush bg = isSelected ? ItemSelectedBg : (isHovered ? ItemHoverBg : ItemNormalBg);
        Pen borderPen = new(isSelected ? BorderGoldActive : (isHovered ? GoldAccent : BorderBronze), isSelected ? 1.4 : 1.0);

        // Draw tab geometry (trapezoidal / beveled corner on the right)
        StreamGeometry tabGeom = new();
        using (StreamGeometryContext ctx = tabGeom.Open())
        {
            double r = 4.0;
            ctx.BeginFigure(new Point(rect.Left + LeftCurveWidth, rect.Top), isFilled: true, isClosed: true);
            ctx.LineTo(new Point(rect.Right - 8, rect.Top), isStroked: true, isSmoothJoin: false);
            ctx.LineTo(new Point(rect.Right, rect.Top + 8), isStroked: true, isSmoothJoin: false);
            ctx.LineTo(new Point(rect.Right, rect.Bottom - r), isStroked: true, isSmoothJoin: false);
            ctx.LineTo(new Point(rect.Right - r, rect.Bottom), isStroked: true, isSmoothJoin: false);
            ctx.LineTo(new Point(rect.Left + LeftCurveWidth, rect.Bottom), isStroked: true, isSmoothJoin: false);
            ctx.LineTo(new Point(rect.Left, rect.Bottom - (TabHeight / 2)), isStroked: true, isSmoothJoin: true);
        }
        tabGeom.Freeze();
        dc.DrawGeometry(bg, borderPen, tabGeom);

        // 2. Active Indicator Pip on Left
        if (isSelected)
        {
            Rect indicator = new(rect.Left + 2, rect.Top + 10, 3, rect.Height - 20);
            dc.DrawRoundedRectangle(BorderGoldActive, null, indicator, 1.5, 1.5);
        }

        // 3. Icon (if path data provided)
        double textStartX = rect.Left + 16;
        if (!string.IsNullOrEmpty(item.IconPathData))
        {
            try
            {
                Geometry iconGeom = Geometry.Parse(item.IconPathData);
                dc.PushTransform(new TranslateTransform(rect.Left + 14, rect.Top + (TabHeight / 2) - 8));
                dc.DrawGeometry(null, new Pen(isSelected ? BorderGoldActive : (isHovered ? GoldAccent : TextMuted), 1.3), iconGeom);
                dc.Pop();
                textStartX += 18;
            }
            catch
            {
                // Fallback gracefully if geometry fails to parse
            }
        }

        // 4. Title Text
        FormattedText titleFt = new(
            item.Title,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            TitleTypeface,
            13.0,
            isSelected ? TextInk : (isHovered ? TextInk : TextMuted),
            dpi);

        double titleY = string.IsNullOrEmpty(item.Subtitle)
            ? rect.Top + (TabHeight - titleFt.Height) / 2
            : rect.Top + 8;

        dc.DrawText(titleFt, new Point(textStartX, titleY));

        // 5. Subtitle (if available)
        if (!string.IsNullOrEmpty(item.Subtitle))
        {
            FormattedText subFt = new(
                item.Subtitle,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                SubtitleTypeface,
                10.5,
                TextFaint,
                dpi);

            dc.DrawText(subFt, new Point(textStartX, rect.Top + 27));
        }

        // 6. Badge indicator
        if (!string.IsNullOrEmpty(item.BadgeText))
        {
            FormattedText bFt = new(
                item.BadgeText,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                BadgeTypeface,
                9.5,
                BadgeFg,
                dpi);

            double bWidth = Math.Max(16, bFt.Width + 8);
            double bHeight = 15;
            double bx = rect.Right - bWidth - 8;
            double by = rect.Top + (TabHeight - bHeight) / 2;

            Rect bRect = new(bx, by, bWidth, bHeight);
            dc.DrawRoundedRectangle(BadgeBg, null, bRect, 4, 4);
            dc.DrawText(bFt, new Point(bx + (bWidth - bFt.Width) / 2, by + 1));
        }
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
