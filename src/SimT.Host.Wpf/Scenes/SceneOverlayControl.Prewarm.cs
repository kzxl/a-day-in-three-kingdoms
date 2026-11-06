using System.IO;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;

namespace SimT.Host.Wpf.Scenes;

public sealed partial class SceneOverlayControl
{
    private static Task<CoreWebView2Environment?>? s_sharedEnvTask;
    private static CoreWebView2Controller? s_warmController;

    public static Task<CoreWebView2Environment?> GetOrCreateEnvironmentAsync() =>
        s_sharedEnvTask ??= CreateSharedEnvironmentAsync();

    public static async Task PrewarmFromWindowAsync(Window hostWindow, string? assetsScenesDirectory = null)
    {
        try
        {
            var env = await GetOrCreateEnvironmentAsync();
            if (env is null || s_warmController is not null || !hostWindow.IsLoaded)
                return;

            var hwnd = new WindowInteropHelper(hostWindow).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            var controller = await env.CreateCoreWebView2ControllerAsync(hwnd);
            if (!hostWindow.IsLoaded)
            {
                controller.Close();
                return;
            }

            controller.IsVisible = false;
            controller.Bounds = new System.Drawing.Rectangle(0, 0, 1, 1);
            s_warmController = controller;

            var scenesDir = assetsScenesDirectory ?? Path.Combine(AppContext.BaseDirectory, "assets", "scenes");
            if (Directory.Exists(scenesDir))
            {
                controller.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "simt.scene",
                    Path.GetFullPath(scenesDir),
                    CoreWebView2HostResourceAccessKind.Allow);
                controller.CoreWebView2.Navigate("https://simt.scene/index.html");
            }
        }
        catch
        {
            // Pre-warming is best-effort and must never disturb the title screen.
        }
    }

    public static void ReleaseWarmController()
    {
        var controller = Interlocked.Exchange(ref s_warmController, null);
        if (controller is null)
            return;
        try
        {
            controller.Close();
        }
        catch
        {
            // Ignore cleanup errors on window teardown.
        }
    }

    private static async Task<CoreWebView2Environment?> CreateSharedEnvironmentAsync()
    {
        try
        {
            var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
            if (string.IsNullOrWhiteSpace(version))
                return null;

            var extraArgs = Environment.GetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS");
            var hasExtraArgs = !string.IsNullOrWhiteSpace(extraArgs);
            var userDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SimT",
                hasExtraArgs ? "webview-cdp" : "webview");
            Directory.CreateDirectory(userDataDir);

            var envOptions = hasExtraArgs ? new CoreWebView2EnvironmentOptions(extraArgs) : null;
            return await CoreWebView2Environment.CreateAsync(null, userDataDir, envOptions);
        }
        catch
        {
            return null;
        }
    }
}
