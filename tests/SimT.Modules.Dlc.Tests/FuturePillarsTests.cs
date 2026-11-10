using SimT.Contracts.Playing;
using Xunit;

namespace SimT.Modules.Dlc.Tests;

public sealed class FuturePillarsTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void Pillar2_Naval_fleet_shipyard_commissions_warships_and_wins_riverine_campaigns()
    {
        using var kernel = _host.Boot();
        var military = kernel.Capabilities.Get<IMilitaryCommand>(CapabilityIds.MilitaryCommand);
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);

        wallet.Pay(2000, "test.grant", "Seed naval funds", "test:naval:grant");

        var initialFleet = military.GetNavalFleet(1L);
        var msgBuild = military.CommissionWarship(1L, NavalShipKind.LauThuyen);
        Assert.Contains("Lâu Thuyền", msgBuild);

        var updatedFleet = military.GetNavalFleet(1L);
        Assert.Equal(initialFleet.LauThuyenCount + 1, updatedFleet.LauThuyenCount);
        Assert.Equal(initialFleet.NavalPower + 75, updatedFleet.NavalPower);

        var balanceBeforeCampaign = wallet.Balance;
        var msgCampaign = military.LaunchNavalCampaign(1L);
        Assert.Contains("Thủy Chiến", msgCampaign);

        var afterCampaignFleet = military.GetNavalFleet(1L);
        Assert.Equal(updatedFleet.PatrolVictoryCount + 1, afterCampaignFleet.PatrolVictoryCount);
        Assert.NotNull(afterCampaignFleet.LastPatrolDate);
        Assert.True(wallet.Balance > balanceBeforeCampaign);
    }

    [Fact]
    public void Pillar3_Espionage_deploys_spy_cells_executes_whispers_and_acquires_hermit_tomes()
    {
        using var kernel = _host.Boot();
        var espionage = kernel.Capabilities.Get<IEspionageNetwork>(CapabilityIds.EspionageNetwork);
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);

        wallet.Pay(2000, "test.grant", "Seed espionage funds", "test:espionage:grant");

        var initialNode = espionage.GetSpyNode(5L);
        Assert.Equal(0, initialNode.IntelLevel);

        var deployMsg = espionage.DeploySpyCell(5L);
        Assert.Contains("Cấp 1", deployMsg);

        var nodeAfterDeploy = espionage.GetSpyNode(5L);
        Assert.Equal(1, nodeAfterDeploy.IntelLevel);

        var whisperMsg = espionage.ExecuteCovertWhisper(5L, "price_whisper");
        Assert.Contains("Mật Kế Thành Công", whisperMsg);
        Assert.Equal(1, espionage.GetSpyNode(5L).SabotageCount);

        var tomes = espionage.GetHermitTomes();
        Assert.Equal(5, tomes.Count);

        // Seeking Thái Bình Yêu Thuật at citadel 5 succeeds because we deployed a Level 1 spy cell at citadel 5
        var tomeMsg = espionage.SeekHermitTome("thai_binh_yeu_thuat");
        Assert.Contains("Thái Bình Yêu Thuật", tomeMsg);

        var updatedTome = espionage.GetHermitTomes().Single(t => t.Code == "thai_binh_yeu_thuat");
        Assert.True(updatedTome.IsAcquired);
    }

    [Fact]
    public void Pillar4_Lineage_arranges_political_marriage_and_passes_torch_to_designated_heir()
    {
        using var kernel = _host.Boot();
        var lineage = kernel.Capabilities.Get<ILineageHouse>(CapabilityIds.LineageHouse);
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);

        wallet.Pay(3000, "test.grant", "Seed lineage funds", "test:lineage:grant");

        Assert.True(lineage.Marry().Ok);
        Assert.True(lineage.Beget().Ok);

        var child = Assert.Single(lineage.Children);
        Assert.True(lineage.DesignateHeir(child.Id).Ok);

        var allianceOutcome = lineage.ArrangePoliticalMarriage(child.Id, 1L);
        Assert.True(allianceOutcome.Ok);
        Assert.Single(lineage.PoliticalMarriages);
        Assert.True(lineage.TotalTariffDiscountPermille >= 50);

        var initialGen = lineage.CurrentGeneration;
        var initialPrestige = lineage.ClanPrestige;

        var successionOutcome = lineage.PassTorchToHeir();
        Assert.True(successionOutcome.Ok);
        Assert.Equal(initialGen + 1, lineage.CurrentGeneration);
        Assert.True(lineage.ClanPrestige >= initialPrestige + 50);
        Assert.Single(lineage.GenerationHistory);
    }

    [Fact]
    public void Pillar5_Mod_loader_discovers_and_applies_custom_items_and_achievements_evaluate()
    {
        using var kernel = _host.Boot();
        var modLoader = kernel.Capabilities.Get<IModLoaderService>(CapabilityIds.ModLoader);
        var itemHub = kernel.Capabilities.Get<IItemHubService>(CapabilityIds.ItemHub);
        var ambitions = kernel.Capabilities.Get<IEndgameAmbitions>(CapabilityIds.EndgameAmbitions);

        var initialHubCount = itemHub.Entries.Count;
        var templateMsg = modLoader.CreateSampleModTemplate();
        Assert.Contains("Mod SDK", templateMsg);

        var discovered = modLoader.DiscoverMods();
        Assert.Contains(discovered, m => m.ModId == "tk.treasures.pack");

        var applyMsg = modLoader.ApplyModPackage("tk.treasures.pack");
        Assert.Contains("Tam Quốc Kỳ Trân Dị Bảo Lục", applyMsg);
        Assert.True(itemHub.Entries.Count >= initialHubCount + 2);
        Assert.NotNull(itemHub.FindEntryByCode("yeu_dao_thanh_cong"));

        var achievements = ambitions.EvaluateAchievements();
        Assert.True(achievements.Count >= 10);
    }
}
