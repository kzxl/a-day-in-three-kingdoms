using SimT.Contracts.Playing;
using SimT.Modules.Dlc.Market;

namespace SimT.Modules.Market.Tests;

/// <summary>
/// The pricing path is the one place a settlement loop runs thousands of times per month, so
/// it must not allocate. It is also the place where supply pressure lives, so the curves are
/// pinned by tests rather than left to drift.
/// </summary>
public sealed class PricingAllocationTests
{
    private static readonly ItemDef Grain = new(1, "gao", "Gạo", "rice", "grain", 12, 60, 10);
    private static readonly CitadelDef Plain = new(1, "lac_duong", "Lạc Dương", "Luoyang", "trung_nguyen", 1000);
    private static readonly CitadelDef Dear = new(2, "truong_an", "Trường An", "Chang'an", "quan_trung", 1120);

    [Fact]
    public void Quote_allocates_nothing_over_ten_thousand_calls()
    {
        var sink = 0L;
        for (var warmup = 0; warmup < 100; warmup++)
        {
            sink += MarketPricing.Quote(Grain, Plain, 1, 40, 40);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++)
        {
            sink += MarketPricing.Quote(Grain, Plain, (i % 12) + 1, 40, 40);
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

        Assert.Equal(100, MarketPricing.Quote(item, Plain, 1, 40, 40));
        Assert.Equal(105, MarketPricing.Quote(item, Plain, 5, 40, 40));
        Assert.Equal(95, MarketPricing.Quote(item, Plain, 8, 40, 40));
        Assert.Equal(110, MarketPricing.Quote(item, Plain, 11, 40, 40));
    }

    [Fact]
    public void Regional_factor_applies_before_season()
    {
        var item = new ItemDef(1, "gao", "Gạo", "rice", "grain", 100, 60, 10);

        Assert.Equal(112, MarketPricing.Quote(item, Dear, 1, 40, 40));
        Assert.Equal(118, MarketPricing.Quote(item, Dear, 5, 40, 40));
    }

    [Fact]
    public void Scarcity_is_neutral_at_baseline()
    {
        Assert.Equal(1000, MarketPricing.ScarcityPermille(40, 40));
    }

    [Fact]
    public void Scarcity_peaks_when_the_shelf_is_empty()
    {
        Assert.Equal(MarketPricing.ScarcityCeilingPermille, MarketPricing.ScarcityPermille(0, 40));
    }

    [Fact]
    public void Scarcity_falls_to_its_floor_at_double_baseline_and_stays_there()
    {
        Assert.Equal(MarketPricing.ScarcityFloorPermille, MarketPricing.ScarcityPermille(80, 40));
        Assert.Equal(MarketPricing.ScarcityFloorPermille, MarketPricing.ScarcityPermille(400, 40));
    }

    [Fact]
    public void An_empty_shelf_is_forty_percent_dearer_than_a_full_one()
    {
        var item = new ItemDef(1, "gao", "Gạo", "rice", "grain", 100, 60, 10);

        var atBaseline = MarketPricing.Quote(item, Plain, 1, 40, 40);
        var empty = MarketPricing.Quote(item, Plain, 1, 0, 40);
        var overstocked = MarketPricing.Quote(item, Plain, 1, 80, 40);

        Assert.Equal(140, empty);
        Assert.True(empty > atBaseline);
        Assert.True(overstocked < atBaseline);
    }

    [Fact]
    public void Buy_back_capacity_is_twice_the_baseline()
    {
        Assert.Equal(80, MarketPricing.BuyBackCapacity(40));
        Assert.Equal(0, MarketPricing.BuyBackCapacity(0));
    }
}
