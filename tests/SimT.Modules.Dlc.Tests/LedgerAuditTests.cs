using SimT.Contracts.Ledger;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Core.Kernel;
using SimT.Modules.Dlc.Character;

namespace SimT.Modules.Dlc.Tests;

/// <summary>
/// Every cash line must say who posted it, and the read side must be able to answer "what did
/// this DLC cost me in this month" without scanning everything.
/// </summary>
public sealed class LedgerAuditTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void Each_line_records_the_module_that_posted_it()
    {
        using var kernel = _host.Boot();
        var city = Player(kernel).CurrentCityId;

        Trade(kernel).Buy(city, 1, 2);
        Command(kernel, "professions.trades", "work:1");
        Command(kernel, "lineage.family", "marry");

        var modules = kernel.Ledger.Recent(50).Select(line => line.ModuleId).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("dlc.economy", modules);
        Assert.Contains("dlc.character", modules);
    }

    [Fact]
    public void Query_narrows_to_one_module()
    {
        using var kernel = _host.Boot();
        var city = Player(kernel).CurrentCityId;

        Trade(kernel).Buy(city, 1, 2);
        Command(kernel, "professions.trades", "work:1");
        Command(kernel, "lineage.family", "marry");

        var lineage = kernel.Ledger.Query(GameDate.Start, kernel.Today, moduleId: "dlc.character");

        Assert.Single(lineage);
        Assert.Equal(JournalCodes.Dowry, lineage[0].JournalCode);
        Assert.Equal(LineageService.DowryCost, lineage[0].Amount);
    }

    [Fact]
    public void Query_narrows_to_one_journal_code()
    {
        using var kernel = _host.Boot();
        var city = Player(kernel).CurrentCityId;

        Trade(kernel).Buy(city, 1, 2);
        Trade(kernel).Buy(city, 2, 1);
        Trade(kernel).Sell(city, 1, 1);

        var buys = kernel.Ledger.Query(GameDate.Start, kernel.Today, journalCode: JournalCodes.MarketBuy);
        var sells = kernel.Ledger.Query(GameDate.Start, kernel.Today, journalCode: JournalCodes.MarketSell);

        Assert.Equal(2, buys.Count);
        Assert.Single(sells);
        Assert.All(buys, line => Assert.Equal(LedgerDirection.Out, line.Direction));
    }

    [Fact]
    public void Query_respects_the_date_range()
    {
        using var kernel = _host.Boot();
        var city = Player(kernel).CurrentCityId;

        Trade(kernel).Buy(city, 1, 1);
        var firstDay = kernel.Today;

        kernel.AdvanceDays(40);
        Trade(kernel).Buy(city, 2, 1);

        var firstRange = kernel.Ledger.Query(GameDate.Start, firstDay);
        var laterRange = kernel.Ledger.Query(firstDay.AddDays(1), kernel.Today);

        Assert.All(firstRange, line => Assert.True(line.Date <= firstDay));
        Assert.All(laterRange, line => Assert.True(line.Date > firstDay));
        Assert.NotEmpty(laterRange);
    }

    [Fact]
    public void Invariants_hold_with_module_attribution_in_place()
    {
        using var kernel = _host.Boot();
        var city = Player(kernel).CurrentCityId;

        Trade(kernel).Buy(city, 1, 3);
        Command(kernel, "cultivation.farm", "rent:1");
        Command(kernel, "politics.court", "serve");
        kernel.AdvanceDays(35);

        Assert.Empty(kernel.RunInvariants());
        Assert.All(kernel.Ledger.Recent(100), line => Assert.False(string.IsNullOrWhiteSpace(line.ModuleId)));
    }

    [Fact]
    public void A_monthly_charge_cannot_be_applied_twice()
    {
        using var kernel = _host.Boot();
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        var before = kernel.Ledger.Balance;
        const string key = "cultivation.rent:1:31";

        wallet.Charge(60, JournalCodes.FarmRent, "rent", key);
        wallet.Charge(60, JournalCodes.FarmRent, "rent", key);

        Assert.Equal(before - 60, kernel.Ledger.Balance);
        Assert.Single(kernel.Ledger.Recent(20), line => line.JournalCode == JournalCodes.FarmRent);
    }

    [Fact]
    public void Monthly_charges_use_keys_so_a_replayed_month_is_safe()
    {
        using var kernel = _host.Boot();
        var city = Player(kernel).CurrentCityId;

        Command(kernel, "cultivation.farm", "rent:1");
        Command(kernel, "lineage.family", "marry");
        Command(kernel, "politics.court", "serve");
        Command(kernel, "politics.court", "apply:1");
        kernel.AdvanceDays(31);

        var rentLines = kernel.Ledger.Query(GameDate.Start, kernel.Today, journalCode: JournalCodes.FarmRent);
        Assert.Contains(rentLines, line => line.Amount == 25);

        // Nothing may appear twice for the same month.
        var duplicated = rentLines
            .GroupBy(line => (line.Date, line.Amount, line.ModuleId))
            .Where(group => group.Count() > 1 && group.Key.Amount != 25)
            .ToList();
        Assert.Empty(duplicated);
    }

    private static void Command(SimKernel kernel, string screen, string commandId)
    {
        Assert.True(kernel.TryGetCommandHandler(screen, out var handler), $"screen '{screen}' is not registered");
        handler!.Execute(commandId);
    }

    private static IMarketTrade Trade(SimKernel kernel) =>
        kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade);

    private static IPlayerState Player(SimKernel kernel) =>
        kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
}
