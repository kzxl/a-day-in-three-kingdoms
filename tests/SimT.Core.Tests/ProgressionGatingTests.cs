using SimT.Contracts.Playing;
using Xunit;

namespace SimT.Core.Tests;

public sealed class ProgressionGatingTests
{
    [Theory]
    [InlineData("city.scene")]
    [InlineData("city.hub")]
    [InlineData("market.stall")]
    [InlineData("professions.trades")]
    [InlineData("content.item_hub")]
    [InlineData("core.ledger")]
    [InlineData("social.network")]
    [InlineData("npc.directory")]
    [InlineData("society.chronicle")]
    [InlineData("player.destiny")]
    [InlineData("player.reputation")]
    public void Core_starter_screens_are_unlocked_on_day_one(string screenId)
    {
        var status = ProgressionGating.Check(screenId, daysPassed: 0, balanceQuan: 2000, reputationTier: 1);
        Assert.True(status.IsUnlocked, $"Starter screen {screenId} should be unlocked on Day 1.");
    }

    [Theory]
    [InlineData("market.bank")]
    [InlineData("market.player_bank")]
    [InlineData("caravan.routes")]
    [InlineData("lineage.family")]
    [InlineData("politics.court")]
    public void Advanced_screens_are_gated_on_day_one_with_modest_funds(string screenId)
    {
        var status = ProgressionGating.Check(screenId, daysPassed: 0, balanceQuan: 2000, reputationTier: 1);
        Assert.False(status.IsUnlocked, $"Advanced screen {screenId} should be gated on Day 1.");
        Assert.NotEmpty(status.ConditionVi);
        Assert.NotEmpty(status.AdviceVi);
    }

    [Fact]
    public void Bank_unlocks_when_capital_reaches_threshold()
    {
        var locked = ProgressionGating.Check("market.bank", daysPassed: 10, balanceQuan: 4000, reputationTier: 1);
        Assert.False(locked.IsUnlocked);

        var unlocked = ProgressionGating.Check("market.bank", daysPassed: 10, balanceQuan: 5500, reputationTier: 1);
        Assert.True(unlocked.IsUnlocked);
    }

    [Fact]
    public void Player_bank_requires_both_high_capital_and_reputation()
    {
        var lowRep = ProgressionGating.Check("market.player_bank", daysPassed: 100, balanceQuan: 30000, reputationTier: 1);
        Assert.False(lowRep.IsUnlocked);

        var ready = ProgressionGating.Check("market.player_bank", daysPassed: 100, balanceQuan: 25000, reputationTier: 3);
        Assert.True(ready.IsUnlocked);
    }

    [Fact]
    public void World_map_unlocks_with_either_experience_or_funds()
    {
        var initial = ProgressionGating.Check("world.map", daysPassed: 0, balanceQuan: 500, reputationTier: 1);
        Assert.False(initial.IsUnlocked);

        var withFunds = ProgressionGating.Check("world.map", daysPassed: 0, balanceQuan: 1500, reputationTier: 1);
        Assert.True(withFunds.IsUnlocked);

        var withDays = ProgressionGating.Check("world.map", daysPassed: 3, balanceQuan: 500, reputationTier: 1);
        Assert.True(withDays.IsUnlocked);
    }
}
