using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SimT.Contracts.Playing;
using SimT.Core.Kernel;

namespace SimT.Host.Wpf;

public partial class BanditBattleWindow : HubModalWindowBase
{
    private readonly SimKernel _kernel;
    private readonly IMilitaryCommand _military;
    private readonly long _strongholdId;
    private readonly TroopBranch _playerBranch;
    private readonly string _citadelName;

    private readonly BanditExpeditionResult _result;
    private int _currentRoundIndex;
    private bool _battleEnded;

    public BanditBattleWindow(
        SimKernel kernel,
        IMilitaryCommand military,
        long strongholdId,
        TroopBranch playerBranch,
        string citadelName)
    {
        InitializeComponent();

        _kernel = kernel;
        _military = military;
        _strongholdId = strongholdId;
        _playerBranch = playerBranch;
        _citadelName = citadelName;

        // Execute expedition to commit game state (gold, loot, date advance, vitals)
        _result = _military.ExecuteBanditExpedition(_strongholdId, _playerBranch);

        SetupInitialStage();
    }

    private void SetupInitialStage()
    {
        var sh = _result.Stronghold;
        var chief = _result.Chief;

        if (_kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map) && map is not null)
        {
            var cit = map.Find(sh.CitadelId);
            if (cit != null) StrongholdTitle.Text = $"{sh.NameVi} · {cit.NameVi}";
        }

        // Top Matchup Banner
        MatchupIcon.Text = TroopBranchHelper.Counters(_playerBranch, chief.Branch) ? "⚡" : (TroopBranchHelper.Counters(chief.Branch, _playerBranch) ? "⚠️" : "⚔️");
        MatchupSummaryText.Text = TroopBranchHelper.MatchupSummaryVi(_playerBranch, chief.Branch);
        MatchupBadgeBorder.BorderBrush = TroopBranchHelper.Counters(_playerBranch, chief.Branch)
            ? (Brush)FindResource("Gain")
            : (TroopBranchHelper.Counters(chief.Branch, _playerBranch) ? (Brush)FindResource("Loss") : (Brush)FindResource("Accent"));

        RoundCounterText.Text = $"HIỆP 1 / {_result.Rounds}";
        ExpeditionStatusText.Text = "Hai bên giáp chiến...";

