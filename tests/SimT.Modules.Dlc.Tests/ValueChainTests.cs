using SimT.Contracts.Playing;
using SimT.Modules.Dlc.Economy.ValueChain;
using Xunit;

namespace SimT.Modules.Dlc.Tests;

public sealed class ValueChainTests
{
    [Theory]
    [InlineData("gao", EconomicValueTier.Tier1_Raw)]
    [InlineData("lua_mi", EconomicValueTier.Tier1_Raw)]
    [InlineData("than_da", EconomicValueTier.Tier1_Raw)]
    [InlineData("go", EconomicValueTier.Tier1_Raw)]
    [InlineData("muoi", EconomicValueTier.Tier2_Processed)]
    [InlineData("sat", EconomicValueTier.Tier2_Processed)]
    [InlineData("dong", EconomicValueTier.Tier2_Processed)]
    [InlineData("vai", EconomicValueTier.Tier2_Processed)]
    [InlineData("ca_kho", EconomicValueTier.Tier2_Processed)]
    [InlineData("ruou", EconomicValueTier.Tier3_Manufactured)]
    [InlineData("lua", EconomicValueTier.Tier3_Manufactured)]
    [InlineData("thuoc", EconomicValueTier.Tier3_Manufactured)]
    [InlineData("cuoc_nong", EconomicValueTier.Tier3_Manufactured)]
    [InlineData("gam", EconomicValueTier.Tier4_Luxury_EndSink)]
    [InlineData("tra", EconomicValueTier.Tier4_Luxury_EndSink)]
    [InlineData("sam", EconomicValueTier.Tier4_Luxury_EndSink)]
    [InlineData("tram_huong", EconomicValueTier.Tier4_Luxury_EndSink)]
    public void Commodity_items_correctly_classified_into_economic_value_tiers(string itemCode, EconomicValueTier expectedTier)
    {
        var tier = ItemValueChain.GetTier(itemCode);
        Assert.Equal(expectedTier, tier);

        var profile = ItemValueChain.GetProfile(itemCode);
        Assert.False(string.IsNullOrWhiteSpace(profile.TierNameVi));
        Assert.False(string.IsNullOrWhiteSpace(profile.BadgeVi));
        Assert.False(string.IsNullOrWhiteSpace(profile.PrimaryConsumerVi));
        Assert.False(string.IsNullOrWhiteSpace(profile.EconomicRoleVi));
    }

    [Fact]
    public void Value_chain_tracks_input_recipe_dependencies()
    {
        // Rice/Wheat are inputs for Wine
        Assert.True(ItemValueChain.IsInputFor("gao", "ruou"));
        Assert.True(ItemValueChain.IsInputFor("lua_mi", "ruou"));

        // Iron and Timber are inputs for Tools
        Assert.True(ItemValueChain.IsInputFor("sat", "cuoc_nong"));
        Assert.True(ItemValueChain.IsInputFor("go", "cuoc_nong"));

        // Silk is input for Brocade (Gấm Thục)
        Assert.True(ItemValueChain.IsInputFor("lua", "gam"));

        // Salt is input for Dried Fish
        Assert.True(ItemValueChain.IsInputFor("muoi", "ca_kho"));
    }

    [Fact]
    public void Unknown_item_resolves_to_safe_fallback_profile()
    {
        var fallback = ItemValueChain.GetProfile("unknown_ancient_relic");
        Assert.Equal(EconomicValueTier.Tier2_Processed, fallback.Tier);
        Assert.Contains("Hàng Hóa", fallback.TierNameVi);
    }
}
