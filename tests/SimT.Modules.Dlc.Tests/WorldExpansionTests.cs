using SimT.Contracts.Playing;
using SimT.Core.Kernel;
using Xunit;

namespace SimT.Modules.Dlc.Tests;

public sealed class WorldExpansionTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private static IWorldMap Map(SimKernel kernel) =>
        kernel.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);

    [Fact]
    public void WorldMap_contains_15_historical_citadels()
    {
        using var kernel = _host.Boot();
        var map = Map(kernel);

        Assert.Equal(15, map.Citadels.Count);

        // Core 3 citadels
        Assert.NotNull(map.Find(1)); // Lạc Dương
        Assert.NotNull(map.Find(2)); // Trường An
        Assert.NotNull(map.Find(3)); // Nghiệp Thành

        // 12 expanded citadels
        var zhuo = map.Find(4);
        Assert.NotNull(zhuo);
        Assert.Equal("trac_huyen", zhuo.Code);
        Assert.Equal("Trác Huyện", zhuo.NameVi);
        Assert.Equal("u_chau", zhuo.Region);

        var chengdu = map.Find(15);
        Assert.NotNull(chengdu);
        Assert.Equal("thanh_do", chengdu.Code);
        Assert.Equal("Thành Đô", chengdu.NameVi);
        Assert.Equal("ich_chau", chengdu.Region);
    }

    [Fact]
    public void WorldMap_routes_connect_interstate_network()
    {
        using var kernel = _host.Boot();
        var map = Map(kernel);

        // Trác Huyện (4) <-> Bắc Bình (8): 1 day
        Assert.Equal(1, map.TravelDays(4, 8));
        Assert.Equal(1, map.TravelDays(8, 4));

        // Lạc Dương (1) <-> Hứa Xương (7): 2 days
        Assert.Equal(2, map.TravelDays(1, 7));
        Assert.Equal(2, map.TravelDays(7, 1));

        // Hán Trung (14) <-> Thành Đô (15): 4 days
        Assert.Equal(4, map.TravelDays(14, 15));
        Assert.Equal(4, map.TravelDays(15, 14));

        // Non-adjacent citadels return 0
        Assert.Equal(0, map.TravelDays(4, 15));
    }

    [Fact]
    public void WorldMap_loads_sublocations_for_citadels()
    {
        using var kernel = _host.Boot();
        var map = Map(kernel);

        // Trác Huyện sublocations (Vườn đào, Thiết điếm, etc.)
        var zhuoSubs = map.Sublocations(4);
        Assert.NotEmpty(zhuoSubs);
        Assert.Contains(zhuoSubs, s => s.NameVi.Contains("Vườn Đào"));

        // Lạc Dương sublocations
        var luoyangSubs = map.Sublocations(1);
        Assert.NotEmpty(luoyangSubs);
        Assert.Contains(luoyangSubs, s => s.NameVi.Contains("Kim Loan Điện"));
    }

    [Fact]
    public void WorldMap_screen_is_registered_and_renders_15_citadels()
    {
        using var kernel = _host.Boot();
        var screen = kernel.Ui.Screens.FirstOrDefault(s => s.ScreenId == "world.map");
        Assert.NotNull(screen);
        Assert.Equal("Bản Đồ", screen.Title);
        Assert.Equal(15, screen.Order);

        var model = screen.Render();
        Assert.Equal("world.map", model.ScreenId);
        Assert.NotEmpty(model.Sections);

        var totalRows = model.Sections.Sum(sec => sec.Rows.Count);
        Assert.Equal(15, totalRows);
        Assert.Contains(model.Headline, m => m.Label == "Thành Đang Trú");
    }

    [Fact]
    public void WorldMap_command_handler_travels_between_adjacent_citadels()
    {
        using var kernel = _host.Boot();
        var hasHandler = kernel.Ui.TryGetCommandHandler("world.map", out var handler);
        Assert.True(hasHandler);
        Assert.NotNull(handler);

        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        Assert.Equal(1, player.CurrentCityId); // Start in Lạc Dương (1)
        var startDate = kernel.Today;

        // Travel to Hứa Xương (7): direct distance is 2 days
        var result = handler.Execute("travel:7");
        Assert.NotNull(result);
        Assert.Contains("Hứa Xương", result);
        Assert.Equal(7, player.CurrentCityId);
        Assert.Equal(startDate.Day + 2, kernel.Today.Day);

        // Attempting to travel to same city returns failure message
        var duplicateResult = handler.Execute("travel:7");
        Assert.Contains("đã đang ở", duplicateResult);
    }

    [Fact]
    public void WorldMap_exposes_ecosystems_and_routes_for_all_citadels()
    {
        using var kernel = _host.Boot();
        var map = Map(kernel);

        Assert.NotEmpty(map.Routes);

        foreach (var c in map.Citadels)
        {
            var eco = map.GetEcosystem(c.Id);
            Assert.NotNull(eco);
            Assert.Equal(c.Id, eco.CitadelId);
            Assert.False(string.IsNullOrWhiteSpace(eco.BiomeNameVi));
            Assert.False(string.IsNullOrWhiteSpace(eco.ClimateVi));
            Assert.False(string.IsNullOrWhiteSpace(eco.PrimarySpecialtyVi));
            Assert.True(eco.DangerLevel >= 1 && eco.DangerLevel <= 5);
            Assert.True(eco.MapX > 0 && eco.MapY > 0);
        }

        // Verify specific historical cities
        var luoyang = map.GetEcosystem(1);
        Assert.Contains("Đế Đô", luoyang.BiomeNameVi);

        var chengdu = map.GetEcosystem(15);
        Assert.Contains("Ba Thục", chengdu.BiomeNameVi);
        Assert.Contains("Thục Cẩm", chengdu.PrimarySpecialtyVi);

        var jianye = map.GetEcosystem(13);
        Assert.Contains("Thủy Quốc", jianye.BiomeNameVi);
        Assert.Contains("Trà", jianye.PrimarySpecialtyVi);
    }

    [Fact]
    public void WorldMap_quote_travel_calculates_itinerary_and_modes()
    {
        using var kernel = _host.Boot();
        var map = Map(kernel);

        // Lạc Dương (1) to Tương Dương (10)
        var walkQuote = map.QuoteTravel(1, 10, TravelMode.Walk);
        Assert.True(walkQuote.BaseDays > 0);
        Assert.Equal(walkQuote.BaseDays, walkQuote.ActualDays);
        Assert.Equal(0, walkQuote.TravelFee);
        Assert.Equal(walkQuote.BaseDays, walkQuote.RationsNeeded);
        Assert.NotEmpty(walkQuote.PathNodeIds);

        var horseQuote = map.QuoteTravel(1, 10, TravelMode.Horse);
        Assert.True(horseQuote.ActualDays <= walkQuote.ActualDays);
        Assert.True(horseQuote.TravelFee > 0);

        var caravanQuote = map.QuoteTravel(1, 10, TravelMode.Caravan);
        Assert.True(caravanQuote.TravelFee > horseQuote.TravelFee);
    }

    [Fact]
    public void WorldMap_command_handler_supports_horse_and_caravan_travel()
    {
        using var kernel = _host.Boot();
        var handler = kernel.Ui.TryGetCommandHandler("world.map", out var h) ? h : null;
        Assert.NotNull(handler);

        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var attrs = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);
        var bag = kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);

        // Add provisions in start city
        bag.Add(player.CurrentCityId, 1, 10); // 1 = gao

        var startDate = kernel.Today;
        // Travel to Hứa Xương (7) with horse
        var result = handler.Execute("travel:7:horse");
        Assert.NotNull(result);
        Assert.Contains("Dịch Mã", result);
        Assert.Contains("Hứa Xương", result);
        Assert.Equal(7, player.CurrentCityId);
        Assert.True(kernel.Today.DayIndex > startDate.DayIndex);
        Assert.True(attrs.Vitals.Fatigue > 0);
    }
}
