namespace SimT.Contracts.Playing;
public sealed record NpcLifetimeDeath(long NpcId, string NameVi, long CitadelId, int Year);
public interface INpcLifetimes
{
    const string CapabilityId = "npc.lifetimes";
    IReadOnlyList<NpcLifetimeDeath> Deaths { get; }
}
