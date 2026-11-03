using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Core.Kernel;
using SimT.Testing;

namespace SimT.Modules.Market.Tests;

/// <summary>
/// Supply pressure: price must respond to what is left on the shelf, the stall must refill
/// itself slowly, and it must refuse to buy back more than it can hold.
/// </summary>
public sealed class MarketDepthTests : IDisposable
{
    private const long Grain = 1;

    private readonly TestHost _host = new("simt-market-depth");

    public void Dispose() => _host.Dispose();

    [Fact]
    public void Draining_the_shelf_raises_the_price()
    {
        using var kernel = _host.Boot();
        var trade = Trade(kernel);
        var city = City(kernel);

        var full = trade.QuoteBuy(city, Grain);
        var outcome = trade.Buy(city, Grain, 30);
        var drained = trade.QuoteBuy(city, Grain);

        Assert.True(outcome.Ok, outcome.Message);
        Assert.Equal(10, trade.StallStock(city, Grain));
        Assert.True(drained > full, $"price should rise: {full} -> {drained}");
    }

    [Fact]
    public void Selling_back_lowers_the_price_again()
    {
        using var kernel = _host.Boot();
        var trade = Trade(kernel);
        var city = City(kernel);

        var full = trade.QuoteBuy(city, Grain);
        trade.Buy(city, Grain, 20);
        var drained = trade.QuoteBuy(city, Grain);

        var sold = trade.Sell(city, Grain, 20);
        var refilled = trade.QuoteBuy(city, Grain);

        Assert.True(sold.Ok, sold.Message);
        Assert.True(drained > full);
        Assert.Equal(full, refilled);
    }

    [Fact]
    public void The_shelf_refills_by_sixty_percent_of_the_gap_each_month()
    {
        using var kernel = _host.Boot();
        var trade = Trade(kernel);
        var city = City(kernel);

        trade.Buy(city, Grain, 20);
        Assert.Equal(20, trade.StallStock(city, Grain));

        kernel.AdvanceDays(31);

        // gap 20 → refill 12
        Assert.Equal(32, trade.StallStock(city, Grain));
    }

    [Fact]
    public void A_stall_refuses_to_buy_more_than_its_capacity()
    {
        using var kernel = _host.Boot();
        var trade = Trade(kernel);
        var city = City(kernel);
        trade.Buy(city, Grain, 5);

        // Simulate a shelf already near its buy-back ceiling (baseline 40 → capacity 80).
        kernel.Db.Execute(
            "UPDATE mk_stall_listing SET stock = 79 WHERE citadel_id=@city AND item_id=@item",
            new DbArgs().Add("@city", city).Add("@item", Grain));

        var outcome = trade.Sell(city, Grain, 2);

        Assert.False(outcome.Ok);
        Assert.Contains("thu mua", outcome.Message);
    }

    [Fact]
    public void Sold_goods_raise_the_shelf_and_the_price_curve_follows()
    {
        using var kernel = _host.Boot();
        var trade = Trade(kernel);
        var city = City(kernel);

        trade.Buy(city, Grain, 40);
        Assert.Equal(0, trade.StallStock(city, Grain));
        var emptyShelfBid = trade.QuoteSell(city, Grain);

        trade.Sell(city, Grain, 40);
        var fullShelfBid = trade.QuoteSell(city, Grain);

        Assert.True(fullShelfBid < emptyShelfBid, $"a fuller shelf should buy back cheaper: {emptyShelfBid} -> {fullShelfBid}");
    }

    [Fact]
    public void Invariants_hold_after_a_supply_cycle()
    {
        using var kernel = _host.Boot();
        var trade = Trade(kernel);
        var city = City(kernel);

        trade.Buy(city, Grain, 30);
        trade.Sell(city, Grain, 10);
        kernel.AdvanceDays(31);
        trade.Buy(city, Grain, 5);

        Assert.Empty(kernel.RunInvariants());
    }

    private static IMarketTrade Trade(SimKernel kernel) =>
        kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade);

    private static long City(SimKernel kernel) =>
        kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState).CurrentCityId;
}
