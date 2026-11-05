using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Multi-line rich tooltip styled in the Lacquer &amp; Silk theme with a Noto Serif title,
/// optional category/rarity subtitle, key-value stat lines, and lore description.
/// </summary>
public sealed class GameRichTooltip : ToolTip
{
    private static readonly Brush SurfaceBg = Freeze(new SolidColorBrush(Color.FromRgb(0x1D, 0x16, 0x11)));
    private static readonly Brush FrameBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x6E, 0x56, 0x34)));
    private static readonly Brush DividerBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x3A, 0x2C, 0x20)));
    private static readonly Brush TitleFg = Freeze(new SolidColorBrush(Color.FromRgb(0xF3, 0xE6, 0xCC)));
    private static readonly Brush AccentFg = Freeze(new SolidColorBrush(Color.FromRgb(0xC4, 0xA0, 0x62)));
    private static readonly Brush BodyFg = Freeze(new SolidColorBrush(Color.FromRgb(0xEA, 0xDF, 0xC6)));
    private static readonly Brush MutedFg = Freeze(new SolidColorBrush(Color.FromRgb(0xB9, 0xAA, 0x8C)));
    private static readonly FontFamily SerifFont = new("Noto Serif, Georgia, Segoe UI");
    private static readonly FontFamily MonoFont = new("Consolas");

    public GameRichTooltip(
        string title,
        string? subtitle = null,
        IReadOnlyList<(string Label, string Value)>? stats = null,
        string? description = null)
    {
        Background = Brushes.Transparent;
        BorderThickness = new Thickness(0);
        Padding = new Thickness(0);
        HasDropShadow = true;

        var card = new Border
        {
            Background = SurfaceBg,
            BorderBrush = FrameBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(12, 10, 12, 10),
            MaxWidth = 340,
            SnapsToDevicePixels = true
        };

        var stack = new StackPanel();

        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontFamily = SerifFont,
            FontSize = 13.5,
            FontWeight = FontWeights.Bold,
            Foreground = TitleFg,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            stack.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = AccentFg,
                Margin = new Thickness(0, 2, 0, 0)
            });
        }

        if (stats is { Count: > 0 })
        {
            stack.Children.Add(new Border
            {
                Height = 1,
                Background = DividerBrush,
                Margin = new Thickness(0, 7, 0, 6)
            });

            foreach (var (label, val) in stats)
            {
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 3) };
                var valBlock = new TextBlock
                {
                    Text = val,
                    FontFamily = MonoFont,
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = BodyFg,
                    TextAlignment = TextAlignment.Right
                };
                DockPanel.SetDock(valBlock, Dock.Right);
                row.Children.Add(valBlock);
                row.Children.Add(new TextBlock
                {
                    Text = label,
                    FontSize = 11.5,
                    Foreground = MutedFg,
                    Margin = new Thickness(0, 0, 12, 0)
                });
                stack.Children.Add(row);
            }
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            stack.Children.Add(new Border
            {
                Height = 1,
                Background = DividerBrush,
                Margin = new Thickness(0, 6, 0, 6)
            });

            stack.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = 11.5,
                Foreground = MutedFg,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 16
            });
        }

        card.Child = stack;
        Content = card;
    }

    private static Brush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
