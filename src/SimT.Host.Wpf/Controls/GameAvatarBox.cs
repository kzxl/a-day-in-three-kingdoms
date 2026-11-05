using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SimT.Contracts.Playing;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Character avatar and rank portrait frame control.
/// Standardizes NPC and protagonist profile badges with rank tier borders (S/A/B/C),
/// localized initials, and presence indicators.
/// </summary>
public sealed class GameAvatarBox : Grid
{
    private static readonly Brush RankSBg = Freeze(new SolidColorBrush(Color.FromRgb(0x2D, 0x22, 0x0A)));
    private static readonly Brush RankSBorder = Freeze(new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)));
    private static readonly Brush RankSFg = Freeze(new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24)));

    private static readonly Brush RankABg = Freeze(new SolidColorBrush(Color.FromRgb(0x24, 0x16, 0x36)));
    private static readonly Brush RankABorder = Freeze(new SolidColorBrush(Color.FromRgb(0x8B, 0x5C, 0xF6)));
    private static readonly Brush RankAFg = Freeze(new SolidColorBrush(Color.FromRgb(0xC4, 0xB5, 0xFD)));

    private static readonly Brush RankBBg = Freeze(new SolidColorBrush(Color.FromRgb(0x10, 0x24, 0x38)));
    private static readonly Brush RankBBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x02, 0x84, 0xC7)));
    private static readonly Brush RankBFg = Freeze(new SolidColorBrush(Color.FromRgb(0x7D, 0xD3, 0xFC)));

    private static readonly Brush RankCBg = Freeze(new SolidColorBrush(Color.FromRgb(0x1A, 0x20, 0x2C)));
    private static readonly Brush RankCBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)));
    private static readonly Brush RankCFg = Freeze(new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)));

    private static readonly Brush OnlineGreen = Freeze(new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)));
    private static readonly Brush OnlineBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x0E, 0x11, 0x16)));

    private readonly Border _frame;
    private readonly TextBlock _initialsBlock;
    private readonly Border _badgeRank;
    private readonly TextBlock _badgeRankText;
    private readonly Border _presenceDot;

    public GameAvatarBox()
    {
        Width = 46;
        Height = 46;
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalAlignment = HorizontalAlignment.Center;

        _frame = new Border
        {
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1.5),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        _initialsBlock = new TextBlock
        {
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _frame.Child = _initialsBlock;

        Children.Add(_frame);

        // Rank label mini-pill at bottom right
        _badgeRank = new Border
        {
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(3, 0, 3, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, -2, -2)
        };
        _badgeRankText = new TextBlock
        {
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            Foreground = Freeze(new SolidColorBrush(Colors.White))
        };
        _badgeRank.Child = _badgeRankText;
        Children.Add(_badgeRank);

        // Presence dot at top right
        _presenceDot = new Border
        {
            Width = 9,
            Height = 9,
            CornerRadius = new CornerRadius(4.5),
            Background = OnlineGreen,
            BorderBrush = OnlineBorder,
            BorderThickness = new Thickness(1.5),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, -2, -2, 0),
            Visibility = Visibility.Collapsed
        };
        Children.Add(_presenceDot);

        SetRank(NpcRank.B);
    }

    public void Configure(string name, NpcRank rank, bool isPresent = false)
    {
        var initials = ExtractInitials(name);
        _initialsBlock.Text = initials;
        SetRank(rank);
        _presenceDot.Visibility = isPresent ? Visibility.Visible : Visibility.Collapsed;
        ToolTip = $"{name} ({RankTitle(rank)})";
    }

    public void SetRank(NpcRank rank)
    {
        var (bg, border, fg) = rank switch
        {
            NpcRank.S => (RankSBg, RankSBorder, RankSFg),
            NpcRank.A => (RankABg, RankABorder, RankAFg),
            NpcRank.B => (RankBBg, RankBBorder, RankBFg),
            _ => (RankCBg, RankCBorder, RankCFg)
        };

        _frame.Background = bg;
        _frame.BorderBrush = border;
        _initialsBlock.Foreground = fg;

        _badgeRank.Background = border;
        _badgeRankText.Text = rank.ToString();
    }

    private static string ExtractInitials(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return "?";
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1) return parts[0].Length <= 2 ? parts[0] : parts[0][..2];
        return parts[^1]; // Last name in Vietnamese (e.g. "Vũ" in "Quan Vũ", "Tháo" in "Tào Tháo")
    }

    private static string RankTitle(NpcRank rank) => rank switch
    {
        NpcRank.S => "Đẳng Cấp Tuyệt Thế (S)",
        NpcRank.A => "Đẳng Cấp Danh Tướng / Trọng Thần (A)",
        NpcRank.B => "Đẳng Cấp Lương Tướng / Cán Bộ (B)",
        _ => "Đẳng Cấp Thường Nhân / Du Hiệp (C)"
    };

    private static Brush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
