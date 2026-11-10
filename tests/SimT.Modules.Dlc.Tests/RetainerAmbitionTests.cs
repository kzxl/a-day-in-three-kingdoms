using SimT.Contracts.Data;
using SimT.Contracts.Events;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Core.Kernel;

namespace SimT.Modules.Dlc.Tests;

public sealed class RetainerAmbitionTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private static ISocialNetwork Social(SimKernel kernel) =>
        kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);

    private static IWallet Wallet(SimKernel kernel) =>
        kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);

    private static INpcRegistry Registry(SimKernel kernel) =>
        kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);

    private static void AdvanceMonth(SimKernel kernel) =>
        kernel.Capabilities.Get<IEventPublisher>(CapabilityIds.EventPublisher)
            .Publish(new MonthClosed(kernel.Today, kernel.Today.AddDays(-1)));

    private static string Command(SimKernel kernel, string screenId, string commandId)
    {
        Assert.True(kernel.TryGetCommandHandler(screenId, out var handler), $"Screen {screenId} is not registered");
        return handler!.Execute(commandId) ?? string.Empty;
    }

    [Fact]
    public void Ambitious_accountant_with_low_loyalty_embezzles_funds_and_audit_recovers_it()
    {
        using var kernel = _host.Boot();
        var social = Social(kernel);
        var wallet = Wallet(kernel);
        var registry = Registry(kernel);

        // Find an ambitious NPC or insert a retainer with high ambition (e.g. Hegemon archetype)
        // npc_0016 is Tào Tháo (Hegemon) or npc_0004 is Lữ Bố (Hegemon, Ambition 90, Integrity 45)
        var luBu = registry.FindByCode("npc_0004") ?? registry.All[0];
        var psyche = social.GetPsycheProfile(luBu.Id);
        Assert.True(psyche.Ambition >= 70);

        // Seed wallet
        wallet.Pay(10000, "TEST_SEED", "Khởi nghiệp");

        // Insert as accountant with low loyalty (40)
        kernel.Db.Execute(
            """
            INSERT INTO kz_retainer (npc_id, role_assigned, monthly_stipend, loyalty, recruited_date)
            VALUES (@npc, 'accountant', 50, 40, @date)
            """,
            DbArgs.One("@npc", luBu.Id).Add("@date", GameDateCodec.Format(kernel.Today)));

        var balanceBefore = wallet.Liquid;

        // Advance month: triggers ProcessRetainerStipends and ProcessRetainerAmbitionFriction
        AdvanceMonth(kernel);

        // Funds were embezzled
        var balanceAfter = wallet.Liquid;
        Assert.True(balanceAfter < balanceBefore - 50, $"Expected funds to be embezzled, before={balanceBefore}, after={balanceAfter}");

        // Retainer loyalty dropped further
        var ret = social.GetRetainer(luBu.Id);
        Assert.NotNull(ret);
        Assert.True(ret.Loyalty < 40);

        // Incidents recorded
        var incidents = social.GetRecentRetainerIncidents(5);
        Assert.Contains(incidents, inc => inc.NpcId == luBu.Id && inc.IncidentType == "EMBEZZLE");

        // Now conduct audit
        var audit = social.AuditRetainer(luBu.Id);
        Assert.True(audit.IsEmbezzling);
        Assert.Contains("Phát hiện", audit.NarrativeVi);

        // Audit recovered partial funds
        Assert.True(wallet.Liquid > balanceAfter);

        // Loyalty improved after being disciplined
        var retAfterAudit = social.GetRetainer(luBu.Id);
        Assert.True(retAfterAudit!.Loyalty > ret.Loyalty);
    }

    [Fact]
    public void Placating_retainer_boosts_loyalty_and_records_incident()
    {
        using var kernel = _host.Boot();
        var social = Social(kernel);
        var wallet = Wallet(kernel);
        var registry = Registry(kernel);

        var npc = registry.All[1];

        // Seed wallet
        wallet.Pay(5000, "TEST_SEED", "Khởi nghiệp");

        kernel.Db.Execute(
            """
            INSERT INTO kz_retainer (npc_id, role_assigned, monthly_stipend, loyalty, recruited_date)
            VALUES (@npc, 'bodyguard', 60, 50, @date)
            """,
            DbArgs.One("@npc", npc.Id).Add("@date", GameDateCodec.Format(kernel.Today)));

        var res = social.PlacateRetainer(npc.Id, 300);
        Assert.True(res.Success);
        Assert.Equal(50, res.OldLoyalty);
        Assert.True(res.NewLoyalty >= 70);

        var ret = social.GetRetainer(npc.Id);
        Assert.Equal(res.NewLoyalty, ret!.Loyalty);

        var incidents = social.GetRecentRetainerIncidents(5);
        Assert.Contains(incidents, inc => inc.NpcId == npc.Id && inc.IncidentType == "PLACATED");
    }

    [Fact]
    public void Sworn_brother_provides_emergency_aid_when_player_is_low_on_funds()
    {
        using var kernel = _host.Boot();
        var social = Social(kernel);
        var wallet = Wallet(kernel);
        var registry = Registry(kernel);

        var brotherNpc = registry.All[2];

        // Register as sworn brother
        kernel.Db.Execute(
            """
            INSERT INTO kz_sworn_brother (npc_id, oath_date, bond_title, assist_count)
            VALUES (@npc, @date, 'Nghĩa Huynh', 0)
            """,
            DbArgs.One("@npc", brotherNpc.Id).Add("@date", GameDateCodec.Format(kernel.Today)));

        // Case 1: Player has abundant funds (> 3000)
        wallet.Pay(5000, "TEST_SEED", "Quá nhiều tiền");
        var failAid = social.RequestSwornBrotherAid(brotherNpc.Id);
        Assert.False(failAid.Success);
        Assert.Contains("tự lập", failAid.NarrativeVi);

        // Drain wallet to 500 Quan
        wallet.Charge(wallet.Liquid - 500, "TEST_DRAIN", "Tiêu hao sa cơ");

        // Case 2: Player is in dire straits
        var successAid = social.RequestSwornBrotherAid(brotherNpc.Id);
        Assert.True(successAid.Success);
        Assert.Equal(1500, successAid.CopperAid);
        Assert.Contains("dựng lại cơ nghiệp", successAid.NarrativeVi);
        Assert.Equal(2000, wallet.Liquid);

        // Assist count updated
        var sworn = social.GetSwornBrothers().First(s => s.NpcId == brotherNpc.Id);
        Assert.Equal(1, sworn.AssistCount);
    }

    [Fact]
    public void Social_command_handler_dispatches_audit_placate_and_sworn_aid()
    {
        using var kernel = _host.Boot();
        var social = Social(kernel);
        var wallet = Wallet(kernel);
        var registry = Registry(kernel);

        var npc = registry.All[3];
        wallet.Pay(5000, "TEST_SEED", "Khởi nghiệp");

        kernel.Db.Execute(
            """
            INSERT INTO kz_retainer (npc_id, role_assigned, monthly_stipend, loyalty, recruited_date)
            VALUES (@npc, 'advisor', 40, 60, @date)
            """,
            DbArgs.One("@npc", npc.Id).Add("@date", GameDateCodec.Format(kernel.Today)));

        var auditMsg = Command(kernel, "social.network", $"retainer:audit:{npc.Id}");
        Assert.Contains("thẩm định sổ sách", auditMsg);

        var placateMsg = Command(kernel, "social.network", $"retainer:placate:{npc.Id}:300");
        Assert.Contains("Ban thưởng", placateMsg);

        // Register sworn brother and drain funds
        kernel.Db.Execute(
            """
            INSERT INTO kz_sworn_brother (npc_id, oath_date, bond_title, assist_count)
            VALUES (@npc, @date, 'Nghĩa Đệ', 0)
            """,
            DbArgs.One("@npc", npc.Id).Add("@date", GameDateCodec.Format(kernel.Today)));

        wallet.Charge(wallet.Liquid - 200, "TEST_DRAIN", "Hết tiền");
        var aidMsg = Command(kernel, "social.network", $"sworn:aid:{npc.Id}");
        Assert.Contains("tương trợ", aidMsg);
    }
}
