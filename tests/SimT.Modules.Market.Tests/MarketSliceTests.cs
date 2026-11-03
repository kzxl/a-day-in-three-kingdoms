using System.Security.Cryptography;
using System.Text;
using SimT.Contracts.Playing;
using SimT.Core.Kernel;
using SimT.Testing;

namespace SimT.Modules.Market.Tests;

/// <summary>
/// Boots the real kernel with the real module artefacts. If a DLC boundary is fake, these
/// tests fail before any UI work is attempted.
/// </summary>
public sealed class MarketSliceTests : IDisposable
{

    private readonly TestHost _host = new("simt-market");

    public void Dispose() => _host.Dispose();

    [Fact]
    public void Kernel_boots_with_the_product_composition()
    {
        using var kernel = Boot();

        Assert.Equal(kernel.Product.Modules.Count, kernel.Modules.Count);
        Assert.Contains(kernel.Modules, module => module.Manifest.Id == "base.content");
        Assert.Contains(kernel.Modules, module => module.Manifest.Id == "dlc.market");
        Assert.True(kernel.Capabilities.IsProvided(CapabilityIds.MarketTrade));
        Assert.True(kernel.Capabilities.IsProvided(CapabilityIds.MarketWarehouse));
    }

    [Fact]
    public void Base_modules_start_before_the_dlc_that_requires_them()
    {
        using var kernel = Boot();
        var order = kernel.Modules.Select(module => module.Manifest.Id).ToList();

        Assert.True(order.IndexOf("base.player") < order.IndexOf("dlc.market"));
        Assert.True(order.IndexOf("base.world") < order.IndexOf("base.player"));
    }

    [Fact]
    public void Buy_moves_cash_through_the_ledger_and_puts_goods_in_the_bag()
    {
        using var kernel = Boot();
        var trade = kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade);
        var bag = kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);

        var before = kernel.Ledger.Balance;
        var price = trade.QuoteBuy(player.CurrentCityId, 1);
        var outcome = trade.Buy(player.CurrentCityId, 1, 5);

        Assert.True(outcome.Ok, outcome.Message);
        Assert.Equal(before - (price * 5), kernel.Ledger.Balance);
        Assert.Equal(5, bag.Quantity(player.CurrentCityId, 1));
    }

    [Fact]
    public void Buy_fails_loudly_when_the_stall_is_empty()
    {
        using var kernel = Boot();
        var trade = kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);

        var outcome = trade.Buy(player.CurrentCityId, 1, 10_000);

        Assert.False(outcome.Ok);
        Assert.Contains("không đủ hàng", outcome.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Selling_pays_sixty_percent_of_the_ask()
    {
        using var kernel = Boot();
        var trade = kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);

        trade.Buy(player.CurrentCityId, 2, 1);
        var sellQuote = trade.QuoteSell(player.CurrentCityId, 2);
        var before = kernel.Ledger.Balance;

        var outcome = trade.Sell(player.CurrentCityId, 2, 1);

        Assert.True(outcome.Ok);
        Assert.Equal(sellQuote, outcome.UnitPrice);
        Assert.Equal(before + sellQuote, kernel.Ledger.Balance);
    }

    [Fact]
    public void Warehouse_rent_is_charged_again_at_the_month_boundary()
    {
        using var kernel = Boot();
        var warehouse = kernel.Capabilities.Get<IMarketWarehouse>(CapabilityIds.MarketWarehouse);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);

        Assert.True(warehouse.Rent(player.CurrentCityId, out _));
        var afterRent = kernel.Ledger.Balance;
        var rent = warehouse.DefaultMonthlyRent;

        kernel.AdvanceDays(31);

        Assert.Equal(afterRent - rent, kernel.Ledger.Balance);
        Assert.Contains(
            kernel.Ledger.Recent(100),
            line => line.JournalCode == Contracts.Ledger.JournalCodes.WarehouseRent);
    }

    [Fact]
    public void Warehouse_rejects_more_than_its_capacity()
    {
        using var kernel = Boot();
        var warehouse = kernel.Capabilities.Get<IMarketWarehouse>(CapabilityIds.MarketWarehouse);
        var trade = kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);

        warehouse.Rent(player.CurrentCityId, out _);
        trade.Buy(player.CurrentCityId, 1, 8);

        Assert.True(warehouse.Store(player.CurrentCityId, 1, 5, out _));
        Assert.Equal(5, warehouse.StockAt(player.CurrentCityId, 1));
        Assert.True(warehouse.Take(player.CurrentCityId, 1, 2, out _));
        Assert.Equal(3, warehouse.StockAt(player.CurrentCityId, 1));
    }

    [Fact]
    public void Same_seed_and_same_actions_produce_the_same_ledger()
    {
        using var first = new TestHost("simt-market-a");
        using var second = new TestHost("simt-market-b");

        Assert.Equal(RunScript(first), RunScript(second));
    }

    private static string RunScript(TestHost host)
    {
        using var kernel = host.Boot();
        var trade = kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade);
        var warehouse = kernel.Capabilities.Get<IMarketWarehouse>(CapabilityIds.MarketWarehouse);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);

        for (var day = 0; day < 3; day++)
        {
            trade.Buy(player.CurrentCityId, (day % 2) + 1, 2);
            trade.Sell(player.CurrentCityId, 1, 1);
            warehouse.Rent(player.CurrentCityId, out _);
            kernel.AdvanceDays(10);
        }

        kernel.Save();

        var builder = new StringBuilder();
        foreach (var line in kernel.Ledger.Recent(10_000))
        {
            builder.Append(line.Date).Append('|').Append(line.JournalCode).Append('|')
                .Append((int)line.Direction).Append('|').Append(line.Amount).Append('|')
                .Append(line.BalanceAfter).Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private SimKernel Boot() => _host.Boot();
}
