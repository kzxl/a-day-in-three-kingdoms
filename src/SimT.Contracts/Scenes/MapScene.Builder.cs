using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Contracts.Scenes;

public sealed partial record MapScene
{
    public static (
        IReadOnlyDictionary<long, (long FactionId, string FactionNameVi, string FactionColor, string GovernorNameVi)> CityGovernance,
        IReadOnlyList<MapFactionView> Factions,
        string Season) ResolveFactionsAndSeason(ICapabilityBroker capabilities, IReadOnlyList<CitadelDef> citadels, GameDate today)
    {
        var season = GameDateCodec.SeasonKey(today.Month);
        var cityGov = new Dictionary<long, (long FactionId, string FactionNameVi, string FactionColor, string GovernorNameVi)>();
        if (!capabilities.TryGet<IPoliticalCourt>(CapabilityIds.CourtOffice, out var court) || court is null)
            return (cityGov, Array.Empty<MapFactionView>(), season);

        capabilities.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var npcRegistry);
        var factionById = court.Factions.ToDictionary(f => f.Id);
        var govByCity = court.Governances.ToDictionary(g => g.CitadelId);
        var counts = new Dictionary<long, int>();

        foreach (var c in citadels)
        {
            if (!govByCity.TryGetValue(c.Id, out var gov) || !factionById.TryGetValue(gov.FactionId, out var fac))
                continue;

            var govName = gov.GovernorNpcId > 0 && npcRegistry?.Find(gov.GovernorNpcId) is { } govNpc
                ? RoleTitleVi.ShortName(govNpc.NameVi)
                : string.Empty;
            cityGov[c.Id] = (fac.Id, fac.NameVi, fac.ColorHex, govName);
            counts[fac.Id] = counts.GetValueOrDefault(fac.Id) + 1;
        }

        var factions = court.Factions
            .Select(f => new MapFactionView(f.Id, f.NameVi, f.ColorHex, counts.GetValueOrDefault(f.Id)))
            .Where(f => f.CityCount > 0)
            .ToList();

        return (cityGov, factions, season);
    }

    public static MapScene? Build(ICapabilityBroker capabilities, GameDate today, long cash, string? recommendation = null)
    {
        if (!capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map) || map is null ||
            !capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var playerState) || playerState is null)
            return null;

        var currentCityId = playerState.CurrentCityId;
        var currentCityName = map.Find(currentCityId)?.NameVi ?? "Lạc Dương";
        var fatigue = capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) && attrs is not null ? attrs.Vitals.Fatigue : 0;
        capabilities.TryGet<IBag>(CapabilityIds.PlayerBag, out var bag);
        capabilities.TryGet<IItemCatalog>(CapabilityIds.ItemCatalog, out var items);

        var (cityGov, factions, season) = ResolveFactionsAndSeason(capabilities, map.Citadels, today);
        var cities = new List<MapCityView>(map.Citadels.Count);
        foreach (var c in map.Citadels)
        {
            var eco = map.GetEcosystem(c.Id);
            var quotes = new List<MapTravelQuoteView>();
            if (c.Id != currentCityId)
            {
                var qWalk = map.QuoteTravel(currentCityId, c.Id, TravelMode.Walk);
                var qHorse = map.QuoteTravel(currentCityId, c.Id, TravelMode.Horse);
                var qCaravan = map.QuoteTravel(currentCityId, c.Id, TravelMode.Caravan);
                quotes.Add(new MapTravelQuoteView("walk", "Bộ hành", $"Không tốn tiền · Mang {qWalk.CarryCapacity} đv", qWalk.BaseDays, qWalk.ActualDays, qWalk.TravelFee, qWalk.RationsNeeded, qWalk.FatigueExpected, qWalk.PathNodeIds, qWalk.CarryCapacity));
                quotes.Add(new MapTravelQuoteView("horse", "Dịch mã", $"Nhanh nhất · Mang {qHorse.CarryCapacity} đv", qHorse.BaseDays, qHorse.ActualDays, qHorse.TravelFee, qHorse.RationsNeeded, qHorse.FatigueExpected, qHorse.PathNodeIds, qHorse.CarryCapacity));
                quotes.Add(new MapTravelQuoteView("caravan", "Tiêu cục", $"Không gặp cướp · Mang {qCaravan.CarryCapacity} đv", qCaravan.BaseDays, qCaravan.ActualDays, qCaravan.TravelFee, qCaravan.RationsNeeded, qCaravan.FatigueExpected, qCaravan.PathNodeIds, qCaravan.CarryCapacity));
            }
            var storedVi = bag is not null
                ? TravelLuggage.FormatItemQuantities(bag.Rows(c.Id).Select(r => (r.ItemId, r.Quantity)), items)
                : string.Empty;
            var (facId, facName, facColor, govName) = cityGov.TryGetValue(c.Id, out var g) ? g : (0L, string.Empty, string.Empty, string.Empty);
            cities.Add(new MapCityView(
                c.Id, c.Code, c.NameVi, RoleTitleVi.FormatRegion(c.Region),
                eco.MapX, eco.MapY, c.PriceFactorPermille, eco.DangerLevel,
                eco.BiomeNameVi, eco.ClimateVi, eco.TopographyVi,
                eco.PrimarySpecialtyVi, eco.SecondarySpecialtyVi, quotes, storedVi,
                facId, facName, facColor, govName));
        }

        var currentBag = bag is not null
            ? bag.Rows(currentCityId)
                .Where(r => r.Quantity > 0)
                .Select(r =>
                {
                    var def = items?.Find(r.ItemId);
                    return new MapBagItemView(r.ItemId, def?.NameVi ?? $"#{r.ItemId}", r.Quantity, def?.BasePrice ?? 1);
                })
                .OrderByDescending(x => x.BasePrice).ThenBy(x => x.ItemId).ToList()
            : new List<MapBagItemView>();

        var routes = map.Routes.Select(r => new MapRouteView(r.FromCitadelId, r.ToCitadelId, r.Days)).ToList();
        var recText = recommendation ?? $"Từ {currentCityName}, bấm một thành trên bản đồ để xem lộ trình ngắn nhất, chi phí, hành lý và sản vật trước khi lên đường.";
        return new MapScene(
            currentCityId, currentCityName, GameDateCodec.ShortVi(today),
            cash, fatigue, "art/map/thien_ha.webp",
            SceneArtCatalog.PlayerPortraitFace, recText, cities, routes, currentBag, season, factions);
    }
}
