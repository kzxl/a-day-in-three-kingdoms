using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Core.Kernel;

namespace SimT.Modules.Dlc.Tests;

public sealed class CourtFactionAndInspectionTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private static IPoliticalCourt Court(SimKernel kernel) =>
        kernel.Capabilities.Get<IPoliticalCourt>(CapabilityIds.CourtOffice);

    private static IWallet Wallet(SimKernel kernel) =>
        kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);

    private static string Command(SimKernel kernel, string screenId, string commandId)
    {
        Assert.True(kernel.TryGetCommandHandler(screenId, out var handler), $"Screen {screenId} is not registered");
        return handler!.Execute(commandId) ?? string.Empty;
    }

    [Fact]
    public void Court_factions_load_initial_standings_and_shift_via_lobbying()
    {
        using var kernel = _host.Boot();
        var court = Court(kernel);
        var wallet = Wallet(kernel);

        var standings = court.CourtFactionStandings;
        Assert.Equal(3, standings.Count);
        Assert.Contains(standings, s => s.FactionCode == "eunuch" && s.StandingScore == 0);
        Assert.Contains(standings, s => s.FactionCode == "scholar" && s.StandingScore == 0);
        Assert.Contains(standings, s => s.FactionCode == "militarist" && s.StandingScore == 0);

        // Fund player
        wallet.Pay(5000, "TEST_SEED", "Khởi nghiệp");

        // Lobby Eunuchs (Hoạn Quan Đảng)
        var resEunuch = court.LobbyCourtFaction("eunuch", 300);
        Assert.True(resEunuch.Ok);
        Assert.Contains("Thập Thường Thị", resEunuch.Message);

        var afterEunuch = court.CourtFactionStandings;
        var eunuchScore = afterEunuch.First(s => s.FactionCode == "eunuch").StandingScore;
        var scholarScore = afterEunuch.First(s => s.FactionCode == "scholar").StandingScore;
        Assert.True(eunuchScore > 0, "Eunuch standing should increase");
        Assert.True(scholarScore < 0, "Scholar standing should drop when bribing eunuchs");

        // Lobby Scholars (Thanh Lưu Sĩ Tộc)
        var repBefore = court.Reputation;
        var resScholar = court.LobbyCourtFaction("scholar", 300);
        Assert.True(resScholar.Ok);
        Assert.True(court.Reputation > repBefore);
    }

    [Fact]
    public void Imperial_inspection_open_ledger_praises_peaceful_governance()
    {
        using var kernel = _host.Boot();
        var court = Court(kernel);

        // Ensure city 1 (Lạc Dương) has low unrest
        kernel.Db.Execute(
            "UPDATE po_city_governance SET unrest_level = 15 WHERE citadel_id = 1");

        var meritsBefore = court.Merits;
        var repBefore = court.Reputation;

        var inspection = court.RespondToImperialInspection("OPEN_LEDGER");
        Assert.True(inspection.Success);
        Assert.Equal("PRAISED", inspection.Outcome);
        Assert.True(inspection.MeritDelta > 0);
        Assert.Contains("khen ngợi", inspection.NarrativeVi);

        Assert.True(court.Merits > meritsBefore);
        Assert.True(court.Reputation > repBefore);

        // Recorded in log
        var logs = court.RecentImperialInspections(5);
        Assert.NotEmpty(logs);
        Assert.Equal("OPEN_LEDGER", logs[0].ApproachCode);
        Assert.Equal("PRAISED", logs[0].Outcome);
    }

    [Fact]
    public void Imperial_inspection_bribe_consumes_copper_and_boosts_eunuch_standing()
    {
        using var kernel = _host.Boot();
        var court = Court(kernel);
        var wallet = Wallet(kernel);

        wallet.Pay(1000, "TEST_SEED", "Quỹ hối lộ");

        var balBefore = wallet.Liquid;
        var inspection = court.RespondToImperialInspection("BRIBE");
        Assert.True(inspection.Success);
        Assert.Equal("PRAISED", inspection.Outcome);
        Assert.Equal(-400, inspection.CopperDelta);
        Assert.Equal(balBefore - 400, wallet.Liquid);

        var standings = court.CourtFactionStandings;
        var eunuchScore = standings.First(s => s.FactionCode == "eunuch").StandingScore;
        Assert.True(eunuchScore >= 20);
    }

    [Fact]
    public void Court_command_handler_dispatches_lobby_and_inspection()
    {
        using var kernel = _host.Boot();
        var court = Court(kernel);
        var wallet = Wallet(kernel);

        wallet.Pay(5000, "TEST_SEED", "Khởi nghiệp");

        var lobbyMsg = Command(kernel, "politics.court", "court:lobby:militarist:300");
        Assert.Contains("biên ải", lobbyMsg);

        var triggerMsg = Command(kernel, "politics.court", "inspect:trigger");
        Assert.Contains("Ngự Sử Đài", triggerMsg);

        var inspectMsg = Command(kernel, "politics.court", "inspect:respond:open_ledger");
        Assert.Contains("sổ sách nha môn", inspectMsg);
    }
}
