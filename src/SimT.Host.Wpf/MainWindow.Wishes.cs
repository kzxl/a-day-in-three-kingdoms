using SimT.Host.Wpf.Controls;
namespace SimT.Host.Wpf;
public partial class MainWindow
{
    private void OpenWishDestination(long cityId)
    {
        SelectScreen("world.map");
        if (SceneOverlay.IsAvailable)
        {
            var scene=BuildMapScenePayload();
            if (scene is not null) SceneOverlay.ShowMapScene(scene with { FocusCityId=cityId });
        }
        else
        {
            foreach(var child in ContentPanel.Children)
                if (child is GameWorldMapControl map) map.FocusWishCity(cityId);
        }
    }
}
