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
        CheckPendingHistoricalEventsAfterAdvance();
    }

    private async Task AdvanceAsync(int days, string? customStatus = null)
    {
        if (_isAdvancing) return;
        _isAdvancing = true;

        try
        {
            SetAdvancingState(true);
            Audio.GameAudio.Engine.PlaySfx("ui.turn");
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
            CheckPendingHistoricalEventsAfterAdvance();
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

        if (from.Month != to.Month)
        {
            var net = income - spend;
            var sign = net >= 0 ? "+" : "";
            ShowGameToast("TỔNG KẾT TÀI CHÍNH THÁNG", $"Tháng {from.Month} khép lại: Thu {income.ToString("N0", Vietnamese)} · Chi {spend.ToString("N0", Vietnamese)} · Ròng {sign}{net.ToString("N0", Vietnamese)} quan", "💰", (Brush)FindResource(net >= 0 ? "Gain" : "Loss"));
        }
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
            var cashBefore = _kernel.Ledger.Balance;
            resultMsg = handler.Execute(commandId) ?? "Xong.";
            PlayCommandAudio(commandId, cashBefore);
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
        if (toDate != fromDate)
        {
            CheckPendingHistoricalEventsAfterAdvance();
        }
    }

    private string ScreenForCommand() =>
        NavList.SelectedItem is NavEntry entry ? entry.ScreenId : _screenId;

    private void TradeRadar_Click(object sender, RoutedEventArgs e)
    {
        var compassWindow = new TradeCompassModalWindow(_kernel) { Owner = this };
        compassWindow.ShowDialog();
    }

    private void Ambitions_Click(object sender, RoutedEventArgs e)
    {
        var ambitionsWindow = new VictoryAmbitionsModalWindow(_kernel) { Owner = this };
        ambitionsWindow.ShowDialog();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new DlcSettingsWindow(_kernel) { Owner = this };
        settingsWindow.ShowDialog();
    }

    private void HotkeysGuide_Click(object sender, RoutedEventArgs e)
    {
        var guideWindow = new HotkeysGuideModalWindow { Owner = this };
        guideWindow.ShowDialog();
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
            NavigateByActionTag(targetScreen);
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
            NavigateByActionTag(targetScreen);
        }
    }

    private void ProposalDismiss_Click(object sender, RoutedEventArgs e)
    {
        TopProposalCard.Visibility = Visibility.Collapsed;
    }

    private void NavigateByActionTag(string tag)
    {
        if (tag.StartsWith("focus:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = tag.Split(':', 3);
            if (parts.Length == 3)
            {
                SelectScreenAndFocusSection(parts[1], parts[2]);
                return;
            }
        }

        SelectScreen(tag);
    }

    public void UpdateStrategicState()
    {
        UpdateTopProposalCard();

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

        // Screen-contextual strategy counsel
        switch (_screenId)
        {
            case "politics.court":
                StrategicAdvisorBox.AdvisorName = "MƯU SĨ PHỦ ĐỆ";
                StrategicAdvisorBox.AdvisorTitle = "Chính Lộ Tham Mưu";
                StrategicAdvisorBox.AvatarInitials = "Mưu";
                if (_kernel.Capabilities.TryGet<IPoliticalCourt>(CapabilityIds.PoliticalCourt, out var court) && court is not null)
                {
                    var demand = court.GetPendingExtortionDemands().FirstOrDefault();
                    var scrutiny = court.GetWarlordScrutiny();
                    if (demand is not null)
                    {
                        StrategicAdvisorBox.AdviceMessage = $"CẤP BÁO: {demand.WarlordName} đòi nộp {demand.AmountSilver:N0} Bạc trước ngày #{demand.DeadlineDay} ({demand.Reason}). Hãy phúc đáp ngay!";
                        StrategicAdvisorBox.ActionButtonLabel = "Nghi Kỵ";
                        StrategicAdvisorBox.Tag = "focus:politics.court:NGHI KỴ";
                        return;
                    }
                    if (scrutiny.ScrutinyScore >= 50)
                    {
                        StrategicAdvisorBox.AdviceMessage = $"Chư hầu {scrutiny.RulingFactionName} đang nghi kỵ ({scrutiny.ScrutinyScore}/100 - {scrutiny.RiskTier}). Nên hiến lương hoặc dựa thế phe phái!";
                        StrategicAdvisorBox.ActionButtonLabel = "Phe Phái";
                        StrategicAdvisorBox.Tag = "focus:politics.court:PHE PHÁI";
                        return;
                    }
                }
                StrategicAdvisorBox.AdviceMessage = "Tích lũy uy danh qua việc chẩn tế dân bần, kết giao Tam Đại Thế Lực và lập công vụ để triều đình cất nhắc phẩm hàm.";
                StrategicAdvisorBox.ActionButtonLabel = "Triều Đình";
                StrategicAdvisorBox.Tag = "politics.court";
                return;

            case "military.arena":
            case "military.strongholds":
                StrategicAdvisorBox.AdvisorName = "GIÁO ĐẦU QUÂN DOANH";
                StrategicAdvisorBox.AdvisorTitle = "Binh Cơ Chỉ Đạo";
                StrategicAdvisorBox.AvatarInitials = "Võ";
                StrategicAdvisorBox.AdviceMessage = "Khắc chế binh chủng là đạo thắng lợi: Kỵ khắc Bộ (+25%), Bộ khắc Cung (+25%), Cung khắc Kỵ (+25%).";
                StrategicAdvisorBox.ActionButtonLabel = "Tỷ Võ";
                StrategicAdvisorBox.Tag = "military.arena";
                return;

            case "cultivation.farm":
                StrategicAdvisorBox.AdvisorName = "ĐIỀN TRANG NÔNG ĐẦU";
                StrategicAdvisorBox.AdvisorTitle = "Nông Vụ Cố Vấn";
                StrategicAdvisorBox.AvatarInitials = "Nông";
                StrategicAdvisorBox.AdviceMessage = "Thời vụ bốn mùa: Xuân hạ gieo cấy, thu hoạch vào thu, tích trữ lương thảo đợi mùa giáp hạt giá cao.";
                StrategicAdvisorBox.ActionButtonLabel = "Điền Trang";
                StrategicAdvisorBox.Tag = "cultivation.farm";
                return;

            case "content.items":
            case "content.item_hub":
                StrategicAdvisorBox.AdvisorName = "BÁCH VẬT GIÁM ĐỊNH";
                StrategicAdvisorBox.AdvisorTitle = "Đồ Giám Tiên Sinh";
                StrategicAdvisorBox.AvatarInitials = "Giám";
                StrategicAdvisorBox.AdviceMessage = "Nắm rõ hạn sử dụng và địa lý đặc sản 24 loại kỳ vật để vận tải liên châu không lo hao hụt mục nát.";
                StrategicAdvisorBox.ActionButtonLabel = "Đồ Giám";
                StrategicAdvisorBox.Tag = "content.item_hub";
                return;

            case "player.reputation":
                StrategicAdvisorBox.AdvisorName = "DANH SĨ HUYỀN ĐÀM";
                StrategicAdvisorBox.AdvisorTitle = "Thanh Nghị Luận Danh";
                StrategicAdvisorBox.AvatarInitials = "Danh";
                StrategicAdvisorBox.AdviceMessage = "Thi ân bố đức cứu tế dân nghèo, kết giao hào kiệt bốn phương để nâng cấp vị thế trong bảng Phong Thần.";
                StrategicAdvisorBox.ActionButtonLabel = "Uy Danh";
                StrategicAdvisorBox.Tag = "player.reputation";
                return;

            case "caravan.dispatch":
            case "caravan.routes":
                StrategicAdvisorBox.AdvisorName = "TIÊU CỤC TỔNG TIÊU";
                StrategicAdvisorBox.AdvisorTitle = "Hành Trình Hộ Tiêu";
                StrategicAdvisorBox.AvatarInitials = "Tiêu";
                StrategicAdvisorBox.AdviceMessage = "Đường xa nhiều đạo tặc, hãy điều phối hộ vệ tinh nhuệ và nâng cấp xe thồ để tối ưu chuyến tải.";
                StrategicAdvisorBox.ActionButtonLabel = "Thương Đoàn";
                StrategicAdvisorBox.Tag = "caravan.routes";
                return;

            case "professions.trades":
                StrategicAdvisorBox.AdvisorName = "BÁCH NGHỆ TỔ SƯ";
                StrategicAdvisorBox.AdvisorTitle = "Thợ Cả Phường Hội";
                StrategicAdvisorBox.AvatarInitials = "Nghệ";
                StrategicAdvisorBox.AdviceMessage = "Rèn luyện tay nghề nâng bậc, chế tác thần binh hộ giáp và hoàn thành ủy thác thương hội để nhận bổng lộc.";
                StrategicAdvisorBox.ActionButtonLabel = "Chế Tác";
                StrategicAdvisorBox.Tag = "professions.trades";
                return;

            case "npc.directory":
                StrategicAdvisorBox.AdvisorName = "NGUYỆT ĐÁN BÌNH SĨ";
                StrategicAdvisorBox.AdvisorTitle = "Hào Kiệt Giám Định";
                StrategicAdvisorBox.AvatarInitials = "Nhân";
                if (_kernel.Capabilities.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social) && social is not null)
                {
                    var risky = social.GetRetainers().OrderByDescending(r => r.BetrayalRiskPercent).FirstOrDefault(r => r.BetrayalRiskPercent >= 35 || r.Loyalty < 55);
                    if (risky is not null)
                    {
                        StrategicAdvisorBox.AdviceMessage = $"Gia thần {risky.Name} đang dao động (Trung thành {risky.Loyalty}/100 · Phản tâm {risky.BetrayalRiskPercent}%). Hãy ban thưởng trấn an ngay!";
                        StrategicAdvisorBox.ActionButtonLabel = "Trấn An";
                        StrategicAdvisorBox.Tag = "focus:npc.directory:GIA THẦN";
                        return;
                    }
                }
                StrategicAdvisorBox.AdviceMessage = "Thiên hạ anh hùng quy về một mối. Thăm hỏi và tặng quà hợp sở thích để chiêu mộ danh sĩ làm gia thần.";
                StrategicAdvisorBox.ActionButtonLabel = "Dạo Phố";
                StrategicAdvisorBox.Tag = CitySceneScreenId;
                return;
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

    private void UpdateTopProposalCard()
    {
        if (TopProposalCard is null) return;

        // 1. Check urgent Warlord Extortion Demand or High Scrutiny (Crisis / Alert)
        if (_kernel.Capabilities.TryGet<IPoliticalCourt>(CapabilityIds.PoliticalCourt, out var court) && court is not null)
        {
            var demand = court.GetPendingExtortionDemands().FirstOrDefault();
            if (demand is not null)
            {
                TopProposalCard.Severity = ProposalSeverity.Crisis;
                TopProposalCard.SourceTitle = $"TỐI HẬU THƯ CHƯ HẦU · {demand.WarlordName.ToUpperInvariant()}";
                TopProposalCard.SummaryText = $"Đòi cống nạp {demand.AmountSilver:N0} Bạc trước ngày #{demand.DeadlineDay} ({demand.Reason}). Nếu kháng lệnh sẽ bị khám xét tịch biên!";
                TopProposalCard.PrimaryActionLabel = "Ứng Phó Ngay";
                TopProposalCard.Tag = "focus:politics.court:NGHI KỴ";
                TopProposalCard.Visibility = Visibility.Visible;
                return;
            }

            var scrutiny = court.GetWarlordScrutiny();
            if (scrutiny.ScrutinyScore >= 65)
            {
                TopProposalCard.Severity = ProposalSeverity.Alert;
                TopProposalCard.SourceTitle = $"NGHI KỴ CHƯ HẦU ({scrutiny.ScrutinyScore}/100) · {scrutiny.RulingFactionName.ToUpperInvariant()}";
                TopProposalCard.SummaryText = $"Mức độ nghi kỵ đã lên cấp {scrutiny.RiskTier} (Tài sản bị dòm ngó: {scrutiny.EstimatedWealthSilver:N0} Bạc). Hãy hiến lương thảo hoặc tìm phe phái che chở.";
                TopProposalCard.PrimaryActionLabel = "Giải Trừ Nghi Kỵ";
                TopProposalCard.Tag = "focus:politics.court:NGHI KỴ";
                TopProposalCard.Visibility = Visibility.Visible;
                return;
            }
        }

        // 2. Check Retainer Betrayal Risk (Alert)
        if (_kernel.Capabilities.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social) && social is not null)
        {
            var risky = social.GetRetainers()
                .OrderByDescending(r => r.BetrayalRiskPercent)
                .FirstOrDefault(r => r.BetrayalRiskPercent >= 40 || r.Loyalty < 50);
            if (risky is not null)
            {
                TopProposalCard.Severity = ProposalSeverity.Alert;
                TopProposalCard.SourceTitle = $"CẢNH BÁO PHẢN TÂM · GIA THẦN {risky.Name.ToUpperInvariant()}";
                TopProposalCard.SummaryText = $"Lòng trung thành giảm còn {risky.Loyalty}/100 (Nguy cơ phản bội {risky.BetrayalRiskPercent}%). Cần ban thưởng hoặc ủy lạo trước khi mật tin bị bán cho đối thủ.";
                TopProposalCard.PrimaryActionLabel = "Trấn An Gia Thần";
                TopProposalCard.Tag = "focus:npc.directory:GIA THẦN";
                TopProposalCard.Visibility = Visibility.Visible;
                return;
            }
        }

        // 3. Check High-Margin Contraband Smuggling Opportunity in current city (Opportunity)
        if (_kernel.Capabilities.TryGet<IEconomicWarfare>(CapabilityIds.EconomicWarfare, out var warfare) && warfare is not null &&
            _kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var player) && player is not null)
        {
            var route = warfare.GetActiveContrabandRoutes()
                .FirstOrDefault(r => r.OriginCityId == player.CurrentCityId || r.DestinationCityId == player.CurrentCityId);
            if (route is not null)
            {
                TopProposalCard.Severity = ProposalSeverity.Opportunity;
                TopProposalCard.SourceTitle = $"MẬT BÁO BUÔN LẬU · {route.ItemName.ToUpperInvariant()}";
                TopProposalCard.SummaryText = $"Tuyến chiến sự {route.OriginCityName} → {route.DestinationCityName} đang khan hiếm quốc cấm, giá chợ đen tăng vọt x{(route.WarPriceMultiplierBps / 10000.0):0.0} (Tuần tra {route.PatrolRiskPercent}%).";
                TopProposalCard.PrimaryActionLabel = "Mở Sạp Chợ Đen";
                TopProposalCard.Tag = "focus:market.stall:BUÔN LẬU";
                TopProposalCard.Visibility = Visibility.Visible;
                return;
            }
        }

        TopProposalCard.Visibility = Visibility.Collapsed;
    }
}
