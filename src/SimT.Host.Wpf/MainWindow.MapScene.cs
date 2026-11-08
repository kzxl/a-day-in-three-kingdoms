using SimT.Contracts.Scenes;

namespace SimT.Host.Wpf;

public partial class MainWindow
{
    public bool TryOpenMapScene(string? recommendation = null)
    {
        if (!SceneOverlay.IsAvailable)
            return false;

        var scene = BuildMapScenePayload(recommendation);
        return scene is not null && SceneOverlay.ShowMapScene(scene);
    }

    public MapScene? BuildMapScenePayload(string? recommendation = null) =>
        MapScene.Build(_kernel.Capabilities, _kernel.Today, _kernel.Ledger.Liquid, recommendation);
}
