using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Theme-styled modal confirmation and alert dialog replacing standard Windows MessageBox.
/// </summary>
public sealed class GameConfirmDialog : Window
{
    private static readonly Brush DialogBg = Freeze(new SolidColorBrush(Color.FromRgb(0x15, 0x11, 0x0D)));
    private static readonly Brush CardBg = Freeze(new SolidColorBrush(Color.FromRgb(0x1D, 0x16, 0x11)));
    private static readonly Brush FrameBorder = Freeze(new SolidColorBrush(Color.FromRgb(0xC4, 0xA0, 0x62)));
    private static readonly Brush LineBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x3A, 0x2C, 0x20)));
    private static readonly Brush TitleFg = Freeze(new SolidColorBrush(Color.FromRgb(0xF3, 0xE6, 0xCC)));
    private static readonly Brush BodyFg = Freeze(new SolidColorBrush(Color.FromRgb(0xEA, 0xDF, 0xC6)));
    private static readonly Brush DangerAccent = Freeze(new SolidColorBrush(Color.FromRgb(0xA8, 0x32, 0x1F)));
    private static readonly FontFamily SerifFont = new("Noto Serif, Georgia, Segoe UI");

    private bool _confirmed;

    private GameConfirmDialog(
        string title,
        string message,
        string confirmLabel,
        string? cancelLabel,
        bool isDanger)
    {
        Title = title;
        Width = 440;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = false;
        Background = DialogBg;
        DarkTitleBar.Apply(this);

        var outer = new Border
        {
            Background = CardBg,
            BorderBrush = isDanger ? DangerAccent : FrameBorder,
            BorderThickness = new Thickness(1.5),
            Padding = new Thickness(22, 18, 22, 18)
        };

        var stack = new StackPanel();

        var headRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 10)
        };

        headRow.Children.Add(new Path
        {
            Data = Geometry.Parse(isDanger
                ? "M12,3 L21,19 L3,19 Z M12,9 L12,14 M12,17 L12,17.2"
                : "M12,2 L15,9 L22,12 L15,15 L12,22 L9,15 L2,12 L9,9 Z"),
            Stroke = isDanger ? DangerAccent : FrameBorder,
            StrokeThickness = 1.6,
            Width = 16,
            Height = 16,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 9, 0)
        });

        headRow.Children.Add(new TextBlock
        {
            Text = title,
            FontFamily = SerifFont,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = TitleFg,
            VerticalAlignment = VerticalAlignment.Center
        });
        stack.Children.Add(headRow);

        stack.Children.Add(new Border
        {
            Height = 1,
            Background = LineBorder,
            Margin = new Thickness(0, 0, 0, 12)
        });

        stack.Children.Add(new TextBlock
        {
            Text = message,
            FontSize = 13,
            Foreground = BodyFg,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 19,
            Margin = new Thickness(0, 0, 0, 18)
        });

        var btnRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        if (!string.IsNullOrWhiteSpace(cancelLabel))
        {
            var cancelBtn = new GameButton(cancelLabel, GameButtonVariant.Secondary)
            {
                MinWidth = 96,
                Margin = new Thickness(0, 0, 8, 0)
            };
            cancelBtn.Click += (_, _) =>
            {
                _confirmed = false;
                Close();
            };
            btnRow.Children.Add(cancelBtn);
        }

        var confirmBtn = new GameButton(
            confirmLabel,
            isDanger ? GameButtonVariant.Danger : GameButtonVariant.Primary)
        {
            MinWidth = 108
        };
        confirmBtn.Click += (_, _) =>
        {
            _confirmed = true;
            Close();
        };
        btnRow.Children.Add(confirmBtn);

        stack.Children.Add(btnRow);
        outer.Child = stack;
        Content = outer;
    }

    public static bool Ask(
        Window? owner,
        string title,
        string message,
        string confirmLabel = "Xác nhận",
        string cancelLabel = "Hủy bỏ",
        bool isDanger = true)
    {
        var dlg = new GameConfirmDialog(title, message, confirmLabel, cancelLabel, isDanger);
        if (owner is { IsLoaded: true })
        {
            dlg.Owner = owner;
        }
        dlg.ShowDialog();
        return dlg._confirmed;
    }

    public static void Alert(
        Window? owner,
        string title,
        string message,
        string okLabel = "Đã hiểu",
        bool isError = false)
    {
        var dlg = new GameConfirmDialog(title, message, okLabel, null, isError);
        if (owner is { IsLoaded: true })
        {
            dlg.Owner = owner;
        }
        dlg.ShowDialog();
    }

    private static Brush Freeze(SolidColorBrush b)
    {
        b.Freeze();
        return b;
    }
}
