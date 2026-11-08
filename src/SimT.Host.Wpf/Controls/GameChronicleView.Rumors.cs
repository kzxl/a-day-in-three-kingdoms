using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Host.Wpf.Controls;

public sealed partial class GameChronicleView
{
    public void SetFilter(string filter)
    {
        _currentFilter = filter;
        BuildFilterButtons();
        RenderList();
    }

    private void LoadRumorEvents()
    {
        if (_kernel is null || !_kernel.Capabilities.TryGet<ITavernRumors>(CapabilityIds.TavernRumors, out var rumors)
            || rumors is null) return;
        _kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map);
        foreach (var rumor in rumors.History())
        {
            var expired = rumor.IsExpired(_kernel.Today.DayIndex);
            _allEvents.Add(new ChronicleEventModel
            {
                Id = -rumor.Id,
                Date = GameDate.FromDayIndex(rumor.HeardDay),
                CitadelName = map?.Find(rumor.HeardCityId)?.NameVi ?? "",
                CategoryKey = "rumors",
                CategoryNameVi = "Tin đồn",
                Headline = $"{rumor.TitleVi} · {rumor.StatusVi}",
                Narrative = rumor.NarrativeVi + $"\n\nKiểm chứng: {rumor.StatusVi}. " +
                    (expired ? "Tin đã hết hạn." : "Tin còn hiệu lực."),
                IsMuted = expired
            });
        }
    }
}
