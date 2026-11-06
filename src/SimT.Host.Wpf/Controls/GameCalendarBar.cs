using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using SimT.Contracts.Time;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Imperial Calendar & Time Pill at top header.
/// Compact, elegant display of Year, Season, Day in Month (1-30), Turn,
/// with interactive transition ticker and progress indicator.
/// Replaces the bulky 30-slot grid with a polished tactical HUD widget.
/// </summary>
public sealed class GameCalendarBar : Border
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    private readonly TextBlock _dateText;
    private readonly TextBlock _turnText;
    private readonly Border _progressFill;
    private readonly Border _tickerBadge;
    private readonly TextBlock _tickerText;

    private GameDate _currentDate = GameDate.Start;
    private int _currentTurn = 1;

    public GameCalendarBar()
    {
        // Styling matches top header badges
        Background = (Brush)Application.Current.FindResource("SurfaceAlt");
        BorderBrush = (Brush)Application.Current.FindResource("Line");
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(4);
        Padding = new Thickness(10, 4, 10, 4);
        VerticalAlignment = VerticalAlignment.Center;
        Cursor = System.Windows.Input.Cursors.Help;

        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        // 1. Calendar Glyph / Icon
        var iconPath = new Path
        {
            Data = Geometry.Parse("M19,4 H5 C3.89,4 3,4.89 3,6 V20 C3,21.1 3.89,22 5,22 H19 C20.1,22 21,21.1 21,20 V6 C21,4.89 20.1,4 19,4 Z M19,20 H5 V10 H19 V20 Z"),
            Stroke = (Brush)Application.Current.FindResource("Accent"),
            StrokeThickness = 1.2,
            Width = 12,
            Height = 12,
            Stretch = Stretch.Uniform,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        stack.Children.Add(iconPath);

        // 2. Main Date & Season display (e.g. "16/06/184 · Mùa Hạ")
        _dateText = new TextBlock
        {
            Style = (Style)Application.Current.FindResource("Caption"),
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.FindResource("Ink"),
            VerticalAlignment = VerticalAlignment.Center
        };
        stack.Children.Add(_dateText);

        // 3. Delimiter
        var sep = new TextBlock
        {
            Text = " · ",
            Foreground = (Brush)Application.Current.FindResource("Faint"),
            VerticalAlignment = VerticalAlignment.Center
        };
        stack.Children.Add(sep);

        // 4. Turn and day of month (e.g. "Lượt 2 · Ngày 16/30")
        _turnText = new TextBlock
        {
            Style = (Style)Application.Current.FindResource("Caption"),
            Foreground = (Brush)Application.Current.FindResource("Muted"),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        };
        stack.Children.Add(_turnText);

        // 5. Month Progress Bar (compact mini bar indicating day 1-30)
        var progressTrack = new Border
        {
            Width = 36,
            Height = 4,
            Background = new SolidColorBrush(Color.FromRgb(0x28, 0x1E, 0x1A)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0x2E, 0x26)),
            BorderThickness = new Thickness(0.5),
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(6, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        _progressFill = new Border
        {
            Background = (Brush)Application.Current.FindResource("Accent"),
            CornerRadius = new CornerRadius(1.5),
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = 18
        };
        progressTrack.Child = _progressFill;
        stack.Children.Add(progressTrack);

        // 6. Transition Ticker Badge (shows when stepping days: "+1 ngày", "+7 ngày", etc.)
        _tickerBadge = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x40, 0xF5, 0xD0, 0x61)),
            BorderBrush = (Brush)Application.Current.FindResource("Accent"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 1, 5, 1),
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        _tickerText = new TextBlock
        {
            Text = "+1 ngày",
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)Application.Current.FindResource("Accent")
        };
        _tickerBadge.Child = _tickerText;
        stack.Children.Add(_tickerBadge);

        Child = stack;
    }

    /// <summary>
    /// Synchronously configures the bar for the given date (used on boot/switch).
    /// </summary>
    public void SetDate(GameDate date, int turn = 1)
    {
        _currentDate = date;
        _currentTurn = turn;
        _tickerBadge.Visibility = Visibility.Collapsed;

        UpdateDisplay();
    }

    /// <summary>
    /// Steps a single day forward during an interactive multi-day advance loop.
    /// Pulses date display, updates mini progress bar, and updates ticker text.
    /// </summary>
    public async Task StepToDateAsync(GameDate prevDate, GameDate nextDate, int currentStep, int totalSteps)
    {
        _currentDate = nextDate;

        // Show transition ticker
        _tickerBadge.Visibility = Visibility.Visible;
        _tickerText.Text = totalSteps == 1 ? "+1 ngày" : $"+{currentStep}/{totalSteps} ngày";

        UpdateDisplay();

        // Brief delay per step for smooth tactile feedback
        var delay = totalSteps switch
        {
            1 => 180,
            <= 3 => 220,
            <= 7 => 140,
            _ => 90
        };
        await Task.Delay(delay);

        if (currentStep >= totalSteps)
        {
            _ = FadeOutTickerAsync(1000);
        }
    }

    /// <summary>
    /// Animates catch-up time passage when a task or command spent days internally
    /// (e.g. travel across citadels, crafting equipment, court duties).
    /// </summary>
    public async Task AnimateCatchUpAsync(GameDate from, GameDate to, string? taskDescription = null)
    {
        var totalDays = (to.Year - from.Year) * 360 + (to.Month - from.Month) * 30 + (to.Day - from.Day);
        if (totalDays <= 0)
        {
            SetDate(to, _currentTurn);
            return;
        }

        _tickerBadge.Visibility = Visibility.Visible;
        _tickerText.Text = $"+{totalDays} ngày";

        var steps = Math.Min(totalDays, 8);
        for (var i = 1; i <= steps; i++)
        {
            var simDays = (int)(totalDays * (i / (double)steps));
            var simDate = from.AddDays(simDays);
            _currentDate = simDate;
            UpdateDisplay();
            await Task.Delay(50);
        }

        _currentDate = to;
        UpdateDisplay();

        var desc = string.IsNullOrWhiteSpace(taskDescription) ? $"✓ +{totalDays} ngày" : $"✓ {taskDescription} (+{totalDays} ngày)";
        _tickerText.Text = desc;
        _ = FadeOutTickerAsync(1500);
    }

    private void UpdateDisplay()
    {
        var season = GameDateCodec.SeasonVi(_currentDate.Month);
        var eraName = GameDateCodec.EraVi(_currentDate.Year);
        var remainingDays = GameDate.DaysPerMonth - _currentDate.Day;

        // Date text
        _dateText.Text = $"{GameDateCodec.ShortVi(_currentDate)} · {season}";

        // Turn & day of month text
        _turnText.Text = $"Lượt {_currentTurn} · Ngày {_currentDate.Day}/30";

        // Progress bar width (36px track width)
        var ratio = Math.Clamp(_currentDate.Day / (double)GameDate.DaysPerMonth, 0.0, 1.0);
        _progressFill.Width = Math.Max(2.0, ratio * 36.0);

        // Rich historical tooltip
        var closeAlert = remainingDays switch
        {
            0 => "⚠️ HÔM NAY LÀ NGÀY CHỐT SỔ THÁNG (kết toán kinh tế, thuế khóa và bổng lộc)!",
            1 => "⏳ Ngày mai sẽ kết thúc tháng hiện tại.",
            _ => $"Còn {remainingDays} ngày nữa là đến kỳ chốt sổ tháng (ngày 30)."
        };

        ToolTip = $"📅 THỜI GIAN ĐÔNG HÁN\n" +
                  $"• Niên hiệu: {eraName} (Năm {_currentDate.Year})\n" +
                  $"• Thời tiết: {season}\n" +
                  $"• Tiến độ: Ngày {_currentDate.Day}/30 · Lượt {_currentTurn}\n" +
                  $"• Lưu ý: {closeAlert}";
    }

    private async Task FadeOutTickerAsync(int delayMs)
    {
        await Task.Delay(delayMs);
        if (_tickerBadge.Visibility == Visibility.Visible)
        {
            var anim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(300));
            anim.Completed += (_, _) =>
            {
                _tickerBadge.Visibility = Visibility.Collapsed;
                _tickerBadge.Opacity = 1.0;
            };
            _tickerBadge.BeginAnimation(OpacityProperty, anim);
        }
    }
}
