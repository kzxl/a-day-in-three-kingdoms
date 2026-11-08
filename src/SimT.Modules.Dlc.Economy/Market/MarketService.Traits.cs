using SimT.Contracts.Data;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;

namespace SimT.Modules.Dlc.Economy;

internal sealed partial class MarketService
{
    private bool HasNpcTrait(long npcId, string traitCode)
    {
        if (npcId <= 0 || _capabilities is null)
            return false;

        if (_capabilities.TryGet<INpcTraits>(CapabilityIds.NpcTraits, out var npcTraits) && npcTraits is not null)
            return npcTraits.HasTrait(npcId, traitCode);

        if (_capabilities.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social) && social is not null)
        {
            var traits = social.GetTraits(npcId);
            for (var i = 0; i < traits.Count; i++)
            {
                if (string.Equals(traits[i].Code, traitCode, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    public int AdjustNpcOfferStock(long npcId, int baseQuantity)
    {
        if (baseQuantity <= 0)
            return 0;

        if (HasNpcTrait(npcId, NpcTraitCodes.KeoKiet))
            return Math.Max(1, baseQuantity * NpcTraitRules.KeoKietStockPercent / 100);

        return baseQuantity;
    }

    internal long AdjustNpcOfferBasePrice(long cityId, long npcId, long itemId, long rawUnitPrice)
    {
        var basePrice = rawUnitPrice;
        if (HasNpcTrait(npcId, NpcTraitCodes.TrongChuTin))
        {
            var catalogBase = _items.Find(itemId)?.BasePrice ?? rawUnitPrice;
            basePrice = Math.Max(1L, catalogBase * NpcTraitRules.TrongChuTinPricePercent / 100);
        }

        if (HasNpcTrait(npcId, NpcTraitCodes.ThamTai))
            basePrice = Math.Max(1L, (basePrice * NpcTraitRules.ThamTaiSellToPlayerPercent + 50) / 100);
        else if (HasNpcTrait(npcId, NpcTraitCodes.HaoSang))
            basePrice = Math.Max(1L, basePrice * NpcTraitRules.HaoSangSellToPlayerPercent / 100);

        return ApplyDebateHagglePrice(cityId, npcId, itemId, basePrice);
    }

    public long QuoteNpcOfferUnitPrice(long cityId, long npcId, long itemId, int discountPermille = 50)
    {
        var rawAsk = HasNpcTrait(npcId, NpcTraitCodes.TrongChuTin)
            ? (_items.Find(itemId)?.BasePrice ?? QuoteBuy(cityId, itemId))
            : QuoteBuy(cityId, itemId);

        var adjustedAsk = AdjustNpcOfferBasePrice(cityId, npcId, itemId, rawAsk);
        return Math.Max(1L, adjustedAsk * (1000 - discountPermille) / 1000);
    }

    public long QuoteNpcBuyBidUnitPrice(long cityId, long npcId, long itemId, int bidMarkupPermille = 100)
    {
        var baseBid = HasNpcTrait(npcId, NpcTraitCodes.TrongChuTin)
            ? (_items.Find(itemId)?.BasePrice ?? QuoteSell(cityId, itemId)) * 82 / 100
            : QuoteSell(cityId, itemId);

        var markedUp = Math.Max(1L, baseBid * (1000 + bidMarkupPermille) / 1000);

        if (HasNpcTrait(npcId, NpcTraitCodes.ThamTai))
            return Math.Max(1L, markedUp * NpcTraitRules.ThamTaiBuyFromPlayerPercent / 100);

        if (HasNpcTrait(npcId, NpcTraitCodes.HaoSang))
            return Math.Max(1L, (markedUp * NpcTraitRules.HaoSangBuyFromPlayerPercent + 50) / 100);

        return markedUp;
    }

    public TradeResult SellToNpcStall(long cityId, long stallId, long itemId, int quantity)
    {
        if (quantity <= 0)
            return TradeResult.Fail("Số lượng bán phải lớn hơn 0.");

        var stall = StallsAt(cityId).FirstOrDefault(s => s.Id == stallId);
        if (stall is null)
            return TradeResult.Fail("Sạp hàng không tồn tại ở thành trì hiện tại.");

        var item = _items.Find(itemId);
        if (item is null)
            return TradeResult.Fail("Hàng hóa không tồn tại.");

        var held = _bag.Quantity(cityId, itemId);
        if (held < quantity)
            return TradeResult.Fail($"Trong hành trang chỉ có {held} {item.NameVi}.");

        var npcId = stall.NpcId ?? 0L;
        var unitBid = QuoteNpcBuyBidUnitPrice(cityId, npcId, itemId, bidMarkupPermille: 50);
        var total = unitBid * quantity;

        if (!_bag.Remove(cityId, itemId, quantity))
            return TradeResult.Fail("Không thể xuất hàng từ hành trang.");

        _wallet.Pay(total, "mk.npc_sell", $"Bán {quantity} {item.NameVi} cho '{stall.NameVi}'");

        return new TradeResult(true, unitBid, total, $"Đã bán {quantity} {item.NameVi} cho '{stall.NameVi}' thu về {total} Quan.");
    }
}
