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
}
