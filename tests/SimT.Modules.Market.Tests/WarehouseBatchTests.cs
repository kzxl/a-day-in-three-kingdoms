using SimT.Contracts;
using SimT.Contracts.Playing;
using SimT.Core.Kernel;
using SimT.Testing;

namespace SimT.Modules.Market.Tests;

public sealed class WarehouseBatchTests : IDisposable
{
    private const long Grain = 1; // Perishable (ShelfLifeDays > 0)
    private const long Silk = 4;  // Durable (ShelfLifeDays == 0)

    private readonly TestHost _host = new("simt-warehouse-batch");

    public void Dispose() => _host.Dispose();

    [Fact]
    public void StoreAll_with_perishable_only_stores_only_perishable_goods()
    {
        using var kernel = _host.Boot();
        var (trade, warehouse, bag, city) = Parts(kernel);

        warehouse.Rent(city, out _);

        // Buy Grain (perishable) and Silk (durable)
        trade.Buy(city, Grain, 20);
        trade.Buy(city, Silk, 5);

        Assert.Equal(20, bag.Quantity(city, Grain));
        Assert.Equal(5, bag.Quantity(city, Silk));

        // Store only perishables
        var stored = warehouse.StoreAll(city, perishableOnly: true, out var msg);
        Assert.True(stored, msg);

        // Grain should be in warehouse, Silk stays in bag
        Assert.Equal(0, bag.Quantity(city, Grain));
        Assert.Equal(20, warehouse.StockAt(city, Grain));
        Assert.Equal(5, bag.Quantity(city, Silk));
        Assert.Equal(0, warehouse.StockAt(city, Silk));
    }

    [Fact]
    public void StoreAll_stores_all_items_up_to_capacity()
    {
        using var kernel = _host.Boot();
        var (trade, warehouse, bag, city) = Parts(kernel);

        warehouse.Rent(city, out _);

        trade.Buy(city, Grain, 20);
        trade.Buy(city, Silk, 5);

        var stored = warehouse.StoreAll(city, perishableOnly: false, out var msg);
        Assert.True(stored, msg);

        Assert.Equal(0, bag.Quantity(city, Grain));
        Assert.Equal(20, warehouse.StockAt(city, Grain));
        Assert.Equal(0, bag.Quantity(city, Silk));
        Assert.Equal(5, warehouse.StockAt(city, Silk));
    }

    [Fact]
    public void TakeAll_withdraws_all_items_from_warehouse_to_bag()
    {
        using var kernel = _host.Boot();
        var (trade, warehouse, bag, city) = Parts(kernel);

        warehouse.Rent(city, out _);

        trade.Buy(city, Grain, 15);
        trade.Buy(city, Silk, 4);

        warehouse.StoreAll(city, perishableOnly: false, out _);
        Assert.Equal(15, warehouse.StockAt(city, Grain));
        Assert.Equal(4, warehouse.StockAt(city, Silk));

        var taken = warehouse.TakeAll(city, out var msg);
        Assert.True(taken, msg);

        Assert.Equal(0, warehouse.StockAt(city, Grain));
        Assert.Equal(0, warehouse.StockAt(city, Silk));
        Assert.Equal(15, bag.Quantity(city, Grain));
        Assert.Equal(4, bag.Quantity(city, Silk));
    }

    [Fact]
    public void CommandHandler_handles_batch_warehouse_commands()
    {
        using var kernel = _host.Boot();
        var (trade, warehouse, bag, city) = Parts(kernel);

        warehouse.Rent(city, out _);

        trade.Buy(city, Grain, 10);
        trade.Buy(city, Silk, 2);

        Assert.True(kernel.TryGetCommandHandler("market.warehouse", out var handler));
        Assert.NotNull(handler);

        var msgStorePerishable = handler.Execute("store:perishable");
        Assert.NotNull(msgStorePerishable);
        Assert.Equal(10, warehouse.StockAt(city, Grain));
        Assert.Equal(2, bag.Quantity(city, Silk));

        var msgStoreAll = handler.Execute("store:all");
        Assert.NotNull(msgStoreAll);
        Assert.Equal(2, warehouse.StockAt(city, Silk));

        var msgTakeAll = handler.Execute("take:all");
        Assert.NotNull(msgTakeAll);
        Assert.Equal(0, warehouse.StockAt(city, Grain));
        Assert.Equal(0, warehouse.StockAt(city, Silk));
        Assert.Equal(10, bag.Quantity(city, Grain));
        Assert.Equal(2, bag.Quantity(city, Silk));
    }

    private static (IMarketTrade Trade, IMarketWarehouse Warehouse, IBag Bag, long City) Parts(SimKernel kernel)
    {
        var city = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState).CurrentCityId;
        return (
            kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade),
            kernel.Capabilities.Get<IMarketWarehouse>(CapabilityIds.MarketWarehouse),
            kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag),
            city);
    }
}
