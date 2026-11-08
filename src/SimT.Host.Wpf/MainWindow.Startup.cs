using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SimT.Contracts;
using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;
using SimT.Host.Wpf.Controls;

namespace SimT.Host.Wpf;

public partial class MainWindow
{
    private LoadingProgressState? _loadingState;
    private DispatcherTimer? _startupOverlayTimer;
    private Task? _sceneWarmTask;
    private bool _startupOverlayDismissed;

    public GameLoadingScreen LoadingScreenControl => LoadingScreen;

    private void InitializeSceneHost(LoadingProgressState? loadingState)
    {
        _loadingState = loadingState;
        SceneOverlay.ClientMessageReceived += OnSceneClientMessageReceived;
        SceneOverlay.ScenePainted += OnSceneOverlayPainted;

        if (_isSmoke)
        {
            _ = SceneOverlay.InitializeWarmAsync(string.Empty, skipForSmoke: true);
            LoadingScreen.Visibility = Visibility.Collapsed;
            _startupOverlayDismissed = true;
            return;
        }

        var useLoadingScreen = _loadingState is not null;
        if (useLoadingScreen)
        {
            _loadingState!.UpdateFromKernel(_kernel, _meta.Name);
            LoadingScreen.AttachState(_loadingState);
            LoadingScreen.EnterCityRequested += (_, _) => OnLoadingScreenEnterCity();
            SceneOverlay.ConcealUntilPainted(holdAfterPaint: true);

            _startupOverlayTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher)
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            _startupOverlayTimer.Tick += (_, _) =>
            {
                _startupOverlayTimer?.Stop();
                _startupOverlayTimer = null;
                _loadingState?.MarkStepDone(4);
                _loadingState?.MarkStepDone(5);
            };
            _startupOverlayTimer.Start();
        }
        else
        {
            LoadingScreen.Visibility = Visibility.Collapsed;
            _startupOverlayDismissed = true;
        }

        var scenesDir = Path.Combine(AppContext.BaseDirectory, "assets", "scenes");
        SourceInitialized += (_, _) =>
            _sceneWarmTask ??= SceneOverlay.InitializeWarmAsync(scenesDir, skipForSmoke: _isSmoke);

        Loaded += async (_, _) =>
        {
            var warmTask = _sceneWarmTask ??= SceneOverlay.InitializeWarmAsync(scenesDir, skipForSmoke: _isSmoke);
            if (_loaded && string.Equals(_screenId, CitySceneScreenId, StringComparison.Ordinal) &&
                BuildCityScene() is { } prebuiltCity)
            {
                SceneOverlay.PrequeueScene("scene:city", prebuiltCity);
                StatusText.Text = $"Đang ở {prebuiltCity.CityNameVi} ({prebuiltCity.Season}) · bấm địa điểm hoặc nhân sĩ trong thành để tương tác.";
            }

            await warmTask;
            Scenes.SceneOverlayControl.ReleaseWarmController();
            if (!_loaded || !SceneOverlay.IsAvailable)
            {
                _startupOverlayTimer?.Stop();
                _startupOverlayTimer = null;
                _loadingState?.MarkStepDone(4);
                _loadingState?.MarkStepDone(5);
                return;
            }

            _loadingState?.MarkStepDone(4);

            if (string.Equals(_screenId, CitySceneScreenId, StringComparison.Ordinal))
            {
                if (SceneOverlay.ActiveScene is null && !TryOpenCityScene())
                    _loadingState?.MarkStepDone(5);
            }
            else if (TryOpenFeatureScene(_screenId))
            {
                ClearWpfPageContent(ScreenTitle.Text);
            }
            else
            {
                _loadingState?.MarkStepDone(5);
            }
        };
    }

    private void OnSceneOverlayPainted(object? scene)
    {
        _startupOverlayTimer?.Stop();
        _startupOverlayTimer = null;
        _loadingState?.MarkStepDone(4);
        _loadingState?.MarkStepDone(5);

        if (_startupOverlayDismissed)
            SceneOverlay.RevealPaintedScene();
    }

    private void OnLoadingScreenEnterCity()
    {
        _startupOverlayTimer?.Stop();
        _startupOverlayTimer = null;
        _startupOverlayDismissed = true;

        var revealTimer = new DispatcherTimer(DispatcherPriority.Render, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(310)
        };
        revealTimer.Tick += (_, _) =>
        {
            revealTimer.Stop();
            SceneOverlay.AllowRevealOnPaint();
            SceneOverlay.RevealPaintedScene();
        };
        revealTimer.Start();
    }

    private bool HandleLoadingScreenKeyDown(KeyEventArgs e)
    {
        if (LoadingScreen.Visibility != Visibility.Visible || LoadingScreen.IsDismissed)
            return false;

        if (e.Key == Key.Enter && LoadingScreen.IsReady)
            LoadingScreen.TriggerEnterCity();

        e.Handled = true;
        return true;
    }

    private static bool IsWebSceneScreen(string screenId) =>
        screenId is CitySceneScreenId or "world.map" or "social.network" or "market.stall" or "npc.directory";

    public void DismissStartupOverlay(bool immediate)
    {
        _startupOverlayTimer?.Stop();
        _startupOverlayTimer = null;
        _startupOverlayDismissed = true;
        SceneOverlay.AllowRevealOnPaint();
        SceneOverlay.RevealPaintedScene();

        if (LoadingScreen.Visibility != Visibility.Visible && LoadingScreen.IsDismissed)
            return;

        LoadingScreen.TriggerEnterCity(immediate: immediate || _isSmoke || !IsLoaded);
    }

    private void AttachDebugOverlayLoop()
    {
        Loaded += (_, _) =>
        {
            var sw = Stopwatch.StartNew();
            var lastT = 0.0;
            CompositionTarget.Rendering += (_, _) =>
            {
                var curT = sw.Elapsed.TotalSeconds;
                var dt = curT - lastT;
                lastT = curT;
                if (DebugOverlay.Visibility == Visibility.Visible)
                {
                    DebugOverlay.Diagnostics.BeginFrame();
                    DebugOverlay.Diagnostics.EndFrame(dt);
                    DebugOverlay.Refresh(dt);
                }
            };
        };
    }

    private static string Describe(Exception error) => error switch
    {
        SimException sim => $"[{sim.Code}] {sim.Message}",
        _ => $"[internal] {error.GetType().Name}: {error.Message}"
    };

    private static string ShortVersion()
    {
        var full = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "unknown";

        var plus = full.IndexOf('+');
        if (plus < 0)
            return $"Một Ngày Ở Tam Quốc {full}";

        var hash = full[(plus + 1)..];
        return $"Một Ngày Ở Tam Quốc {full[..plus]} · {hash[..Math.Min(7, hash.Length)]}";
    }

    private static string FormatRegionName(string? regionCode) =>
        RoleTitleVi.FormatRegion(regionCode);
}
