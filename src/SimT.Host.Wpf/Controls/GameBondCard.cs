using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Dedicated reusable RPG social bond and memory card control.
/// Renders sworn brothers, retainers, and interaction memories with unified design token styling.
/// </summary>
public sealed class GameBondCard : Border
{
    private readonly TextBlock _titleText;
    private readonly TextBlock _subtitleText;
    private readonly TextBlock _badgeRightText;

    public GameBondCard()
    {
        Background = (Brush)Application.Current.FindResource("SurfaceAlt");
        BorderBrush = (Brush)Application.Current.FindResource("Line");
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(6);
        Padding = new Thickness(10, 7, 10, 7);
        Margin = new Thickness(0, 0, 0, 6);

        var dock = new DockPanel();

        _badgeRightText = new TextBlock
        {
            Style = (Style)Application.Current.FindResource("Caption"),
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(_badgeRightText, Dock.Right);
        dock.Children.Add(_badgeRightText);

        var info = new StackPanel();
        _titleText = new TextBlock
        {
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)Application.Current.FindResource("Text")
        };
        info.Children.Add(_titleText);

        _subtitleText = new TextBlock
        {
            Style = (Style)Application.Current.FindResource("Caption"),
            Foreground = (Brush)Application.Current.FindResource("Faint"),
            Margin = new Thickness(0, 2, 0, 0)
        };
        info.Children.Add(_subtitleText);

        dock.Children.Add(info);
        Child = dock;

        MouseEnter += (_, _) => BorderBrush = (Brush)Application.Current.FindResource("LineSoft") ?? Brushes.SlateGray;
        MouseLeave += (_, _) => BorderBrush = (Brush)Application.Current.FindResource("Line");
    }

    public void ConfigureSwornBrother(string name, string bondTitle, string oathDate, int assistCount)
    {
        _titleText.Text = $"🍑 {name} ({bondTitle})";
        _subtitleText.Text = $"Thề ước kim lan: {oathDate}";
        _badgeRightText.Text = $"⚔️ Trợ chiến: {assistCount} trận";
        _badgeRightText.Foreground = (Brush)Application.Current.FindResource("Accent");
    }

    public void ConfigureRetainer(string name, string role, int stipend, int loyalty, string recruitDate)
    {
        _titleText.Text = $"🛡️ {name} · {role}";
        _subtitleText.Text = $"Bổng lộc: {stipend:N0} Quan/tháng · Gia nhập: {recruitDate}";
        _badgeRightText.Text = $"Trung thành: {loyalty}/100";
        _badgeRightText.Foreground = (Brush)Application.Current.FindResource(loyalty >= 80 ? "Gain" : "Accent");
    }

    public void ConfigureMemory(string date, string topic, int delta, string summaryVi)
    {
        var topicIcon = topic switch
        {
            "chat" => "💬 Đàm đạo",
            "gift" => "🎁 Tặng quà",
            "recruit" => "🤝 Chiêu mộ",
            _ => "⚡ Tương tác"
        };

        _titleText.Text = $"[{date}] {topicIcon}";
        _subtitleText.Text = summaryVi;
        _subtitleText.Foreground = (Brush)Application.Current.FindResource("Text");

        if (delta != 0)
        {
            _badgeRightText.Text = $"{(delta > 0 ? "+" : "")}{delta} ♥";
            _badgeRightText.Foreground = (Brush)Application.Current.FindResource(delta >= 0 ? "Gain" : "Loss");
        }
        else
        {
            _badgeRightText.Text = "";
        }
    }
}
