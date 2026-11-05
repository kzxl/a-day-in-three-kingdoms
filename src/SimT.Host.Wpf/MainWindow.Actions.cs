using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SimT.Contracts;
using SimT.Contracts.Ledger;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Host.Wpf;

public partial class MainWindow
{
    private bool _isAdvancing;

    private async void Advance_Click(object sender, RoutedEventArgs e) => await AdvanceAsync(1);

    private async void Rest_Click(object sender, RoutedEventArgs e)
    {
        if (_isAdvancing) return;

        if (_kernel.Capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) && attrs is not null)
        {
            attrs.Rest(1);
        }

        ShowFloatingFeedback("💤 Tĩnh dưỡng hồi phục!", (Brush)FindResource("Gain"), 180, 50);
        await AdvanceAsync(1, "Đã nghỉ ngơi tĩnh dưỡng 1 ngày: Thể lực hồi phục trọn vẹn, tinh thần sảng khoái.");
    }

    private async void AdvanceWeek_Click(object sender, RoutedEventArgs e) => await AdvanceAsync(7);

    private void Stroll_Click(object sender, RoutedEventArgs e)
    {
        if (_isAdvancing) return;

        if (_kernel.Capabilities.TryGet<ICityStroll>(CapabilityIds.CityStroll, out var stroll) && stroll is not null &&
            _kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var player) && player is not null)
        {
            var modal = new CityStrollModalWindow(_kernel, stroll, player.CurrentCityId) { Owner = this };
            modal.ShowDialog();

            _kernel.Save();
            UpdateHeader();
            RenderSelected();
        }
        else
        {
            StatusText.Text = "Tính năng du ngoạn thành trì chưa khả dụng.";
        }
    }

    public void AdvanceSync(int days)
    {
        var from = _kernel.Today;
        _kernel.AdvanceDays(days);
        _kernel.Save();
        UpdateHeader();
        RenderSelected();
        ShowReport(from, _kernel.Today);
    }

    private async Task AdvanceAsync(int days, string? customStatus = null)
    {
        if (_isAdvancing) return;
        _isAdvancing = true;

        try
        {
            SetAdvancingState(true);
            var from = _kernel.Today;
            var prevBalance = _kernel.Ledger.Balance;

            for (var step = 1; step <= days; step++)
            {
                var prev = _kernel.Today;
                _kernel.AdvanceDays(1);
                var curr = _kernel.Today;

                await CalendarBar.StepToDateAsync(prev, curr, step, days);
                UpdateHeaderDateOnly(curr);
            }

            _kernel.Save();
            UpdateHeader();
            RenderSelected();
            ShowReport(from, _kernel.Today);

            var netDelta = _kernel.Ledger.Balance - prevBalance;
            if (netDelta > 0)
            {
                ShowFloatingFeedback($"+{netDelta:N0} Quan", (Brush)FindResource("Gain"), 180, 50);
            }
            else if (netDelta < 0)
            {
                ShowFloatingFeedback($"{netDelta:N0} Quan", (Brush)FindResource("Loss"), 180, 50);
            }
            StatusText.Text = customStatus ?? $"Đã sang {GameDateCodec.DescribeVi(_kernel.Today)} (+{days} ngày).";
        }
        catch (Exception error)
        {
            StatusText.Text = Describe(error);
        }
        finally
        {
            SetAdvancingState(false);
            _isAdvancing = false;
        }
    }

    private void SetAdvancingState(bool busy)
    {
        AdvanceButton.IsEnabled = !busy;
        RestButton.IsEnabled = !busy;
        AdvanceWeekButton.IsEnabled = !busy;
        StrollButton.IsEnabled = !busy;
        Cursor = busy ? System.Windows.Input.Cursors.Wait : null;
    }

    /// <summary>
    /// What the days just spent actually did: money in, money out, and the charges the month
    /// close took. The old turn report in one card.
    /// </summary>
    private void ShowReport(GameDate from, GameDate to)
    {
        var lines = _kernel.Ledger.Query(from, to, limit: 500);
        var income = lines.Where(line => line.Direction == LedgerDirection.In).Sum(line => line.Amount);
        var spend = lines.Where(line => line.Direction == LedgerDirection.Out).Sum(line => line.Amount);
        var charges = lines
            .Where(line => line.Direction == LedgerDirection.Out)
            .GroupBy(line => line.JournalCode)
            .OrderByDescending(group => group.Sum(line => line.Amount))
            .Take(3)
            .Select(group => $"{group.Key} {group.Sum(line => line.Amount).ToString("N0", Vietnamese)}");

        var days = (int)(to.DayIndex - from.DayIndex);
        var heading = $"BÁO CÁO · {GameDateCodec.ShortVi(from)} → {GameDateCodec.ShortVi(to)} ({days} ngày)";
        var summary = $"thu {income.ToString("N0", Vietnamese)} · chi {spend.ToString("N0", Vietnamese)} · "
                      + $"ròng {(income - spend).ToString("N0", Vietnamese)} quan";

        var card = new StackPanel();
        card.Children.Add(new TextBlock { Text = heading, Style = (Style)FindResource("Overline") });
        card.Children.Add(new TextBlock { Text = summary, Margin = new Thickness(0, 4, 0, 0) });
        if (charges.Any())
        {
            card.Children.Add(new TextBlock
            {
                Text = "khoản lớn nhất: " + string.Join(" · ", charges),
                Style = (Style)FindResource("Caption"),
                Foreground = (Brush)FindResource("Faint"),
                Margin = new Thickness(0, 3, 0, 0)
            });
        }

        ContentPanel.Children.Insert(0, new Border
        {
            Background = (Brush)FindResource("SurfaceAlt"),
            BorderBrush = (Brush)FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 14),
            Child = card
        });
    }

    private async void RowCommand_Click(object sender, RoutedEventArgs e)
    {
        if (_isAdvancing) return;

        if (sender is not Button { Tag: string commandId })
        {
            return;
        }

        await ExecuteScreenCommandAsync(commandId);
    }

    public async Task ExecuteScreenCommandAsync(string commandId, string? targetScreenId = null)
    {
        if (_isAdvancing) return;

        if (_kernel.Ui.TryPresentModal(commandId))
        {
            return;
        }

        var screenId = targetScreenId ?? ScreenForCommand();
        if (!_kernel.TryGetCommandHandler(screenId, out var handler) || handler is null)
        {
            StatusText.Text = $"Màn hình '{screenId}' không nhận lệnh.";
            return;
        }

        var fromDate = _kernel.Today;
        string? resultMsg = null;
        try
        {
            resultMsg = handler.Execute(commandId) ?? "Xong.";
            StatusText.Text = resultMsg;
            if (!string.IsNullOrWhiteSpace(resultMsg) && !resultMsg.StartsWith("Lỗi", StringComparison.OrdinalIgnoreCase))
            {
                ShowGameToast("Thao Tác Hoàn Tất", resultMsg, "◈", (Brush)FindResource("Accent"));
            }
        }
        catch (Exception error)
        {
            StatusText.Text = Describe(error);
        }

        var toDate = _kernel.Today;
        if (toDate != fromDate)
        {
            _isAdvancing = true;
            SetAdvancingState(true);
            try
            {
                var daysDiff = (toDate.Year - fromDate.Year) * 360 + (toDate.Month - fromDate.Month) * 30 + (toDate.Day - fromDate.Day);
                await CalendarBar.AnimateCatchUpAsync(fromDate, toDate, $"Hoàn thành công việc (+{daysDiff} ngày)");
            }
            finally
            {
                SetAdvancingState(false);
                _isAdvancing = false;
            }
        }

        _kernel.Save();
        UpdateHeader();
        RenderSelected();
    }

    private string ScreenForCommand() =>
        NavList.SelectedItem is NavEntry entry ? entry.ScreenId : _screenId;

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new DlcSettingsWindow(_kernel) { Owner = this };
        settingsWindow.ShowDialog();
    }

    private void PlayerCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        OpenPlayerProfileModal();
    }

    public void ShowFloatingFeedback(string text, Brush brush, double startX = 200, double startY = 80)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = brush,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new TranslateTransform(0, 0)
        };

        Canvas.SetLeft(tb, startX);
        Canvas.SetTop(tb, startY);
        FloatingCanvas.Children.Add(tb);

        var moveAnim = new System.Windows.Media.Animation.DoubleAnimation(0, -36, TimeSpan.FromMilliseconds(850))
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };
        var fadeAnim = new System.Windows.Media.Animation.DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(850));

        fadeAnim.Completed += (_, _) => FloatingCanvas.Children.Remove(tb);

        if (tb.RenderTransform is TranslateTransform tt)
        {
            tt.BeginAnimation(TranslateTransform.YProperty, moveAnim);
        }
        tb.BeginAnimation(OpacityProperty, fadeAnim);
    }

    private DispatcherTimer? _toastTimer;

    public void ShowGameToast(string title, string message, string glyph = "⚡", Brush? accent = null)
    {
        ToastTitle.Text = title;
        ToastMessage.Text = message;
        ToastGlyph.Text = glyph;
        if (accent is not null)
        {
            ToastTitle.Foreground = accent;
            ToastBanner.BorderBrush = accent;
        }
        else
        {
            ToastTitle.Foreground = (Brush)FindResource("Accent");
            ToastBanner.BorderBrush = (Brush)FindResource("Accent");
        }

        ToastBanner.Visibility = Visibility.Visible;
        var slideIn = new System.Windows.Media.Animation.DoubleAnimation(-20, 0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };
        var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1.0, TimeSpan.FromMilliseconds(220));

        ToastTransform.BeginAnimation(TranslateTransform.YProperty, slideIn);
        ToastBanner.BeginAnimation(OpacityProperty, fadeIn);

        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2800) };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            var slideOut = new System.Windows.Media.Animation.DoubleAnimation(0, -20, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }
            };
            var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1.0, 0, TimeSpan.FromMilliseconds(180));
            fadeOut.Completed += (_, _) => ToastBanner.Visibility = Visibility.Collapsed;

            ToastTransform.BeginAnimation(TranslateTransform.YProperty, slideOut);
            ToastBanner.BeginAnimation(OpacityProperty, fadeOut);
        };
        _toastTimer.Start();
    }
}
