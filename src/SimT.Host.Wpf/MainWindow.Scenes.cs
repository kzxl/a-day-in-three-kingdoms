using System.IO;
using SimT.Contracts.Scenes;

namespace SimT.Host.Wpf;

public partial class MainWindow
{
    private void InitializeSceneHost()
    {
        SceneOverlay.ClientMessageReceived += OnSceneClientMessageReceived;
        Loaded += async (_, _) =>
        {
            var scenesDir = Path.Combine(AppContext.BaseDirectory, "assets", "scenes");
            await SceneOverlay.InitializeWarmAsync(scenesDir, skipForSmoke: _isSmoke);
        };
    }

    private void OnSceneClientMessageReceived(SceneClientMessage message, object? activeScene)
    {
        var type = message.Type.Trim().ToLowerInvariant();
        if (type == "close")
        {
            UpdateHeader();
            RefreshBadges();
            RenderSelected();
        }
    }
}
