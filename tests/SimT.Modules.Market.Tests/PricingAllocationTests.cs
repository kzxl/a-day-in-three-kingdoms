using SimT.Contracts.Playing;
using SimT.Modules.Dlc.Market;

namespace SimT.Modules.Market.Tests;

/// <summary>
/// The pricing path is the one place a settlement loop runs thousands of times per month, so
/// it must not allocate. This is a budget test, not a micro-benchmark: it fails the moment
/// someone introduces a string, a LINQ call or a boxed value into the hot path.
/// </summary>
public sealed class PricingAllocationTests
{
    [Fact]
    public void Quote_allocates_nothing_over_ten_thousand_calls()
    {
        var item = new ItemDef(1, "gao", "Gạo", "rice", "grain", 12, 60, 10);
        var citadel = new CitadelDef(1, "lac_duong", "Lạc Dương", "Luoyang", "trung_nguyen", 1120);

        long sink = 0;
        for (var warmup = 0; warmup < 100; warmup++)
        {
            sink += MarketPricing.Quote(item, citadel, 1);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++)
        {
            sink += MarketPricing.Quote(item, citadel, (i % 12) + 1);
            sink += MarketPricing.SellQuote(sink % 1000 + 1);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.True(sink > 0);
    }

    [Fact]
    public void Season_scales_prices_the_same_way_for_every_call()
    {
        var item = new ItemDef(1, "gao", "Gạo", "rice", "grain", 100, 60, 10);
        var citadel = new CitadelDef(1, "x", "X", "X", "r", 1000);

        Assert.Equal(100, MarketPricing.Quote(item, citadel, 1));
        Assert.Equal(105, MarketPricing.Quote(item, citadel, 5));
        Assert.Equal(95, MarketPricing.Quote(item, citadel, 8));
        Assert.Equal(110, MarketPricing.Quote(item, citadel, 11));
    }

    [Fact]
    public void Regional_factor_applies_before_season()
    {
        var item = new ItemDef(1, "gao", "Gạo", "rice", "grain", 100, 60, 10);
        var dear = new CitadelDef(2, "y", "Y", "Y", "r", 1120);

        Assert.Equal(112, MarketPricing.Quote(item, dear, 1));
        Assert.Equal(118, MarketPricing.Quote(item, dear, 5));
    }
}
