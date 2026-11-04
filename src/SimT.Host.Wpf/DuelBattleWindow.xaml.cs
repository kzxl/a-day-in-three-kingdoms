using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SimT.Contracts.Playing;

namespace SimT.Host.Wpf;

public partial class DuelBattleWindow : Window
{
    private readonly IMilitaryCommand _military;
    private readonly INpcRegistry _registry;
    private readonly IPlayerAttributes _attributes;
    private readonly IPlayerState _playerState;
    private readonly IWallet _wallet;
    private readonly NpcDef _npc;
    private readonly string _citadelName;

    private TroopBranch _playerBranch = TroopBranch.Infantry;
    private TroopBranch _opponentBranch = TroopBranch.Infantry;
    private DuelResult? _duelResult;
    private int _currentRoundIndex = -1;
    private readonly DispatcherTimer _timer;
    private bool _isAutoPlaying = false;
    private int _speedMultiplier = 1;

    public DuelBattleWindow(
        IMilitaryCommand military,
        INpcRegistry registry,
        IPlayerAttributes attributes,
        IPlayerState playerState,
        IWallet wallet,
        long npcId,
        string citadelName)
    {
        InitializeComponent();

        _military = military;
        _registry = registry;
        _attributes = attributes;
        _playerState = playerState;
        _wallet = wallet;
        _citadelName = citadelName;

        _npc = _registry.Find(npcId)
               ?? throw new ArgumentException($"Không tìm thấy võ sĩ mã {npcId}.");

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1200)
        };
        _timer.Tick += Timer_Tick;

        InitDuelStage(citadelName);
    }

    private void InitDuelStage(string citadelName)
    {
        CitadelArenaTitle.Text = $"Diễn Võ Đài · {citadelName}";
        PlayerNameText.Text = _playerState.Name;
        PlayerTitleText.Text = $"Võ Lực: {_attributes.Attributes.War} · Căn Cốt: {_attributes.Attributes.Con}";

        OpponentNameText.Text = _npc.NameVi;
        OpponentRoleText.Text = _npc.Role;
        OpponentAvatarCharText.Text = !string.IsNullOrEmpty(_npc.NameVi) ? _npc.NameVi[..1] : "武";
        OpponentWarText.Text = $"Võ Lực: {_npc.Attributes.War} · Căn Cốt: {_npc.Attributes.Con}";

        _opponentBranch = _military.ResolveNpcBranch(_npc.Id);
        OpponentBranchBadge.Text = $"{TroopBranchHelper.Icon(_opponentBranch)} {TroopBranchHelper.NameVi(_opponentBranch)}";

        UpdateMatchupBanner();

        // Starting HP and Spirit
        var playerMaxHp = 100 + (_attributes.Attributes.Con / 2);
        var opponentMaxHp = 100 + (_npc.Attributes.Con / 2);

        PlayerHpBar.Maximum = playerMaxHp;
        PlayerHpBar.Value = playerMaxHp;
        PlayerHpLabel.Text = $"HP: {playerMaxHp} / {playerMaxHp}";

        OpponentHpBar.Maximum = opponentMaxHp;
        OpponentHpBar.Value = opponentMaxHp;
        OpponentHpLabel.Text = $"HP: {opponentMaxHp} / {opponentMaxHp}";

        PlayerSpiritBar.Value = _attributes.Attributes.Wil >= 80 ? 10 : 0;
        PlayerSpiritLabel.Text = $"Khí Lực: {PlayerSpiritBar.Value} / 100";

        OpponentSpiritBar.Value = _npc.Attributes.Wil >= 80 ? 10 : 0;
        OpponentSpiritLabel.Text = $"Khí Lực: {OpponentSpiritBar.Value} / 100";

        RoundCounterText.Text = "CHUẨN BỊ";
        DuelStatusText.Text = "Sẵn sàng giao chiến";
        ClashBannerText.Text = "QUÂN CƠ DIỄN VÕ";
        ClashNoticeText.Text = "Chọn Binh Chủng của bạn rồi bấm '▶ Tự Động Đấu' hoặc 'Hiệp Kế'!";
    }

    private void UpdateMatchupBanner()
    {
        MatchupSummaryText.Text = TroopBranchHelper.MatchupSummaryVi(_playerBranch, _opponentBranch);
        if (TroopBranchHelper.Counters(_playerBranch, _opponentBranch))
        {
            MatchupBadgeBorder.BorderBrush = (Brush)FindResource("Gain");
            MatchupIcon.Text = "⚡";
        }
        else if (TroopBranchHelper.Counters(_opponentBranch, _playerBranch))
        {
            MatchupBadgeBorder.BorderBrush = (Brush)FindResource("Loss");
            MatchupIcon.Text = "⚠️";
        }
        else
        {
            MatchupBadgeBorder.BorderBrush = (Brush)FindResource("Accent");
            MatchupIcon.Text = "⚔️";
        }
    }

    private void PlayerBranchRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (PlayerBranchInfantryRadio.IsChecked == true) _playerBranch = TroopBranch.Infantry;
        else if (PlayerBranchCavalryRadio.IsChecked == true) _playerBranch = TroopBranch.Cavalry;
        else if (PlayerBranchArchersRadio.IsChecked == true) _playerBranch = TroopBranch.Archers;

        UpdateMatchupBanner();
    }

    private void EnsureDuelExecuted()
    {
        if (_duelResult is not null) return;

        // Lock branch selector once combat starts
        PlayerBranchInfantryRadio.IsEnabled = false;
        PlayerBranchCavalryRadio.IsEnabled = false;
        PlayerBranchArchersRadio.IsEnabled = false;

        _duelResult = _military.ChallengeArena(_npc.Id, _playerBranch);
    }

    private void AutoPlayButton_Click(object sender, RoutedEventArgs e)
    {
        EnsureDuelExecuted();

        if (_isAutoPlaying)
        {
            _timer.Stop();
            _isAutoPlaying = false;
            AutoPlayButton.Content = "▶ Tiếp Tục Tự Động";
            DuelStatusText.Text = "Đang tạm dừng";
        }
        else
        {
            _isAutoPlaying = true;
            AutoPlayButton.Content = "⏸ Tạm Dừng";
            DuelStatusText.Text = "Đang tự động giao tranh";
            _timer.Start();
        }
    }

    private void NextRoundButton_Click(object sender, RoutedEventArgs e)
    {
        EnsureDuelExecuted();

        if (_isAutoPlaying)
        {
            _timer.Stop();
            _isAutoPlaying = false;
            AutoPlayButton.Content = "▶ Tự Động Đấu (Auto-Play)";
        }

        AdvanceNextRound();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        AdvanceNextRound();
    }

    private void AdvanceNextRound()
    {
        if (_duelResult?.RoundLogs is null || _duelResult.RoundLogs.Count == 0)
        {
            ConcludeDuel();
            return;
        }

        _currentRoundIndex++;

        if (_currentRoundIndex >= _duelResult.RoundLogs.Count)
        {
            ConcludeDuel();
            return;
        }

        var roundLog = _duelResult.RoundLogs[_currentRoundIndex];
        RenderRound(roundLog);

        if (_currentRoundIndex == _duelResult.RoundLogs.Count - 1)
        {
            ConcludeDuel();
        }
    }

    private void RenderRound(DuelRoundLog log)
    {
        RoundCounterText.Text = $"HIỆP {log.RoundNumber} / {_duelResult?.RoundLogs?.Count ?? 5}";
        DuelStatusText.Text = log.IsCritical ? "BẠO KÍCH!" : (log.IsParried ? "HÓA GIẢI!" : "GIAO PHONG");

        // Center clash banner & pulse animation
        ClashBannerText.Text = log.IsCritical ? "💥 BẠO KÍCH KINH HOÀNG!" : (log.IsParried ? "🛡️ HÓA GIẢI PHẢN KÍCH!" : "⚔️ SONG HÙNG TƯƠNG ĐỐI");
        ClashNoticeText.Text = $"Bạn gây {log.PlayerDamageDealt} ST · Đối thủ đánh trả {log.OpponentDamageDealt} ST";

        // Action badges
        PlayerActionIcon.Text = TacticalDuelActionHelper.Icon(log.PlayerAction);
        PlayerActionText.Text = TacticalDuelActionHelper.NameVi(log.PlayerAction);

        OpponentActionIcon.Text = TacticalDuelActionHelper.Icon(log.OpponentAction);
        OpponentActionText.Text = TacticalDuelActionHelper.NameVi(log.OpponentAction);

        // Floating Damage Popup
        PlayerDamagePopup.Text = log.OpponentDamageDealt > 0 ? $"-{log.OpponentDamageDealt}" : (log.IsParried ? "ĐỠ!" : "");
        OpponentDamagePopup.Text = log.PlayerDamageDealt > 0 ? $"-{log.PlayerDamageDealt}" : (log.IsParried ? "ĐỠ!" : "");

        // Animate HP Bar and Spirit Bar
        AnimateProgressBar(PlayerHpBar, log.PlayerHpAfter);
        PlayerHpLabel.Text = $"HP: {log.PlayerHpAfter} / {_duelResult?.PlayerMaxHp ?? 100}";

        AnimateProgressBar(OpponentHpBar, log.OpponentHpAfter);
        OpponentHpLabel.Text = $"HP: {log.OpponentHpAfter} / {_duelResult?.OpponentMaxHp ?? 100}";

        PlayerSpiritBar.Value = log.PlayerSpiritAfter;
        PlayerSpiritLabel.Text = $"Khí Lực: {log.PlayerSpiritAfter} / 100";

        OpponentSpiritBar.Value = log.OpponentSpiritAfter;
        OpponentSpiritLabel.Text = $"Khí Lực: {log.OpponentSpiritAfter} / 100";

        // Clash symbol scale animation
        var anim = new DoubleAnimation(1.35, 1.0, TimeSpan.FromMilliseconds(350));
        ClashScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
        ClashScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, anim);

        // Add to Narrative Chronicle
        AppendChronicleEntry(log);
    }

    private void AnimateProgressBar(ProgressBar bar, double targetValue)
    {
        var anim = new DoubleAnimation(bar.Value, targetValue, TimeSpan.FromMilliseconds(400))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        bar.BeginAnimation(ProgressBar.ValueProperty, anim);
    }

    private void AppendChronicleEntry(DuelRoundLog log)
    {
        var border = new Border
        {
            Background = (Brush)FindResource("SurfaceAlt"),
            BorderBrush = (Brush)FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 8)
        };

        var stack = new StackPanel();

        var header = new StackPanel { Orientation = Orientation.Horizontal };
        var roundTag = new TextBlock
        {
            Text = $"HIỆP {log.RoundNumber}",
            FontWeight = FontWeights.Bold,
            FontSize = 11.5,
            Foreground = (Brush)FindResource("Accent"),
            Margin = new Thickness(0, 0, 8, 0)
        };
        var actionTag = new TextBlock
        {
            Text = $"[Bạn: {TacticalDuelActionHelper.NameVi(log.PlayerAction)}] vs [{_npc.NameVi}: {TacticalDuelActionHelper.NameVi(log.OpponentAction)}]",
            FontSize = 11.5,
            Foreground = (Brush)FindResource("Muted")
        };
        header.Children.Add(roundTag);
        header.Children.Add(actionTag);
        stack.Children.Add(header);

        var body = new TextBlock
        {
            Text = log.NarrativeVi,
            FontSize = 12.5,
            Foreground = (Brush)FindResource("Text"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        };
        stack.Children.Add(body);

        border.Child = stack;
        ChroniclePanel.Children.Add(border);
        ChronicleScrollViewer.ScrollToEnd();
    }

    private void SkipButton_Click(object sender, RoutedEventArgs e)
    {
        EnsureDuelExecuted();
        _timer.Stop();
        _isAutoPlaying = false;

        if (_duelResult?.RoundLogs is not null)
        {
            // Catch up all unrendered rounds into the chronicle
            while (_currentRoundIndex < _duelResult.RoundLogs.Count - 1)
            {
                _currentRoundIndex++;
                var round = _duelResult.RoundLogs[_currentRoundIndex];
                AppendChronicleEntry(round);
            }

            var lastLog = _duelResult.RoundLogs[^1];
            RenderRound(lastLog);
        }

        ConcludeDuel();
    }

    private void ConcludeDuel()
    {
        _timer.Stop();
        _isAutoPlaying = false;
        AutoPlayButton.IsEnabled = false;
        NextRoundButton.IsEnabled = false;
        SkipButton.IsEnabled = false;

        if (_duelResult is null) return;

        if (_duelResult.Won)
        {
            ClashBannerText.Text = "🏆 ĐẠI THẮNG QUÁN QUÂN LÔI ĐÀI!";
            ClashBannerBorder.Background = (Brush)FindResource("AccentSoft");
            ClashBannerBorder.BorderBrush = (Brush)FindResource("Gain");

            FooterNoticeText.Text = $"🏆 ĐẠI THẮNG! Bạn nhận được {_duelResult.GoldReward} Quan (+35 Uy Danh, +30 Võ Lực EXP, +15 Thống Soái EXP)!";
            _attributes.GainFame(35, $"Đại thắng võ tướng {_npc.NameVi} tại lôi đài {_citadelName}.");
            FooterNoticeText.Foreground = (Brush)FindResource("Gain");

            CloseButton.Content = "🏆 Hoàn Tất & Nhận Thưởng";
            CloseButton.Style = (Style)FindResource("PrimaryButton");
        }
        else
        {
            ClashBannerText.Text = "⚔️ BẠI TRẬN TRÊN ĐÀI VÕ";
            ClashBannerBorder.Background = (Brush)FindResource("SurfaceAlt");
            ClashBannerBorder.BorderBrush = (Brush)FindResource("Loss");

            FooterNoticeText.Text = $"⚔️ BẠI TRẬN! Đã học hỏi được kinh nghiệm giao chiến (+5 Uy Danh, +15 Võ Lực EXP).";
            _attributes.GainFame(5, $"Kiên cường giao đấu cùng võ tướng {_npc.NameVi} tại lôi đài {_citadelName}.");
            FooterNoticeText.Foreground = (Brush)FindResource("Muted");

            CloseButton.Content = "🚪 Rời Lôi Đài";
            CloseButton.ClearValue(FrameworkElement.StyleProperty);
        }
    }

    private void SpeedButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && int.TryParse(tag, out var mult))
        {
            _speedMultiplier = mult;
            _timer.Interval = mult switch
            {
                2 => TimeSpan.FromMilliseconds(600),
                4 => TimeSpan.FromMilliseconds(250),
                _ => TimeSpan.FromMilliseconds(1200)
            };

            Speed1xButton.Style = (Style)FindResource(mult == 1 ? "PrimaryButton" : "ChipButton");
            Speed2xButton.Style = (Style)FindResource(mult == 2 ? "PrimaryButton" : "ChipButton");
            Speed4xButton.Style = (Style)FindResource(mult == 4 ? "PrimaryButton" : "ChipButton");
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        DialogResult = true;
        Close();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _timer.Stop();
            Close();
            e.Handled = true;
        }
        else if (e.Key == Key.Space)
        {
            AutoPlayButton_Click(AutoPlayButton, new RoutedEventArgs());
            e.Handled = true;
        }
    }
}
