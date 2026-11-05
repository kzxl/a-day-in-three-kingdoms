using SimT.Contracts.Diagnostics;

namespace SimT.Modules.Dlc.Economy;

internal static class EconomyInvariants
{
    public static IEnumerable<IInvariant> All()
    {
        foreach (var inv in MarketInvariants.All()) yield return inv;
        foreach (var inv in CaravanInvariants.All()) yield return inv;
        foreach (var inv in FarmInvariants.All()) yield return inv;
        foreach (var inv in ProfessionInvariants.All()) yield return inv;
    }
}
