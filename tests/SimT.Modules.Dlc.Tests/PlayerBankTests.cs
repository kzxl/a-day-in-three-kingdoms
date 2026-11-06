using SimT.Contracts.Logging;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Modules.Dlc.Economy.Banking;
using Xunit;

namespace SimT.Modules.Dlc.Tests;

public sealed class PlayerBankTests
{
    private sealed class TestLog : ILog
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message, Exception? error = null) { }
    }

    [Fact]
    public void PlayerBank_establish_and_manage_branch_network_and_logistics()
    {
        using var host = new DlcTestHost();
        using var kernel = host.Boot();

        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        var clock = kernel.Clock;
        var log = new TestLog();

        // Ensure player has sufficient funds to start a private banking empire
        wallet.Pay(20_000, "test.fund", "Nạp quỹ kiểm thử");
        var currentCash = wallet.Liquid;

        var bankService = new PlayerBankService(kernel.Db, clock, wallet, log);

        // 1. Establish Bank HQ
        var estRes = bankService.EstablishBank("Gia Cát Ngân Hiệu", 1, 6000);
        Assert.True(estRes.Success);
        Assert.Equal(currentCash - 6000, wallet.Liquid);

        var bank = bankService.GetBank();
        Assert.NotNull(bank);
        Assert.Equal("Gia Cát Ngân Hiệu", bank.BankName);
        Assert.Equal(1, bank.HeadquartersCitadelId);
        Assert.Equal(60, bank.Reputation);

        var branches = bankService.GetBranches(bank.Id);
        Assert.Single(branches);
        var hqBranch = branches[0];
        Assert.Equal(1, hqBranch.CitadelId);
        Assert.Equal(6000, hqBranch.VaultCash);
        Assert.Equal("ACTIVE", hqBranch.Status);

        // 2. Open a Branch in Citadel 2 (e.g. Thành Đô / Trường An)
        var openRes = bankService.OpenBranch(2, "Gia Cát Ngân Hiệu - Phân Hiệu Tây Xuyên", 3000);
        Assert.True(openRes.Success);

        branches = bankService.GetBranches(bank.Id);
        Assert.Equal(2, branches.Count);
        var branch2 = bankService.GetBranch(bank.Id, 2);
        Assert.NotNull(branch2);
        Assert.Equal(3000, branch2.VaultCash);

        // 3. Deposit & Withdraw from Branch Vault
        var depRes = bankService.DepositToBranchVault(branch2.Id, 1000);
        Assert.True(depRes.Success);
        branch2 = bankService.GetBranch(bank.Id, 2);
        Assert.Equal(4000, branch2!.VaultCash);

        var withRes = bankService.WithdrawFromBranchVault(branch2.Id, 500);
        Assert.True(withRes.Success);
        branch2 = bankService.GetBranch(bank.Id, 2);
        Assert.Equal(3500, branch2!.VaultCash);

        // 4. Dispatch Vault Convoy (Transfer 1,500 bullion from HQ #1 to Branch #2)
        var convoyRes = bankService.DispatchConvoy(hqBranch.Id, branch2.Id, 1500);
        Assert.True(convoyRes.Success);

        hqBranch = bankService.GetBranch(bank.Id, 1)!;
        Assert.Equal(4500, hqBranch.VaultCash); // 6000 - 1500

        var convoys = bankService.GetConvoys(bank.Id);
        Assert.Single(convoys);
        var convoy = convoys[0];
        Assert.Equal("IN_TRANSIT", convoy.Status);
        Assert.Equal(1500, convoy.BullionAmount);
        Assert.Equal(clock.Today.DayIndex + 3, convoy.EtaDay);

        // 5. Simulate Monthly Turn after arrival (Advance clock by 5 days)
        var futureDate = clock.Today.AddDays(5);
        bankService.SimulateMonthlyTurn(futureDate);

        // Convoy delivered
        convoys = bankService.GetConvoys(bank.Id);
        Assert.Equal("DELIVERED", convoys[0].Status);

        // Branch 2 received 1500 + monthly growth
        branch2 = bankService.GetBranch(bank.Id, 2)!;
        // 3500 (before) + 1500 (convoy) + deposit growth
        Assert.True(branch2.VaultCash >= 5000);
        Assert.True(branch2.NpcDeposits > 0);
    }
}
