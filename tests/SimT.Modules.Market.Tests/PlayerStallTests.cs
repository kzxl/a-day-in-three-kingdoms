using SimT.Contracts.Ledger;
using SimT.Contracts.Playing;
using SimT.Core.Kernel;
using SimT.Testing;

namespace SimT.Modules.Market.Tests;

public sealed class PlayerStallTests : IDisposable
{
    private readonly TestHost _host = new("simt-market-stall");

    public void Dispose() => _host.Dispose();

    [Fact]
    public void RentStall_creates_stall_and_charges_rent()
    {
        using var kernel = Boot();
        var stall = kernel.Capabilities.Get<IPlayerStall>(CapabilityIds.PlayerStall);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);

        var before = kernel.Ledger.Liquid;
        var ok = stall.RentStall(player.CurrentCityId, out var msg);

        Assert.True(ok, msg);
        Assert.Equal(before - stall.DefaultStallRent, kernel.Ledger.Liquid);

        var info = stall.StallInfo(player.CurrentCityId);
        Assert.NotNull(info);
        Assert.Equal(stall.DefaultStallRent, info.MonthlyRent);
        Assert.True(info.IsOpen);
    }

    [Fact]
    public void Stock_and_unstock_moves_items_between_bag_and_shelf()
    {
        using var kernel = Boot();
        var trade = kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade);
        var stall = kernel.Capabilities.Get<IPlayerStall>(CapabilityIds.PlayerStall);
        var bag = kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var cityId = player.CurrentCityId;

        // Buy 5 rice into bag
        trade.Buy(cityId, 1, 5);
        Assert.Equal(5, bag.Quantity(cityId, 1));

        stall.RentStall(cityId, out _);

        // Stock 3 rice to shelf
        var stockOk = stall.Stock(cityId, 1, 3, out var stockMsg);
        Assert.True(stockOk, stockMsg);
        Assert.Equal(2, bag.Quantity(cityId, 1));

        var shelves = stall.Shelves(cityId);
        Assert.Single(shelves);
        Assert.Equal(1, shelves[0].ItemId);
        Assert.Equal(3, shelves[0].Quantity);

        // Unstock 1 rice from shelf
        var unstockOk = stall.Unstock(cityId, 1, 1, out var unstockMsg);
        Assert.True(unstockOk, unstockMsg);
        Assert.Equal(3, bag.Quantity(cityId, 1));

        shelves = stall.Shelves(cityId);
        Assert.Equal(2, shelves[0].Quantity);
    }

    [Fact]
    public void StockAll_moves_all_bag_goods_to_shelf()
    {
        using var kernel = Boot();
        var trade = kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade);
        var stall = kernel.Capabilities.Get<IPlayerStall>(CapabilityIds.PlayerStall);
        var bag = kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var cityId = player.CurrentCityId;

        trade.Buy(cityId, 1, 3);
        trade.Buy(cityId, 2, 2);
        stall.RentStall(cityId, out _);

        var ok = stall.StockAll(cityId, out var msg);
        Assert.True(ok, msg);

        Assert.Equal(0, bag.Quantity(cityId, 1));
        Assert.Equal(0, bag.Quantity(cityId, 2));

        var shelves = stall.Shelves(cityId);
        Assert.Equal(2, shelves.Count);
    }

    [Fact]
    public void DailyRetailSales_sells_goods_and_pays_player()
    {
        using var kernel = Boot();
        var trade = kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade);
        var stall = kernel.Capabilities.Get<IPlayerStall>(CapabilityIds.PlayerStall);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var cityId = player.CurrentCityId;

        trade.Buy(cityId, 1, 5);
        stall.RentStall(cityId, out _);
        stall.Stock(cityId, 1, 5, out _);

        // Price at market rate so it sells fast
        var marketPrice = trade.QuoteBuy(cityId, 1);
        stall.SetPrice(cityId, 1, marketPrice, out _);

        var liquidBefore = kernel.Ledger.Liquid;

        // Advance 1 day
        kernel.AdvanceDays(1);

        var shelves = stall.Shelves(cityId);
        var remaining = shelves.FirstOrDefault(s => s.ItemId == 1)?.Quantity ?? 0;
        Assert.True(remaining < 5, "Expected some units to be sold");
        Assert.True(kernel.Ledger.Liquid > liquidBefore, "Expected sales revenue deposited");

        var recentSales = kernel.Ledger.Recent(10).Where(l => l.JournalCode == JournalCodes.StallSale).ToList();
        Assert.NotEmpty(recentSales);
    }

    private SimKernel Boot() => _host.Boot();
}
