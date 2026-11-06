using System.IO;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SimT.Contracts.Playing;

namespace SimT.Host.Wpf;

public partial class MainWindow
{
    private DispatcherTimer? _startupOverlayTimer;
    private Task? _sceneWarmTask;
    private bool _startupOverlayDismissed;

    private void InitializeSceneHost()
    {
        SceneOverlay.ClientMessageReceived += OnSceneClientMessageReceived;
        SceneOverlay.ScenePainted += _ => DismissStartupOverlay(immediate: false);

        if (_isSmoke)
        {
            _ = SceneOverlay.InitializeWarmAsync(string.Empty, skipForSmoke: true);
            StartupOverlay.Visibility = Visibility.Collapsed;
            _startupOverlayDismissed = true;
            return;
        }

        StartupBgImage.Source = TitleWindow.GetSharedBackgroundImage();
        StartupStatusText.Text = $"Đang vào {ResolveCurrentCityName()}...";
        StartupOverlay.Visibility = Visibility.Visible;
        StartupOverlay.Opacity = 1.0;
        SceneOverlay.ConcealUntilPainted();

        _startupOverlayTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher)
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _startupOverlayTimer.Tick += (_, _) => DismissStartupOverlay(immediate: true);
        _startupOverlayTimer.Start();

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
            if (!_loaded || !SceneOverlay.IsAvailable)
            {
                DismissStartupOverlay(immediate: true);
                return;
            }

            if (string.Equals(_screenId, CitySceneScreenId, StringComparison.Ordinal))
            {
                if (SceneOverlay.ActiveScene is null && !TryOpenCityScene())
                    DismissStartupOverlay(immediate: true);
            }
            else if (TryOpenFeatureScene(_screenId))
            {
                ClearWpfPageContent(ScreenTitle.Text);
            }
            else
            {
                DismissStartupOverlay(immediate: true);
            }
        };
    }

    private static bool IsWebSceneScreen(string screenId) =>
        screenId is CitySceneScreenId or "world.map" or "social.network" or "market.stall" or "npc.directory";

    private string ResolveCurrentCityName()
    {
        if (_kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var ps) && ps is not null &&
            _kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map) &&
            map?.Find(ps.CurrentCityId) is { } cit)
        {
            return cit.NameVi;
        }

        return "Lạc Dương";
    }

    public void DismissStartupOverlay(bool immediate)
    {
        _startupOverlayTimer?.Stop();
        _startupOverlayTimer = null;
        SceneOverlay.RevealPaintedScene();

        if (_startupOverlayDismissed)
        {
            if (immediate && StartupOverlay.Visibility != Visibility.Collapsed)
            {
                StartupOverlay.BeginAnimation(OpacityProperty, null);
                StartupOverlay.Visibility = Visibility.Collapsed;
            }
            return;
        }

        _startupOverlayDismissed = true;
        StartupOverlay.IsHitTestVisible = false;
        if (immediate || _isSmoke || !IsLoaded)
        {
            StartupOverlay.BeginAnimation(OpacityProperty, null);
            StartupOverlay.Opacity = 0;
            StartupOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        void CollapseNow()
        {
            StartupOverlay.BeginAnimation(OpacityProperty, null);
            StartupOverlay.Opacity = 0;
            StartupOverlay.Visibility = Visibility.Collapsed;
        }

        var fadeTimer = new DispatcherTimer(DispatcherPriority.Render, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(165)
        };
        fadeTimer.Tick += (_, _) =>
        {
            fadeTimer.Stop();
            CollapseNow();
        };
        fadeTimer.Start();

        var fade = new DoubleAnimation(StartupOverlay.Opacity, 0.0, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        fade.Completed += (_, _) =>
        {
            fadeTimer.Stop();
            CollapseNow();
        };
        StartupOverlay.BeginAnimation(OpacityProperty, fade);
    }
}
