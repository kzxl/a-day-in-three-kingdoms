using SimT.Contracts.Ledger;
using SimT.Contracts.Playing;
using SimT.Core.Kernel;
using SimT.Modules.Dlc.Lineage;
using SimT.Testing;

namespace SimT.Modules.Dlc.Tests;

/// <summary>Household: dowry, children, one heir, and upkeep that actually gets charged.</summary>
public sealed class LineageTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void Marriage_records_a_spouse_and_charges_the_dowry()
    {
        using var kernel = _host.Boot();
        var before = kernel.Ledger.Balance;

        var message = Command(kernel, "marry");

        Assert.Contains("Thành hôn", message);
        Assert.True(House(kernel).IsMarried);
        Assert.NotNull(House(kernel).SpouseName);
        Assert.Equal(before - LineageService.DowryCost, kernel.Ledger.Balance);
        Assert.Contains(kernel.Ledger.Recent(20), line => line.JournalCode == JournalCodes.Dowry);
    }

    [Fact]
    public void A_second_marriage_is_refused()
    {
        using var kernel = _host.Boot();
        Command(kernel, "marry");

        var message = Command(kernel, "marry");

        Assert.Contains("Đã có chính thất", message);
        Assert.Equal(1, kernel.Db.ScalarLong("SELECT COUNT(*) FROM ln_spouse"));
    }

    [Fact]
    public void Children_require_a_marriage()
    {
        using var kernel = _host.Boot();

        var message = Command(kernel, "beget");

        Assert.Contains("Chưa có chính thất", message);
        Assert.Equal(0, House(kernel).ChildCount);
    }

    [Fact]
    public void Children_are_born_after_marriage_and_cost_a_fee()
    {
        using var kernel = _host.Boot();
        Command(kernel, "marry");
        var before = kernel.Ledger.Balance;

        var message = Command(kernel, "beget");

        Assert.Contains("chào đời", message);
        Assert.Equal(1, House(kernel).ChildCount);
        Assert.Equal(before - LineageService.ChildCost, kernel.Ledger.Balance);
    }

    [Fact]
    public void Exactly_one_child_can_be_heir()
    {
        using var kernel = _host.Boot();
        Command(kernel, "marry");
        Command(kernel, "beget");
        Command(kernel, "beget");

        var children = kernel.Db.Query("SELECT id FROM ln_child ORDER BY id", row => row.GetInt64(0));
        Command(kernel, $"heir:{children[0]}");
        var firstHeir = House(kernel).HeirName;
        Command(kernel, $"heir:{children[1]}");

        Assert.NotNull(firstHeir);
        Assert.NotEqual(firstHeir, House(kernel).HeirName);
        Assert.Equal(1, kernel.Db.ScalarLong("SELECT COUNT(*) FROM ln_child WHERE is_heir = 1"));
    }

    [Fact]
    public void Designating_a_stranger_as_heir_is_refused()
    {
        using var kernel = _host.Boot();
        Command(kernel, "marry");
        Command(kernel, "beget");

        var message = Command(kernel, "heir:999");

        Assert.Contains("Không có người con này", message);
    }

    [Fact]
    public void Monthly_upkeep_is_charged_at_the_boundary()
    {
        using var kernel = _host.Boot();
        Command(kernel, "marry");
        Command(kernel, "beget");

        var upkeep = House(kernel).MonthlyUpkeep;
        Assert.True(upkeep > 0);

        var before = kernel.Ledger.Balance;
        kernel.AdvanceDays(31);

        Assert.Equal(before - upkeep, kernel.Ledger.Balance);
        Assert.Contains(kernel.Ledger.Recent(20), line => line.JournalCode == JournalCodes.FamilyUpkeep);
    }

    [Fact]
    public void Unmarried_player_pays_no_upkeep()
    {
        using var kernel = _host.Boot();
        var before = kernel.Ledger.Balance;

        kernel.AdvanceDays(31);

        Assert.Equal(0, House(kernel).MonthlyUpkeep);
        Assert.Equal(before, kernel.Ledger.Balance);
    }

    [Fact]
    public void Invariants_hold_after_a_full_family_cycle()
    {
        using var kernel = _host.Boot();
        Command(kernel, "marry");
        Command(kernel, "beget");
        Command(kernel, "beget");
        var child = kernel.Db.Query("SELECT id FROM ln_child ORDER BY id LIMIT 1", row => row.GetInt64(0)).Single();
        Command(kernel, $"heir:{child}");
        kernel.AdvanceDays(65);

        Assert.Empty(kernel.RunInvariants());
    }

    [Fact]
    public void The_same_seed_produces_the_same_spouse()
    {
        using var first = new TestHost("simt-lineage-a");
        using var second = new TestHost("simt-lineage-b");

        Assert.Equal(MarryAndReadSpouse(first), MarryAndReadSpouse(second));
    }

    private static string MarryAndReadSpouse(TestHost host)
    {
        using var kernel = host.Boot();
        Command(kernel, "marry");
        return House(kernel).SpouseName ?? string.Empty;
    }

    private static string Command(SimKernel kernel, string commandId)
    {
        Assert.True(kernel.TryGetCommandHandler("lineage.family", out var handler), "lineage screen is not registered");
        return handler!.Execute(commandId) ?? string.Empty;
    }

    private static ILineageHouse House(SimKernel kernel) =>
        kernel.Capabilities.Get<ILineageHouse>(CapabilityIds.LineageHouse);
}
