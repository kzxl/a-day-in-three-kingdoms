using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;
using SimT.Contracts.Time;

namespace SimT.Host.Wpf;

public partial class MainWindow
{
    private void TryOpenFeatureSceneForCurrentScreen()
    {
        switch (_screenId)
        {
            case "world.map":
                TryOpenMapScene();
                break;
            case "social.network":
                TryOpenSocialScene();
                break;
            case "market.stall":
                TryOpenMarketScene();
                break;
            case "npc.directory":
                TryOpenDossierScene();
                break;
        }
    }

    public bool TryOpenMapScene(string? recommendation = null)
    {
        if (!SceneOverlay.IsAvailable ||
            !_kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map) || map is null ||
            !_kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var playerState) || playerState is null)
        {
            return false;
        }

        var currentCityId = playerState.CurrentCityId;
        var currentCity = map.Find(currentCityId);
        var currentCityName = currentCity?.NameVi ?? "Lạc Dương";

        var fatigue = 0;
        if (_kernel.Capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) && attrs is not null)
        {
            fatigue = attrs.Vitals.Fatigue;
        }

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
                quotes.Add(new MapTravelQuoteView("walk", "Bộ hành", "Không tốn tiền, mệt nhất", qWalk.BaseDays, qWalk.ActualDays, qWalk.TravelFee, qWalk.RationsNeeded, qWalk.FatigueExpected, qWalk.PathNodeIds));
                quotes.Add(new MapTravelQuoteView("horse", "Dịch mã", "Nhanh nhất", qHorse.BaseDays, qHorse.ActualDays, qHorse.TravelFee, qHorse.RationsNeeded, qHorse.FatigueExpected, qHorse.PathNodeIds));
                quotes.Add(new MapTravelQuoteView("caravan", "Tiêu cục", "Không gặp cướp đường", qCaravan.BaseDays, qCaravan.ActualDays, qCaravan.TravelFee, qCaravan.RationsNeeded, qCaravan.FatigueExpected, qCaravan.PathNodeIds));
            }

            cities.Add(new MapCityView(
                c.Id,
                c.Code,
                c.NameVi,
                RoleTitleVi.FormatRegion(c.Region),
                eco.MapX,
                eco.MapY,
                c.PriceFactorPermille,
                eco.DangerLevel,
                eco.BiomeNameVi,
                eco.ClimateVi,
                eco.TopographyVi,
                eco.PrimarySpecialtyVi,
                eco.SecondarySpecialtyVi,
                quotes));
        }

        var routes = map.Routes
            .Select(r => new MapRouteView(r.FromCitadelId, r.ToCitadelId, r.Days))
            .ToList();

        var recText = recommendation ??
                      $"Từ {currentCityName}, bấm một thành trên bản đồ để xem lộ trình ngắn nhất, chi phí và sản vật trước khi lên đường.";

        var scene = new MapScene(
            currentCityId,
            currentCityName,
            GameDateCodec.ShortVi(_kernel.Today),
            _kernel.Ledger.Liquid,
            fatigue,
            "art/map/thien_ha.webp",
            SceneArtCatalog.PlayerPortraitFace,
            recText,
            cities,
            routes);

        return SceneOverlay.ShowMapScene(scene);
    }

    public bool TryOpenSocialScene(string? recommendation = null, long selectedNpcId = 0) => false;

    public bool TryOpenMarketScene(string? recommendation = null, long selectedItemId = 0) => false;

    public bool TryOpenDossierScene(string? recommendation = null, long selectedNpcId = 0) => false;

    private async void HandleMapSceneMessage(string type, string id, MapScene _)
    {
        if (type != "choice" || !id.StartsWith("travel:", StringComparison.Ordinal))
        {
            return;
        }

        await ExecuteScreenCommandAsync(id, "world.map");
        TryOpenMapScene();
    }

    private void HandleSocialSceneMessage(string type, string id, SocialScene _)
    {
    }

    private void HandleMarketSceneMessage(string type, string id, MarketScene _)
    {
    }

    private void HandleDossierSceneMessage(string type, string id, DossierScene _)
    {
    }
}
