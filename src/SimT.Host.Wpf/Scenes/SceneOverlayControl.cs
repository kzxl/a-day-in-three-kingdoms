using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using SimT.Contracts.Scenes;

namespace SimT.Host.Wpf.Scenes;

/// <summary>
/// Warm WebView2 scene overlay covering the main stage area (<c>Grid.Column="1"</c>).
/// Created once per <see cref="MainWindow"/>, maps <c>https://simt.scene/</c> to
/// <c>assets/scenes/</c>, and validates all inbound messages via <see cref="SceneTrustGuard"/>.
/// </summary>
public sealed class SceneOverlayControl : Border, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private WebView2? _webView;
    private bool _initAttempted;
    private bool _isAvailable;
    private bool _isJsReady;
    private object? _activeScene;
    private string? _pendingEnvelopeJson;

    public event Action<SceneClientMessage, object?>? ClientMessageReceived;

    public bool IsAvailable => _isAvailable;

    public object? ActiveScene => _activeScene;

    public bool IsShowingScene => Visibility == Visibility.Visible && _activeScene is not null;

    public SceneOverlayControl()
    {
        Visibility = Visibility.Collapsed;
        Background = System.Windows.Media.Brushes.Transparent;
        SnapsToDevicePixels = true;
    }

    /// <summary>
    /// Initializes the warm WebView2 instance asynchronously. Safe to call even when the
    /// Evergreen WebView2 Runtime is absent on the machine; falls back cleanly without throwing.
    /// </summary>
    public async Task InitializeWarmAsync(string assetsScenesDirectory, bool skipForSmoke = false)
    {
        if (_initAttempted)
            return;
        _initAttempted = true;

        if (skipForSmoke || !Directory.Exists(assetsScenesDirectory))
        {
            _isAvailable = false;
            return;
        }

        try
        {
            var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
            if (string.IsNullOrWhiteSpace(version))
            {
                _isAvailable = false;
                return;
            }
        }
        catch
        {
            _isAvailable = false;
            return;
        }

        try
        {
            var extraArgs = Environment.GetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS");
            var hasExtraArgs = !string.IsNullOrWhiteSpace(extraArgs);
            var userDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SimT",
                hasExtraArgs ? "webview-cdp" : "webview");
            Directory.CreateDirectory(userDataDir);

            _webView = new WebView2
            {
                DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 21, 17, 13),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Child = _webView;

            var envOptions = hasExtraArgs ? new CoreWebView2EnvironmentOptions(extraArgs) : null;
            var env = await CoreWebView2Environment.CreateAsync(null, userDataDir, envOptions);
            await _webView.EnsureCoreWebView2Async(env);

            var core = _webView.CoreWebView2;
            ConfigureSettings(core, hasExtraArgs);

            core.SetVirtualHostNameToFolderMapping(
                "simt.scene",
                Path.GetFullPath(assetsScenesDirectory),
                CoreWebView2HostResourceAccessKind.Allow);

            core.WebMessageReceived += OnWebMessageReceived;
            core.Navigate("https://simt.scene/index.html");
            _isAvailable = true;
        }
        catch
        {
            _isAvailable = false;
            Child = null;
            _webView?.Dispose();
            _webView = null;
        }
    }

    public bool ShowTalkScene(TalkScene scene)
    {
        if (!_isAvailable)
            return false;

        _activeScene = scene;
        Visibility = Visibility.Visible;
        SendEnvelope("scene:talk", scene);
        return true;
    }

    public bool ShowDuelScene(DuelScene scene)
    {
        if (!_isAvailable)
            return false;

        _activeScene = scene;
        Visibility = Visibility.Visible;
        SendEnvelope("scene:duel", scene);
        return true;
    }

    public bool ShowCityScene(CityScene scene)
    {
        if (!_isAvailable)
            return false;

        _activeScene = scene;
        Visibility = Visibility.Visible;
        SendEnvelope("scene:city", scene);
        return true;
    }

    public bool ShowMapScene(MapScene scene)
    {
        if (!_isAvailable)
            return false;

        _activeScene = scene;
        Visibility = Visibility.Visible;
        SendEnvelope("scene:map", scene);
        return true;
    }

    public bool ShowSocialScene(SocialScene scene)
    {
        if (!_isAvailable)
            return false;

        _activeScene = scene;
        Visibility = Visibility.Visible;
        SendEnvelope("scene:social", scene);
        return true;
    }

    public bool ShowMarketScene(MarketScene scene)
    {
        if (!_isAvailable)
            return false;

        _activeScene = scene;
        Visibility = Visibility.Visible;
        SendEnvelope("scene:market", scene);
        return true;
    }

    public bool ShowDossierScene(DossierScene scene)
    {
        if (!_isAvailable)
            return false;

        _activeScene = scene;
        Visibility = Visibility.Visible;
        SendEnvelope("scene:dossier", scene);
        return true;
    }

    public void HideScene()
    {
        _activeScene = null;
        _pendingEnvelopeJson = null;
        Visibility = Visibility.Collapsed;
        if (_isAvailable && _isJsReady && _webView?.CoreWebView2 is not null)
        {
            try
            {
                _webView.CoreWebView2.PostWebMessageAsJson("{\"type\":\"scene:hide\"}");
            }
            catch
            {
                // Ignore if WebView2 is shutting down.
            }
        }
    }

    private void SendEnvelope<T>(string type, T payload)
    {
        var json = JsonSerializer.Serialize(new { type, scene = payload }, JsonOptions);
        if (_isJsReady && _webView?.CoreWebView2 is not null)
        {
            try
            {
                _webView.CoreWebView2.PostWebMessageAsJson(json);
                _pendingEnvelopeJson = null;
                return;
            }
            catch
            {
                // Fall through to queue if not ready.
            }
        }

        _pendingEnvelopeJson = json;
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        SceneClientMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<SceneClientMessage>(e.WebMessageAsJson, JsonOptions);
        }
        catch
        {
            return;
        }

        if (!SceneTrustGuard.IsAllowedMessage(message, _activeScene))
            return;

        var normalizedType = message!.Type.Trim().ToLowerInvariant();
        if (normalizedType == "ready")
        {
            _isJsReady = true;
            if (!string.IsNullOrEmpty(_pendingEnvelopeJson) && _webView?.CoreWebView2 is not null)
            {
                try
                {
                    _webView.CoreWebView2.PostWebMessageAsJson(_pendingEnvelopeJson);
                    _pendingEnvelopeJson = null;
                }
                catch
                {
                    // Ignore transient failure.
                }
            }
            return;
        }

        if (normalizedType == "close")
        {
            var closedScene = _activeScene;
            ClientMessageReceived?.Invoke(message, closedScene);
            if (ReferenceEquals(_activeScene, closedScene))
                HideScene();
            return;
        }

        ClientMessageReceived?.Invoke(message, _activeScene);
    }

    private static void ConfigureSettings(CoreWebView2 core, bool enableDevTools = false)
    {
        var settings = core.Settings;
        settings.IsStatusBarEnabled = false;
        settings.IsZoomControlEnabled = false;
        settings.IsPinchZoomEnabled = false;
        settings.IsBuiltInErrorPageEnabled = false;
        settings.AreDefaultScriptDialogsEnabled = false;
#if DEBUG
        settings.AreDevToolsEnabled = true;
        settings.AreDefaultContextMenusEnabled = true;
        settings.AreBrowserAcceleratorKeysEnabled = true;
#else
        settings.AreDevToolsEnabled = enableDevTools;
        settings.AreDefaultContextMenusEnabled = false;
        settings.AreBrowserAcceleratorKeysEnabled = false;
#endif
    }

    public void Dispose()
    {
        try
        {
            if (_webView?.CoreWebView2 is not null)
                _webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
            _webView?.Dispose();
        }
        catch
        {
            // Ignore disposal exceptions on shutdown.
        }
        finally
        {
            _webView = null;
        }
    }
}
