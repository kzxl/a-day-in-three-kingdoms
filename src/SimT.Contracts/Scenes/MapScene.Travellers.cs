using System.Text.Json.Serialization;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Contracts.Scenes;

public sealed partial record MapScene
{
    [JsonPropertyName("travellers")]
    public IReadOnlyList<MapTravellerView> Travellers { get; init; } = Array.Empty<MapTravellerView>();

    private static IReadOnlyList<MapTravellerView> BuildTravellers(ICapabilityBroker capabilities,
        GameDate today, IReadOnlyList<MapCityView> cities)
    {
        if (!capabilities.TryGet<ISocietyChronicle>(CapabilityIds.SocietyChronicle, out var society) || society is null)
            return Array.Empty<MapTravellerView>();
        var result = new List<MapTravellerView>();
        var byId = cities.ToDictionary(c => c.Id);
        string Color(long city) => byId.GetValueOrDefault(city)?.FactionColor is { Length: > 0 } color ? color : "#c4a062";
        double Stable(long id) => 0.15 + ((id * 37 + today.DayIndex * 17) % 700) / 1000.0;
        void Add(string id, string kind, string label, long from, long to, double progress)
        {
            if (from == to || !byId.ContainsKey(from) || !byId.ContainsKey(to) || result.Count >= 30) return;
            result.Add(new(id, kind, label, from, to, Math.Clamp(progress, 0, 1), Color(to)));
        }

        // Reserve room for the other sources even when many rivals are travelling.
        foreach (var r in society.Rivals.GetAllRivals().Where(r => r.CurrentCitadelId != r.TargetCitadelId)
            .OrderBy(r => r.NpcId).Take(12))
            Add($"rival:{r.NpcId}", "rival", r.NpcNameVi, r.CurrentCitadelId, r.TargetCitadelId, Stable(r.NpcId));

        if (capabilities.TryGet<ICaravanGuild>(CapabilityIds.CaravanGuild, out var guild) && guild is not null)
            foreach (var r in guild.Runs.Where(r => r.Status == "RUNNING" && today.DayIndex >= r.DepartDayIndex &&
                today.DayIndex <= r.ArriveDayIndex).OrderBy(r => r.Id).Take(6))
                Add($"caravan:{r.Id}", "caravan", $"Thương đoàn của bạn #{r.Id}", r.FromCityId, r.ToCityId,
                    (today.DayIndex - r.DepartDayIndex) / (double)Math.Max(1, r.ArriveDayIndex - r.DepartDayIndex));

        if (capabilities.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var npcs) && npcs is not null)
        {
            var away = society.AllActivities().Where(a => !a.IsDeceased)
                .Select(a => (Activity: a, Npc: npcs.Find(a.NpcId)))
                .Where(x => x.Npc is not null && !x.Npc.IsDeceased(society) && x.Npc.CitadelId != x.Activity.CurrentCitadelId)
                .OrderByDescending(x => x.Activity.Reputation).ThenBy(x => x.Activity.NpcId).Take(8);
            foreach (var (a, n) in away)
                Add($"npc:{a.NpcId}", "npc", n!.NameVi, n.CitadelId, a.CurrentCitadelId, Stable(a.NpcId));
        }

        foreach (var e in society.GetHistoricalEvents(true).OrderBy(e => e.Code))
            foreach (var impact in e.Impacts.Where(i => i.Kind is "route_block" or "route_danger"))
            {
                var start = (e.OccurredDate ?? e.ScheduledDate).DayIndex;
                if (today.DayIndex < start || today.DayIndex > start + impact.DurationDays) continue;
                var to = impact.TargetCitadelId ?? e.RelatedCitadelIds.FirstOrDefault(c => c != impact.CitadelId);
                Add($"army:{e.Code}:{impact.CitadelId}:{to}", "army", $"Quân binh · {e.TitleVi}",
                    impact.CitadelId, to, Stable(impact.CitadelId));
            }
        return result;
    }
}

public sealed record MapTravellerView(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("labelVi")] string LabelVi,
    [property: JsonPropertyName("fromId")] long FromId,
    [property: JsonPropertyName("toId")] long ToId,
    [property: JsonPropertyName("progress")] double Progress,
    [property: JsonPropertyName("color")] string Color);
