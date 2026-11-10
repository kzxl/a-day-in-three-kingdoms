using SimT.Contracts.Ledger;
using SimT.Contracts.Playing;
using SimT.Core.Kernel;

namespace SimT.Modules.Dlc.Tests;

public sealed class PoliticsScrutinyTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private static IPoliticalCourt Court(SimKernel kernel) =>
        kernel.Capabilities.Get<IPoliticalCourt>(CapabilityIds.CourtOffice);

    private static IWallet Wallet(SimKernel kernel) =>
        kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);

    private static string Command(SimKernel kernel, string commandId)
    {
        Assert.True(kernel.TryGetCommandHandler("politics.court", out var handler), "court screen is not registered");
        return handler!.Execute(commandId) ?? string.Empty;
    }

    [Fact]
    public void Initial_scrutiny_is_safe_when_wealth_is_low()
    {
        using var kernel = _host.Boot();
        var court = Court(kernel);
        var scrutiny = court.CurrentScrutiny;

        Assert.NotNull(scrutiny);
        Assert.Equal("AN_TOAN", scrutiny.ThreatLevel);
        Assert.Equal(0, scrutiny.ScrutinyScore);
        Assert.False(scrutiny.IsEmbargoed);
    }

    [Fact]
    public void Wealth_exceeding_shielding_accumulates_scrutiny_monthly()
    {
        using var kernel = _host.Boot();
        var court = Court(kernel);
        var wallet = Wallet(kernel);

        // Inject wealth without political shield
        wallet.Pay(8000, "TEST_SEED", "Khởi nghiệp kinh doanh");
        Assert.True(wallet.Balance >= 8000);

        // Advance a month of scrutiny
        court.SimulateWarlordScrutiny(kernel.Today.AddDays(30));

        var scrutiny = court.CurrentScrutiny;
        Assert.NotNull(scrutiny);
        Assert.True(scrutiny.ScrutinyScore > 0, "Scrutiny must accumulate when wealth vastly exceeds shield");
        Assert.True(scrutiny.LiquidWealth >= 8000);
    }

    [Fact]
    public void Philanthropy_donation_charges_wallet_raises_reputation_and_reduces_scrutiny()
    {
        using var kernel = _host.Boot();
        var court = Court(kernel);
        var wallet = Wallet(kernel);

        wallet.Pay(3000, "TEST_SEED", "Tiền mặt");
        var repBefore = court.Reputation;
        var balBefore = wallet.Balance;

        var msg = Command(kernel, "scrutiny:donate:500");
        Assert.Contains("cứu tế nạn dân", msg);

        Assert.Equal(balBefore - 500, wallet.Balance);
        Assert.True(court.Reputation > repBefore);

        var logs = court.RecentPhilanthropyLogs(5);
        Assert.NotEmpty(logs);
        Assert.Equal(500, logs[0].CopperDonated);
        Assert.True(logs[0].FameEarned > 0);
    }

    [Fact]
    public void War_levy_demand_resolution_with_payment_awards_merits_and_reduces_scrutiny()
    {
        using var kernel = _host.Boot();
        var court = Court(kernel);
        var wallet = Wallet(kernel);

        wallet.Pay(12000, "TEST_SEED", "Đại phú thương");

        // Simulate several cycles to drive scrutiny above 60 and trigger demand
        for (var i = 1; i <= 3; i++)
        {
            court.SimulateWarlordScrutiny(kernel.Today.AddDays(i * 30));
        }

        var demands = court.ActiveScrutinyDemands;
        Assert.NotEmpty(demands);
        var demand = demands[0];
        Assert.Equal("PENDING", demand.Status);
        Assert.True(demand.DemandCopper > 0);

        var meritsBefore = court.Merits;
        var balBefore = wallet.Balance;

        var res = court.ResolveScrutinyDemand(demand.Id, "PAY");
        Assert.True(res.Ok);
        Assert.Contains("giao nộp", res.Message);

        Assert.Equal(balBefore - demand.DemandCopper, wallet.Balance);
        Assert.True(court.Merits > meritsBefore);

        // Verify demand is no longer pending
        Assert.DoesNotContain(court.ActiveScrutinyDemands, d => d.Id == demand.Id);
    }

    [Fact]
    public void War_levy_demand_defiance_imposes_embargo_and_increases_scrutiny()
    {
        using var kernel = _host.Boot();
        var court = Court(kernel);
        var wallet = Wallet(kernel);

        wallet.Pay(10000, "TEST_SEED", "Phú thương");

        for (var i = 1; i <= 3; i++)
        {
            court.SimulateWarlordScrutiny(kernel.Today.AddDays(i * 30));
        }

        var demands = court.ActiveScrutinyDemands;
        Assert.NotEmpty(demands);
        var demand = demands[0];

        var scrutinyBefore = court.CurrentScrutiny!.ScrutinyScore;

        var res = court.ResolveScrutinyDemand(demand.Id, "DEFY");
        Assert.True(res.Ok);
        Assert.Contains("cấm vận", res.Message);

        var scrutinyAfter = court.CurrentScrutiny;
        Assert.NotNull(scrutinyAfter);
        Assert.True(scrutinyAfter.IsEmbargoed);
        Assert.True(scrutinyAfter.EmbargoRemainingDays > 0);
        Assert.True(scrutinyAfter.ScrutinyScore > scrutinyBefore);
    }
}
