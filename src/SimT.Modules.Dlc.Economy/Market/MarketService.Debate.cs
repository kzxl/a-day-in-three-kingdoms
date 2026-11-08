using SimT.Contracts.Playing;

namespace SimT.Modules.Dlc.Economy;

internal sealed partial class MarketService
{
    internal long ApplyDebateHagglePrice(long cityId, long npcId, long itemId, long basePrice)
    {
        if (basePrice <= 0 || _capabilities is null)
            return Math.Max(1L, basePrice);

        if (!_capabilities.TryGet<IDebateService>(CapabilityIds.Debate, out var debate) || debate is null)
            return Math.Max(1L, basePrice);

        var modPct = debate.GetHagglePriceModifierPercent(cityId, npcId, itemId);
        if (modPct == 0)
            return Math.Max(1L, basePrice);

        return Math.Max(1L, (basePrice * (100 + modPct) + 50) / 100);
    }

    internal long ApplyBulkHaggleBuyUnitPrice(long cityId, long itemId, int quantity, long unitPrice)
    {
        if (quantity < IDebateService.MinHaggleLotQuantity || unitPrice <= 0 || _capabilities is null)
            return unitPrice;

        if (!_capabilities.TryGet<IDebateService>(CapabilityIds.Debate, out var debate) || debate is null)
            return unitPrice;

        var modPct = debate.GetCityBulkHaggleModifierPercent(cityId, itemId);
        if (modPct == 0)
            return unitPrice;

        return Math.Max(1L, (unitPrice * (100 + modPct) + 50) / 100);
    }
}
