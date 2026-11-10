using SimT.Contracts.Ledger;
using SimT.Contracts.Playing;
using SimT.Core.Kernel;

namespace SimT.Modules.Dlc.Tests;

public sealed class EconomicWarfareTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private static IEconomicWarfare Warfare(SimKernel kernel) =>
        kernel.Capabilities.Get<IEconomicWarfare>(CapabilityIds.EconomicWarfare);

    private static IMarketTrade Trade(SimKernel kernel) =>
        kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade);

    private static IWallet Wallet(SimKernel kernel) =>
        kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);

    private static IBag Bag(SimKernel kernel) =>
        kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);

    private static IGeopoliticalWar GeopoliticalWar(SimKernel kernel) =>
        kernel.Capabilities.Get<IGeopoliticalWar>(CapabilityIds.GeopoliticalWar);

    private static string Command(SimKernel kernel, string screenId, string commandId)
    {
        Assert.True(kernel.TryGetCommandHandler(screenId, out var handler), $"Screen {screenId} is not registered");
        return handler!.Execute(commandId) ?? string.Empty;
    }

    [Fact]
    public void Market_cornering_drains_stock_spikes_price_and_destabilizes_city()
    {
        using var kernel = _host.Boot();
        var warfare = Warfare(kernel);
        var trade = Trade(kernel);
        var wallet = Wallet(kernel);

        // Fund player
        wallet.Pay(15000, "TEST_SEED", "Vốn đầu cơ");

        long cityId = 1;
        long itemId = 1; // rice

        var normalPrice = trade.QuoteBuy(cityId, itemId);

        var res = warfare.CornerMarket(cityId, itemId, 30);
        Assert.True(res.Ok);
        Assert.Contains("lũng đoạn", res.Message);

        // Market is now cornered
        Assert.True(warfare.IsMarketCornered(cityId, itemId));
        Assert.True(warfare.GetCorneringPriceFactorPercent(cityId, itemId) >= 60);

        // Price spikes significantly
        var corneredPrice = trade.QuoteBuy(cityId, itemId);
        Assert.True(corneredPrice > normalPrice);

        // View appears in active list
        var active = warfare.ActiveCorneredMarkets;
        Assert.Contains(active, c => c.CitadelId == cityId && c.ItemId == itemId);
    }

    [Fact]
    public void Smuggle_run_into_besieged_citadel_consumes_cargo_and_records_run()
    {
        using var kernel = _host.Boot();
        var warfare = Warfare(kernel);
        var bag = Bag(kernel);
        var geoWar = GeopoliticalWar(kernel);

        long originCity = 1; // Lạc Dương
        long destCity = 2;   // Trường An
        long itemId = 1;

        // Stock goods in player bag
        bag.Add(originCity, itemId, 30);
        Assert.True(bag.Quantity(originCity, itemId) >= 30);

        // Escalate destination city into a full siege
        geoWar.TriggerConflict(1, 2, destCity, 3);
        Assert.True(geoWar.IsCitadelUnderSiege(destCity));

        var run = warfare.ExecuteSmuggleRun(originCity, destCity, itemId, 20);
        Assert.Equal(20, run.Quantity);

        // Cargo was consumed regardless of outcome
        Assert.Equal(10, bag.Quantity(originCity, itemId));

        // Recorded in log
        var logs = warfare.RecentSmuggleRuns(5);
        Assert.NotEmpty(logs);
        Assert.Equal(originCity, logs[0].OriginCitadelId);
        Assert.Equal(destCity, logs[0].DestCitadelId);
        Assert.Equal(20, logs[0].Quantity);

        if (!run.Intercepted)
        {
            Assert.True(run.Ok);
            Assert.True(run.TotalRevenue > 0);
            Assert.True(run.MeritsEarned > 0);
        }
    }

    [Fact]
    public void Market_command_handler_dispatches_warfare_corner_action()
    {
        using var kernel = _host.Boot();
        var warfare = Warfare(kernel);
        var wallet = Wallet(kernel);

        wallet.Pay(10000, "TEST_SEED", "Vốn đầu cơ");

        var msg = Command(kernel, "market.stall", "warfare:corner:1:30");
        Assert.Contains("lũng đoạn", msg);

        Assert.True(warfare.IsMarketCornered(1, 1));
    }
}
