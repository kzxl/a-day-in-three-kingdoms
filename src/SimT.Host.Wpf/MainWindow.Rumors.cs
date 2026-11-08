using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;

namespace SimT.Host.Wpf;

public partial class MainWindow
{
    public void PresentTavern(bool listen)
    {
        if (!_kernel.Capabilities.TryGet<ITavernRumors>(CapabilityIds.TavernRumors, out var rumors)
            || rumors is null) return;
        if (!_kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var player)
            || player is null) return;
        try
        {
            var news = listen ? rumors.Listen() : rumors.ActiveAt(player.CurrentCityId);
            if (listen) { _kernel.Save(); UpdateHeader(); InvalidateBadges(); RefreshBadges(); }
            var narrative = news.Count == 0
                ? "Khách đường xa tụ họp bên bình rượu. Mời một tuần rượu để nghe chuyện thiên hạ."
                : string.Join("\n\n", news.Select((r, i) => $"{i + 1}. {r.NarrativeVi}"));
            TryOpenCityScene(new CityEventCard("Chuyện bên bình rượu", "Tửu quán", narrative,
                news.Count == 0 ? $"{TavernRules.CostQuan} quan · {TavernRules.BatchSize} tin" : "Tin lưu 10 ngày · nghe lại miễn phí"),
                [new("tavern:listen", news.Count == 0
                    ? $"Uống rượu nghe chuyện ({TavernRules.CostQuan} quan)" : "Nghe lại chuyện cũ (miễn phí)",
                    "Ba chuyện đường xa"), new("tavern:leave", "Rời tửu quán", "Trở lại phố")]);
        }
        catch (Exception ex) { StatusText.Text = Describe(ex); }
    }
}
