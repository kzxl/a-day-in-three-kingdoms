using SimT.Contracts.Modules;
using SimT.Contracts.Playing;

namespace SimT.Modules.Dlc.Power;

internal sealed partial class CourtService
{
    internal ICapabilityBroker? Capabilities { get; set; }

    private int ApplyDebateTaxRelief(long citadelId, int rawTaxRatePercent)
    {
        if (rawTaxRatePercent <= 0 || Capabilities is null)
            return rawTaxRatePercent;

        if (!Capabilities.TryGet<IDebateService>(CapabilityIds.Debate, out var debate) || debate is null)
            return rawTaxRatePercent;

        var relief = debate.GetTaxReliefPercent(citadelId);
        if (relief <= 0)
            return rawTaxRatePercent;

        return Math.Max(1, rawTaxRatePercent - relief);
    }
}
