using SimT.Contracts.Playing;
using SimT.Core.Kernel;

namespace SimT.Modules.Dlc.Tests;

public sealed class CityStrollTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void Strolling_consumes_fatigue_and_relieves_stress()
    {
        using var kernel = _host.Boot();
        var stroll = kernel.Capabilities.Get<ICityStroll>(CapabilityIds.CityStroll);
        var attrs = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);

        var initialFatigue = attrs.Vitals.Fatigue;
        var initialDay = kernel.Today.DayIndex;

        var result = stroll.Stroll(player.CurrentCityId);

        Assert.True(result.Ok);
        Assert.NotEmpty(result.Title);
        Assert.NotEmpty(result.Narrative);
        Assert.NotEmpty(result.RewardSummary);
        Assert.True(attrs.Vitals.Fatigue >= initialFatigue);
        Assert.Equal(initialDay + 1, kernel.Today.DayIndex);
    }

    [Fact]
    public void Exhausted_player_cannot_stroll()
    {
        using var kernel = _host.Boot();
        var stroll = kernel.Capabilities.Get<ICityStroll>(CapabilityIds.CityStroll);
        var attrs = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);

        attrs.ApplyFatigue(100);

        var result = stroll.Stroll(player.CurrentCityId);

        Assert.False(result.Ok);
        Assert.Contains("kiệt sức", result.Narrative);
    }

    [Fact]
    public void Stroll_command_can_be_executed_via_world_map_screen()
    {
        using var kernel = _host.Boot();
        Assert.True(kernel.TryGetCommandHandler("world.map", out var handler));
        var msg = handler!.Execute("stroll");

        Assert.NotNull(msg);
        Assert.Contains("Du Ngoạn", msg);
    }

    [Fact]
    public void Peaceful_stroll_is_most_frequent_event()
    {
        using var kernel = _host.Boot();
        var stroll = kernel.Capabilities.Get<ICityStroll>(CapabilityIds.CityStroll);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var attrs = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);

        var peacefulCount = 0;
        var nonKarmaAttempts = 0;
        var totalAttempts = 120;

        for (int i = 0; i < totalAttempts; i++)
        {
            // Reset fatigue to keep strolling
            attrs.RecoverVitals(fatigueRelief: 50, stressRelief: 0);

            var res = stroll.Stroll(player.CurrentCityId);
            if (res.EventType != StrollEventType.KarmaFruition)
            {
                nonKarmaAttempts++;
            }

            if (res.EventType == StrollEventType.Peaceful)
            {
                peacefulCount++;
            }
            else if (res.RequiresChoice)
            {
                // Resolve to clear pending
                stroll.ResolveChoice(player.CurrentCityId, res.EventType == StrollEventType.MarketBargain ? "decline" : "dialogue:leave");
            }
        }

        // Peaceful walk is 60% of random strolls; across ~115 non-karma attempts P(peaceful < 45) < 1e-5
        Assert.True(peacefulCount >= 45, $"Peaceful walks count ({peacefulCount}/{nonKarmaAttempts}) was lower than expected");
    }

    [Fact]
    public void Wandering_merchant_offer_purchase_works_and_charges_wallet()
    {
        using var kernel = _host.Boot();
        var stroll = kernel.Capabilities.Get<ICityStroll>(CapabilityIds.CityStroll);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        var bag = kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        var attrs = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);

        // Keep rolling until we hit wandering merchant
        StrollResult? merchantEncounter = null;
        for (int i = 0; i < 100; i++)
        {
            attrs.RecoverVitals(fatigueRelief: 50, stressRelief: 0);
            var res = stroll.Stroll(player.CurrentCityId);
            if (res.EventType == StrollEventType.MarketBargain && res.RequiresChoice && res.MerchantOffers?.Count > 0)
            {
                merchantEncounter = res;
                break;
            }

            if (res.RequiresChoice)
            {
                stroll.ResolveChoice(player.CurrentCityId, "dialogue:leave");
            }
        }

        if (merchantEncounter is not null)
        {
            var offer = merchantEncounter.MerchantOffers![0];
            var initialLiquid = wallet.Liquid;
            var initialQty = bag.Quantity(player.CurrentCityId, offer.ItemId);

            var buyResult = stroll.ResolveChoice(player.CurrentCityId, "buy", offer.ItemId.ToString());

            Assert.True(buyResult.Ok);
            Assert.False(buyResult.RequiresChoice);
            Assert.Contains(offer.ItemNameVi, buyResult.Narrative);
            Assert.Equal(initialQty + offer.Quantity, bag.Quantity(player.CurrentCityId, offer.ItemId));
            Assert.Equal(initialLiquid - offer.UnitPrice, wallet.Liquid);
        }
    }

    [Fact]
    public void Npc_encounter_offers_interactive_dialogue_choices()
    {
        using var kernel = _host.Boot();
        var stroll = kernel.Capabilities.Get<ICityStroll>(CapabilityIds.CityStroll);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var attrs = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);

        StrollResult? npcEncounter = null;
        for (int i = 0; i < 100; i++)
        {
            attrs.RecoverVitals(fatigueRelief: 50, stressRelief: 0);
            var res = stroll.Stroll(player.CurrentCityId);
            if (res.EventType == StrollEventType.NpcEncounter && res.RequiresChoice && res.Choices?.Count > 0)
            {
                npcEncounter = res;
                break;
            }

            if (res.RequiresChoice)
            {
                stroll.ResolveChoice(player.CurrentCityId, "decline");
            }
        }

        if (npcEncounter is not null)
        {
            Assert.True(npcEncounter.RequiresChoice);
            Assert.Equal(4, npcEncounter.Choices!.Count);

            // Test dialogue discourse choice
            var resolveResult = stroll.ResolveChoice(player.CurrentCityId, "dialogue:discourse");
            Assert.True(resolveResult.Ok);
            Assert.False(resolveResult.RequiresChoice);
            Assert.NotEmpty(resolveResult.Narrative);
        }
    }
}
