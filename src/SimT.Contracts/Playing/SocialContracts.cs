using SimT.Contracts.Time;

namespace SimT.Contracts.Playing;

/// <summary>
/// A bilateral social bond between the player and an NPC.
/// </summary>
public sealed record RelationshipDef(
    long NpcId,
    int Affinity, // -100 (nemesis) to +100 (sworn soulmate)
    int Trust,    // 0 to 100
    string Impression,
    int GiftCount,
    GameDate LastInteractionDate);

/// <summary>
/// A recorded memory of an interaction or favor between player and NPC.
/// </summary>
public sealed record MemoryEntry(
    long Id,
    long NpcId,
    GameDate Date,
    string Topic, // "chat", "gift", "recruit", "business"
    string Sentiment, // "positive", "neutral", "negative"
    int AffinityDelta,
    string SummaryVi);

/// <summary>
/// A recruited retainer/officer serving the player's personal faction or enterprise.
/// </summary>
public sealed record RetainerDef(
    long Id,
    long NpcId,
    string RoleAssigned, // "bodyguard", "accountant", "advisor", "manager"
    int MonthlyStipend,
    int Loyalty, // 0 to 100
    GameDate RecruitedDate);

/// <summary>
/// Social network, relationship matrix, and retainer management surface owned by <c>dlc.social</c>.
/// </summary>
public interface ISocialNetwork
{
    RelationshipDef GetRelationship(long npcId);

    IReadOnlyList<RelationshipDef> AllRelationships();

    IReadOnlyList<MemoryEntry> GetMemories(long npcId, int limit = 20);

    IReadOnlyList<RetainerDef> GetRetainers();

    RetainerDef? GetRetainer(long npcId);

    /// <summary>Engages in dialogue with an NPC in the same settlement.</summary>
    string ChatWithNpc(long npcId);

    /// <summary>Presents a gift from the player's inventory to an NPC.</summary>
    string GiftNpc(long npcId, long itemId);

    /// <summary>Recruits an allied NPC as a retainer if affinity is high enough.</summary>
    string RecruitRetainer(long npcId, string role, int stipend);

    /// <summary>Dismisses a retainer from personal service.</summary>
    string DismissRetainer(long npcId);

    /// <summary>Requests mentorship/skill training from a friendly NPC.</summary>
    string LearnSkillFromNpc(long npcId, string skillCode);
}
