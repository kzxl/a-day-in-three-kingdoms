using SimT.Contracts.Playing;
using SimT.Core.Kernel;
using SimT.Testing;

namespace SimT.Modules.Market.Tests;

/// <summary>
/// A price is worth reading as a series, not a number: one sample per day, kept for sixty days,
/// and it has to move when the shelf does.
/// </summary>
public sealed class PriceHistoryTests : IDisposable
{
    private const long Grain = 1;
    private const long Tea = 3;

    private readonly TestHost _host = new("simt-price-history");

    public void Dispose() => _host.Dispose();

    [Fact]
    public void One_sample_is_recorded_per_day()
    {
        using var kernel = _host.Boot();
        var trade = Trade(kernel);
        var city = City(kernel);

        Assert.Empty(trade.PriceHistory(city, Grain));

        kernel.AdvanceDays(3);

        var history = trade.PriceHistory(city, Grain);
        Assert.Equal(3, history.Count);
        Assert.All(history, price => Assert.True(price > 0));
    }

    [Fact]
    public void Draining_the_shelf_shows_up_in_the_series()
    {
        using var kernel = _host.Boot();
        var trade = Trade(kernel);
        var city = City(kernel);

        kernel.AdvanceDays(1);
        var first = trade.PriceHistory(city, Grain).Last();

        trade.Buy(city, Grain, 40);
        kernel.AdvanceDays(1);
        var afterDrain = trade.PriceHistory(city, Grain).Last();

        Assert.True(afterDrain > first, $"price should climb with scarcity: {first} -> {afterDrain}");
    }

    [Fact]
    public void History_keeps_only_the_last_sixty_days()
    {
        using var kernel = _host.Boot();
        var trade = Trade(kernel);
        var city = City(kernel);

        kernel.AdvanceDays(75);

        var history = trade.PriceHistory(city, Grain, days: 200);
        Assert.True(history.Count <= 60, $"expected at most 60 samples, got {history.Count}");
        Assert.True(history.Count >= 55, $"expected the window to stay full, got {history.Count}");
    }

    [Fact]
    public void Another_item_keeps_its_own_series()
    {
        using var kernel = _host.Boot();
        var trade = Trade(kernel);
        var city = City(kernel);

        trade.Buy(city, Grain, 40);
        kernel.AdvanceDays(2);

        var grain = trade.PriceHistory(city, Grain);
        var tea = trade.PriceHistory(city, Tea);

        // Buying is a two-day action, so the buy itself already produced samples; what matters
        // is that each item keeps its own series.
        Assert.NotEmpty(grain);
        Assert.Equal(grain.Count, tea.Count);
        Assert.NotEqual(grain.Last(), tea.Last());
    }

    [Fact]
    public void Invariants_hold_while_prices_are_sampled()
    {
        using var kernel = _host.Boot();
        var trade = Trade(kernel);
        var city = City(kernel);

        trade.Buy(city, Grain, 30);
        kernel.AdvanceDays(31);
        trade.Sell(city, Grain, 10);

        Assert.Empty(kernel.RunInvariants());
    }

    private static IMarketTrade Trade(SimKernel kernel) =>
        kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade);

    private static long City(SimKernel kernel) =>
        kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState).CurrentCityId;
}
