using SimT.Contracts.Time;
namespace SimT.Contracts.Playing;

public sealed record NpcBond(long NpcA, long NpcB, string Kind, int Strength, GameDate SinceDate, string Source)
{
    public long Other(long npcId) => npcId == NpcA ? NpcB : NpcA;
}
/// <summary>Read-only NPC bonds; parent/lord/mentor run from A to B.</summary>
public interface INpcBonds
{
    const string CapabilityId = "npc.bonds";
    IReadOnlyList<NpcBond> ForNpc(long npcId);
}
public static class NpcBondRules
{
    public const int CloseAffinity = 60;
    public const int IntroductionBonus = 10;
    public const int RivalPenaltyDivisor = 5;
    public const int MonthlyChangeLimit = 3;
}
