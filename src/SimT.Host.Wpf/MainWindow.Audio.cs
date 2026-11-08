namespace SimT.Host.Wpf;

public partial class MainWindow
{
    private void PlayScreenNavigationAudio(string target)
    {
        if (_screenId != target && target is not (CitySceneScreenId or "world.map"))
            Audio.GameAudio.Engine.PlaySfx("ui.book");
    }

    private void ShowDuelWithAudio(System.Windows.Window window)
    {
        var previousScene = SceneOverlay.ActiveScene;
        Audio.GameAudio.PlayDuelMusic();
        try { window.ShowDialog(); }
        finally { Audio.GameAudio.ShowScene(previousScene); }
    }

    private void PlayCommandAudio(string command, long cashBefore)
    {
        // The command API returns prose for failures too; a committed cash movement confirms a trade.
        if ((command.StartsWith("buy:", StringComparison.Ordinal) || command.StartsWith("sell:", StringComparison.Ordinal) || command.StartsWith("npc_buy:", StringComparison.Ordinal)) &&
            _kernel.Ledger.Balance != cashBefore)
            Audio.GameAudio.Engine.PlaySfx("trade.coin");
    }
}
