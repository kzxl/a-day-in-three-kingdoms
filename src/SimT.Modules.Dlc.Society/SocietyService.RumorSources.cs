using SimT.Contracts.Playing;

namespace SimT.Modules.Dlc.Society;

internal sealed partial class SocietyService
{
    private List<RumorView> BuildRumorCandidates(long heardCity)
    {
        var facts = new List<RumorView>();
        var day = _clock.Today.DayIndex;
        RumorView Fact(string kind, string title, string text, long city, long? item = null,
            long? npc = null, long? seed = null, string? key = null, long? eventDay = null) =>
            new(0, heardCity, day, day + TavernRules.LifetimeDays, kind, title,
                "Nghe nói… " + text, null, item, city, npc, seed, key, eventDay);

        if (_broker is not null
            && _broker.TryGet<IMarketTrade>(CapabilityIds.MarketTrade, out var market) && market is not null
            && _broker.TryGet<IItemCatalog>(CapabilityIds.ItemCatalog, out var items) && items is not null)
        {
            var prices = from city in _map.Citadels.Where(c => c.Id != heardCity)
                         from item in items.All.Where(i => i.BasePrice > 0)
                         let quote = market.QuoteBuy(city.Id, item.Id)
                         where quote > 0 && quote != item.BasePrice
                         orderby Math.Abs((double)quote / item.BasePrice - 1) descending, city.Id, item.Id
                         select Fact("price", "Chuyện giá chợ",
                             $"{item.NameVi} tại {city.NameVi} đang {(quote > item.BasePrice ? "khan, giá cao" : "ế, giá thấp")}: " +
                             $"{quote} quan (giá gốc {item.BasePrice} quan).", city.Id, item: item.Id);
            facts.AddRange(prices.Take(12));
        }

        foreach (var seed in GetActiveSeeds().Where(s => s.TriggerAfterDay >= day
                     && s.TriggerAfterDay <= day + 30).OrderBy(s => s.TriggerAfterDay).ThenBy(s => s.Id))
            facts.Add(Fact("event", "Chuyện sắp tới", $"{seed.TitleVi} tại {seed.CitadelNameVi}, " +
                $"còn khoảng {seed.TriggerAfterDay - day} ngày.", seed.CitadelId,
                seed: seed.Id, eventDay: seed.TriggerAfterDay));
        foreach (var source in EventSources())
            foreach (var hint in source.Upcoming(day, 30).OrderBy(h => h.Day).ThenBy(h => h.Key))
                if (_map.Find(hint.CityId) is { } city)
                    facts.Add(Fact("event", hint.TitleVi, $"{hint.NarrativeVi} tại {city.NameVi}.",
                        city.Id, key: hint.Key, eventDay: hint.Day));

        foreach (var npc in _registry.All.OrderByDescending(n => Math.Max(n.Attributes.War, n.Attributes.Int))
                     .ThenBy(n => n.Id).Take(12))
        {
            var location = npc.CurrentCitadelId((ISocietyChronicle)this);
            if (_map.Find(location) is { } city)
                facts.Add(Fact("whereabouts", "Tung tích danh sĩ",
                    $"{npc.NameVi} đang ở {city.NameVi}.", city.Id, npc: npc.Id));
        }

        IMilitaryCommand? military = null;
        _broker?.TryGet(CapabilityIds.MilitaryCommand, out military);
        foreach (var route in _map.Routes.OrderBy(r => r.FromCitadelId).ThenBy(r => r.ToCitadelId))
        {
            var danger = _map.GetEcosystem(route.ToCitadelId).DangerLevel;
            var hasBandits = military?.StrongholdsAt(route.ToCitadelId).Any(s => s.ClearedCount == 0) == true;
            if (danger < 7 && !hasBandits) continue;
            if (_map.Find(route.FromCitadelId) is not { } from || _map.Find(route.ToCitadelId) is not { } to) continue;
            facts.Add(Fact("danger", "Đường xa hiểm trở",
                $"đường từ {from.NameVi} tới {to.NameVi} có {(hasBandits ? "thổ phỉ" : "nguy hiểm cao")}.", to.Id));
        }
        return facts;
    }
}
