using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Dedicated reusable RPG Stat Card control.
/// Displays attribute rank, effective points, equipment/trait bonus, EXP progress bar,
/// and comprehensive tooltips with built-in dark enterprise hover states.
/// </summary>
public sealed class GameStatCard : Border
{
    private readonly TextBlock _labelText;
    private readonly TextBlock _rankText;
    private readonly Border _rankBorder;
    private readonly TextBlock _valueText;
    private readonly TextBlock _bonusText;
    private readonly DockPanel _expLine;
    private readonly TextBlock _expValText;
    private readonly GameGaugeBar _gaugeBar;

    public GameStatCard()
    {
        Background = (Brush)Application.Current.FindResource("SurfaceAlt");
        BorderBrush = (Brush)Application.Current.FindResource("Line");
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(6);
        Padding = new Thickness(10, 8, 10, 8);
        Margin = new Thickness(4, 4, 4, 4);

        var stack = new StackPanel();

        // Line 1: Label (Left), Rank Badge & Value (Right)
        var line1 = new DockPanel();

        var valueStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(valueStack, Dock.Right);

        _valueText = new TextBlock
        {
            Style = (Style)Application.Current.FindResource("Number"),
            FontWeight = FontWeights.Bold,
            FontSize = 14
        };
        valueStack.Children.Add(_valueText);

        _bonusText = new TextBlock
        {
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)Application.Current.FindResource("Gain"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(3, 0, 0, 0),
            Visibility = Visibility.Collapsed
        };
        valueStack.Children.Add(_bonusText);
        line1.Children.Add(valueStack);

        _rankBorder = new Border
        {
            Background = (Brush)Application.Current.FindResource("Surface"),
            BorderBrush = (Brush)Application.Current.FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 1, 5, 1),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(_rankBorder, Dock.Right);

        _rankText = new TextBlock
        {
            FontSize = 10,
            FontWeight = FontWeights.Bold
        };
        _rankBorder.Child = _rankText;
        line1.Children.Add(_rankBorder);

        _labelText = new TextBlock
        {
            Style = (Style)Application.Current.FindResource("Caption"),
            Foreground = (Brush)Application.Current.FindResource("Text"),
            VerticalAlignment = VerticalAlignment.Center
        };
        line1.Children.Add(_labelText);
        stack.Children.Add(line1);

        // Line 2: EXP Sub-header (only visible when exp has value)
        _expLine = new DockPanel
        {
            Margin = new Thickness(0, 5, 0, 2),
            Visibility = Visibility.Collapsed
        };

        _expValText = new TextBlock
        {
            Style = (Style)Application.Current.FindResource("Number"),
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.FindResource("Accent")
        };
        DockPanel.SetDock(_expValText, Dock.Right);
        _expLine.Children.Add(_expValText);

        var expLabel = new TextBlock
        {
            Text = "EXP CHỈ SỐ",
            Style = (Style)Application.Current.FindResource("Overline"),
            FontSize = 9.5,
            Foreground = (Brush)Application.Current.FindResource("Faint"),
            VerticalAlignment = VerticalAlignment.Center
        };
        _expLine.Children.Add(expLabel);
        stack.Children.Add(_expLine);

        // Line 3: Hardware-accelerated Gauge Bar
        _gaugeBar = new GameGaugeBar
        {
            Height = 6,
            Margin = new Thickness(0, 4, 0, 0)
        };
        stack.Children.Add(_gaugeBar);

        Child = stack;

        // Hover effect for game feel
        MouseEnter += (_, _) => BorderBrush = (Brush)Application.Current.FindResource("LineSoft") ?? Brushes.SlateGray;
        MouseLeave += (_, _) => BorderBrush = (Brush)Application.Current.FindResource("Line");
    }

    public void Configure(string label, int baseValue, int bonus, string tip, bool verified, int? exp = null)
    {
        var effective = baseValue + bonus;
        var rank = effective switch
        {
            >= 95 => "S+",
            >= 90 => "S",
            >= 80 => "A",
            >= 70 => "B",
            >= 60 => "C",
            _ => "D"
        };

        var barBrush = (Brush)(effective >= 90
            ? Application.Current.FindResource("Accent")
            : effective >= 80
                ? Application.Current.FindResource("Gain")
                : effective >= 70
                    ? new SolidColorBrush(Color.FromRgb(0x4A, 0x90, 0xE2))
                    : Application.Current.FindResource("Muted"));

        var accentBrush = (Brush)Application.Current.FindResource("Accent");

        _labelText.Text = label;
        _valueText.Text = verified ? effective.ToString() : (effective >= 80 ? $"~{(effective / 5) * 5}" : "???");
        _valueText.Foreground = barBrush;

        if (bonus > 0 && verified)
        {
            _bonusText.Text = $" (+{bonus})";
            _bonusText.Visibility = Visibility.Visible;
        }
        else
        {
            _bonusText.Visibility = Visibility.Collapsed;
        }

        _rankText.Text = verified ? rank : (effective >= 80 ? rank : "?");
        _rankText.Foreground = barBrush;

        if (exp.HasValue)
        {
            var curExp = Math.Clamp(exp.Value, 0, 99);
            _expLine.Visibility = Visibility.Visible;
            _expValText.Text = $"{curExp}/100";
            _gaugeBar.SetProgress(curExp, 100.0, accentBrush);
            _gaugeBar.Margin = new Thickness(0, 2, 0, 0);
        }
        else
        {
            _expLine.Visibility = Visibility.Collapsed;
            _gaugeBar.SetProgress(effective, 120.0, barBrush);
            _gaugeBar.Margin = new Thickness(0, 6, 0, 0);
        }

        // Build ToolTip
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(label);
        sb.AppendLine(tip);
        if (verified)
        {
            sb.AppendLine();
            sb.Append($"📊 Chỉ số: {effective} điểm (Bậc {rank})");
            if (bonus > 0)
            {
                sb.Append($" [Cơ bản: {baseValue}, Ngoại vật/Khí vận: +{bonus}]");
            }
            sb.AppendLine();
        }
        if (exp.HasValue)
        {
            var curExp = Math.Clamp(exp.Value, 0, 99);
            var needed = 100 - curExp;
            sb.AppendLine($"⭐ Kinh nghiệm chỉ số: {curExp}/100 EXP");
            sb.Append($"⚡ Tích lũy thêm {needed} EXP để thăng cấp lên {effective + 1}.");
        }

        ToolTip = sb.ToString().TrimEnd();
    }

    public void ConfigureMasked(string label, string tip)
    {
        _labelText.Text = label;
        _labelText.Foreground = (Brush)Application.Current.FindResource("Muted");
        _valueText.Text = "???";
        _valueText.Foreground = (Brush)Application.Current.FindResource("Faint");
        _bonusText.Visibility = Visibility.Collapsed;
        _rankText.Text = "🔒";
        _rankText.Foreground = (Brush)Application.Current.FindResource("Faint");
        _expLine.Visibility = Visibility.Collapsed;
        _gaugeBar.SetProgress(0, 100.0, (Brush)Application.Current.FindResource("Line"));
        _gaugeBar.Opacity = 0.3;
        ToolTip = tip;
    }
}