        // Player Column
        if (_kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var playerState) && playerState is not null)
        {
            PlayerNameText.Text = playerState.Name;
        }
        PlayerBranchBadge.Text = $"{TroopBranchHelper.Icon(_playerBranch)} {TroopBranchHelper.NameVi(_playerBranch)}";

        PlayerTroopBar.Maximum = _result.PlayerInitialTroops;
        PlayerTroopBar.Value = _result.PlayerInitialTroops;
        PlayerTroopGhostBar.Maximum = _result.PlayerInitialTroops;
        PlayerTroopGhostBar.Value = _result.PlayerInitialTroops;
        PlayerTroopValueText.Text = $"{_result.PlayerInitialTroops:N0} / {_result.PlayerInitialTroops:N0}";

        var pMaxHp = 100;
        if (_kernel.Capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) && attrs is not null)
        {
            pMaxHp = 100 + (attrs.Attributes.Con / 2);
        }
        PlayerHpBar.Maximum = pMaxHp;
        PlayerHpBar.Value = pMaxHp;
        PlayerHpValueText.Text = $"{pMaxHp} / {pMaxHp}";

        // Bandit Column
        ChiefAvatarText.Text = chief.AvatarChar;
        ChiefNameText.Text = chief.NameVi;
        ChiefRoleText.Text = chief.TitleVi;
        ChiefBranchBadge.Text = $"{TroopBranchHelper.Icon(chief.Branch)} {TroopBranchHelper.NameVi(chief.Branch)}";

        BanditTroopBar.Maximum = _result.BanditInitialTroops;
        BanditTroopBar.Value = _result.BanditInitialTroops;
        BanditTroopGhostBar.Maximum = _result.BanditInitialTroops;
        BanditTroopGhostBar.Value = _result.BanditInitialTroops;
        BanditTroopValueText.Text = $"{_result.BanditInitialTroops:N0} / {_result.BanditInitialTroops:N0}";

        ChiefHpBar.Maximum = chief.MaxHp;
        ChiefHpBar.Value = chief.MaxHp;
        ChiefHpValueText.Text = $"{chief.MaxHp} / {chief.MaxHp}";
        ChiefSkillCalloutText.Text = $"⛰ Địa hình: {chief.TerrainNameVi} · {TroopBranchHelper.NameVi(chief.Branch)}";

        // Play initial round
        PlayNextRound();
    }

    private void PlayNextRound()
    {
        if (_currentRoundIndex >= _result.RoundLogs.Count)
        {
            ConcludeBattle();
            return;
        }

        var log = _result.RoundLogs[_currentRoundIndex];
        _currentRoundIndex++;

        // Update Headers
        RoundCounterText.Text = $"HIỆP {log.RoundNumber} / {_result.Rounds}";
        ClashPhaseTitle.Text = $"HIỆP {log.RoundNumber} GIÁP CHIẾN";

        // Animate Bars with Ghosting
        AnimateBar(PlayerTroopBar, log.PlayerTroopsRemaining);
        AnimateGhostBar(PlayerTroopGhostBar, log.PlayerTroopsRemaining);
        PlayerTroopValueText.Text = $"{log.PlayerTroopsRemaining:N0} / {_result.PlayerInitialTroops:N0}";

        AnimateBar(PlayerHpBar, log.PlayerHpRemaining);
        PlayerHpValueText.Text = $"{log.PlayerHpRemaining} / {PlayerHpBar.Maximum}";

        AnimateBar(BanditTroopBar, log.BanditTroopsRemaining);
        AnimateGhostBar(BanditTroopGhostBar, log.BanditTroopsRemaining);
        BanditTroopValueText.Text = $"{log.BanditTroopsRemaining:N0} / {_result.BanditInitialTroops:N0}";

        AnimateBar(ChiefHpBar, log.BanditHpRemaining);
        ChiefHpValueText.Text = $"{log.BanditHpRemaining} / {_result.Chief.MaxHp}";

        // Clashing Thrust Animation
        AnimateThrust(PlayerAvatarTransform, 14.0);
        AnimateThrust(ChiefAvatarTransform, -14.0);

        // Floating Damage Feedback
        if (log.PlayerDamageDealt > 0)
        {
            SpawnFloatingCombatNumber($"-{log.PlayerDamageDealt:N0} ⚔️", (Brush)FindResource("Gain"), x: 620, y: 80);
        }
        if (log.BanditDamageDealt > 0)
        {
            SpawnFloatingCombatNumber($"-{log.BanditDamageDealt:N0} 🛡️", (Brush)FindResource("Loss"), x: 120, y: 80);
        }

        // Skill Activation & Screen Shake
        if (!string.IsNullOrEmpty(log.SkillTriggeredVi))
        {
            TriggerScreenShake(5.0);
            SpawnFloatingCombatNumber($"⚡ {log.SkillTriggeredVi}", (Brush)FindResource("Accent"), x: 340, y: 35, isSkill: true);
        }

        // Narrative Display
        NarrativeDisplayText.Text = log.NarrativeVi;

        // Feed log entry
        var entryBorder = new Border
        {
            Background = (Brush)FindResource("Surface"),
            BorderBrush = (Brush)FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 0, 6)
        };

        var stack = new StackPanel();
        var header = new TextBlock
        {
            Text = $"Hiệp {log.RoundNumber}: Diệt {log.PlayerDamageDealt:N0} giặc | Mất {log.BanditDamageDealt:N0} quân",
            FontWeight = FontWeights.Bold,
            FontSize = 11,
            Foreground = (Brush)FindResource("Text")
        };
        stack.Children.Add(header);

        if (!string.IsNullOrEmpty(log.SkillTriggeredVi))
        {
            var skillText = new TextBlock
            {
                Text = $"⚡ {log.SkillTriggeredVi}",
                FontSize = 10.5,
                Foreground = (Brush)FindResource("Accent"),
                TextWrapping = TextWrapping.Wrap
            };
            stack.Children.Add(skillText);
        }

        entryBorder.Child = stack;
        CombatFeedStack.Children.Add(entryBorder);
        CombatFeedScroll.ScrollToEnd();

        // Check if last round
        if (_currentRoundIndex >= _result.RoundLogs.Count || log.IsDecisive)
        {
            ConcludeBattle();
        }
    }

    private void ConcludeBattle()
    {
        _battleEnded = true;

        NextRoundBtn.Visibility = Visibility.Collapsed;
        AutoResolveBtn.Visibility = Visibility.Collapsed;

        CloseBtn.Content = _result.Won ? "Thu Quân Về Thành 🏆" : "Rút Lui Về Thành ↩";
        CloseBtn.Style = (Style)FindResource("PrimaryButton");

        RoundCounterText.Text = _result.Won ? "ĐẠI THẮNG QUÂN GIA" : "BẠI TRẬN RÚT QUÂN";
        RoundCounterText.Foreground = _result.Won ? (Brush)FindResource("Gain") : (Brush)FindResource("Loss");

        ExpeditionStatusText.Text = _result.Won ? "Sào huyệt đã bị triệt phá hoàn toàn!" : "Binh lực suy kiệt, tạm lui lui binh.";

        ClashGlyphText.Text = _result.Won ? "🏆" : "🏳️";
        ClashPhaseTitle.Text = _result.Won ? "TOÀN THẮNG BÌNH ĐỊNH" : "THẤT BẠI LUI BINH";
        ClashPhaseTitle.Foreground = _result.Won ? (Brush)FindResource("Gain") : (Brush)FindResource("Loss");

        NarrativeDisplayText.Text = _result.SummaryVi;

        // Vitals and date summary
        BottomStatusText.Text = _result.Won
            ? $"Chiến Lợi Phẩm: +{_result.LootGold:N0} Quan, +2 {_result.ItemRewardName}, +{_result.FameGained} Uy Danh, +{_result.VirtueGained} Đức Hạnh."
            : "Tổn thất nhuệ khí và thể lực. Hãy nghỉ ngơi dưỡng sức trước khi trở lại.";

        NotifyHubStateChanged();
    }

    private static void AnimateBar(ProgressBar bar, double targetValue)
    {
        var anim = new DoubleAnimation(bar.Value, Math.Max(0, targetValue), TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        bar.BeginAnimation(RangeBase.ValueProperty, anim);
    }

    private static void AnimateGhostBar(ProgressBar ghostBar, double targetValue)
    {
        var anim = new DoubleAnimation(ghostBar.Value, Math.Max(0, targetValue), TimeSpan.FromMilliseconds(450))
        {
            BeginTime = TimeSpan.FromMilliseconds(200),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        ghostBar.BeginAnimation(RangeBase.ValueProperty, anim);
    }

    private static void AnimateThrust(TranslateTransform tt, double dx)
    {
        var anim = new DoubleAnimation(0, dx, TimeSpan.FromMilliseconds(90))
        {
            AutoReverse = true,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        tt.BeginAnimation(TranslateTransform.XProperty, anim);
    }

    private void TriggerScreenShake(double intensity)
    {
        var anim = new DoubleAnimation(-intensity, intensity, TimeSpan.FromMilliseconds(40))
        {
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(2),
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        anim.Completed += (_, _) => StageShakeTransform.BeginAnimation(TranslateTransform.XProperty, null);
        StageShakeTransform.BeginAnimation(TranslateTransform.XProperty, anim);
    }

    private void SpawnFloatingCombatNumber(string text, Brush brush, double x, double y, bool isSkill = false)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = isSkill ? 14 : 13,
            FontWeight = FontWeights.Bold,
            Foreground = brush,
            RenderTransform = new TranslateTransform(0, 0)
        };

        Canvas.SetLeft(tb, x);
        Canvas.SetTop(tb, y);
        CombatVfxCanvas.Children.Add(tb);

        var moveAnim = new DoubleAnimation(0, -32, TimeSpan.FromMilliseconds(750))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var fadeAnim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(750));
        fadeAnim.Completed += (_, _) => CombatVfxCanvas.Children.Remove(tb);

        if (tb.RenderTransform is TranslateTransform tt)
        {
            tt.BeginAnimation(TranslateTransform.YProperty, moveAnim);
        }
        tb.BeginAnimation(OpacityProperty, fadeAnim);
    }

    private void NextRound_Click(object sender, RoutedEventArgs e)
    {
        if (!_battleEnded)
        {
            PlayNextRound();
        }
    }

    private void AutoResolve_Click(object sender, RoutedEventArgs e)
    {
        while (!_battleEnded && _currentRoundIndex < _result.RoundLogs.Count)
        {
            PlayNextRound();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
