using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Contracts.Ui;

namespace SimT.Modules.Dlc.Tests;

public sealed class RealmRankingsAndRivalryTensionTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void DayOne_fame_leaderboard_stratifies_historical_legends_above_novice_player_while_anchoring_player()
    {
        using var kernel = _host.Boot();
        var rep = kernel.Capabilities.Get<IPlayerReputation>(CapabilityIds.PlayerReputation);

        var board = rep.GetLeaderboard(10);
        Assert.Equal(10, board.Count);

        var topLegend = board[0];
        Assert.False(topLegend.IsPlayer, "Day-1 novice player (100 Fame) must not start at Rank #1 above Three Kingdoms warlords/legends.");
        Assert.True(topLegend.Fame >= 500, $"Expected top historical figure to have stratified Fame >= 500, got {topLegend.Fame}.");

        var playerEntry = board.Single(e => e.IsPlayer);
        Assert.Equal(100, playerEntry.Fame);
        Assert.True(playerEntry.Rank > 10, $"Expected Day-1 novice player to rank below top 10 legends, got #{playerEntry.Rank}.");
    }

    [Fact]
    public void FivePillar_realm_rankings_rank_all_npcs_and_compute_next_rival_and_milestone_goals()
    {
        using var kernel = _host.Boot();
        var society = kernel.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle);

        var allBoards = society.GetAllRealmRankings(top: 15);
        Assert.Equal(5, allBoards.Count);

        foreach (var category in Enum.GetValues<RealmRankingCategory>())
        {
            var board = society.GetRealmRanking(category, top: 20);
            Assert.NotEmpty(board.TopEntries);
            Assert.False(string.IsNullOrWhiteSpace(board.TitleVi));
            Assert.False(string.IsNullOrWhiteSpace(board.ProgressionAdviceVi));

            // Monotonic descending order among top entries (excluding anchored player if appended at bottom)
            var topStrict = board.TopEntries.Take(board.TopEntries.Count - 1).ToList();
            for (var i = 0; i < topStrict.Count - 1; i++)
            {
                Assert.True(topStrict[i].Score >= topStrict[i + 1].Score,
                    $"Board {category} unsorted at index {i}: {topStrict[i].Score} < {topStrict[i + 1].Score}");
            }

            Assert.True(board.PlayerEntry.IsPlayer);
            Assert.True(board.PlayerEntry.Rank > 1, $"Day-1 player should have aspirational NPCs ahead in {category}.");
            Assert.NotNull(board.NextRivalToOvertake);
            Assert.Equal(board.PlayerEntry.Rank - 1, board.NextRivalToOvertake!.Rank);
            Assert.True(board.GapToNextRival >= 1);
            Assert.NotNull(board.MilestoneTarget);
        }
    }

    [Fact]
    public void Orthogonal_strategic_actions_convert_wealth_into_fame_or_rival_suppression_with_moral_tradeoffs()
    {
        using var kernel = _host.Boot();
        var society = kernel.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle);
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        var rep = kernel.Capabilities.Get<IPlayerReputation>(CapabilityIds.PlayerReputation);
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);

        // Give player late-game capital to test wealth-sink actions
        wallet.Pay(3000, "CORE.STARTING_CASH", "Seed test capital");

        var fameBefore = rep.Standing.Fame;
        var virtueBefore = rep.Standing.Virtue;
        var symp = society.HostGrandSymposium();

        Assert.True(symp.Success);
        Assert.True(symp.CostQuan >= 500);
        Assert.True(rep.Standing.Fame > fameBefore);
        Assert.True(rep.Standing.Virtue > virtueBefore);

        // Now test Orthogonal Hegemon/Pragmatist Trade Suppression: drains rival capital & boosts fame, but costs Virtue and creates Rival Grudge!
        var topRivalBefore = society.Rivals.GetAllRivals().OrderByDescending(r => r.CapitalQuan).First();
        var virtueMid = rep.Standing.Virtue;
        var suppress = society.LaunchCommercialSuppression(topRivalBefore.NpcId);

        Assert.True(suppress.Success);
        Assert.True(suppress.VirtueDelta < 0, "Aggressive commercial suppression must carry a negative Virtue moral trade-off.");
        Assert.True(rep.Standing.Virtue < virtueMid);

        var topRivalAfter = society.Rivals.GetRival(topRivalBefore.NpcId)!;
        Assert.True(topRivalAfter.CapitalQuan < topRivalBefore.CapitalQuan, "Rival capital should be drained by price war.");
        Assert.True(social.GetRelationship(topRivalBefore.NpcId).Affinity < 0, "Targeted rival must hold negative affinity grudge.");

        Assert.Empty(kernel.RunInvariants());
    }

    [Fact]
    public void AntiSnowball_coalition_pressure_triggers_when_player_accumulates_high_wealth()
    {
        using var kernel = _host.Boot();
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        var society = kernel.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);

        // Make player wealthy enough (>= 12,000 Quan) to trigger Rival Counter-Pressure
        wallet.Pay(12000, "CORE.STARTING_CASH", "High net worth test");

        kernel.AdvanceDays(30);

        var commerceLogs = society.RecentEvents(limit: 80, category: "commerce");
        Assert.Contains(commerceLogs, e =>
            e.CitadelId == player.CurrentCityId &&
            e.HeadlineVi.Contains("Thương Chiến Chèn Ép"));
    }

    [Fact]
    public void RealmRankingsScreen_renders_all_five_categories_and_executes_commands()
    {
        using var kernel = _host.Boot();
        var screen = kernel.Ui.Screens.FirstOrDefault(s => s.ScreenId == "society.rankings");
        Assert.NotNull(screen);
        Assert.True(kernel.Ui.TryGetCommandHandler("society.rankings", out var handler));
        Assert.NotNull(handler);

        foreach (var tab in new[] { "wealth", "fame", "martial", "wisdom", "influence" })
        {
            var msg = handler!.Execute($"tab:{tab}");
            Assert.NotNull(msg);
            var model = screen!.Render();
            Assert.NotEmpty(model.Sections);
            Assert.NotEmpty(model.Headline);
            Assert.Contains(model.Sections, s => s.Heading.Contains("MỤC TIÊU PHẤN ĐẤU") || s.Heading.Contains("BẢNG"));
        }
    }
}
