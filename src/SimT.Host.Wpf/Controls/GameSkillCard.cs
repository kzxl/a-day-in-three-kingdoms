using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SimT.Contracts.Playing;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Dedicated reusable RPG skill card control for Tam Quốc feats and martial arts.
/// Features tier stars, categorized badges, effect banners, and interactive hover response.
/// </summary>
public sealed class GameSkillCard : Border
{
    private readonly TextBlock _titleText;
    private readonly TextBlock _starText;
    private readonly TextBlock _catText;
    private readonly Border _catBorder;
    private readonly TextBlock _descText;
    private readonly TextBlock _effectText;

    public GameSkillCard()
    {
        Background = (Brush)Application.Current.FindResource("SurfaceAlt");
        BorderBrush = (Brush)Application.Current.FindResource("Line");
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(6);
        Padding = new Thickness(12, 10, 12, 10);
        Margin = new Thickness(0, 0, 0, 8);

        var stack = new StackPanel();
        var headerDock = new DockPanel();

        var badgeStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(badgeStack, Dock.Right);

        _starText = new TextBlock
        {
            Foreground = (Brush)Application.Current.FindResource("Gain"),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        badgeStack.Children.Add(_starText);

        _catBorder = new Border
        {
            Background = (Brush)Application.Current.FindResource("Surface"),
            BorderBrush = (Brush)Application.Current.FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2)
        };
        _catText = new TextBlock
        {
            FontSize = 10,
            FontWeight = FontWeights.Bold
        };
        _catBorder.Child = _catText;
        badgeStack.Children.Add(_catBorder);
        headerDock.Children.Add(badgeStack);

        _titleText = new TextBlock
        {
            FontSize = 13.5,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)Application.Current.FindResource("Text")
        };
        headerDock.Children.Add(_titleText);
        stack.Children.Add(headerDock);

        _descText = new TextBlock
        {
            Style = (Style)Application.Current.FindResource("Caption"),
            Foreground = (Brush)Application.Current.FindResource("Muted"),
            Margin = new Thickness(0, 4, 0, 2),
            TextWrapping = TextWrapping.Wrap
        };
        stack.Children.Add(_descText);

        var effectBorder = new Border
        {
            Background = (Brush)Application.Current.FindResource("AccentSoft"),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 2, 6, 2),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 0)
        };
        _effectText = new TextBlock
        {
            FontSize = 10.5,
            FontWeight = FontWeights.Medium,
            Foreground = (Brush)Application.Current.FindResource("Accent")
        };
        effectBorder.Child = _effectText;
        stack.Children.Add(effectBorder);

        Child = stack;

        MouseEnter += (_, _) => BorderBrush = (Brush)Application.Current.FindResource("LineSoft") ?? Brushes.SlateGray;
        MouseLeave += (_, _) => BorderBrush = (Brush)Application.Current.FindResource("Line");
    }

    public GameSkillCard(NpcSkillDef skill) : this()
    {
        Configure(skill);
    }

    public void Configure(NpcSkillDef skill)
    {
        _titleText.Text = skill.NameVi;
        _starText.Text = new string('★', Math.Clamp(skill.Tier, 1, 5));
        _descText.Text = skill.Description;
        _effectText.Text = $"⚡ Hiệu Quả: {skill.EffectSummary}";

        var (catName, catBrush) = skill.Category switch
        {
            "martial" => ("Võ Đạo", (Brush)new SolidColorBrush(Color.FromRgb(0xE0, 0x6C, 0x75))),
            "strategist" => ("Mưu Lược", (Brush)new SolidColorBrush(Color.FromRgb(0x4D, 0x96, 0xFF))),
            "governance" => ("Trị Quốc", (Brush)new SolidColorBrush(Color.FromRgb(0x5A, 0xC0, 0x8A))),
            "commerce" => ("Thương Mại", (Brush)Application.Current.FindResource("Accent")),
            _ => ("Kỳ Tài", (Brush)new SolidColorBrush(Color.FromRgb(0xBD, 0x93, 0xF9)))
        };

        _catText.Text = catName;
        _catText.Foreground = catBrush;
    }
}
