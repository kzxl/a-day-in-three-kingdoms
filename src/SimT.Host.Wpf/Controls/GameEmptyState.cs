using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Reusable Empty State control fulfilling R_UX_MATRIX (4-state matrix).
/// Distinguishes between zero data vs search mismatch, paired with clear explanation
/// and an optional call-to-action (CTA) trigger.
/// </summary>
public sealed class GameEmptyState : Border
{
    private static readonly Brush DefaultBg = Freeze(new SolidColorBrush(Color.FromRgb(0x17, 0x1E, 0x28)));
    private static readonly Brush DefaultBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x26, 0x30, 0x3D)));
    private static readonly Brush TextColor = Freeze(new SolidColorBrush(Color.FromRgb(0xE8, 0xED, 0xF5)));
    private static readonly Brush MutedColor = Freeze(new SolidColorBrush(Color.FromRgb(0x8A, 0x97, 0xA8)));

    public event RoutedEventHandler? ActionClick;

    private readonly TextBlock _iconBlock;
    private readonly TextBlock _titleBlock;
    private readonly TextBlock _descBlock;
    private readonly Button _actionButton;

    public GameEmptyState()
    {
        Background = DefaultBg;
        BorderBrush = DefaultBorder;
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(8);
        Padding = new Thickness(24, 32, 24, 32);
        Margin = new Thickness(0, 0, 0, 14);
        HorizontalAlignment = HorizontalAlignment.Stretch;

        var stack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        _iconBlock = new TextBlock
        {
            Text = "📜",
            FontSize = 32,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        };

        _titleBlock = new TextBlock
        {
            Text = "Chưa Có Dữ Liệu",
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = TextColor,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 6)
        };

        _descBlock = new TextBlock
        {
            Text = "Hiện tại chưa có thông tin hoặc đối tượng nào được ghi nhận tại phân khu này.",
            FontSize = 12.5,
            Foreground = MutedColor,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            MaxWidth = 420,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        _actionButton = new Button
        {
            Content = "Thao tác",
            Margin = new Thickness(0, 14, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        _actionButton.Click += (s, e) => ActionClick?.Invoke(this, e);

        stack.Children.Add(_iconBlock);
        stack.Children.Add(_titleBlock);
        stack.Children.Add(_descBlock);
        stack.Children.Add(_actionButton);

        Child = stack;
    }

    public GameEmptyState(string title, string description, string icon = "📜", string? actionText = null) : this()
    {
        Configure(title, description, icon, actionText);
    }

    public void Configure(string title, string description, string icon = "📜", string? actionText = null)
    {
        _titleBlock.Text = title;
        _descBlock.Text = description;
        _iconBlock.Text = icon;

        if (!string.IsNullOrWhiteSpace(actionText))
        {
            _actionButton.Content = actionText;
            _actionButton.Visibility = Visibility.Visible;
        }
        else
        {
            _actionButton.Visibility = Visibility.Collapsed;
        }
    }

    public static GameEmptyState ForSearch(string filterText, RoutedEventHandler? onClear = null)
    {
        var state = new GameEmptyState(
            "Không Tìm Thấy Kết Quả",
            $"Không có mục nào khớp với từ khóa tìm kiếm \"{filterText}\". Vui lòng kiểm tra lại bộ lọc.",
            "🔍",
            onClear is not null ? "Xóa bộ lọc" : null);

        if (onClear is not null)
        {
            state.ActionClick += onClear;
        }

        return state;
    }

    private static Brush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
