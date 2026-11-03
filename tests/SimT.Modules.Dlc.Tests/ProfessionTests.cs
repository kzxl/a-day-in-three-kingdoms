using SimT.Contracts.Playing;
using SimT.Core.Kernel;

namespace SimT.Modules.Dlc.Tests;

/// <summary>Day labour: a batch must cost days and tools, and practice must actually pay off.</summary>
public sealed class ProfessionTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void A_batch_costs_days_and_tools_and_yields_goods()
    {
        using var kernel = _host.Boot();
        var player = Player(kernel);
        var trade = Guild(kernel).Trades.First(candidate => candidate.Code == "nong_phu");
        var beforeBalance = kernel.Ledger.Balance;
        var beforeDate = kernel.Today;

        var message = Command(kernel, $"work:{trade.Id}");

        Assert.Contains("Nông Phu", message);
        Assert.Equal(beforeDate.AddDays(trade.DaysPerBatch), kernel.Today);
        Assert.Equal(beforeBalance - trade.ToolFee, kernel.Ledger.Balance);
        Assert.Equal(trade.BaseQuantity, Bag(kernel).Quantity(player.CurrentCityId, trade.OutputItemId));
        Assert.Equal(1, Guild(kernel).PracticeCount(trade.Id));
    }

    [Fact]
    public void A_batch_without_cash_is_refused_and_time_does_not_move()
    {
        using var kernel = _host.Boot();
        var before = kernel.Today;

        kernel.Ledger.Move(kernel.Today, kernel.Ledger.Liquid - 1, toSafe: true, Contracts.Ledger.JournalCodes.SafeDeposit, "cất két");

        var message = Command(kernel, "work:4");

        Assert.Contains("Quan", message);
        Assert.Equal(before, kernel.Today);
        Assert.Equal(0, Guild(kernel).PracticeCount(4));
    }

    [Fact]
    public void Practice_raises_the_tier_and_the_yield()
    {
        using var kernel = _host.Boot();
        var trade = Guild(kernel).Trades.First(candidate => candidate.Code == "nong_phu");

        for (var batch = 0; batch < ProfessionServiceConstants.PracticesPerTier; batch++)
        {
            Command(kernel, $"work:{trade.Id}");
        }

        Assert.Equal(1, Guild(kernel).Tier(trade.Id));

        var before = Bag(kernel).Quantity(Player(kernel).CurrentCityId, trade.OutputItemId);
        Command(kernel, $"work:{trade.Id}");
        var gained = Bag(kernel).Quantity(Player(kernel).CurrentCityId, trade.OutputItemId) - before;

        Assert.Equal(trade.BaseQuantity + 1, gained);
    }

    [Fact]
    public void Tier_is_capped()
    {
        using var kernel = _host.Boot();
        var trade = Guild(kernel).Trades.First(candidate => candidate.Code == "nong_phu");

        for (var batch = 0; batch < 40; batch++)
        {
            Command(kernel, $"work:{trade.Id}");
        }

        Assert.Equal(5, Guild(kernel).Tier(trade.Id));
    }

    [Fact]
    public void An_unknown_trade_is_refused()
    {
        using var kernel = _host.Boot();

        var message = Command(kernel, "work:99");

        Assert.Contains("không tồn tại", message);
    }

    [Fact]
    public void Invariants_hold_after_many_batches()
    {
        using var kernel = _host.Boot();

        for (var batch = 0; batch < 12; batch++)
        {
            Command(kernel, "work:1");
            Command(kernel, "work:3");
        }

        Assert.Empty(kernel.RunInvariants());
    }

    private static string Command(SimKernel kernel, string commandId)
    {
        Assert.True(kernel.TryGetCommandHandler("professions.trades", out var handler), "professions screen is not registered");
        return handler!.Execute(commandId) ?? string.Empty;
    }

    private static IPlayerState Player(SimKernel kernel) =>
        kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);

    private static IBag Bag(SimKernel kernel) =>
        kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);

    private static IProfessionGuild Guild(SimKernel kernel) =>
        kernel.Capabilities.Get<IProfessionGuild>(CapabilityIds.ProfessionGuild);
}

/// <summary>Mirror of the service constants for tests, so a change on either side is visible.</summary>
internal static class ProfessionServiceConstants
{
    public const int PracticesPerTier = 4;
}
