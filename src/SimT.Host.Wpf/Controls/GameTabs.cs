using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SimT.Host.Wpf.Controls;

public sealed record GameTabItem(string Key, string Title, int? Count = null);

/// <summary>
/// Plaque-style tab strip with a warm bronze underline on the active tab.
/// Replaces scattered filter buttons and standard TabControl headers.
/// </summary>
public sealed class GameTabs : Border
{
    private static readonly Brush BarBorderBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x3A, 0x2C, 0x20)));
    private static readonly Brush ActiveBg = Freeze(new SolidColorBrush(Color.FromRgb(0x24, 0x1A, 0x13)));
    private static readonly Brush HoverBg = Freeze(new SolidColorBrush(Color.FromRgb(0x1F, 0x17, 0x11)));
    private static readonly Brush BronzeLine = Freeze(new SolidColorBrush(Color.FromRgb(0xC4, 0xA0, 0x62)));
    private static readonly Brush ActiveFg = Freeze(new SolidColorBrush(Color.FromRgb(0xF3, 0xE6, 0xCC)));
    private static readonly Brush InactiveFg = Freeze(new SolidColorBrush(Color.FromRgb(0xB9, 0xAA, 0x8C)));
    private static readonly Brush BadgeBg = Freeze(new SolidColorBrush(Color.FromRgb(0x33, 0x26, 0x19)));

    private readonly WrapPanel _strip;
    private readonly List<GameTabItem> _items = [];
    private int _selectedIndex;

    public event Action<int, GameTabItem>? SelectionChanged;

    public GameTabs()
    {
        BorderBrush = BarBorderBrush;
        BorderThickness = new Thickness(0, 0, 0, 1);
        Margin = new Thickness(0, 0, 0, 12);

        _strip = new WrapPanel { Orientation = Orientation.Horizontal };
        Child = _strip;
    }

    public GameTabs(IEnumerable<GameTabItem> items, int selectedIndex = 0) : this()
    {
        SetTabs(items, selectedIndex);
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set => SelectIndex(value, notify: true);
    }

    public GameTabItem? SelectedTab =>
        _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null;

    public void SetTabs(IEnumerable<GameTabItem> items, int selectedIndex = 0)
    {
        _items.Clear();
        _items.AddRange(items);
        _selectedIndex = Math.Clamp(selectedIndex, 0, Math.Max(0, _items.Count - 1));
        RebuildVisuals();
    }

    public void SelectIndex(int index, bool notify = true)
    {
        if (index < 0 || index >= _items.Count) return;
        _selectedIndex = index;
        RebuildVisuals();
        if (notify)
        {
            SelectionChanged?.Invoke(_selectedIndex, _items[_selectedIndex]);
        }
    }

    private void RebuildVisuals()
    {
        _strip.Children.Clear();
        for (var i = 0; i < _items.Count; i++)
        {
            var idx = i;
            var item = _items[i];
            var isSelected = i == _selectedIndex;

            var tabBorder = new Border
            {
                Background = isSelected ? ActiveBg : Brushes.Transparent,
                BorderBrush = isSelected ? BronzeLine : Brushes.Transparent,
                BorderThickness = new Thickness(0, 0, 0, 2),
                CornerRadius = new CornerRadius(4, 4, 0, 0),
                Padding = new Thickness(14, 8, 14, 8),
                Margin = new Thickness(0, 0, 4, -1),
                MinHeight = 36,
                Cursor = Cursors.Hand
            };

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            var titleBlock = new TextBlock
            {
                Text = item.Title,
                FontSize = 12.5,
                FontWeight = isSelected ? FontWeights.Bold : FontWeights.SemiBold,
                Foreground = isSelected ? ActiveFg : InactiveFg,
                VerticalAlignment = VerticalAlignment.Center
            };
            row.Children.Add(titleBlock);

            if (item.Count.HasValue)
            {
                var badge = new Border
                {
                    Background = BadgeBg,
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(6, 1, 6, 1),
                    Margin = new Thickness(7, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = item.Count.Value.ToString(),
                        FontSize = 10,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = BronzeLine
                    }
                };
                row.Children.Add(badge);
            }

            tabBorder.Child = row;

            tabBorder.MouseEnter += (_, _) =>
            {
                if (idx != _selectedIndex)
                {
                    tabBorder.Background = HoverBg;
                    titleBlock.Foreground = ActiveFg;
                }
            };
            tabBorder.MouseLeave += (_, _) =>
            {
                if (idx != _selectedIndex)
                {
                    tabBorder.Background = Brushes.Transparent;
                    titleBlock.Foreground = InactiveFg;
                }
            };
            tabBorder.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                SelectIndex(idx, notify: true);
            };

            _strip.Children.Add(tabBorder);
        }
    }

    private static Brush Freeze(SolidColorBrush b)
    {
        b.Freeze();
        return b;
    }
}
