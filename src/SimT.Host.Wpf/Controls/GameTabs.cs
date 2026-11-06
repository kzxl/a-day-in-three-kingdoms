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
        var newItems = items as IList<GameTabItem> ?? items.ToList();

        if (AreTabsEquivalent(_items, newItems))
        {
            var clamped = Math.Clamp(selectedIndex, 0, Math.Max(0, _items.Count - 1));
            if (_selectedIndex != clamped)
            {
                _selectedIndex = clamped;
                UpdateVisualStyles();
            }
            return;
        }

        _items.Clear();
        _items.AddRange(newItems);
        _selectedIndex = Math.Clamp(selectedIndex, 0, Math.Max(0, _items.Count - 1));
        RebuildVisuals();
    }

    public void SelectIndex(int index, bool notify = true)
    {
        if (index < 0 || index >= _items.Count) return;
        if (_selectedIndex == index) return;
        _selectedIndex = index;
        UpdateVisualStyles();
        if (notify)
        {
            SelectionChanged?.Invoke(_selectedIndex, _items[_selectedIndex]);
        }
    }

    private static bool AreTabsEquivalent(List<GameTabItem> current, IList<GameTabItem> next)
    {
        if (current.Count != next.Count) return false;
        for (var i = 0; i < current.Count; i++)
        {
            if (current[i].Key != next[i].Key ||
                current[i].Title != next[i].Title ||
                current[i].Count != next[i].Count)
            {
                return false;
            }
        }
        return true;
    }

    private void UpdateVisualStyles()
    {
        for (var i = 0; i < _strip.Children.Count; i++)
        {
            if (_strip.Children[i] is not Border tabBorder) continue;
            var isSelected = (i == _selectedIndex);
            tabBorder.Background = isSelected ? ActiveBg : Brushes.Transparent;
            tabBorder.BorderBrush = isSelected ? BronzeLine : Brushes.Transparent;

            if (tabBorder.Child is StackPanel row)
            {
                var titleBlock = row.Children.OfType<TextBlock>().FirstOrDefault(tb => (string?)tb.Tag == "title")
                                 ?? row.Children.OfType<TextBlock>().FirstOrDefault();
                if (titleBlock != null)
                {
                    titleBlock.FontWeight = isSelected ? FontWeights.Bold : FontWeights.SemiBold;
                    titleBlock.Foreground = isSelected ? ActiveFg : InactiveFg;
                }
            }
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

            var (icon, label) = ParseIconAndTitle(item.Title);

            if (!string.IsNullOrEmpty(icon))
            {
                var iconBlock = new TextBlock
                {
                    Text = icon,
                    FontSize = 13.0,
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                row.Children.Add(iconBlock);
            }

            var titleBlock = new TextBlock
            {
                Tag = "title",
                Text = label,
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

    private static (string? Icon, string Label) ParseIconAndTitle(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return (null, string.Empty);
        var trimmed = raw.Trim();
        var firstSpace = trimmed.IndexOf(' ');
        if (firstSpace > 0 && firstSpace <= 5)
        {
            var potentialIcon = trimmed[..firstSpace].Trim();
            if (potentialIcon.Any(c => char.IsSurrogate(c) || char.GetUnicodeCategory(c) is System.Globalization.UnicodeCategory.OtherSymbol or System.Globalization.UnicodeCategory.ModifierSymbol))
            {
                return (potentialIcon, trimmed[(firstSpace + 1)..].Trim());
            }
        }
        return (null, trimmed);
    }

    private static Brush Freeze(SolidColorBrush b)
    {
        b.Freeze();
        return b;
    }
}
