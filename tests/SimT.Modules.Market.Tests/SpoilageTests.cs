using SimT.Contracts.Playing;
using SimT.Core.Kernel;
using SimT.Testing;

namespace SimT.Modules.Market.Tests;

/// <summary>
/// Storage is not free and not safe: perishables decay every day, durables do not, and a
/// warehouse that is left alone eventually empties itself.
/// </summary>
public sealed class SpoilageTests : IDisposable
{
    private const long Grain = 1;
    private const long Silk = 4;

    private readonly TestHost _host = new("simt-spoilage");

    public void Dispose() => _host.Dispose();

    [Fact]
    public void Grain_loses_roughly_its_storage_rate_over_a_month()
    {
        using var kernel = _host.Boot();
        var (trade, warehouse, city) = Parts(kernel);

        trade.Buy(city, Grain, 40);
        warehouse.Rent(city, out _);
        Assert.True(warehouse.Store(city, Grain, 40, out var stored), stored);
        Assert.Equal(40, warehouse.StockAt(city, Grain));

        kernel.AdvanceDays(30);

        var left = warehouse.StockAt(city, Grain);
        Assert.InRange(left, 15, 25);
    }

    [Fact]
    public void Silk_never_spoils_because_it_has_no_shelf_life()
    {
        using var kernel = _host.Boot();
        var (trade, warehouse, city) = Parts(kernel);

        trade.Buy(city, Silk, 2);
        warehouse.Rent(city, out _);
        Assert.True(warehouse.Store(city, Silk, 2, out _));

        kernel.AdvanceDays(90);

        Assert.Equal(2, warehouse.StockAt(city, Silk));
    }

    [Fact]
    public void An_abandoned_warehouse_empties_its_perishables_and_stays_coherent()
    {
        using var kernel = _host.Boot();
        var (trade, warehouse, city) = Parts(kernel);

        trade.Buy(city, Grain, 20);
        warehouse.Rent(city, out _);
        warehouse.Store(city, Grain, 20, out _);

        kernel.AdvanceDays(200);

        Assert.Equal(0, warehouse.StockAt(city, Grain));
        Assert.Empty(kernel.RunInvariants());
    }

    [Fact]
    public void Withdrawing_what_is_left_is_still_possible_after_decay()
    {
        using var kernel = _host.Boot();
        var (trade, warehouse, city) = Parts(kernel);

        trade.Buy(city, Grain, 30);
        warehouse.Rent(city, out _);
        warehouse.Store(city, Grain, 30, out _);
        kernel.AdvanceDays(30);

        var left = warehouse.StockAt(city, Grain);
        var before = kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag).Quantity(city, Grain);
        Assert.True(warehouse.Take(city, Grain, left, out var message), message);

        Assert.Equal(before + left, kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag).Quantity(city, Grain));
        Assert.Equal(0, warehouse.StockAt(city, Grain));
    }

    private static (IMarketTrade Trade, IMarketWarehouse Warehouse, long City) Parts(SimKernel kernel)
    {
        var city = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState).CurrentCityId;
        return (
            kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade),
            kernel.Capabilities.Get<IMarketWarehouse>(CapabilityIds.MarketWarehouse),
            city);
    }
}
