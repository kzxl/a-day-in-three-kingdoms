using SimT.Contracts.Playing;
using SimT.Core.Kernel;
using SimT.Testing;
using Xunit;

namespace SimT.Modules.Market.Tests;

public sealed class NpcStallTests : IDisposable
{
    private readonly TestHost _host = new("simt-market-npc-stalls");

    public void Dispose() => _host.Dispose();

    private SimKernel Boot() => _host.Boot();

    [Fact]
    public void NpcMarket_loads_real_stalls_across_citadels()
    {
        using var kernel = Boot();
        var market = kernel.Capabilities.Get<INpcMarket>(CapabilityIds.NpcMarket);

        // Luoyang stalls
        var luoyangStalls = market.StallsAt(1);
        Assert.NotEmpty(luoyangStalls);
        Assert.Equal(5, luoyangStalls.Count);

        // Check sectors
        Assert.Contains(luoyangStalls, s => s.Sector == "grain");
        Assert.Contains(luoyangStalls, s => s.Sector == "textile");
        Assert.Contains(luoyangStalls, s => s.Sector == "medicine");
        Assert.Contains(luoyangStalls, s => s.Sector == "metal");
        Assert.Contains(luoyangStalls, s => s.Sector == "luxury");

        // Stall 3 is owned by NPC 1
        var medStall = luoyangStalls.First(s => s.Sector == "medicine");
        Assert.Equal("Hiệu Thuốc Tế Thế", medStall.NameVi);
        Assert.Equal("npc", medStall.OwnerKind);
        Assert.Equal(1L, medStall.NpcId);
    }

    [Fact]
    public void NpcMarket_loads_npc_offers_and_allows_purchase()
    {
        using var kernel = Boot();
        var market = kernel.Capabilities.Get<INpcMarket>(CapabilityIds.NpcMarket);
        var bag = kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var cityId = player.CurrentCityId; // 1 (Luoyang)

        var offers = market.OffersAt(cityId);
        Assert.NotEmpty(offers);

        // Offer 1 is Medicine (item 7)
        var medOffer = offers.First(o => o.ItemId == 7);
        Assert.Equal(20, medOffer.Quantity);
        Assert.Equal(110L, medOffer.UnitPrice);
        Assert.Equal(50, medOffer.DiscountPermille); // 5% discount

        var liquidBefore = kernel.Ledger.Liquid;
        var bagBefore = bag.Quantity(cityId, 7);

        // Buy 2 units: effective unit price = 110 * 950 / 1000 = 104 Quan
        var result = market.BuyFromNpcOffer(cityId, medOffer.Id, 2);
        Assert.True(result.Ok, result.Message);
        Assert.Equal(104L, result.UnitPrice);
        Assert.Equal(208L, result.Total);

        // Verify ledger charge
        Assert.Equal(liquidBefore - 208L, kernel.Ledger.Liquid);

        // Verify item added to player's bag
        Assert.Equal(bagBefore + 2, bag.Quantity(cityId, 7));

        // Verify offer quantity deducted
        var updatedOffer = market.OffersAt(cityId).First(o => o.Id == medOffer.Id);
        Assert.Equal(18, updatedOffer.Quantity);
    }

    [Fact]
    public void NpcMarket_buy_rejects_insufficient_liquid_or_over_quantity()
    {
        using var kernel = Boot();
        var market = kernel.Capabilities.Get<INpcMarket>(CapabilityIds.NpcMarket);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var cityId = player.CurrentCityId;

        var offers = market.OffersAt(cityId);
        var offer = offers.First();

        // Over-quantity request
        var overResult = market.BuyFromNpcOffer(cityId, offer.Id, offer.Quantity + 10);
        Assert.False(overResult.Ok);
        Assert.Contains("Sạp chỉ còn", overResult.Message);

        // Non-positive quantity
        var zeroResult = market.BuyFromNpcOffer(cityId, offer.Id, 0);
        Assert.False(zeroResult.Ok);

        // Wrong citadel
        var wrongCityResult = market.BuyFromNpcOffer(999, offer.Id, 1);
        Assert.False(wrongCityResult.Ok);
        Assert.Contains("không ở thành trì hiện tại", wrongCityResult.Message);
    }

    [Fact]
    public void NpcMarket_restocks_offers_on_month_close()
    {
        using var kernel = Boot();
        var market = kernel.Capabilities.Get<INpcMarket>(CapabilityIds.NpcMarket);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var cityId = player.CurrentCityId;

        var offer = market.OffersAt(cityId).First(o => o.ItemId == 7);
        var buyResult = market.BuyFromNpcOffer(cityId, offer.Id, 5);
        Assert.True(buyResult.Ok);

        var remaining = market.OffersAt(cityId).First(o => o.Id == offer.Id).Quantity;
        Assert.Equal(15, remaining);

        // Advance 30 days to trigger MonthClosed
        kernel.AdvanceDays(30);

        var restocked = market.OffersAt(cityId).First(o => o.Id == offer.Id).Quantity;
        Assert.Equal(offer.MaxQuantity, restocked);
    }

    [Fact]
    public void StallScreen_renders_all_item_categories_in_vietnamese_without_raw_keys()
    {
        using var kernel = Boot();
        var stallScreen = kernel.Ui.Screens.Single(s => s.ScreenId == "market.stall").Render();

        var headings = stallScreen.Sections.Select(s => s.Heading).ToList();
        Assert.Contains(headings, h => h.Contains("Hạt Giống", StringComparison.Ordinal));
        Assert.Contains(headings, h => h.Contains("Vật Tư Nông Nghiệp", StringComparison.Ordinal));
        Assert.Contains(headings, h => h.Contains("Nông Cụ", StringComparison.Ordinal));
        Assert.DoesNotContain(headings, h => string.Equals(h, "seed", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(headings, h => string.Equals(h, "farming", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(headings, h => string.Equals(h, "tool", StringComparison.OrdinalIgnoreCase));
    }
}
