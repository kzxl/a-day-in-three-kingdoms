using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SimT.Contracts.Time;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Dedicated reusable RPG Chronology & Calendar HUD control.
/// Displays Han Dynasty imperial reign era, seasonal weather glyph,
/// month progress gauge bar, and career tenure.
/// </summary>
public sealed class GameChronoCard : Border
{
    private readonly TextBlock _eraText;
    private readonly TextBlock _yearText;
    private readonly TextBlock _seasonText;
    private readonly TextBlock _dateText;
    private readonly TextBlock _turnText;
    private readonly TextBlock _remainingText;
    private readonly GameGaugeBar _monthGauge;

    public GameChronoCard()
    {
        Background = (Brush)Application.Current.FindResource("Surface");
        BorderBrush = (Brush)Application.Current.FindResource("Line");
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(8);
        Padding = new Thickness(11, 10, 11, 10);
        Margin = new Thickness(0, 10, 0, 0);

        var stack = new StackPanel();

        // Header: "NIÊN ĐẠI" & Era Reign
        var headerLine = new DockPanel();
        _eraText = new TextBlock
        {
            Style = (Style)Application.Current.FindResource("Caption"),
            Foreground = (Brush)Application.Current.FindResource("Accent"),
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(_eraText, Dock.Right);
        headerLine.Children.Add(_eraText);

        var titleText = new TextBlock
        {
            Style = (Style)Application.Current.FindResource("Overline"),
            Text = "NIÊN ĐẠI",
            VerticalAlignment = VerticalAlignment.Center
        };
        headerLine.Children.Add(titleText);
        stack.Children.Add(headerLine);

        // Big Year Number + SCN Suffix
        var yearStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
        _yearText = new TextBlock
        {
            Style = (Style)Application.Current.FindResource("Display"),
            FontSize = 26,
            FontWeight = FontWeights.Bold
        };
        yearStack.Children.Add(_yearText);

        var suffix = new TextBlock
        {
            Text = "SCN",
            Style = (Style)Application.Current.FindResource("Caption"),
            Foreground = (Brush)Application.Current.FindResource("Faint"),
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(6, 0, 0, 5)
        };
        yearStack.Children.Add(suffix);
        stack.Children.Add(yearStack);

        // Season & Month
        _seasonText = new TextBlock
        {
            Style = (Style)Application.Current.FindResource("Caption"),
            Foreground = (Brush)Application.Current.FindResource("Text"),
            FontWeight = FontWeights.Medium,
            Margin = new Thickness(0, 1, 0, 0)
        };
        stack.Children.Add(_seasonText);

        // Day of month line
        var dateDock = new DockPanel { Margin = new Thickness(0, 2, 0, 0) };
        _remainingText = new TextBlock
        {
            Style = (Style)Application.Current.FindResource("Caption"),
            Foreground = (Brush)Application.Current.FindResource("Faint"),
            FontSize = 10.5
        };
        DockPanel.SetDock(_remainingText, Dock.Right);
        dateDock.Children.Add(_remainingText);

        _dateText = new TextBlock
        {
            Style = (Style)Application.Current.FindResource("Caption"),
            Foreground = (Brush)Application.Current.FindResource("Muted"),
            FontSize = 11
        };
        dateDock.Children.Add(_dateText);
        stack.Children.Add(dateDock);

        // Month progress gauge bar
        _monthGauge = new GameGaugeBar
        {
            Height = 4,
            Margin = new Thickness(0, 5, 0, 4)
        };
        stack.Children.Add(_monthGauge);

        // Turn & Start info
        _turnText = new TextBlock
        {
            Style = (Style)Application.Current.FindResource("Caption"),
            Foreground = (Brush)Application.Current.FindResource("Faint"),
            FontSize = 10.5,
            Margin = new Thickness(0, 2, 0, 0)
        };
        stack.Children.Add(_turnText);

        Child = stack;

        MouseEnter += (_, _) => BorderBrush = (Brush)Application.Current.FindResource("LineSoft") ?? Brushes.SlateGray;
        MouseLeave += (_, _) => BorderBrush = (Brush)Application.Current.FindResource("Line");
    }

    public void Configure(GameDate today, int turn, GameDate startDate)
    {
        _yearText.Text = today.Year.ToString();
        _eraText.Text = GameDateCodec.EraVi(today.Year);

        var glyph = GameDateCodec.SeasonGlyph(today.Month);
        var season = GameDateCodec.SeasonVi(today.Month);
        var monthName = GameDateCodec.MonthNameVi(today.Month);
        _seasonText.Text = $"{glyph} {season} · Tháng {monthName}";

        var remaining = GameDate.DaysPerMonth - today.Day;
        _dateText.Text = $"Ngày {today.Day}/{GameDate.DaysPerMonth} · {GameDateCodec.ShortVi(today)}";
        _remainingText.Text = remaining > 0 ? $"còn {remaining} ngày" : "chốt tháng";

        var accentBrush = (Brush)Application.Current.FindResource("Accent");
        _monthGauge.SetProgress(today.Day, GameDate.DaysPerMonth, accentBrush);

        _turnText.Text = $"Lượt {turn} · {GameDateCodec.ShortVi(startDate)} khởi nghiệp";
    }
}
