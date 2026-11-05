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
    public void High_rank_office_requires_faction_allegiance()
    {
        using var kernel = _host.Boot();
        var court = Court(kernel);

        // Raise reputation to qualify for Rank 4 (Biệt Giá requires 40 rep)
        for (var i = 0; i < 15; i++)
        {
            Command(kernel, "serve");
        }
        Assert.True(court.Reputation >= 40);

        // Applying for Rank 4 without faction allegiance is refused
        var blocked = Command(kernel, "apply:4");
        Assert.Contains("quy thuận một thế lực", blocked);
        Assert.Equal(0, court.OfficeId);

        // Pledge allegiance to Cao Wei (Faction 1)
        Command(kernel, "join_faction:1");
        Assert.Equal(1, court.AllegianceFactionId);

        // Now application succeeds
        var granted = Command(kernel, "apply:4");
        Assert.Contains("Được bổ nhiệm", granted);
        Assert.Equal(4, court.OfficeId);
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

    [Fact]
    public void All_nine_ranks_of_officialdom_are_registered_and_ordered_by_rank()
    {
        using var kernel = _host.Boot();
        var court = Court(kernel);

        Assert.Equal(9, court.Offices.Count);
        for (var i = 0; i < court.Offices.Count; i++)
        {
            Assert.Equal(i + 1, court.Offices[i].Rank);
            if (i > 0)
            {
                Assert.True(court.Offices[i].MonthlyStipend > court.Offices[i - 1].MonthlyStipend);
                Assert.True(court.Offices[i].ReputationRequired > court.Offices[i - 1].ReputationRequired);
            }
        }
    }

    [Fact]
    public void Twelve_warlord_factions_are_seeded_with_valid_capitals_and_armies()
    {
        using var kernel = _host.Boot();
        var court = Court(kernel);

        Assert.Equal(12, court.Factions.Count);
        Assert.Contains(court.Factions, f => f.Code == "cao_wei" && f.TroopCount >= 50000);
        Assert.Contains(court.Factions, f => f.Code == "shu_han" && f.TroopCount >= 30000);
        Assert.Contains(court.Factions, f => f.Code == "dong_wu" && f.TroopCount >= 40000);

        Assert.Equal(20, court.Governances.Count);
        Assert.All(court.Governances, g =>
        {
            Assert.InRange(g.UnrestLevel, 0, 100);
            Assert.True(g.DefenseStrength >= 50);
            Assert.True(g.TaxRatePercent >= 10);
        });
    }

    [Fact]
    public void Pledging_allegiance_to_a_faction_pays_bounty_and_grants_monthly_subsidy()
    {
        using var kernel = _host.Boot();
        var court = Court(kernel);

        // Cannot join below 5 reputation
        var failMsg = Command(kernel, "join_faction:1");
        Assert.Contains("tối thiểu 5 điểm Uy tín", failMsg);
        Assert.Equal(0, court.AllegianceFactionId);

        // Serve 3 times to get 6 reputation
        Command(kernel, "serve");
        Command(kernel, "serve");
        Command(kernel, "serve");
        Assert.True(court.Reputation >= 5);

        var balanceBefore = kernel.Ledger.Balance;
        var joinMsg = Command(kernel, "join_faction:1");
        Assert.Contains("Quy thuận minh chủ", joinMsg);
        Assert.Equal(1, court.AllegianceFactionId);
        Assert.Equal("Tào Ngụy", court.AllegianceFactionName);
        Assert.Equal(balanceBefore + 100, kernel.Ledger.Balance);

        // Monthly advance pays 50 copper faction subsidy
        var balanceBeforeMonth = kernel.Ledger.Balance;
        kernel.AdvanceDays(31);
        Assert.True(kernel.Ledger.Balance >= balanceBeforeMonth + CourtService.FactionStipendBonus);

        // Resign allegiance lowers 5 reputation
        var repBefore = court.Reputation;
        var leaveMsg = Command(kernel, "leave_faction");
        Assert.Contains("rời khỏi", leaveMsg);
        Assert.Equal(0, court.AllegianceFactionId);
        Assert.Equal(repBefore - 5, court.Reputation);
    }

    [Fact]
    public void Enacting_relief_policy_calms_unrest_and_earns_reputation()
    {
        using var kernel = _host.Boot();
        var court = Court(kernel);
        var initialGov = court.Governances.First(g => g.CitadelId == 1);
        var initialUnrest = initialGov.UnrestLevel;
        var repBefore = court.Reputation;

        var msg = Command(kernel, "policy:relief:1");
        Assert.Contains("Thi hành An Dân", msg);

        var afterGov = court.Governances.First(g => g.CitadelId == 1);
        Assert.Equal(Math.Max(0, initialUnrest - 15), afterGov.UnrestLevel);
        Assert.Equal(repBefore + 3, court.Reputation);

        var decrees = court.RecentDecrees(5);
        Assert.NotEmpty(decrees);
        Assert.Equal("relief", decrees[0].PolicyCode);
    }

    [Fact]
    public void Enacting_tax_policy_requires_holding_an_office_and_raises_cash_with_unrest_penalty()
    {
        using var kernel = _host.Boot();
        var court = Court(kernel);

        // Without office: tax is rejected
        var failMsg = Command(kernel, "policy:tax:1");
        Assert.Contains("Phải là quan chức", failMsg);

        // Gain office
        Command(kernel, "serve");
        Command(kernel, "apply:1");
        Assert.Equal(1, court.OfficeId);

        var initialGov = court.Governances.First(g => g.CitadelId == 1);
        var balanceBefore = kernel.Ledger.Balance;

        var msg = Command(kernel, "policy:tax:1");
        Assert.Contains("Thu về 120 Quan", msg);
        Assert.Equal(balanceBefore + 120, kernel.Ledger.Balance);

        var afterGov = court.Governances.First(g => g.CitadelId == 1);
        Assert.Equal(initialGov.UnrestLevel + 10, afterGov.UnrestLevel);
    }

    [Fact]
    public void Enacting_pacification_requires_severe_unrest_and_restores_order()
    {
        using var kernel = _host.Boot();
        var court = Court(kernel);

        // Unrest is low, pacify is rejected
        var failMsg = Command(kernel, "policy:pacify:1");
        Assert.Contains("chưa tới mức bạo động", failMsg);

        // Elevate unrest using taxation
        Command(kernel, "serve");
        Command(kernel, "apply:1");
        Command(kernel, "policy:tax:1");
        Command(kernel, "policy:tax:1"); // unrest +20 -> 25 + 20 = 45%

        var highGov = court.Governances.First(g => g.CitadelId == 1);
        Assert.True(highGov.UnrestLevel >= 35);

        var repBefore = court.Reputation;
        var msg = Command(kernel, "policy:pacify:1");
        Assert.Contains("Dẹp loạn thành công", msg);

        var restoredGov = court.Governances.First(g => g.CitadelId == 1);
        Assert.Equal(highGov.UnrestLevel - 25, restoredGov.UnrestLevel);
        Assert.Equal(repBefore + 4, court.Reputation);
    }

    private static string Command(SimKernel kernel, string commandId)
    {
        Assert.True(kernel.TryGetCommandHandler("politics.court", out var handler), "court screen is not registered");
        return handler!.Execute(commandId) ?? string.Empty;
    }

    private static IPoliticalCourt Court(SimKernel kernel) =>
        kernel.Capabilities.Get<IPoliticalCourt>(CapabilityIds.CourtOffice);
}
