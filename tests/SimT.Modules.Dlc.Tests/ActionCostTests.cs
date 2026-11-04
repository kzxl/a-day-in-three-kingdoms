using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Core.Kernel;

namespace SimT.Modules.Dlc.Tests;

/// <summary>
/// End-to-end version of the rule that makes the calendar matter: every meaningful action spends
/// days, and the player is told how many before committing.
/// </summary>
public sealed class ActionCostTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void A_trade_session_costs_two_days()
    {
        using var kernel = _host.Boot();
        var trade = Trade(kernel);
        var city = City(kernel);

        var before = kernel.Today;
        Assert.True(trade.Buy(city, 1, 1).Ok);
        Assert.Equal(before.AddDays(2), kernel.Today);

        var afterBuy = kernel.Today;
        Assert.True(trade.Sell(city, 1, 1).Ok);
        Assert.Equal(afterBuy.AddDays(2), kernel.Today);
    }

    [Fact]
    public void Renting_a_warehouse_costs_a_day()
    {
        using var kernel = _host.Boot();
        var warehouse = Warehouse(kernel);
        var before = kernel.Today;

        Assert.True(warehouse.Rent(City(kernel), out var message), message);

        Assert.Equal(before.AddDays(1), kernel.Today);
    }

    [Fact]
    public void Moving_stock_is_free()
    {
        using var kernel = _host.Boot();
        var trade = Trade(kernel);
        var warehouse = Warehouse(kernel);
        var city = City(kernel);

        trade.Buy(city, 1, 2);
        warehouse.Rent(city, out _);
        var before = kernel.Today;

        Assert.True(warehouse.Store(city, 1, 1, out var message), message);

        Assert.Equal(before, kernel.Today);
    }

    [Fact]
    public void Sending_a_caravan_costs_a_day()
    {
        using var kernel = _host.Boot();
        Trade(kernel).Buy(City(kernel), 1, 2);
        var destination = Map(kernel).RouteNeighbours(City(kernel))[0];
        var before = kernel.Today;

        Command(kernel, "caravan.routes", $"send:{destination}");

        Assert.Equal(before.AddDays(1), kernel.Today);
    }

    [Fact]
    public void A_marriage_costs_a_day()
    {
        using var kernel = _host.Boot();
        var before = kernel.Today;

        Command(kernel, "lineage.family", "marry");

        Assert.Equal(before.AddDays(1), kernel.Today);
    }

    [Fact]
    public void Land_work_has_a_price_in_days()
    {
        using var kernel = _host.Boot();
        var plot = HomePlot(kernel);

        var beforeRent = kernel.Today;
        Command(kernel, "cultivation.farm", $"rent:{plot.Id}");
        Assert.Equal(beforeRent.AddDays(1), kernel.Today);

        var beforePlant = kernel.Today;
        Command(kernel, "cultivation.farm", $"plant:{plot.Id}:gao");
        Assert.Equal(beforePlant.AddDays(2), kernel.Today);

        var ready = Guild(kernel).Plots.Single(candidate => candidate.Id == plot.Id).ReadyDayIndex!.Value;
        kernel.AdvanceDays((int)Math.Max(1, ready - kernel.Today.DayIndex));

        var beforeHarvest = kernel.Today;
        Command(kernel, "cultivation.farm", $"harvest:{plot.Id}");
        Assert.Equal(beforeHarvest.AddDays(2), kernel.Today);
    }

    private static void Command(SimKernel kernel, string screen, string commandId)
    {
        Assert.True(kernel.TryGetCommandHandler(screen, out var handler), $"screen '{screen}' is not registered");
        handler!.Execute(commandId);
    }

    private static PlotView HomePlot(SimKernel kernel) =>
        Guild(kernel).Plots.First(plot => plot.CitadelId == City(kernel));

    private static IMarketTrade Trade(SimKernel kernel) =>
        kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade);

    private static IMarketWarehouse Warehouse(SimKernel kernel) =>
        kernel.Capabilities.Get<IMarketWarehouse>(CapabilityIds.MarketWarehouse);

    private static IWorldMap Map(SimKernel kernel) =>
        kernel.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);

    private static ICultivationEstate Guild(SimKernel kernel) =>
        kernel.Capabilities.Get<ICultivationEstate>(CapabilityIds.CultivationEstate);

    private static long City(SimKernel kernel) =>
        kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState).CurrentCityId;
}
