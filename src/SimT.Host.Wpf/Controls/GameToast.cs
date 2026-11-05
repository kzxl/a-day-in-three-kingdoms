using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SimT.Host.Wpf.Controls;

public enum GameToastKind
{
    Event,
    Gain,
    Loss
}

/// <summary>
/// Floating top-right toast notification host replacing the static bottom status line.
/// Supports Gain (jade), Loss (cinnabar), and Event (bronze) notifications that auto-dismiss.
/// </summary>
public sealed class GameToastHost : StackPanel
{
    private static readonly Brush CardBg = Freeze(new SolidColorBrush(Color.FromArgb(0xF4, 0x1D, 0x16, 0x11)));
    private static readonly Brush CardBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x46, 0x35, 0x26)));
    private static readonly Brush GainAccent = Freeze(new SolidColorBrush(Color.FromRgb(0x3F, 0x6B, 0x55)));
    private static readonly Brush LossAccent = Freeze(new SolidColorBrush(Color.FromRgb(0xA8, 0x32, 0x1F)));
    private static readonly Brush EventAccent = Freeze(new SolidColorBrush(Color.FromRgb(0xC4, 0xA0, 0x62)));
    private static readonly Brush TextFg = Freeze(new SolidColorBrush(Color.FromRgb(0xF3, 0xE6, 0xCC)));

    public bool SuppressTimers { get; set; }

    public GameToastHost()
    {
        Orientation = Orientation.Vertical;
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Top;
        Margin = new Thickness(0, 18, 22, 0);
        MaxWidth = 420;
        IsHitTestVisible = false;
    }

    public void ShowToast(string message, GameToastKind? explicitKind = null)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        var clean = message.Trim();
        if (clean == "Sẵn sàng.") return;

        var kind = explicitKind ?? InferKind(clean);
        var accent = kind switch
        {
            GameToastKind.Gain => GainAccent,
            GameToastKind.Loss => LossAccent,
            _ => EventAccent
        };

        var iconPath = kind switch
        {
            GameToastKind.Gain => "M4,16 L10,10 L14,14 L20,7 M15,7 L20,7 L20,12",
            GameToastKind.Loss => "M12,8 L12,13 M12,17 L12,17.2 M12,3 L21,19 L3,19 Z",
            _ => "M12,3 L14.5,9.5 L21,12 L14.5,14.5 L12,21 L9.5,14.5 L3,12 L9.5,9.5 Z"
        };

        var card = new Border
        {
            Background = CardBg,
            BorderBrush = CardBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 9, 14, 9),
            Margin = new Thickness(0, 0, 0, 8),
            SnapsToDevicePixels = true
        };

        var dock = new DockPanel();
        var bar = new Border
        {
            Width = 3.5,
            CornerRadius = new CornerRadius(2),
            Background = accent,
            Margin = new Thickness(0, 1, 10, 1)
        };
        DockPanel.SetDock(bar, Dock.Left);
        dock.Children.Add(bar);

        var icon = new Path
        {
            Data = Geometry.Parse(iconPath),
            Stroke = accent,
            StrokeThickness = 1.6,
            Width = 14,
            Height = 14,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 9, 0)
        };
        DockPanel.SetDock(icon, Dock.Left);
        dock.Children.Add(icon);

        dock.Children.Add(new TextBlock
        {
            Text = clean,
            FontSize = 12,
            Foreground = TextFg,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        });

        card.Child = dock;

        while (Children.Count >= 3)
        {
            Children.RemoveAt(0);
        }

        Children.Add(card);

        if (!SuppressTimers)
        {
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3.6)
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                Children.Remove(card);
            };
            timer.Start();
        }
    }

    public static GameToastKind InferKind(string message)
    {
        var lower = message.ToLowerInvariant();
        if (lower.StartsWith("[") && (lower.Contains("error") || lower.Contains("invalid") || lower.Contains("overdraft") || lower.Contains("internal")))
            return GameToastKind.Loss;
        if (lower.Contains("không đủ") || lower.Contains("không thể") || lower.Contains("từ chối") || lower.Contains("cạn kiệt") || lower.Contains("thất bại") || lower.Contains("lỗ "))
            return GameToastKind.Loss;
        if (lower.Contains("đã mua") || lower.Contains("đã bán") || lower.Contains("thu hoạch") || lower.Contains("nhận ") || lower.Contains("thắng") || lower.Contains("hồi phục") || lower.Contains("lãi "))
            return GameToastKind.Gain;
        return GameToastKind.Event;
    }

    private static Brush Freeze(SolidColorBrush b)
    {
        b.Freeze();
        return b;
    }
}
