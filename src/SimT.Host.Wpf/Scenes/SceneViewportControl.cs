using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using SimT.Contracts.Scenes;

namespace SimT.Host.Wpf.Scenes;

/// <summary>
/// Hardware-accelerated 2D Scene Viewport hosting the unified 60 FPS render loop.
/// Renders 4 decoupled layers: Background -> Entities -> Weather/FX -> Foreground/HUD.
/// Also applies cinematic transition overlays (Fade/Wipe).
/// </summary>
public sealed class SceneViewportControl : FrameworkElement
{
    private ISceneManager? _sceneManager;
    private readonly Stopwatch _stopwatch = new();
    private double _lastFrameTimestamp;
    private bool _isRenderingLoopActive;

    public ISceneManager? SceneManager
    {
        get => _sceneManager;
        set
        {
            if (_sceneManager != value)
            {
                _sceneManager = value;
                InvalidateVisual();
            }
        }
    }

    public SceneViewportControl()
    {
        ClipToBounds = true;
        SnapsToDevicePixels = true;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_isRenderingLoopActive)
        {
            _stopwatch.Restart();
            _lastFrameTimestamp = 0;
            CompositionTarget.Rendering += OnCompositionRendering;
            _isRenderingLoopActive = true;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_isRenderingLoopActive)
        {
            CompositionTarget.Rendering -= OnCompositionRendering;
            _stopwatch.Stop();
            _isRenderingLoopActive = false;
        }
    }

    private void OnCompositionRendering(object? sender, EventArgs e)
    {
        if (_sceneManager == null || ActualWidth <= 0 || ActualHeight <= 0) return;

        var currentTimestamp = _stopwatch.Elapsed.TotalSeconds;
        var deltaTime = currentTimestamp - _lastFrameTimestamp;
        _lastFrameTimestamp = currentTimestamp;

        // Cap deltaTime to avoid spiral-of-death during spikes (e.g. window dragging)
        if (deltaTime > 0.1) deltaTime = 0.1;

        _sceneManager.Update(deltaTime);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var width = ActualWidth;
        var height = ActualHeight;

        if (width <= 0 || height <= 0) return;

        var bounds = new Rect(0, 0, width, height);

        // Fallback default enterprise dark backdrop
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x16)), null, bounds);

        var currentScene = _sceneManager?.CurrentScene;
        if (currentScene is IScene2DRenderer renderer)
        {
            // Layer 1: Background & Terrain/Parchment
            renderer.RenderBackground(dc, width, height);

            // Layer 2: World Entities, Characters, Hotspots
            renderer.RenderEntities(dc, width, height);

            // Layer 3: Atmospheric Weather & Ambient Particles (Rain, Snow, Fog)
            renderer.RenderWeatherFx(dc, width, height);

            // Layer 4: Foreground & Dialogue Overlays
            renderer.RenderForeground(dc, width, height);
        }

        // Cinematic Transition Overlay (Fade-to-Black / Cross-fade)
        if (_sceneManager != null && _sceneManager.IsInTransition)
        {
            var progress = _sceneManager.TransitionProgress;
            // 0.0 -> 0.5 (fade to dark), 0.5 -> 1.0 (fade back in)
            var opacity = progress < 0.5 ? progress * 2.0 : (1.0 - progress) * 2.0;
            opacity = Math.Clamp(opacity, 0.0, 1.0);

            var overlayBrush = new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), 0x0A, 0x0A, 0x0C));
            dc.DrawRectangle(overlayBrush, null, bounds);
        }
    }
}
