using SimT.Contracts.Ledger;
using SimT.Contracts.Playing;
using SimT.Core.Kernel;
using SimT.Modules.Dlc.Politics;

namespace SimT.Modules.Dlc.Tests;

/// <summary>
/// Court career: service costs days and money, offices have reputation gates, and a stipend is
/// a real monthly cashflow rather than a printed number.
/// </summary>
public sealed class PoliticsTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void Service_costs_days_and_money_and_raises_reputation()
    {
        using var kernel = _host.Boot();
        var before = kernel.Ledger.Balance;
        var beforeDate = kernel.Today;

        var message = Command(kernel, "serve");

        Assert.Contains("uy tín", message);
        Assert.Equal(CourtService.ServiceReputation, Court(kernel).Reputation);
        Assert.Equal(before - CourtService.ServiceCost, kernel.Ledger.Balance);
        Assert.Equal(beforeDate.AddDays(CourtService.ServiceDays), kernel.Today);
    }

    [Fact]
    public void An_office_below_the_reputation_gate_is_refused()
    {
        using var kernel = _host.Boot();

        var message = Command(kernel, "apply:3");

        Assert.Contains("uy tín", message);
        Assert.Equal(0, Court(kernel).OfficeId);
    }

    [Fact]
    public void An_eligible_office_is_granted()
    {
        using var kernel = _host.Boot();
        Command(kernel, "serve");

        var message = Command(kernel, "apply:1");

        Assert.Contains("Được bổ nhiệm", message);
        Assert.Equal(1, Court(kernel).OfficeId);
        Assert.Equal("Huyện Thừa", Court(kernel).OfficeName);
    }

    [Fact]
    public void A_second_office_requires_resigning_first()
    {
        using var kernel = _host.Boot();
        Command(kernel, "serve");
        Command(kernel, "apply:1");

        var blocked = Command(kernel, "apply:2");
        Assert.Contains("chức vụ khác", blocked);

        Assert.Contains("Từ chức", Command(kernel, "resign"));
        Assert.Equal(0, Court(kernel).OfficeId);
    }

    [Fact]
    public void The_monthly_stipend_is_paid_at_the_boundary()
    {
        using var kernel = _host.Boot();
        Command(kernel, "serve");
        Command(kernel, "apply:1");
        var stipend = Court(kernel).MonthlyStipend;
        Assert.True(stipend > 0);

        var before = kernel.Ledger.Balance;
        kernel.AdvanceDays(31);

        Assert.Equal(before + stipend, kernel.Ledger.Balance);
        Assert.Contains(kernel.Ledger.Recent(20), line => line.JournalCode == JournalCodes.CourtStipend);
    }

    [Fact]
    public void No_stipend_without_an_office()
    {
        using var kernel = _host.Boot();
        var before = kernel.Ledger.Balance;

        kernel.AdvanceDays(31);

        Assert.Equal(before, kernel.Ledger.Balance);
        Assert.DoesNotContain(kernel.Ledger.Recent(20), line => line.JournalCode == JournalCodes.CourtStipend);
    }

    [Fact]
    public void Service_without_cash_is_refused_and_time_does_not_move()
    {
        using var kernel = _host.Boot();
        kernel.Ledger.Move(kernel.Today, kernel.Ledger.Liquid - 1, toSafe: true, JournalCodes.SafeDeposit, "cất két");
        var before = kernel.Today;

        var message = Command(kernel, "serve");

        Assert.Contains("Quan", message);
        Assert.Equal(before, kernel.Today);
        Assert.Equal(0, Court(kernel).Reputation);
    }

    [Fact]
    public void Invariants_hold_after_a_career()
    {
        using var kernel = _host.Boot();
        Command(kernel, "serve");
        Command(kernel, "serve");
        Command(kernel, "serve");
        Command(kernel, "apply:2");
        kernel.AdvanceDays(65);
        Command(kernel, "resign");

        Assert.Empty(kernel.RunInvariants());
    }

    private static string Command(SimKernel kernel, string commandId)
    {
        Assert.True(kernel.TryGetCommandHandler("politics.court", out var handler), "court screen is not registered");
        return handler!.Execute(commandId) ?? string.Empty;
    }

    private static IPoliticalCourt Court(SimKernel kernel) =>
        kernel.Capabilities.Get<IPoliticalCourt>(CapabilityIds.CourtOffice);
}
