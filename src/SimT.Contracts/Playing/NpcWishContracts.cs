using SimT.Contracts.Time;
namespace SimT.Contracts.Playing;

public sealed record NpcWish(long Id, long NpcId, string Template, long ItemId, int Quantity,
    long CityId, long TargetNpcId, GameDate Opened, GameDate Deadline, string State, bool Evidence);
public sealed record NpcWishProgress(NpcWish Wish, string NpcName, string RequestVi, string ProgressVi,
    long DestinationCityId, bool Ready);
public interface INpcWishes
{
    const string CapabilityId = "npc.wishes";
    IReadOnlyList<NpcWishProgress> Journal();
    NpcWishProgress? ForNpc(long npcId);
    IReadOnlyList<string> RecentNotices();
    string Accept(long npcId);
    string Give(long npcId);
}
public static class NpcWishRules
{
    public const int RevealAffinity = 60;
    public const int OpenLimit = 12;
    public const int MonthlyLimit = 3;
    public const int RewardAffinity = 25;
    public const int ExpiryPenalty = 5;
}
