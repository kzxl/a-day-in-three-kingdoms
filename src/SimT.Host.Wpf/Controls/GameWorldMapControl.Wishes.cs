namespace SimT.Host.Wpf.Controls;
public sealed partial class GameWorldMapControl
{
    public void FocusWishCity(long cityId)
    {
        if (_map.Find(cityId) is not null) OnCanvasNodeSelected(cityId);
    }
}
