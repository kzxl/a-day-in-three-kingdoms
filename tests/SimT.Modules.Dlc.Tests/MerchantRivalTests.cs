using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Contracts.Ui;
using SimT.Core.Kernel;

namespace SimT.Modules.Dlc.Tests;

public sealed class MerchantRivalTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void Merchant_rivals_initialized_with_enterprises_and_tiers()
    {
        using var kernel = _host.Boot();
        var rivalry = kernel.Capabilities.Get<IMerchantRivalry>(CapabilityIds.MerchantRivalry);
        Assert.NotNull(rivalry);

        var rivals = rivalry.GetAllRivals();
        Assert.NotEmpty(rivals);

        // Prominent merchants should exist with custom enterprise names and tiers
        var miZhu = rivals.FirstOrDefault(r => r.NpcId == 10);
        Assert.NotNull(miZhu);
        Assert.Contains("Mi Thị", miZhu.EnterpriseName);
        Assert.True(miZhu.MerchantTier >= 3);
        Assert.True(miZhu.CapitalQuan >= 5000);
        Assert.True(miZhu.FleetCarts >= 3);
        Assert.Equal(4, miZhu.PrimaryGoodId); // Silk

        var zhen = rivals.FirstOrDefault(r => r.NpcId == 115);
        Assert.NotNull(zhen);
        Assert.Contains("Chân Thị", zhen.EnterpriseName);
        Assert.True(zhen.MerchantTier >= 3);
        Assert.True(zhen.CapitalQuan >= 5000);

        var luoYangMed = rivals.FirstOrDefault(r => r.NpcId == 1);
        Assert.NotNull(luoYangMed);
        Assert.Contains("Tế Thế", luoYangMed.EnterpriseName);
        Assert.Equal(2, luoYangMed.MerchantTier);
        Assert.Equal(7, luoYangMed.PrimaryGoodId); // Medicine

        // All rivals must satisfy enterprise invariants
        Assert.All(rivals, r =>
        {
            Assert.True(r.CapitalQuan > 0, "Capital must be positive");
            Assert.True(r.FleetCarts >= 1, "Fleet carts must be at least 1");
            Assert.InRange(r.MerchantTier, 1, 5);
            Assert.False(string.IsNullOrWhiteSpace(r.EnterpriseName));
            Assert.False(string.IsNullOrWhiteSpace(r.TierTitleVi));
        });
    }

    [Fact]
    public void Monthly_simulation_advances_rival_trade_volume_and_generates_profits()
    {
        using var kernel = _host.Boot();
        var rivalry = kernel.Capabilities.Get<IMerchantRivalry>(CapabilityIds.MerchantRivalry);
        var society = kernel.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle);

        var initialRivals = rivalry.GetAllRivals();
        var totalInitialCapital = initialRivals.Sum(r => r.CapitalQuan);

        // Advance 30 days to cross month boundary and run monthly simulation
        kernel.AdvanceDays(30);

        var updatedRivals = rivalry.GetAllRivals();
        var totalUpdatedCapital = updatedRivals.Sum(r => r.CapitalQuan);

        // Total merchant capital must grow from commercial arbitrage
        Assert.True(totalUpdatedCapital > totalInitialCapital,
            $"Expected total capital to grow from {totalInitialCapital}, got {totalUpdatedCapital}");

        // Some rivals must have traded volume and updated actions
        Assert.Contains(updatedRivals, r => r.TotalTradeVolume > 0);
        Assert.Contains(updatedRivals, r => !string.IsNullOrEmpty(r.LastActionDesc));

        // Check that commercial events were recorded in chronicle
        var commerceEvents = society.RecentEvents(limit: 50, category: "commerce");
        Assert.NotEmpty(commerceEvents);

        // Invariants must pass cleanly
        var failures = kernel.RunInvariants();
        Assert.Empty(failures);
    }

    [Fact]
    public void Merchant_leaderboard_ranks_player_and_ai_competitors()
    {
        using var kernel = _host.Boot();
        var rivalry = kernel.Capabilities.Get<IMerchantRivalry>(CapabilityIds.MerchantRivalry);

        var leaderboard = rivalry.GetLeaderboard();
        Assert.NotEmpty(leaderboard);

        // Must include the player
        var playerEntry = leaderboard.FirstOrDefault(e => e.IsPlayer);
        Assert.NotNull(playerEntry);
        Assert.True(playerEntry.Rank >= 1);
        Assert.False(string.IsNullOrWhiteSpace(playerEntry.TierTitle));

        // Entries must be monotonically ordered by EstimatedNetWorth descending
        for (int i = 0; i < leaderboard.Count - 1; i++)
        {
            Assert.Equal(i + 1, leaderboard[i].Rank);
            Assert.True(leaderboard[i].EstimatedNetWorth >= leaderboard[i + 1].EstimatedNetWorth,
                $"Leaderboard not sorted at rank {i + 1}: {leaderboard[i].EstimatedNetWorth} vs {leaderboard[i + 1].EstimatedNetWorth}");
        }
    }

    [Fact]
    public void Society_chronicle_screen_renders_leaderboard_and_rival_metrics()
    {
        using var kernel = _host.Boot();
        var screenContribution = kernel.Ui.Screens.FirstOrDefault(s => s.ScreenId == "society.chronicle");
        Assert.NotNull(screenContribution);

        Assert.True(kernel.TryGetCommandHandler("society.chronicle", out var handler));
        Assert.NotNull(handler);

        // Switch filter to rivals leaderboard
        var switchMsg = handler.Execute("filter:rivals");
        Assert.NotNull(switchMsg);
        Assert.Contains("rivals", switchMsg);

        var model = screenContribution.Render();
        Assert.NotNull(model);

        // Main section must be the Merchant Leaderboard
        var mainSection = model.Sections.FirstOrDefault(s => s.Heading.Contains("BẢNG PHÚ HÀO THIÊN HẠ"));
        Assert.NotNull(mainSection);
        Assert.NotEmpty(mainSection.Rows);

        // Must contain metrics for player rank and AI fleets
        Assert.Contains(model.Headline, m => m.Label.Contains("Hạng Thương Gia Của Bạn"));
        Assert.Contains(model.Headline, m => m.Label.Contains("Tài Sản Ròng Người Chơi"));
        Assert.Contains(model.Headline, m => m.Label.Contains("Tổng Thương Đoàn AI"));

        // Commands must have filter:rivals as Primary
        var rivalsCmd = model.Commands.FirstOrDefault(c => c.Id == "filter:rivals");
        Assert.NotNull(rivalsCmd);
        Assert.Equal(CommandKind.Primary, rivalsCmd.Kind);
    }
}
