using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Standardized visual header banner for all SimT information hubs.
/// Guarantees consistent typography, glyph elevation, and badge accents across modules.
/// </summary>
public sealed class GameHubHeader : ContentControl
{
    private readonly Border _root;
    private readonly Border _iconBox;
    private readonly TextBlock _glyphBlock;
    private readonly Border _categoryBadge;
    private readonly TextBlock _categoryBlock;
    private readonly TextBlock _subtitleBlock;
    private readonly TextBlock _titleBlock;
    private readonly Border _rightBadge;
    private readonly TextBlock _rightBadgeTitle;
    private readonly TextBlock _rightBadgeSub;

    public GameHubHeader()
    {
        _glyphBlock = new TextBlock
        {
            FontSize = 30,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        _iconBox = new Border
        {
            Width = 60,
            Height = 60,
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(2),
            Margin = new Thickness(0, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = _glyphBlock
        };

        _categoryBlock = new TextBlock
        {
            FontSize = 10,
            FontWeight = FontWeights.Bold
        };

        _categoryBadge = new Border
        {
            CornerRadius = new CornerRadius(3),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 1, 6, 1),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = _categoryBlock
        };

        _subtitleBlock = new TextBlock
        {
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };

        _titleBlock = new TextBlock
        {
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        _rightBadgeTitle = new TextBlock
        {
            FontSize = 11.5,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        _rightBadgeSub = new TextBlock
        {
            FontSize = 10.5,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 2, 0, 0)
        };

        var rightStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        rightStack.Children.Add(_rightBadgeTitle);
        rightStack.Children.Add(_rightBadgeSub);

        _rightBadge = new Border
        {
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 5, 10, 5),
            VerticalAlignment = VerticalAlignment.Center,
            Child = rightStack
        };

        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 3) };
        titleRow.Children.Add(_categoryBadge);
        titleRow.Children.Add(_subtitleBlock);

        var centerStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        centerStack.Children.Add(titleRow);
        centerStack.Children.Add(_titleBlock);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(_iconBox, 0);
        Grid.SetColumn(centerStack, 1);
        Grid.SetColumn(_rightBadge, 2);

        grid.Children.Add(_iconBox);
        grid.Children.Add(centerStack);
        grid.Children.Add(_rightBadge);

        _root = new Border
        {
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 12),
            Child = grid
        };

        Content = _root;
        Loaded += (_, _) => ApplyTheme();
    }

    private void ApplyTheme()
    {
        _root.Background = TryFindResource("SurfaceAlt") as Brush ?? Brushes.DarkSlateGray;
        _root.BorderBrush = TryFindResource("Line") as Brush ?? Brushes.Gray;
        _iconBox.Background = TryFindResource("Surface") as Brush ?? Brushes.Black;
        _titleBlock.Foreground = TryFindResource("Text") as Brush ?? Brushes.White;
        _rightBadge.Background = TryFindResource("Surface") as Brush ?? Brushes.Black;
        _rightBadge.BorderBrush = TryFindResource("Line") as Brush ?? Brushes.Gray;
        _rightBadgeSub.Foreground = TryFindResource("Muted") as Brush ?? Brushes.LightGray;
    }

    public void Configure(
        string glyph,
        string category,
        string title,
        string subtitle,
        string rightBadgeTitle,
        string rightBadgeSub,
        Color accentColor)
    {
        _glyphBlock.Text = glyph;
        _categoryBlock.Text = category.ToUpperInvariant();
        _categoryBlock.Foreground = new SolidColorBrush(accentColor);
        _categoryBadge.Background = new SolidColorBrush(Color.FromArgb(0x28, accentColor.R, accentColor.G, accentColor.B));
        _categoryBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, accentColor.R, accentColor.G, accentColor.B));

        _iconBox.BorderBrush = new SolidColorBrush(accentColor);

        _titleBlock.Text = title;
        _subtitleBlock.Text = subtitle;
        _subtitleBlock.Foreground = new SolidColorBrush(accentColor);

        _rightBadgeTitle.Text = rightBadgeTitle;
        _rightBadgeTitle.Foreground = new SolidColorBrush(accentColor);
        _rightBadgeSub.Text = rightBadgeSub;
    }
}
