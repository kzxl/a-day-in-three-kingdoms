using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
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
    public string? Subtitle { get; set; }
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
/// Organizes deep menus into lacquered silk tabs with bronze borders,
/// status pips, badge indicators, and inline expandable sub-items.
/// </summary>
public sealed class GameCommandRibbon : Control
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(ObservableCollection<CommandRibbonItem>),
            typeof(GameCommandRibbon),
            new FrameworkPropertyMetadata(null, OnLayoutPropertyChanged));

    public static readonly DependencyProperty SelectedItemIdProperty =
        DependencyProperty.Register(
            nameof(SelectedItemId),
            typeof(string),
            typeof(GameCommandRibbon),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnLayoutPropertyChanged));

    public static readonly DependencyProperty SelectedSubItemIdProperty =
        DependencyProperty.Register(
            nameof(SelectedSubItemId),
            typeof(string),
            typeof(GameCommandRibbon),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnLayoutPropertyChanged));

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

    public string? SelectedSubItemId
    {
        get => (string?)GetValue(SelectedSubItemIdProperty);
        set => SetValue(SelectedSubItemIdProperty, value);
    }

    public event EventHandler<CommandRibbonItemSelectedEventArgs> ItemSelected
    {
        add => AddHandler(ItemSelectedEvent, value);
        remove => RemoveHandler(ItemSelectedEvent, value);
    }

    private static readonly Brush ItemNormalBg = Freeze(new LinearGradientBrush(
        Color.FromRgb(0x27, 0x1D, 0x15), Color.FromRgb(0x1E, 0x17, 0x10), new Point(0, 0), new Point(1, 0)));
    private static readonly Brush ItemHoverBg = Freeze(new LinearGradientBrush(
        Color.FromRgb(0x3B, 0x2A, 0x1C), Color.FromRgb(0x2A, 0x1E, 0x14), new Point(0, 0), new Point(1, 0)));
    private static readonly Brush ItemSelectedBg = Freeze(new LinearGradientBrush(
        Color.FromRgb(0x52, 0x36, 0x20), Color.FromRgb(0x32, 0x22, 0x15), new Point(0, 0), new Point(1, 0)));
    private static readonly Brush SubNormalBg = Freeze(new SolidColorBrush(Color.FromRgb(0x1B, 0x14, 0x0F)));
    private static readonly Brush SubHoverBg = Freeze(new SolidColorBrush(Color.FromRgb(0x2B, 0x1F, 0x16)));
    private static readonly Brush SubSelectedBg = Freeze(new SolidColorBrush(Color.FromRgb(0x3A, 0x28, 0x1A)));

    private static readonly Brush BorderBronze = Freeze(new SolidColorBrush(Color.FromRgb(0x4A, 0x38, 0x24)));
    private static readonly Brush BorderGoldActive = Freeze(new SolidColorBrush(Color.FromRgb(0xC4, 0xA0, 0x62)));
    private static readonly Brush GoldAccent = Freeze(new SolidColorBrush(Color.FromRgb(0xD6, 0xB4, 0x75)));
    private static readonly Brush TextInk = Freeze(new SolidColorBrush(Color.FromRgb(0xF3, 0xE6, 0xCC)));
    private static readonly Brush TextMuted = Freeze(new SolidColorBrush(Color.FromRgb(0xB9, 0xAA, 0x8C)));
    private static readonly Brush TextFaint = Freeze(new SolidColorBrush(Color.FromRgb(0x8F, 0x7F, 0x62)));
    private static readonly Brush BadgeBg = Freeze(new SolidColorBrush(Color.FromRgb(0x8E, 0x28, 0x18)));
    private static readonly Brush BadgeFg = Freeze(new SolidColorBrush(Color.FromRgb(0xFA, 0xEF, 0xD4)));

    private static readonly Typeface TitleTypeface = new(
        new FontFamily("Noto Serif, Georgia, SimSun, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private static readonly Typeface SubtitleTypeface = new(
        new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private static readonly Typeface BadgeTypeface = new(
        new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    private const double TabHeight = 48.0;
    private const double TabSpacing = 6.0;
    private const double SubHeight = 36.0;
    private const double SubSpacing = 3.0;
    private const double LeftCurveWidth = 6.0;

    private (int ItemIdx, int SubIdx) _hovered = (-1, -1);

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

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GameCommandRibbon ribbon)
        {
            ribbon.InvalidateMeasure();
            ribbon.InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size constraint)
    {
        if (ItemsSource is null || ItemsSource.Count == 0)
            return new Size(Width, 0);

        double totalHeight = 4;
        for (int i = 0; i < ItemsSource.Count; i++)
        {
            var item = ItemsSource[i];
            totalHeight += TabHeight + TabSpacing;
            if (IsGroupSelected(item) && item.SubItems.Count > 0)
            {
                totalHeight += item.SubItems.Count * (SubHeight + SubSpacing) + 2;
            }
        }
        return new Size(Width, totalHeight + 4);
    }

    private bool IsGroupSelected(CommandRibbonItem item) =>
        string.Equals(item.Id, SelectedItemId, StringComparison.OrdinalIgnoreCase);

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var next = HitTest(e.GetPosition(this).Y);
        if (next != _hovered)
        {
            _hovered = next;
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = (-1, -1);
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var (itemIdx, subIdx) = HitTest(e.GetPosition(this).Y);
        if (ItemsSource is null || itemIdx < 0 || itemIdx >= ItemsSource.Count)
            return;

        var item = ItemsSource[itemIdx];
        SelectedItemId = item.Id;
        CommandRibbonSubItem? sub = null;
        if (subIdx >= 0 && subIdx < item.SubItems.Count)
        {
            sub = item.SubItems[subIdx];
            SelectedSubItemId = sub.Id;
        }
        else if (item.SubItems.Count > 0)
        {
            sub = item.SubItems.FirstOrDefault(s => string.Equals(s.Id, SelectedSubItemId, StringComparison.OrdinalIgnoreCase))
                ?? item.SubItems[0];
            SelectedSubItemId = sub.Id;
        }

        RaiseEvent(new CommandRibbonItemSelectedEventArgs(ItemSelectedEvent, item, sub));
        InvalidateMeasure();
        InvalidateVisual();
    }

    private (int ItemIdx, int SubIdx) HitTest(double y)
    {
        if (y < 4 || ItemsSource is null || ItemsSource.Count == 0) return (-1, -1);
        double currentY = 4;
        for (int i = 0; i < ItemsSource.Count; i++)
        {
            var item = ItemsSource[i];
            if (y >= currentY && y <= currentY + TabHeight)
                return (i, -1);
            currentY += TabHeight + TabSpacing;

            if (IsGroupSelected(item) && item.SubItems.Count > 0)
            {
                for (int j = 0; j < item.SubItems.Count; j++)
                {
                    if (y >= currentY && y <= currentY + SubHeight)
                        return (i, j);
                    currentY += SubHeight + SubSpacing;
                }
                currentY += 2;
            }
        }
        return (-1, -1);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ItemsSource is null || ItemsSource.Count == 0) return;

        double width = ActualWidth > 0 ? ActualWidth : Width;
        double currentY = 4;
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        for (int i = 0; i < ItemsSource.Count; i++)
        {
            var item = ItemsSource[i];
            bool isSelected = IsGroupSelected(item);
            bool isHovered = (_hovered.ItemIdx == i && _hovered.SubIdx == -1);

            DrawTab(dc, new Rect(0, currentY, width, TabHeight), item, isSelected, isHovered, dpi);
            currentY += TabHeight + TabSpacing;

            if (isSelected && item.SubItems.Count > 0)
            {
                for (int j = 0; j < item.SubItems.Count; j++)
                {
                    var sub = item.SubItems[j];
                    bool subSelected = string.Equals(sub.Id, SelectedSubItemId, StringComparison.OrdinalIgnoreCase);
                    bool subHovered = (_hovered.ItemIdx == i && _hovered.SubIdx == j);
                    DrawSubItem(dc, new Rect(10, currentY, Math.Max(40, width - 12), SubHeight), sub, subSelected, subHovered, dpi);
                    currentY += SubHeight + SubSpacing;
                }
                currentY += 2;
            }
        }
    }

    private void DrawTab(DrawingContext dc, Rect rect, CommandRibbonItem item, bool isSelected, bool isHovered, double dpi)
    {
        Brush bg = isSelected ? ItemSelectedBg : (isHovered ? ItemHoverBg : ItemNormalBg);
        Pen borderPen = new(isSelected ? BorderGoldActive : (isHovered ? GoldAccent : BorderBronze), isSelected ? 1.4 : 1.0);

        StreamGeometry tabGeom = new();
        using (StreamGeometryContext ctx = tabGeom.Open())
        {
            const double r = 4.0;
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

        if (isSelected)
        {
            dc.DrawRoundedRectangle(BorderGoldActive, null, new Rect(rect.Left + 2, rect.Top + 8, 3, rect.Height - 16), 1.5, 1.5);
        }

        double textStartX = rect.Left + 15;
        if (!string.IsNullOrEmpty(item.IconPathData))
        {
            try
            {
                Geometry iconGeom = Geometry.Parse(item.IconPathData);
                Rect bounds = iconGeom.Bounds;
                const double iconBoxSize = 18.0;
                double iconBoxX = rect.Left + 14.0;
                double iconBoxY = rect.Top + (TabHeight - iconBoxSize) / 2.0;

                if (bounds.Width > 0 && bounds.Height > 0)
                {
                    double scale = Math.Min(iconBoxSize / bounds.Width, iconBoxSize / bounds.Height);
                    double offsetX = iconBoxX + (iconBoxSize - bounds.Width * scale) / 2.0 - bounds.X * scale;
                    double offsetY = iconBoxY + (iconBoxSize - bounds.Height * scale) / 2.0 - bounds.Y * scale;

                    Matrix m = Matrix.Identity;
                    m.Scale(scale, scale);
                    m.Translate(offsetX, offsetY);

                    dc.PushTransform(new MatrixTransform(m));
                    dc.DrawGeometry(null, new Pen(isSelected ? BorderGoldActive : (isHovered ? GoldAccent : TextMuted), 1.3), iconGeom);
                    dc.Pop();
                }
                else
                {
                    dc.PushTransform(new TranslateTransform(iconBoxX, iconBoxY));
                    dc.DrawGeometry(null, new Pen(isSelected ? BorderGoldActive : (isHovered ? GoldAccent : TextMuted), 1.3), iconGeom);
                    dc.Pop();
                }

                // Generous 10px spacing between icon bounding box and text
                textStartX = iconBoxX + iconBoxSize + 10.0;
            }
            catch { }
        }

        FormattedText titleFt = new(
            item.Title, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            TitleTypeface, 12.5, isSelected || isHovered ? TextInk : TextMuted, dpi);
        double titleY = string.IsNullOrEmpty(item.Subtitle) ? rect.Top + (TabHeight - titleFt.Height) / 2 : rect.Top + 7;
        dc.DrawText(titleFt, new Point(textStartX, titleY));

        if (!string.IsNullOrEmpty(item.Subtitle))
        {
            FormattedText subFt = new(
                item.Subtitle, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                SubtitleTypeface, 10.0, TextFaint, dpi);
            dc.DrawText(subFt, new Point(textStartX, rect.Top + 25));
        }

        DrawBadge(dc, rect, item.BadgeText, dpi);
    }

    private void DrawSubItem(DrawingContext dc, Rect rect, CommandRibbonSubItem sub, bool isSelected, bool isHovered, double dpi)
    {
        Brush bg = isSelected ? SubSelectedBg : (isHovered ? SubHoverBg : SubNormalBg);
        Pen borderPen = new(isSelected ? BorderGoldActive : (isHovered ? GoldAccent : BorderBronze), isSelected ? 1.2 : 0.8);
        dc.DrawRoundedRectangle(bg, borderPen, rect, 4, 4);

        if (isSelected)
        {
            dc.DrawRoundedRectangle(BorderGoldActive, null, new Rect(rect.Left + 3, rect.Top + 6, 2.5, rect.Height - 12), 1.2, 1.2);
        }

        double textX = rect.Left + 11;
        FormattedText titleFt = new(
            sub.Title, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            TitleTypeface, 11.5, isSelected || isHovered ? TextInk : TextMuted, dpi);
        double titleY = string.IsNullOrEmpty(sub.Subtitle) ? rect.Top + (SubHeight - titleFt.Height) / 2 : rect.Top + 4;
        dc.DrawText(titleFt, new Point(textX, titleY));

        if (!string.IsNullOrEmpty(sub.Subtitle))
        {
            FormattedText subFt = new(
                sub.Subtitle, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                SubtitleTypeface, 9.5, TextFaint, dpi);
            dc.DrawText(subFt, new Point(textX, rect.Top + 19));
        }

        DrawBadge(dc, rect, sub.BadgeText, dpi);
    }

    private static void DrawBadge(DrawingContext dc, Rect rect, string? badgeText, double dpi)
    {
        if (string.IsNullOrEmpty(badgeText)) return;
        FormattedText bFt = new(
            badgeText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            BadgeTypeface, 9.5, BadgeFg, dpi);
        double bWidth = Math.Max(16, bFt.Width + 8);
        const double bHeight = 15;
        double bx = rect.Right - bWidth - 7;
        double by = rect.Top + (rect.Height - bHeight) / 2;
        dc.DrawRoundedRectangle(BadgeBg, null, new Rect(bx, by, bWidth, bHeight), 4, 4);
        dc.DrawText(bFt, new Point(bx + (bWidth - bFt.Width) / 2, by + 1));
    }

    private static Brush Freeze(Brush brush)
    {
        if (brush.CanFreeze) brush.Freeze();
        return brush;
    }
}
