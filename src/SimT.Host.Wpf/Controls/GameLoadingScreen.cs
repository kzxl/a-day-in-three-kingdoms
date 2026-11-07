using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Full-screen loading screen control matching docs/design/loading/Main.dc.html.
/// Used both in LoadingWindow (steps 1–4) and as MainWindow's overlay (steps 5–6).
/// </summary>
public sealed partial class GameLoadingScreen : UserControl
{
    private static readonly DependencyProperty FrameDriverProperty = DependencyProperty.Register(
        "FrameDriver",
        typeof(double),
        typeof(GameLoadingScreen),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly Stopwatch _stopwatch = new();
    private LoadingProgressState? _state;
    private double _lastSeconds;
    private int _lastTipIndex = -1;
    private bool _isLoopAttached;
    private bool _isFadingOut;
    private bool _readyEventFired;

    public bool IsReady => _state?.Ready == true;
    public bool IsDismissed { get; private set; }

    public event EventHandler? ReadyReached;
    public event EventHandler? EnterCityRequested;

    public GameLoadingScreen()
    {
        BuildVisualTree();
        Unloaded += (_, _) => StopLoop();
        SizeChanged += (_, _) =>
        {
            if (_state is not null && !IsDismissed)
                RenderFrame(0.0);
        };
    }

    public void AttachState(LoadingProgressState state)
    {
        _state = state;
        _lastTipIndex = -1;
        _isFadingOut = false;
        _readyEventFired = state.Ready;
        IsDismissed = false;
        IsHitTestVisible = true;
        BeginAnimation(OpacityProperty, null);
        Opacity = 1.0;
        Visibility = Visibility.Visible;

        if (state.Ready)
        {
            _enterPanel.BeginAnimation(OpacityProperty, null);
            _enterPanel.Opacity = 1.0;
            _enterPanel.Visibility = Visibility.Visible;
        }
        else
        {
            _enterPanel.Visibility = Visibility.Collapsed;
        }

        RenderFrame(0.0);
        StartLoop();
    }

    public void StartLoop()
    {
        if (_isLoopAttached || IsDismissed)
            return;

        _stopwatch.Restart();
        _lastSeconds = 0.0;
        var driver = new DoubleAnimation(0.0, 1.0, TimeSpan.FromSeconds(1.1))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        BeginAnimation(FrameDriverProperty, driver);
        CompositionTarget.Rendering += OnRendering;
        _isLoopAttached = true;
    }

    public void StopLoop()
    {
        if (!_isLoopAttached)
            return;

        BeginAnimation(FrameDriverProperty, null);
        CompositionTarget.Rendering -= OnRendering;
        _isLoopAttached = false;
        _stopwatch.Stop();
    }

    public void TriggerEnterCity(bool immediate = false)
    {
        if (IsDismissed || _isFadingOut)
            return;

        if (!immediate && (_state is null || !_state.Ready))
            return;

        _isFadingOut = true;
        IsHitTestVisible = false;
        EnterCityRequested?.Invoke(this, EventArgs.Empty);

        if (immediate || !IsLoaded)
        {
            CompleteDismissal();
            return;
        }

        var fallbackTimer = new DispatcherTimer(DispatcherPriority.Render, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(330)
        };
        fallbackTimer.Tick += (_, _) =>
        {
            fallbackTimer.Stop();
            CompleteDismissal();
        };
        fallbackTimer.Start();

        var fade = new DoubleAnimation(Opacity, 0.0, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        fade.Completed += (_, _) =>
        {
            fallbackTimer.Stop();
            CompleteDismissal();
        };
        BeginAnimation(OpacityProperty, fade);
    }

    private void CompleteDismissal()
    {
        StopLoop();
        IsDismissed = true;
        _isFadingOut = false;
        BeginAnimation(OpacityProperty, null);
        Opacity = 0.0;
        Visibility = Visibility.Collapsed;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (_state is null || IsDismissed || _isFadingOut || Visibility != Visibility.Visible)
            return;

        var now = _stopwatch.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastSeconds, 0.0, 0.15);
        _lastSeconds = now;
        RenderFrame(dt);
    }

    private void RenderFrame(double dt)
    {
        if (_state is null)
            return;

        _campaignTurnText.Text = $"Ván: {_state.CampaignName} · Lượt {_state.Turn}";
        _cityNameText.Text = _state.CityNameVi;
        if (string.IsNullOrWhiteSpace(_state.CityNameHan))
        {
            _cityHanText.Visibility = Visibility.Collapsed;
        }
        else
        {
            _cityHanText.Text = _state.CityNameHan;
            _cityHanText.Visibility = Visibility.Visible;
        }
        _sublineText.Text = _state.SublineText;

        var (completedCount, activeIndex, statuses) = _state.GetSnapshot();
        var roadWidth = ActualWidth > 200.0 ? ActualWidth - 128.0 : 1152.0;
        var target = Math.Clamp(completedCount / 6.0, 0.0, 1.0);

        var remaining = dt;
        do
        {
            var slice = Math.Min(remaining, 0.025);
            _state.IdleTime += slice;
            var step = Math.Min(0.42 * slice, Math.Max(0.0, (target - _state.Pos) * Math.Min(1.0, slice * 6.0)));
            if (slice > 0.0)
                _state.IsMoving = step / slice > 0.024;
            _state.Dist += step * roadWidth;
            _state.Pos = Math.Min(1.0, _state.Pos + step);
            remaining -= slice;
        }
        while (remaining > 0.0001);

        if (completedCount >= 6 && _state.Pos > 0.995 && !_state.Ready)
        {
            _state.Pos = 1.0;
            _state.IsMoving = false;
            _state.Ready = true;
        }

        if (_state.Ready && !_readyEventFired)
        {
            _readyEventFired = true;
            ShowEnterButton();
            ReadyReached?.Invoke(this, EventArgs.Empty);
        }

        UpdateStepVisuals(statuses, _state.IdleTime);
        UpdateTipVisual(_state.IdleTime, dt > 0.0);
        UpdateRoadVisuals(roadWidth, completedCount, activeIndex);
    }

    private void ShowEnterButton()
    {
        if (_enterPanel.Visibility == Visibility.Visible)
            return;

        _enterPanel.Visibility = Visibility.Visible;
        var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(350))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        _enterPanel.BeginAnimation(OpacityProperty, fadeIn);
    }

