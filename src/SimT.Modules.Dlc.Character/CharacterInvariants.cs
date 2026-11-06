using SimT.Contracts.Diagnostics;

namespace SimT.Modules.Dlc.Character;

internal static class CharacterInvariants
{
    public static IEnumerable<IInvariant> All()
    {
        foreach (var inv in SocialInvariants.All()) yield return inv;
        foreach (var inv in LineageInvariants.All()) yield return inv;
    }
}
