using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Core.Kernel;
using SimT.Modules.Dlc.Economy;
using SimT.Modules.Dlc.Economy.Shocks;
using Xunit;

namespace SimT.Modules.Dlc.Tests;

public sealed class TradeCompassLogicTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void MarketTrade_provides_arbitrage_opportunities_across_citadels()
    {
        using var kernel = _host.Boot();
        var trade = kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade);
        var catalog = kernel.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);
        var map = kernel.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);

        Assert.NotNull(trade);
        Assert.NotNull(catalog);
        Assert.NotNull(map);

        var citadels = map.Citadels;
        Assert.True(citadels.Count >= 20);

        var items = catalog.All.Where(i => !i.Category.Equals("Equip", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.NotEmpty(items);

        // Verify across all items that prices vary across citadels producing trade spreads
        var hasAnySpread = false;
        foreach (var item in items)
        {
            var prices = citadels.Select(c => trade.QuoteBuy(c.Id, item.Id)).ToList();
            var min = prices.Min();
            var max = prices.Max();
            if (max > min)
            {
                hasAnySpread = true;
                break;
            }
        }

        Assert.True(hasAnySpread, "Market prices across 20 citadels must have regional spreads for trade arbitrage");
    }

    [Fact]
    public void Arbitrage_corridors_can_be_scanned_and_ranked()
    {
        using var kernel = _host.Boot();
        var trade = kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade);
        var catalog = kernel.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);
        var map = kernel.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);
        var shockEngine = new MarketShockEngine(kernel.Db);

        var corridors = shockEngine.ScanArbitrageCorridors(map, catalog, trade, 6, kernel.Today);
        Assert.NotNull(corridors);

        foreach (var opp in corridors)
        {
            var item = catalog.FindByCode(opp.ItemCode);
            Assert.NotNull(item);
            Assert.True(opp.SourceCitadelId > 0);
            Assert.True(opp.TargetCitadelId > 0);
            Assert.True(opp.ProfitMarginPercent >= 0);
            Assert.Equal(trade.QuoteBuy(opp.SourceCitadelId, item.Id), opp.BuyPriceAtSource);
            Assert.Equal(trade.QuoteSell(opp.TargetCitadelId, item.Id), opp.SellPriceAtTarget);
        }
    }
}
