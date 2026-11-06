using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SimT.Contracts;
using SimT.Contracts.Ledger;
using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;
using SimT.Contracts.Time;
using SimT.Host.Wpf.Controls;

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

        ShowFloatingFeedback("Tĩnh dưỡng hồi phục!", (Brush)FindResource("Gain"), 180, 50);
        await AdvanceAsync(1, "Đã nghỉ ngơi tĩnh dưỡng 1 ngày: Thể lực hồi phục trọn vẹn, tinh thần sảng khoái.");
    }

    private async void AdvanceWeek_Click(object sender, RoutedEventArgs e) => await AdvanceAsync(7);

    private void Stroll_Click(object sender, RoutedEventArgs e) => TriggerCityStroll(preferModalWhenOutsideScene: true);

    private void TriggerCityStroll(bool preferModalWhenOutsideScene = false)
    {
        if (_isAdvancing) return;

        if (_kernel.Capabilities.TryGet<ICityStroll>(CapabilityIds.CityStroll, out var stroll) && stroll is not null &&
            _kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var player) && player is not null)
        {
            if (preferModalWhenOutsideScene && !SceneOverlay.IsVisible)
            {
                var modal = new CityStrollModalWindow(_kernel, stroll, player.CurrentCityId) { Owner = this };
                modal.StrollStateChanged += () =>
                {
                    _kernel.Save();
                    UpdateHeader();
                    RenderSelected();
                };
                modal.ShowDialog();
                _kernel.Save();
                UpdateHeader();
                RenderSelected();
                return;
            }

            var result = stroll.Stroll(player.CurrentCityId);
            _kernel.Save();
            UpdateHeader();

            if (result.Ok)
            {
                RenderSelected();
                ShowNotification($"[{result.LocationName} · {result.Title}] {result.Narrative} » {result.RewardSummary}");
                PresentStrollResultInCityScene(result);
            }
            else
            {
                ShowNotification(result.Narrative, GameToastKind.Loss);
            }
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
        InvalidateBadges();
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
            InvalidateBadges();
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

            ShowNotification(customStatus ?? $"Đã sang {GameDateCodec.DescribeVi(_kernel.Today)} (+{days} ngày).");
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
        string? resultMsg;
        try
        {
            resultMsg = handler.Execute(commandId) ?? "Xong.";
            ShowNotification(resultMsg);
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
        InvalidateBadges();
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

    public void ShowGameToast(string title, string message, string glyph = "", Brush? accent = null)
    {
        _ = glyph;
        _ = accent;
        ShowNotification($"{title}: {message}");
    }

    private void AdvisorAction_Click(object sender, RoutedEventArgs e)
    {
        if (StrategicAdvisorBox.Tag is "rest")
        {
            Rest_Click(sender, e);
        }
        else if (StrategicAdvisorBox.Tag is string targetScreen && !string.IsNullOrEmpty(targetScreen))
        {
            SelectScreen(targetScreen);
        }
        else
        {
            SelectScreen("market.stall");
        }
    }

    private void ProposalAction_Click(object sender, RoutedEventArgs e)
    {
        if (TopProposalCard.Tag is string targetScreen && !string.IsNullOrEmpty(targetScreen))
        {
            SelectScreen(targetScreen);
        }
    }

    private void ProposalDismiss_Click(object sender, RoutedEventArgs e)
    {
        TopProposalCard.Visibility = Visibility.Collapsed;
    }

    public void UpdateStrategicState()
    {
        if (StrategicAdvisorBox is null) return;

        // Check stamina / vitals
        if (_kernel.Capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) && attrs?.Vitals is not null)
        {
            var v = attrs.Vitals;
            if (v.CurrentStamina < 25 || v.Fatigue > 65)
            {
                StrategicAdvisorBox.AdvisorName = "QUẢN GIA THƯƠNG HỘI";
                StrategicAdvisorBox.AdvisorTitle = "Cảnh Báo Sức Khỏe";
                StrategicAdvisorBox.AvatarInitials = "Y";
                StrategicAdvisorBox.AdviceMessage = "Đại nhân lao lực quá độ, thể lực suy kiệt. Xin hãy nghỉ ngơi hoặc tản bộ thư giãn, tránh gặp nguy khi xuất hành!";
                StrategicAdvisorBox.ActionButtonLabel = "Nghỉ Ngơi";
                StrategicAdvisorBox.Tag = "rest";
                return;
            }
        }

        // Check balance
        var liquid = _kernel.Ledger.Liquid;
        if (liquid > 15000)
        {
            StrategicAdvisorBox.AdvisorName = "THƯƠNG HỘI TỔNG QUẢN";
            StrategicAdvisorBox.AdvisorTitle = "Kế Sách Đầu Tư";
            StrategicAdvisorBox.AvatarInitials = "Phú";
            StrategicAdvisorBox.AdviceMessage = $"Ngân lượng lưu động đạt {liquid:N0} quan! Đại nhân nên xem xét cấp vốn cho Thương Đoàn hoặc thâu tóm thêm Điền Trang.";
            StrategicAdvisorBox.ActionButtonLabel = "Thương Đoàn";
            StrategicAdvisorBox.Tag = "caravan.routes";
            return;
        }

        // Default strategy counsel
        StrategicAdvisorBox.AdvisorName = "QUẢN GIA THƯƠNG HỘI";
        StrategicAdvisorBox.AdvisorTitle = "Túc Trí Tham Mưu";
        StrategicAdvisorBox.AvatarInitials = "Sư";
        StrategicAdvisorBox.AdviceMessage = "Thương trường Tam Quốc biến động theo thiên thời địa lợi. Khảo sát giá cả các thành trì sẽ mở ra cơ hội sinh lời lớn.";
        StrategicAdvisorBox.ActionButtonLabel = "Khảo Sát Chợ";
        StrategicAdvisorBox.Tag = "market.stall";
    }
}
