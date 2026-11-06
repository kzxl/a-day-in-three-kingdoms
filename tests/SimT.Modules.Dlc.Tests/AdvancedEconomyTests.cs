using SimT.Contracts.Data;
using SimT.Contracts.Logging;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Modules.Dlc.Economy.Banking;
using SimT.Modules.Dlc.Economy.Caravan;
using SimT.Modules.Dlc.Economy.Shocks;
using Xunit;

namespace SimT.Modules.Dlc.Tests;

public sealed class AdvancedEconomyTests
{
    private sealed class TestLog : ILog
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message, Exception? error = null) { }
    }

    [Fact]
    public void MarketShockEngine_computes_geopolitical_elasticity()
    {
        using var host = new DlcTestHost();
        using var kernel = host.Boot();
        var shockEngine = new MarketShockEngine(kernel.Db);

        // Trigger a siege in Luoyang (citadel 1)
        shockEngine.TriggerShock(1, MarketShockTypes.Siege, 10, 30, 1500, "Quân phiệt bao vây Lạc Dương");

        var active = shockEngine.GetActiveShock(1, 20);
        Assert.NotNull(active);
        Assert.Equal(MarketShockTypes.Siege, active.ShockType);

        // Food price multiplier is severe during siege (>= 2500 permille = 2.5x)
        var foodMult = shockEngine.GetShockMultiplierPermille(1, "gao", 20);
        Assert.True(foodMult >= 2500);

        // Luxury price plummets during siege (<= 500 permille = 0.5x)
        var luxuryMult = shockEngine.GetShockMultiplierPermille(1, "gam", 20);
        Assert.True(luxuryMult <= 500);

        // Normal city has 1000 baseline
        var normalMult = shockEngine.GetShockMultiplierPermille(2, "gao", 20);
        Assert.Equal(1000, normalMult);
    }

    [Fact]
    public void BankingService_handles_deposits_withdrawals_and_silver_drafts()
    {
        using var host = new DlcTestHost();
        using var kernel = host.Boot();
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        var initial = wallet.Liquid;
        var banking = new BankingService(kernel.Db, kernel.Clock, wallet, new TestLog());

        // Deposit 300 Quan
        var depRes = banking.Deposit(1, 300);
        Assert.True(depRes.Success);
        Assert.Equal(initial - 300, wallet.Liquid);

        var acc = banking.GetAccount(1);
        Assert.Equal(300, acc.DepositBalance);

        // Withdraw 100 Quan
        var withRes = banking.Withdraw(1, 100);
        Assert.True(withRes.Success);
        Assert.Equal(initial - 200, wallet.Liquid);
        Assert.Equal(200, banking.GetAccount(1).DepositBalance);

        // Issue a silver draft of 200 Quan (fee 2% = 4 Quan, total 204)
        var draftRes = banking.IssueDraft(1, 200);
        Assert.True(draftRes.Success);
        Assert.Equal(initial - 200 - 204, wallet.Liquid);

        var drafts = banking.GetActiveDrafts();
        Assert.Single(drafts);
        var serial = drafts[0].SerialNumber;
        Assert.Equal(200, drafts[0].FaceValue);

        // Redeem silver draft at another citadel (e.g. citadel 2)
        var redeemRes = banking.RedeemDraft(2, serial);
        Assert.True(redeemRes.Success);
        Assert.Empty(banking.GetActiveDrafts());
        Assert.Equal(initial - 204, wallet.Liquid); // 200 remaining in bank deposit, 4 spent on draft fee
    }

    [Fact]
    public void BankingService_supports_credit_loans_and_monthly_interest()
    {
        using var host = new DlcTestHost();
        using var kernel = host.Boot();
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        var initial = wallet.Liquid;
        var banking = new BankingService(kernel.Db, kernel.Clock, wallet, new TestLog());

        // Borrow 500 Quan
        var loanRes = banking.BorrowLoan(1, 500);
        Assert.True(loanRes.Success);
        Assert.Equal(initial + 500, wallet.Liquid);
        Assert.Equal(500, banking.TotalActiveLoans());

        // Deposit 1000 Quan into bank
        banking.Deposit(1, 1000);
        Assert.Equal(1000, banking.GetAccount(1).DepositBalance);

        // Process monthly interest: deposit +1.5%, loan +5%
        banking.ProcessMonthlyInterest();
        var updatedAcc = banking.GetAccount(1);
        Assert.Equal(1015, updatedAcc.DepositBalance); // 1000 + 15
        Assert.Equal(525, updatedAcc.LoanBalance);     // 500 + 25

        // Repay 525 Quan
        var repayRes = banking.RepayLoan(1, 525);
        Assert.True(repayRes.Success);
        Assert.Equal(0, banking.GetAccount(1).LoanBalance);
    }

    [Fact]
    public void AutomatedLogisticsService_manages_routes_and_cartels()
    {
        using var host = new DlcTestHost();
        using var kernel = host.Boot();
        var logistics = new AutomatedLogisticsService(kernel.Db, new TestLog());

        // Verify the 4 Great Historical Cartels exist
        var cartels = logistics.GetCartels();
        Assert.Equal(4, cartels.Count);
        Assert.Contains(cartels, c => c.CartelCode == "MI_SHI");
        Assert.Contains(cartels, c => c.CartelCode == "TRAN_SHI");
        Assert.Contains(cartels, c => c.CartelCode == "CHU_SHI");
        Assert.Contains(cartels, c => c.CartelCode == "VE_SHI");

        // Adjust standing with Mi Cartel
        logistics.AdjustCartelStanding("MI_SHI", 15);
        var mi = logistics.GetCartels().First(c => c.CartelCode == "MI_SHI");
        Assert.Equal(35, mi.Standing);

        // Create automated trade route
        logistics.CreateRoute("Tuyến Lương Thảo Ký Châu - Lạc Dương", 2, 1, 1, 12, 18, 50);
        var routes = logistics.GetRoutes();
        Assert.Single(routes);
        Assert.Equal("Tuyến Lương Thảo Ký Châu - Lạc Dương", routes[0].Name);
        Assert.True(routes[0].Active);

        // Toggle and delete
        logistics.ToggleRoute(routes[0].Id, false);
        Assert.False(logistics.GetRoutes()[0].Active);

        logistics.DeleteRoute(routes[0].Id);
        Assert.Empty(logistics.GetRoutes());
    }
}
