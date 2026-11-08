using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Core.Kernel;
using SimT.Testing;
using Xunit;

namespace SimT.Modules.Market.Tests;

public sealed class ValueChainTests : IDisposable
{
    private readonly TestHost _host = new("simt-value-chain");

    public void Dispose() => _host.Dispose();

    [Fact]
    public void Value_chain_classifies_staple_goods_across_all_four_tiers()
    {
        using var kernel = _host.Boot();
        var valueChain = kernel.Capabilities.Get<IValueChainService>(CapabilityIds.ValueChain);

        var rice = valueChain.GetProfile("gao");
        var salt = valueChain.GetProfile("muoi");
        var wine = valueChain.GetProfile("ruou");
        var tea = valueChain.GetProfile("tra");

        Assert.Equal(EconomicValueTier.Tier1_Raw, rice.Tier);
        Assert.Equal(EconomicValueTier.Tier2_Processed, salt.Tier);
        Assert.Equal(EconomicValueTier.Tier3_Manufactured, wine.Tier);
        Assert.Equal(EconomicValueTier.Tier4_Luxury_EndSink, tea.Tier);

        Assert.Contains("ruou", rice.Outputs);
        Assert.Contains("gao", wine.Inputs);
    }

    [Fact]
    public void Demand_status_detects_scarcity_equilibrium_and_surplus()
    {
        using var kernel = _host.Boot();
        var valueChain = kernel.Capabilities.Get<IValueChainService>(CapabilityIds.ValueChain);
        var trade = kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade);
        var items = kernel.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);
        var map = kernel.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);

        var city = map.Citadels[0].Id;
        var grain = items.FindByCode("gao")!;

        // Baseline is 40 -> Normal equilibrium
        var normal = valueChain.GetDemandStatus(city, grain.Id);
        Assert.Contains("Ổn định", normal.VelocityLevelVi);

        // Buy down to 10 -> Scarcity (< baseline * 0.5)
        trade.Buy(city, grain.Id, 30);
        var scarce = valueChain.GetDemandStatus(city, grain.Id);
        Assert.Contains("Khan hiếm", scarce.VelocityLevelVi);

        // Sell 60 -> Surplus (> baseline * 1.3)
        kernel.Db.Execute(
            "UPDATE mk_stall_listing SET stock = 65 WHERE citadel_id=@city AND item_id=@item",
            new DbArgs().Add("@city", city).Add("@item", grain.Id));
        var surplus = valueChain.GetDemandStatus(city, grain.Id);
        Assert.Contains("Dư thừa", surplus.VelocityLevelVi);
    }

    [Fact]
    public void Civic_consumption_digests_surplus_stock_back_toward_baseline()
    {
        using var kernel = _host.Boot();
        var valueChain = kernel.Capabilities.Get<IValueChainService>(CapabilityIds.ValueChain);
        var trade = kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade);
        var items = kernel.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);
        var map = kernel.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);

        var city = map.Citadels[0].Id;
        var grain = items.FindByCode("gao")!;

        // Set huge surplus: stock 80 (baseline 40)
        kernel.Db.Execute(
            "UPDATE mk_stall_listing SET stock = 80 WHERE citadel_id=@city AND item_id=@item",
            new DbArgs().Add("@city", city).Add("@item", grain.Id));

        Assert.Equal(80, trade.StallStock(city, grain.Id));

        // Advance a month (31 days) to trigger MonthClosed and civic consumption
        kernel.AdvanceDays(31);

        var stockAfter = trade.StallStock(city, grain.Id);
        Assert.True(stockAfter < 80, $"Surplus stock should decrease, got {stockAfter}");
        Assert.True(stockAfter >= 40, $"Stock should not fall below baseline, got {stockAfter}");
    }

    [Fact]
    public void Regional_resource_nodes_cover_all_citadels_and_extract_specialty_goods()
    {
        using var kernel = _host.Boot();
        var valueChain = kernel.Capabilities.Get<IValueChainService>(CapabilityIds.ValueChain);
        var trade = kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade);

        Assert.Equal(20, valueChain.AllResourceNodes.Count);

        var shangdangNodes = valueChain.GetResourceNodes(18);
        Assert.Single(shangdangNodes);
        Assert.Equal("MINE", shangdangNodes[0].NodeType);
        Assert.Equal(5, shangdangNodes[0].PrimaryItemId); // sat
        Assert.Equal(15, shangdangNodes[0].SecondaryItemId); // than_da

        var ironBefore = trade.StallStock(18, 5);
        var coalBefore = trade.StallStock(18, 15);

        var extracted = valueChain.SimulateRegionalExtraction(18);
        Assert.True(extracted > 0);
        Assert.True(trade.StallStock(18, 5) > ironBefore);
        Assert.True(trade.StallStock(18, 15) > coalBefore);

        var stallScreen = kernel.Ui.Screens.First(s => s.ScreenId == "market.stall");
        var model = stallScreen.Render();
        Assert.Contains(model.Sections, s => s.Heading.Contains("Mỏ Khoáng & Nguồn Cung Đặc Sản Bản Địa"));
    }
}
