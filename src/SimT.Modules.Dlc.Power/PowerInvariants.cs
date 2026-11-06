using SimT.Contracts.Diagnostics;

namespace SimT.Modules.Dlc.Power;

internal static class PowerInvariants
{
    public static IEnumerable<IInvariant> All()
    {
        foreach (var inv in CourtInvariants.All()) yield return inv;
        foreach (var inv in MilitaryInvariants.All()) yield return inv;
    }
}
