using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Reusable enterprise section container card.
/// Standardizes card surfaces with 1px borders, header layouts with icon and title,
/// optional right-aligned action/count slots, and encapsulated body content.
/// </summary>
public sealed class GameSectionCard : Border
{
    private static readonly Brush CardBg = Freeze(new SolidColorBrush(Color.FromRgb(0x1D, 0x16, 0x11)));
    private static readonly Brush CardBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x3A, 0x2C, 0x20)));
    private static readonly Brush HeadingColor = Freeze(new SolidColorBrush(Color.FromRgb(0xC4, 0xA0, 0x62)));
    private static readonly Brush TitleColor = Freeze(new SolidColorBrush(Color.FromRgb(0xF3, 0xE6, 0xCC)));
    private static readonly Brush SubtitleColor = Freeze(new SolidColorBrush(Color.FromRgb(0xB9, 0xAA, 0x8C)));

    private readonly DockPanel _headerPanel;
    private readonly StackPanel _titleStack;
    private readonly TextBlock _iconBlock;
    private readonly TextBlock _headingBlock;
    private readonly TextBlock _subtitleBlock;
    private readonly ContentControl _rightSlot;
    private readonly ContentControl _bodySlot;

    public GameSectionCard()
    {
        Background = CardBg;
        BorderBrush = CardBorder;
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(8);
        Padding = new Thickness(14, 12, 14, 12);
        Margin = new Thickness(0, 0, 0, 14);

        var root = new StackPanel();

        _headerPanel = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };

        _rightSlot = new ContentControl
        {
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(_rightSlot, Dock.Right);
        _headerPanel.Children.Add(_rightSlot);

        var leftStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        _iconBlock = new TextBlock
        {
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            Visibility = Visibility.Collapsed
        };
        leftStack.Children.Add(_iconBlock);

        _titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

        _headingBlock = new TextBlock
        {
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = HeadingColor,
            VerticalAlignment = VerticalAlignment.Center
        };
        _titleStack.Children.Add(_headingBlock);

        _subtitleBlock = new TextBlock
        {
            FontSize = 11,
            Foreground = SubtitleColor,
            Margin = new Thickness(0, 2, 0, 0),
            Visibility = Visibility.Collapsed
        };
        _titleStack.Children.Add(_subtitleBlock);

        leftStack.Children.Add(_titleStack);
        _headerPanel.Children.Add(leftStack);

        root.Children.Add(_headerPanel);

        _bodySlot = new ContentControl
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch
        };
        root.Children.Add(_bodySlot);

        Child = root;
    }

    public GameSectionCard(string heading, UIElement bodyContent, string? icon = null, UIElement? rightContent = null) : this()
    {
        Heading = heading;
        if (!string.IsNullOrWhiteSpace(icon))
        {
            Icon = icon;
        }
        if (rightContent is not null)
        {
            HeaderRight = rightContent;
        }
        CardBody = bodyContent;
    }

    public string Heading
    {
        get => _headingBlock.Text;
        set => _headingBlock.Text = value.ToUpperInvariant();
    }

    public string Icon
    {
        get => _iconBlock.Text;
        set
        {
            _iconBlock.Text = value;
            _iconBlock.Visibility = string.IsNullOrWhiteSpace(value) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    public string? Subtitle
    {
        get => _subtitleBlock.Text;
        set
        {
            _subtitleBlock.Text = value ?? string.Empty;
            _subtitleBlock.Visibility = string.IsNullOrWhiteSpace(value) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    public UIElement? HeaderRight
    {
        get => _rightSlot.Content as UIElement;
        set => _rightSlot.Content = value;
    }

    public UIElement? CardBody
    {
        get => _bodySlot.Content as UIElement;
        set => _bodySlot.Content = value;
    }

    public void SetCountBadge(int count, string unit = "mục")
    {
        HeaderRight = new GameBadge($"{count} {unit}", BadgeVariant.Default);
    }

    private static Brush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