    private void UpdateStepVisuals(LoadingStepStatus[] statuses, double idleTime)
    {
        var pulsePhase = (idleTime % 1.1) / 1.1;
        var pulseEase = 1.0 - (1.0 - pulsePhase) * (1.0 - pulsePhase);
        var pulseScale = 0.5 + 0.9 * pulseEase;
        var pulseOpacity = 0.9 * (1.0 - pulseEase);

        for (var i = 0; i < 6; i++)
        {
            var row = _stepRows[i];
            var st = statuses[i];

            row.CheckPath.Visibility = st == LoadingStepStatus.Done ? Visibility.Visible : Visibility.Collapsed;
            row.ActivePulseGrid.Visibility = st == LoadingStepStatus.Active ? Visibility.Visible : Visibility.Collapsed;
            row.PendingCircle.Visibility = st == LoadingStepStatus.Pending ? Visibility.Visible : Visibility.Collapsed;

            if (st == LoadingStepStatus.Active)
            {
                row.ActiveRingScale.ScaleX = pulseScale;
                row.ActiveRingScale.ScaleY = pulseScale;
                row.ActiveRing.Opacity = pulseOpacity;
                row.Label.FontWeight = FontWeights.SemiBold;
                row.Label.Foreground = IvoryBrush;
            }
            else if (st == LoadingStepStatus.Done)
            {
                row.Label.FontWeight = FontWeights.Normal;
                row.Label.Foreground = StepDoneTextBrush;
            }
            else
            {
                row.Label.FontWeight = FontWeights.Normal;
                row.Label.Foreground = StepPendingTextBrush;
            }
        }
    }

    private void UpdateTipVisual(double idleTime, bool animateTransition)
    {
        var tips = LoadingProgressState.Tips;
        var idx = (int)Math.Floor(idleTime / 4.5) % tips.Count;
        if (idx == _lastTipIndex)
            return;

        var animate = animateTransition && _lastTipIndex >= 0;
        _lastTipIndex = idx;
        _tipText.Text = tips[idx];

        if (animate)
        {
            var fade = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(400))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            _tipText.BeginAnimation(OpacityProperty, fade);
        }
    }

    private void UpdateRoadVisuals(double roadWidth, int completedCount, int activeIndex)
    {
        if (_state is null)
            return;

        _roadDashedLine.X2 = roadWidth;
        _roadFillBar.Width = Math.Round(_state.Pos * roadWidth);

        for (var k = 1; k <= 5; k++)
        {
            var tick = _milestoneTicks[k - 1];
            var ratio = k / 6.0;
            Canvas.SetLeft(tick, ratio * roadWidth - 1.0);
            tick.Background = _state.Pos >= ratio ? GoldAccentBrush : RoadMutedBrush;
        }

        var frameIdx = (_state.Ready || !_state.IsMoving)
            ? ((int)Math.Floor(_state.IdleTime / 0.3) % 4)
            : (4 + ((int)Math.Floor(_state.Dist / 16.0) % 8));

        var frames = GetTravelerFrames();
        if (frames.Length > frameIdx && !ReferenceEquals(_travelerImage.Source, frames[frameIdx]))
            _travelerImage.Source = frames[frameIdx];

        Canvas.SetLeft(_travelerImage, _state.Pos * roadWidth - 105.4);

        var statusIdx = activeIndex >= 0 ? activeIndex : Math.Min(5, completedCount);
        _statusText.Text = _state.Ready
            ? "Đã sẵn sàng · 6/6 bước"
            : $"{LoadingProgressState.StepLabels[statusIdx]}…";
        _percentText.Text = $"{Math.Round(_state.Pos * 100.0)}%";
    }
}
