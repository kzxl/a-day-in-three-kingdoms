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
public sealed partial class SceneOverlayControl : Border, IDisposable
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
    private bool _concealedUntilPainted;
    private bool _holdConcealmentAfterPaint;
    private bool _hasPaintedOnce;
    private object? _activeScene;
    private string? _pendingEnvelopeJson;

    public event Action<SceneClientMessage, object?>? ClientMessageReceived;
    public event Action<object?>? ScenePainted;

    public bool IsAvailable => _isAvailable;

    public object? ActiveScene => _activeScene;

    public bool IsShowingScene => Visibility == Visibility.Visible && _activeScene is not null;

    public SceneOverlayControl()
    {
        Visibility = Visibility.Collapsed;
        Background = System.Windows.Media.Brushes.Transparent;
        SnapsToDevicePixels = true;
    }

    public void ConcealUntilPainted(bool holdAfterPaint = false)
    {
        _concealedUntilPainted = true;
        _holdConcealmentAfterPaint = holdAfterPaint;
        _hasPaintedOnce = false;
        ApplyConcealment();
    }

    public void RevealPaintedScene()
    {
        _holdConcealmentAfterPaint = false;
        if (!_concealedUntilPainted)
            return;
        _concealedUntilPainted = false;
        ApplyConcealment();
    }

    public void AllowRevealOnPaint()
    {
        _holdConcealmentAfterPaint = false;
        if (_hasPaintedOnce)
            RevealPaintedScene();
    }

    private void ApplyConcealment()
    {
        if (_webView is null)
            return;
        _webView.Margin = _concealedUntilPainted
            ? new Thickness(0, 10000, 0, -10000)
            : new Thickness(0);
    }

    public void PrequeueScene<T>(string envelopeType, T scene) where T : notnull
    {
        _activeScene = scene;
        _pendingEnvelopeJson = JsonSerializer.Serialize(new { type = envelopeType, scene }, JsonOptions);
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
            var env = await GetOrCreateEnvironmentAsync();
            if (env is null)
            {
                _isAvailable = false;
                return;
            }

            var hasExtraArgs = !string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS"));

            _webView = new WebView2
            {
                DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 21, 17, 13),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            ApplyConcealment();
            Child = _webView;

            await _webView.EnsureCoreWebView2Async(env);

            var core = _webView.CoreWebView2;
            ConfigureSettings(core, hasExtraArgs);
            core.SetVirtualHostNameToFolderMapping(
                "simt.scene",
                Path.GetFullPath(assetsScenesDirectory),
                CoreWebView2HostResourceAccessKind.Allow);
            core.WebMessageReceived += OnWebMessageReceived;
            if (_activeScene is not null)
                Visibility = Visibility.Visible;
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

    public bool ShowTalkScene(TalkScene scene) => ShowScene("scene:talk", scene);

    public bool ShowDuelScene(DuelScene scene) => ShowScene("scene:duel", scene);

    public bool ShowCityScene(CityScene scene) => ShowScene("scene:city", scene);

    public bool ShowMapScene(MapScene scene) => ShowScene("scene:map", scene);

    public bool ShowSocialScene(SocialScene scene) => ShowScene("scene:social", scene);

    public bool ShowMarketScene(MarketScene scene) => ShowScene("scene:market", scene);

    public bool ShowDossierScene(DossierScene scene) => ShowScene("scene:dossier", scene);

    private bool ShowScene<T>(string envelopeType, T scene) where T : notnull
    {
        if (!_isAvailable)
            return false;

        _activeScene = scene;
        Visibility = Visibility.Visible;
        SendEnvelope(envelopeType, scene);
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

        if (normalizedType == "painted")
        {
            _hasPaintedOnce = true;
            if (!_holdConcealmentAfterPaint)
                RevealPaintedScene();
            ScenePainted?.Invoke(_activeScene);
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
