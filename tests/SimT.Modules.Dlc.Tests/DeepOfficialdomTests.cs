using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Core.Kernel;
using Xunit;

namespace SimT.Modules.Dlc.Tests;

public sealed class DeepOfficialdomTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void Promotion_and_demotion_mechanics_modify_office_and_log_career_transitions()
    {
        using var kernel = _host.Boot();
        var court = kernel.Capabilities.Get<IPoliticalCourt>(CapabilityIds.CourtOffice);
        Assert.NotNull(court);

        // Initially player has no office
        Assert.Equal(0, court.OfficeId);

        // Join a faction first
        var faction = court.Factions[0];
        var joinRes = court.JoinFaction(faction.Id);
        Assert.True(joinRes.Ok || court.Reputation < 5);

        // Raise reputation to unlock first office
        var serveRes = court.Serve();
        Assert.True(serveRes.Ok);
        Assert.True(court.Reputation > 0);

        // Apply for lowest rank office (Phẩm 1)
        var office1 = court.Offices.OrderBy(o => o.Rank).First();
        var applyRes = court.Apply(office1.Id);
        if (!applyRes.Ok && court.Reputation < office1.ReputationRequired)
        {
            // Give reputation if needed
            while (court.Reputation < office1.ReputationRequired)
            {
                court.Serve();
            }
            applyRes = court.Apply(office1.Id);
        }
        Assert.True(applyRes.Ok);
        Assert.Equal(office1.Id, court.OfficeId);

        // Test Promotion
        var promoteRes = court.RequestPromotion();
        // Even if merits are insufficient initially, the failure message informs the player
        if (!promoteRes.Ok)
        {
            Assert.Contains("Công Trạng", promoteRes.Message);
        }

        // Test Demotion
        var demoteRes = court.DemoteOffice("Khảo hạch yếu kém tại bản hạt");
        Assert.True(demoteRes.Ok);
        Assert.Contains("KHIỂN TRÁCH", demoteRes.Message);

        // Career logs must record the demotion
        var logs = court.RecentCareerLogs(5);
        Assert.NotEmpty(logs);
        Assert.Contains(logs, l => l.ChangeType == "DEMOTION");
    }

    [Fact]
    public void Governor_fiefdom_appointment_and_domain_development()
    {
        using var kernel = _host.Boot();
        var court = kernel.Capabilities.Get<IPoliticalCourt>(CapabilityIds.CourtOffice);
        Assert.NotNull(court);

        // Initially no fiefdom
        Assert.Null(court.PlayerFief);
        Assert.Equal(0, court.GovernorCityId);

        // Join faction that controls cities
        var targetGov = court.Governances.First(g => g.FactionId > 0);
        var faction = court.Factions.First(f => f.Id == targetGov.FactionId);
        while (court.Reputation < 5) court.Serve();
        court.JoinFaction(faction.Id);

        // Apply for a governor-eligible rank (Phẩm 3+)
        var office3 = court.Offices.FirstOrDefault(o => o.Rank >= 3) ?? court.Offices[court.Offices.Count - 1];
        while (court.Reputation < office3.ReputationRequired) court.Serve();
        var apply = court.Apply(office3.Id);
        Assert.True(apply.Ok, apply.Message);

        // Appoint governor of faction's controlled city
        var appointRes = court.AppointGovernor(targetGov.CitadelId);
        Assert.True(appointRes.Ok, appointRes.Message);
        Assert.Equal(targetGov.CitadelId, court.GovernorCityId);

        var fief = court.PlayerFief;
        Assert.NotNull(fief);
        Assert.Equal(targetGov.CitadelId, fief.CitadelId);
        Assert.Equal(50, fief.Agriculture);
        Assert.Equal(50, fief.Commerce);
        Assert.Equal(50, fief.Fortification);
        Assert.Equal(50, fief.PublicOrder);

        // Develop fief: Agriculture
        var initDay = kernel.Today.DayIndex;
        var devAgri = court.DevelopFief("agriculture");
        Assert.True(devAgri.Ok);
        Assert.True(kernel.Today.DayIndex > initDay);

        var updatedFief = court.PlayerFief;
        Assert.NotNull(updatedFief);
        Assert.Equal(62, updatedFief.Agriculture);

        // Evaluate Quarterly Governance
        var eval = court.EvaluateQuarterlyGovernance();
        Assert.NotNull(eval);
        Assert.False(string.IsNullOrWhiteSpace(eval.Grade));
        Assert.False(string.IsNullOrWhiteSpace(eval.RankAction));
        Assert.False(string.IsNullOrWhiteSpace(eval.NarrativeVi));

        // Resign governor
        var resignRes = court.ResignGovernor();
        Assert.True(resignRes.Ok);
        Assert.Null(court.PlayerFief);
    }

    [Fact]
    public void Imperial_council_memorials_can_be_submitted()
    {
        using var kernel = _host.Boot();
        var court = kernel.Capabilities.Get<IPoliticalCourt>(CapabilityIds.CourtOffice);
        Assert.NotNull(court);

        // Without faction allegiance, memorial fails
        var failRes = court.SubmitMemorial("reform", "Kế sách khai hoang đồn điền");
        Assert.False(failRes.Ok);
        Assert.Contains("thế lực chư hầu", failRes.Message);

        // Join faction and reach rank 3
        var faction = court.Factions[0];
        while (court.Reputation < 5) court.Serve();
        court.JoinFaction(faction.Id);

        var office3 = court.Offices.FirstOrDefault(o => o.Rank >= 3) ?? court.Offices[court.Offices.Count - 1];
        while (court.Reputation < office3.ReputationRequired) court.Serve();
        court.Apply(office3.Id);

        // Submit memorial
        var memRes = court.SubmitMemorial("reform", "Kế sách phát chẩn khuyến nông");
        Assert.False(string.IsNullOrWhiteSpace(memRes.Message));

        var memorials = court.RecentMemorials(5);
        Assert.NotEmpty(memorials);
        Assert.Contains(memorials, m => m.Category == "reform");
    }

    [Fact]
    public void Autonomous_aide_delegation_and_monthly_digest_generation()
    {
        using var kernel = _host.Boot();
        var court = kernel.Capabilities.Get<IPoliticalCourt>(CapabilityIds.CourtOffice);
        Assert.NotNull(court);

        // Join faction with cities and become governor
        var targetGov = court.Governances.First(g => g.FactionId > 0);
        var faction = court.Factions.First(f => f.Id == targetGov.FactionId);
        while (court.Reputation < 5) court.Serve();
        court.JoinFaction(faction.Id);

        var office3 = court.Offices.FirstOrDefault(o => o.Rank >= 3) ?? court.Offices[court.Offices.Count - 1];
        while (court.Reputation < office3.ReputationRequired) court.Serve();
        court.Apply(office3.Id);

        var appoint = court.AppointGovernor(targetGov.CitadelId);
        Assert.True(appoint.Ok, appoint.Message);

        var fief = court.PlayerFief;
        Assert.NotNull(fief);
        Assert.Equal("BALANCE", fief.PolicyDirective);
        Assert.True(fief.Population > 0);
        Assert.True(fief.GarrisonTroops > 0);
        Assert.Equal(0, fief.CivilAideNpcId);
        Assert.Equal(0, fief.MilitaryAideNpcId);

        // 1. Appoint Aides
        var npcs = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var civilNpc = npcs.All.First();
        var militaryNpc = npcs.All.Skip(1).First();

        var appointCiv = court.AppointFiefAide(civilNpc.Id, "CIVIL");
        Assert.True(appointCiv.Ok, appointCiv.Message);

        var appointMil = court.AppointFiefAide(militaryNpc.Id, "MILITARY");
        Assert.True(appointMil.Ok, appointMil.Message);

        var fiefWithAides = court.PlayerFief;
        Assert.NotNull(fiefWithAides);
        Assert.Equal(civilNpc.Id, fiefWithAides.CivilAideNpcId);
        Assert.Equal(civilNpc.NameVi, fiefWithAides.CivilAideNameVi);
        Assert.Equal(militaryNpc.Id, fiefWithAides.MilitaryAideNpcId);
        Assert.Equal(militaryNpc.NameVi, fiefWithAides.MilitaryAideNameVi);

        // 2. Set Directive to AGRI
        var setDir = court.SetFiefDirective("AGRI");
        Assert.True(setDir.Ok, setDir.Message);
        Assert.Equal("AGRI", court.PlayerFief!.PolicyDirective);

        // 3. Simulate Month Autonomy
        court.SimulateMonthlyFiefAutonomy(new SimT.Contracts.Time.GameDate(184, 10, 30));

        var digest1 = court.LatestMonthlyDigest;
        Assert.NotNull(digest1);
        Assert.Equal(184, digest1.Year);
        Assert.Equal(10, digest1.Month);
        Assert.Equal("AGRI", digest1.Directive);
        Assert.True(digest1.PopulationDelta > 0);
        Assert.True(digest1.MeritsEarned > 0);
        Assert.Contains(civilNpc.NameVi, digest1.CivilActionVi);
        Assert.Contains("Trọng Nông", digest1.SummaryVi);

        // 4. Switch Directive to MILITARY and simulate next month
        court.SetFiefDirective("MILITARY");
        court.SimulateMonthlyFiefAutonomy(new SimT.Contracts.Time.GameDate(184, 11, 30));

        var digests = court.RecentMonthlyDigests(5);
        Assert.Equal(2, digests.Count);
        Assert.Equal("MILITARY", digests[0].Directive);
        Assert.True(digests[0].GarrisonDelta > 0);
        Assert.Contains(militaryNpc.NameVi, digests[0].MilitaryActionVi);

        // 5. Dismiss Aide
        var dismissCiv = court.AppointFiefAide(0, "CIVIL");
        Assert.True(dismissCiv.Ok);
        Assert.Equal(0, court.PlayerFief!.CivilAideNpcId);
        Assert.Equal("Chưa bổ nhiệm", court.PlayerFief!.CivilAideNameVi);
    }

    [Fact]
    public void Fief_megaproject_investment_and_infrastructure_completion()
    {
        using var kernel = _host.Boot();
        var court = kernel.Capabilities.Get<IPoliticalCourt>(CapabilityIds.CourtOffice);
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        Assert.NotNull(court);
        Assert.NotNull(wallet);

        // Join faction with cities and become governor
        var targetGov = court.Governances.First(g => g.FactionId > 0);
        var faction = court.Factions.First(f => f.Id == targetGov.FactionId);
        while (court.Reputation < 5) court.Serve();
        court.JoinFaction(faction.Id);

        var office3 = court.Offices.FirstOrDefault(o => o.Rank >= 3) ?? court.Offices[court.Offices.Count - 1];
        while (court.Reputation < office3.ReputationRequired) court.Serve();
        court.Apply(office3.Id);
        court.AppointGovernor(targetGov.CitadelId);

        // 1. Query Megaprojects
        var projects = court.GetFiefMegaprojects();
        Assert.NotEmpty(projects);
        Assert.Contains(projects, p => p.ProjectCode == "CANAL");

        var canal = projects.First(p => p.ProjectCode == "CANAL");
        Assert.False(canal.IsCompleted);
        Assert.Equal(0, canal.InvestedCopper);

        // Give wallet funds to fund project
        wallet.Pay(3000, "test", "Kinh phí thử nghiệm");

        // 2. Partial investment
        var initAgri = court.PlayerFief!.Agriculture;
        var investPart = court.InvestFiefMegaproject("CANAL", 500);
        Assert.True(investPart.Ok, investPart.Message);

        var updatedProjects = court.GetFiefMegaprojects();
        var canalMid = updatedProjects.First(p => p.ProjectCode == "CANAL");
        Assert.Equal(500, canalMid.InvestedCopper);
        Assert.False(canalMid.IsCompleted);

        // 3. Complete the project
        var investRest = court.InvestFiefMegaproject("CANAL", 1000);
        Assert.True(investRest.Ok, investRest.Message);
        Assert.Contains("KHÁNH THÀNH", investRest.Message);

        var canalDone = court.GetFiefMegaprojects().First(p => p.ProjectCode == "CANAL");
        Assert.True(canalDone.IsCompleted);
        Assert.Equal("COMPLETED", canalDone.Status);

        // Permanent agricultural bonus applied
        Assert.Equal(initAgri + 15, court.PlayerFief!.Agriculture);
    }

    [Fact]
    public void Citadel_military_expeditions_and_tactical_spoils()
    {
        using var kernel = _host.Boot();
        var court = kernel.Capabilities.Get<IPoliticalCourt>(CapabilityIds.CourtOffice);
        Assert.NotNull(court);

        // Join faction with cities and become governor
        var targetGov = court.Governances.First(g => g.FactionId > 0);
        var faction = court.Factions.First(f => f.Id == targetGov.FactionId);
        while (court.Reputation < 5) court.Serve();
        court.JoinFaction(faction.Id);

        var office3 = court.Offices.FirstOrDefault(o => o.Rank >= 3) ?? court.Offices[court.Offices.Count - 1];
        while (court.Reputation < office3.ReputationRequired) court.Serve();
        court.Apply(office3.Id);
        court.AppointGovernor(targetGov.CitadelId);

        // 1. Launch PACIFY_BANDITS
        var initTreasury = court.PlayerFief!.LocalTreasury;
        var initMerits = court.Merits;
        var expRes = court.LaunchFiefExpedition("PACIFY_BANDITS");
        Assert.True(expRes.Ok, expRes.Message);

        // Treasury earned spoils, merits increased
        var fiefAfter = court.PlayerFief!;
        Assert.True(fiefAfter.LocalTreasury > initTreasury);
        Assert.True(court.Merits > initMerits);

        // 2. Launch BORDER_PATROL
        var repBefore = court.Reputation;
        var patrolRes = court.LaunchFiefExpedition("BORDER_PATROL");
        Assert.True(patrolRes.Ok, patrolRes.Message);
        Assert.True(court.Reputation >= repBefore + 3);

        // 3. Logs recorded
        var logs = court.RecentExpeditionLogs(5);
        Assert.Equal(2, logs.Count);
        Assert.Contains(logs, l => l.ExpeditionType == "PACIFY_BANDITS");
        Assert.Contains(logs, l => l.ExpeditionType == "BORDER_PATROL");
    }

    [Fact]
    public void Emergent_fief_crises_and_tactical_resolution_decisions()
    {
        using var kernel = _host.Boot();
        var court = kernel.Capabilities.Get<IPoliticalCourt>(CapabilityIds.CourtOffice);
        Assert.NotNull(court);

        // Join faction and become governor
        var targetGov = court.Governances.First(g => g.FactionId > 0);
        var faction = court.Factions.First(f => f.Id == targetGov.FactionId);
        while (court.Reputation < 5) court.Serve();
        court.JoinFaction(faction.Id);

        var office3 = court.Offices.FirstOrDefault(o => o.Rank >= 3) ?? court.Offices[court.Offices.Count - 1];
        while (court.Reputation < office3.ReputationRequired) court.Serve();
        court.Apply(office3.Id);
        court.AppointGovernor(targetGov.CitadelId);

        // 1. Trigger Crisis
        var trigRes = court.TriggerEmergentFiefCrisis();
        Assert.True(trigRes.Ok, trigRes.Message);

        var active = court.GetActiveFiefCrises();
        Assert.NotEmpty(active);
        var crisis = active[0];
        Assert.Equal("PENDING", crisis.Status);

        // 2. Resolve Crisis
        var choice = crisis.CrisisCode switch
        {
            "CRISIS_FLOOD" => "DISPATCH_CIVIL",
            "CRISIS_PLAGUE" => "ESTABLISH_CLINIC",
            "CRISIS_BANDIT_RAID" => "DISPATCH_MILITARY",
            _ => "CONFISCATE"
        };

        var res = court.ResolveFiefCrisis(crisis.Id, choice);
        Assert.True(res.Ok, res.Message);

        // Now active crises should be empty and recent crises should contain it as RESOLVED
        Assert.Empty(court.GetActiveFiefCrises());
        var recent = court.GetRecentFiefCrises(5);
        Assert.NotEmpty(recent);
        Assert.Equal("RESOLVED", recent[0].Status);
        Assert.False(string.IsNullOrWhiteSpace(recent[0].OutcomeVi));
    }

    [Fact]
    public void Citadel_siege_defense_simulation_and_tactical_phases()
    {
        using var kernel = _host.Boot();
        var court = kernel.Capabilities.Get<IPoliticalCourt>(CapabilityIds.CourtOffice);
        Assert.NotNull(court);

        // Join faction and become governor
        var targetGov = court.Governances.First(g => g.FactionId > 0);
        var faction = court.Factions.First(f => f.Id == targetGov.FactionId);
        while (court.Reputation < 5) court.Serve();
        court.JoinFaction(faction.Id);

        var office3 = court.Offices.FirstOrDefault(o => o.Rank >= 3) ?? court.Offices[court.Offices.Count - 1];
        while (court.Reputation < office3.ReputationRequired) court.Serve();
        court.Apply(office3.Id);
        court.AppointGovernor(targetGov.CitadelId);

        var meritsBefore = court.Merits;

        // 1. Simulate Siege Defense with AMBUSH_FIRE
        var siegeRes = court.SimulateCitadelSiege("Giặc Khăn Vàng Ba Quận", 1500, "AMBUSH_FIRE");
        Assert.True(siegeRes.Ok, siegeRes.Message);
        Assert.Contains("ĐẠI THẮNG", siegeRes.Message);
        Assert.True(court.Merits > meritsBefore);

        // 2. Assert log recorded
        var logs = court.RecentSiegeLogs(5);
        Assert.NotEmpty(logs);
        var latest = logs[0];
        Assert.Equal("VICTORY", latest.Outcome);
        Assert.Equal("AMBUSH_FIRE", latest.TacticalStance);
        Assert.True(latest.AttackerCasualties > latest.DefenseCasualties);
        Assert.True(latest.SpoilsCopper > 0);
        Assert.False(string.IsNullOrWhiteSpace(latest.BattlePhasesVi));
    }

    [Fact]
    public void Aide_candidates_and_talent_scouting_recommendations_and_court_screen_render()
    {
        using var kernel = _host.Boot();
        var court = kernel.Capabilities.Get<IPoliticalCourt>(CapabilityIds.CourtOffice);
        Assert.NotNull(court);

        // Appoint governor
        var targetGov = court.Governances.First(g => g.FactionId > 0);
        var faction = court.Factions.First(f => f.Id == targetGov.FactionId);
        while (court.Reputation < 5) court.Serve();
        court.JoinFaction(faction.Id);

        var office3 = court.Offices.FirstOrDefault(o => o.Rank >= 3) ?? court.Offices[court.Offices.Count - 1];
        while (court.Reputation < office3.ReputationRequired) court.Serve();
        court.Apply(office3.Id);
        court.AppointGovernor(targetGov.CitadelId);

        // 1. Get aide candidates for governor city
        var candidates = court.GetAideCandidates(targetGov.CitadelId);
        Assert.NotEmpty(candidates);

        var first = candidates[0];
        Assert.True(first.NpcId > 0);
        Assert.False(string.IsNullOrWhiteSpace(first.NameVi));
        Assert.False(string.IsNullOrWhiteSpace(first.RecommendationVi));

        // 2. Appoint first candidate as civil aide
        var appRes = court.AppointFiefAide(first.NpcId, "CIVIL");
        Assert.True(appRes.Ok, appRes.Message);

        var updatedFief = court.PlayerFief;
        Assert.NotNull(updatedFief);
        Assert.Equal(first.NpcId, updatedFief.CivilAideNpcId);

        // 3. Verify candidates reflect the appointment
        var updatedCandidates = court.GetAideCandidates(targetGov.CitadelId);
        var appointedCandidate = updatedCandidates.First(c => c.NpcId == first.NpcId);
        Assert.True(appointedCandidate.IsCurrentCivilAide);
        Assert.False(appointedCandidate.IsCurrentMilitaryAide);

        // 4. Verify CourtScreen includes Chiêu Hiền Sảnh
        var screen = kernel.Ui.Screens.FirstOrDefault(s => s.ScreenId == "politics.court");
        Assert.NotNull(screen);
        var model = screen.Render();
        Assert.Contains(model.Sections, s => s.Heading.Contains("Chiêu Hiền Sảnh"));
        Assert.NotNull(model.Metrics);
        Assert.Contains(model.Metrics, m => m.Label == "Phong Ấp");

        // 5. Dismiss civil aide
        var dismissRes = court.AppointFiefAide(0, "CIVIL");
        Assert.True(dismissRes.Ok, dismissRes.Message);
        Assert.Equal(0, court.PlayerFief!.CivilAideNpcId);
    }
}
