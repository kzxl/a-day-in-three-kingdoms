using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using SimT.Contracts.Time;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Tactical Game-like 30-Day Calendar Bar with animated sliding progression.
/// Features day-by-day highlight stepping, active cursor tracking,
/// month settlement warnings, and real-time transition ticker.
/// </summary>
public sealed class GameCalendarBar : Border
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    private enum DaySlotState
    {
        Past,
        Today,
        TransitionStep,
        Future,
        MonthClose
    }

    private sealed class DaySlot
    {
        public int DayNumber { get; }
        public Border Container { get; }
        public TextBlock LabelText { get; }
        public Rectangle IndicatorDot { get; }

        public DaySlot(int dayNumber, Border container, TextBlock labelText, Rectangle indicatorDot)
        {
            DayNumber = dayNumber;
            Container = container;
            LabelText = labelText;
            IndicatorDot = indicatorDot;
        }
    }

    // Header elements
    private readonly TextBlock _monthLabel;
    private readonly TextBlock _remainingLabel;
    private readonly Border _tickerBadge;
    private readonly TextBlock _tickerText;

    // Day slots
    private readonly DaySlot[] _slots = new DaySlot[GameDate.DaysPerMonth];

    private GameDate _currentDate = GameDate.Start;
    private int _currentTurn = 1;

    // Palette brushes
    private static readonly Brush PastBg = new SolidColorBrush(Color.FromRgb(0x11, 0x16, 0x22));
    private static readonly Brush PastBorder = new SolidColorBrush(Color.FromRgb(0x1B, 0x23, 0x31));
    private static readonly Brush PastFg = new SolidColorBrush(Color.FromRgb(0x4A, 0x55, 0x68));

    private static readonly Brush FutureBg = new SolidColorBrush(Color.FromRgb(0x16, 0x1D, 0x2A));
    private static readonly Brush FutureBorder = new SolidColorBrush(Color.FromRgb(0x23, 0x2D, 0x3D));
    private static readonly Brush FutureFg = new SolidColorBrush(Color.FromRgb(0x7E, 0x8E, 0x9F));

    private static readonly Brush TodayBg = new SolidColorBrush(Color.FromRgb(0x2C, 0x22, 0x13));
    private static readonly Brush TodayBorder = new SolidColorBrush(Color.FromRgb(0xF5, 0xD0, 0x61));
    private static readonly Brush TodayFg = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));

    private static readonly Brush PulseBg = new SolidColorBrush(Color.FromRgb(0x4D, 0x3B, 0x15));
    private static readonly Brush PulseBorder = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00));
    private static readonly Brush PulseFg = new SolidColorBrush(Color.FromRgb(0xFF, 0xF4, 0xCE));

    private static readonly Brush CloseBg = new SolidColorBrush(Color.FromRgb(0x26, 0x17, 0x1A));
    private static readonly Brush CloseBorder = new SolidColorBrush(Color.FromRgb(0x9B, 0x2C, 0x2C));
    private static readonly Brush CloseFg = new SolidColorBrush(Color.FromRgb(0xE5, 0x3E, 0x3E));

    public GameCalendarBar()
    {
        Background = (Brush)Application.Current.FindResource("Surface");
        BorderBrush = (Brush)Application.Current.FindResource("Line");
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(8);
        Padding = new Thickness(12, 9, 12, 9);

        var root = new StackPanel();

        // 1. Header: Month title, transition ticker badge, and countdown
        var headerDock = new DockPanel();

        // Left: Month name and era
        _monthLabel = new TextBlock
        {
            Style = (Style)Application.Current.FindResource("Overline"),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 11,
            Foreground = (Brush)Application.Current.FindResource("Text")
        };
        DockPanel.SetDock(_monthLabel, Dock.Left);
        headerDock.Children.Add(_monthLabel);

        // Right: Turn and days left
        _remainingLabel = new TextBlock
        {
            Style = (Style)Application.Current.FindResource("Caption"),
            Foreground = (Brush)Application.Current.FindResource("Faint"),
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(_remainingLabel, Dock.Right);
        headerDock.Children.Add(_remainingLabel);

        // Center: Transition Ticker (shows when time advances)
        _tickerBadge = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x40, 0xF5, 0xD0, 0x61)),
            BorderBrush = TodayBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 2, 8, 2),
            HorizontalAlignment = HorizontalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        _tickerText = new TextBlock
        {
            Text = "⏳ Đang chuyển ngày...",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = TodayBorder
        };
        _tickerBadge.Child = _tickerText;
        headerDock.Children.Add(_tickerBadge);

        root.Children.Add(headerDock);

        // 2. Timeline: 30 day slots
        var grid = new UniformGrid
        {
            Rows = 1,
            Columns = GameDate.DaysPerMonth,
            Margin = new Thickness(0, 8, 0, 0)
        };

        for (var day = 1; day <= GameDate.DaysPerMonth; day++)
        {
            var cellGrid = new Grid();
            cellGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            cellGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock
            {
                Text = day.ToString(Vietnamese),
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(label, 0);
            cellGrid.Children.Add(label);

            var dot = new Rectangle
            {
                Height = 2.5,
                Width = 12,
                RadiusX = 1,
                RadiusY = 1,
                Fill = TodayBorder,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 2),
                Visibility = Visibility.Collapsed
            };
            Grid.SetRow(dot, 1);
            cellGrid.Children.Add(dot);

            var container = new Border
            {
                Height = 28,
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(1.5, 0, 1.5, 0),
                Child = cellGrid
            };

            var slot = new DaySlot(day, container, label, dot);
            _slots[day - 1] = slot;
            grid.Children.Add(container);
        }

        root.Children.Add(grid);
        Child = root;
    }

    /// <summary>
    /// Synchronously configures the bar for the given date (used on boot/switch).
    /// </summary>
    public void SetDate(GameDate date, int turn = 1)
    {
        _currentDate = date;
        _currentTurn = turn;
        _tickerBadge.Visibility = Visibility.Collapsed;

        UpdateLabels();
        RefreshAllSlots();
    }

    /// <summary>
    /// Steps a single day forward during an interactive multi-day advance loop.
    /// Highlights the moving day, pauses briefly, and settles into Today.
    /// </summary>
    public async Task StepToDateAsync(GameDate prevDate, GameDate nextDate, int currentStep, int totalSteps)
    {
        _currentDate = nextDate;

        // Show transition ticker
        _tickerBadge.Visibility = Visibility.Visible;
        _tickerText.Text = $"⏳ Đang chuyển ngày: Ngày {nextDate.Day}/30 (+{currentStep}/{totalSteps})";

        // Update labels
        UpdateLabels();

        // 1. Previous day transitions to Past
        if (prevDate.Month == nextDate.Month && prevDate.Day >= 1 && prevDate.Day <= GameDate.DaysPerMonth)
        {
            ApplySlotStyle(_slots[prevDate.Day - 1], DaySlotState.Past);
        }
        else
        {
            // Month rolled over: refresh all slots for new month
            RefreshAllSlots();
        }

        // 2. Next day lights up with transition pulse
        if (nextDate.Day >= 1 && nextDate.Day <= GameDate.DaysPerMonth)
        {
            var nextSlot = _slots[nextDate.Day - 1];
            ApplySlotStyle(nextSlot, DaySlotState.TransitionStep);

            // Step delay: comfortable pacing giving the user a tangible feeling of time progressing
            var delay = totalSteps switch
            {
                1 => 220,
                <= 3 => 260,
                <= 7 => 180,
                _ => 110
            };
            await Task.Delay(delay);

            // Settle to Today
            ApplySlotStyle(nextSlot, DaySlotState.Today);
        }

        // If completed all steps, show completion message
        if (currentStep >= totalSteps)
        {
            _tickerText.Text = $"✓ Đã sang {nextDate.Day}/{nextDate.Month}/{nextDate.Year} (Hoàn tất +{totalSteps} ngày)";
            _ = FadeOutTickerAsync(1400);
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
        var reason = string.IsNullOrWhiteSpace(taskDescription) ? "Thao tác hoàn thành" : taskDescription;
        _tickerText.Text = $"⏳ {reason}: Ngày {from.Day} ➔ Ngày {to.Day} (+{totalDays} ngày)...";

        // If in the same month, step day-by-day
        if (from.Month == to.Month && from.Year == to.Year)
        {
            var stepDelay = totalDays switch
            {
                1 => 240,
                <= 3 => 280,
                <= 7 => 190,
                _ => 120
            };

            for (var d = from.Day; d <= to.Day; d++)
            {
                if (d > from.Day)
                {
                    ApplySlotStyle(_slots[d - 2], DaySlotState.Past);
                }

                var currentSlot = _slots[d - 1];
                ApplySlotStyle(currentSlot, DaySlotState.TransitionStep);
                _tickerText.Text = $"⏳ {reason}: Tiến đến ngày {d}/30 (+{d - from.Day}/{totalDays})...";
                await Task.Delay(stepDelay);

                if (d == to.Day)
                {
                    ApplySlotStyle(currentSlot, DaySlotState.Today);
                }
            }
        }
        else
        {
            // Crossed months: step through end of month, roll over, and step into new month
            var daysToEndOfMonth = GameDate.DaysPerMonth - from.Day;
            for (var d = from.Day; d <= GameDate.DaysPerMonth; d++)
            {
                if (d > from.Day) ApplySlotStyle(_slots[d - 2], DaySlotState.Past);
                ApplySlotStyle(_slots[d - 1], DaySlotState.TransitionStep);
                await Task.Delay(130);
            }

            // Month rollover visual
            _currentDate = to;
            UpdateLabels();
            RefreshAllSlots();

            for (var d = 1; d <= to.Day; d++)
            {
                if (d > 1) ApplySlotStyle(_slots[d - 2], DaySlotState.Past);
                var slot = _slots[d - 1];
                ApplySlotStyle(slot, DaySlotState.TransitionStep);
                await Task.Delay(130);
                if (d == to.Day)
                {
                    ApplySlotStyle(slot, DaySlotState.Today);
                }
            }
        }

        _currentDate = to;
        UpdateLabels();
        _tickerText.Text = $"✓ {reason} xong! Đã sang {to.Day}/{to.Month}/{to.Year}.";
        _ = FadeOutTickerAsync(1600);
    }

    private void UpdateLabels()
    {
        var glyph = GameDateCodec.SeasonGlyph(_currentDate.Month);
        var seasonName = GameDateCodec.SeasonVi(_currentDate.Month);
        var monthName = GameDateCodec.MonthNameVi(_currentDate.Month);
        var eraName = GameDateCodec.EraVi(_currentDate.Year);

        _monthLabel.Text = $"{glyph} THÁNG {monthName.ToUpperInvariant()} · NĂM {_currentDate.Year} ({eraName})";

        var remaining = GameDate.DaysPerMonth - _currentDate.Day + 1;
        _remainingLabel.Text = $"Lượt {_currentTurn} · Ngày {_currentDate.Day}/30 · Còn {remaining} ngày";
    }

    private void RefreshAllSlots()
    {
        for (var day = 1; day <= GameDate.DaysPerMonth; day++)
        {
            var slot = _slots[day - 1];
            var state = day < _currentDate.Day ? DaySlotState.Past
                      : day == _currentDate.Day ? DaySlotState.Today
                      : day == GameDate.DaysPerMonth ? DaySlotState.MonthClose
                      : DaySlotState.Future;

            ApplySlotStyle(slot, state);
        }
    }

    private static void ApplySlotStyle(DaySlot slot, DaySlotState state)
    {
        var day = slot.DayNumber;
        switch (state)
        {
            case DaySlotState.Past:
                slot.Container.Background = PastBg;
                slot.Container.BorderBrush = PastBorder;
                slot.Container.BorderThickness = new Thickness(1);
                slot.LabelText.Foreground = PastFg;
                slot.LabelText.FontWeight = FontWeights.Normal;
                slot.IndicatorDot.Visibility = Visibility.Collapsed;
                slot.Container.ToolTip = $"Đã qua · Ngày {day}";
                break;

            case DaySlotState.Future:
                slot.Container.Background = FutureBg;
                slot.Container.BorderBrush = FutureBorder;
                slot.Container.BorderThickness = new Thickness(1);
                slot.LabelText.Foreground = FutureFg;
                slot.LabelText.FontWeight = FontWeights.Normal;
                slot.IndicatorDot.Visibility = Visibility.Collapsed;
                slot.Container.ToolTip = $"Sắp tới · Ngày {day}";
                break;

            case DaySlotState.MonthClose:
                slot.Container.Background = CloseBg;
                slot.Container.BorderBrush = CloseBorder;
                slot.Container.BorderThickness = new Thickness(1.5);
                slot.LabelText.Foreground = CloseFg;
                slot.LabelText.FontWeight = FontWeights.Bold;
                slot.IndicatorDot.Visibility = Visibility.Collapsed;
                slot.Container.ToolTip = $"Ngày {day} · Chốt sổ tháng & Quyết toán tài chính";
                break;

            case DaySlotState.Today:
                slot.Container.Background = TodayBg;
                slot.Container.BorderBrush = TodayBorder;
                slot.Container.BorderThickness = new Thickness(1.5);
                slot.LabelText.Foreground = TodayFg;
                slot.LabelText.FontWeight = FontWeights.Bold;
                slot.IndicatorDot.Fill = TodayBorder;
                slot.IndicatorDot.Visibility = Visibility.Visible;
                slot.Container.ToolTip = $"Hôm nay · Ngày {day}";
                break;

            case DaySlotState.TransitionStep:
                slot.Container.Background = PulseBg;
                slot.Container.BorderBrush = PulseBorder;
                slot.Container.BorderThickness = new Thickness(2);
                slot.LabelText.Foreground = PulseFg;
                slot.LabelText.FontWeight = FontWeights.Bold;
                slot.IndicatorDot.Fill = PulseBorder;
                slot.IndicatorDot.Visibility = Visibility.Visible;
                slot.Container.ToolTip = $"Đang chuyển qua ngày {day}...";
                break;
        }
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
