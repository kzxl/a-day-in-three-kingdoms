using SimT.Contracts.Time;

namespace SimT.Contracts.Playing;

/// <summary>
/// 4 Psychological Archetypes governing NPC personality, dialogue affinity, and gift preferences.
/// </summary>
public enum CharacterArchetype
{
    Hegemon = 1,     // Bá Đạo (Trọng võ công, danh vọng, vũ khí giáp trụ, phản đối quy phục)
    Benevolent = 2,  // Vương Đạo (Trọng nhân nghĩa, bá tánh, lương thảo, y dược, chán ghét trục lợi)
    Pragmatist = 3,  // Thương Nhân / Thực Dụng (Trọng tiền tài, trao đổi sòng phẳng, hàng xa xỉ)
    Hermit = 4       // Ẩn Sĩ / Phương Ngoại (Trọng phẩm trà, kỳ vật, thanh tịnh, chán ghét nịnh bợ)
}

/// <summary>
/// Detailed psyche and personality archetype profile of an NPC in the Three Kingdoms.
/// </summary>
public sealed record NpcPsycheProfile(
    long NpcId,
    CharacterArchetype Archetype,
    string ArchetypeVi,
    string BadgeVi,
    int Ambition,
    int Integrity,
    int Materialism);

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

public sealed record NpcDispositionDef(
    long NpcId,
    bool IsGrudged,
    bool IsFavored,
    int PriceFactorPermille,
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
/// A sworn brother or sister bound by the sacred Peach Garden oath (Kết Nghĩa Kim Lan).
/// </summary>
public sealed record SwornBrotherDef(
    long NpcId,
    GameDate OathDate,
    string BondTitle,
    int AssistCount);

/// <summary>
/// NPC social and recruitment tier based on talent, prestige, and historical stature.
/// </summary>
public enum NpcRank
{
    S, // Legendary commanders, peerless strategists, top luminaries (Quan Vũ, Triệu Vân, Gia Cát Lượng, Chu Du)
    A, // Renowned generals, high court ministers, master scholars (Lư Thực, Vương Doãn, Phú Hào)
    B, // Capable officers, local champions, senior artisans
    C  // Commoners, local merchants, tavern keepers, wanderers
}

/// <summary>
/// Requirement specification for recruiting an NPC as a personal retainer.
/// </summary>
public sealed record RecruitRequirementDef(
    NpcRank Rank,
    int RequiredAffinity,
    int RequiredFame,
    int SigningFee,
    int MinMonthlyStipend,
    bool IsWarlord,
    bool CanRecruit,
    string StatusSummary);

/// <summary>
/// A letter/missive sent by a friendly or allied NPC to the player.
/// </summary>
public sealed record LetterDef(
    long Id,
    GameDate SentDate,
    long SenderNpcId,
    string SenderNameVi,
    long SenderCitadelId,
    string TitleVi,
    string ContentVi,
    long? GiftItemId,
    string? GiftItemCode,
    string? GiftItemNameVi,
    int GiftItemQuantity,
    int GiftCopper,
    bool IsRead,
    bool IsClaimed);

/// <summary>
/// First-meeting record for an NPC in Võ tướng lục.
/// </summary>
public sealed record MetNpcRecord(
    long NpcId,
    GameDate FirstMetDate,
    long MetCitadelId);

/// <summary>
/// Claimed collection milestone in Võ tướng lục.
/// </summary>
public sealed record RosterMilestoneRecord(
    string MilestoneKey,
    GameDate ClaimedDate,
    int FameAwarded);

/// <summary>
/// Social network, relationship matrix, and retainer management surface owned by <c>dlc.social</c>.
/// </summary>
public interface ISocialNetwork
{
    RelationshipDef GetRelationship(long npcId);

    /// <summary>Evaluates memory and relationship-driven behavioral disposition (grudge markup, favor discount, trade sanctions).</summary>
    NpcDispositionDef GetDisposition(long npcId) => new(npcId, false, false, 1000, "Bình thường");

    /// <summary>Gets the first-meeting record for an NPC if the player has met them.</summary>
    MetNpcRecord? GetMetNpc(long npcId) => null;

    /// <summary>Gets all met NPC records in Võ tướng lục.</summary>
    IReadOnlyList<MetNpcRecord> GetAllMetNpcs() => Array.Empty<MetNpcRecord>();

    /// <summary>Records the player's first meeting with an NPC if not already recorded, and evaluates collection milestones.</summary>
    void RecordMetNpc(long npcId, GameDate? date = null, long? citadelId = null) { }

    /// <summary>Gets all claimed Võ tướng lục collection milestones.</summary>
    IReadOnlyList<RosterMilestoneRecord> GetClaimedRosterMilestones() => Array.Empty<RosterMilestoneRecord>();

    /// <summary>Evaluates and awards any newly reached Võ tướng lục collection milestones (10, 50, 100 met NPCs, all historical events witnessed).</summary>
    IReadOnlyList<RosterMilestoneRecord> CheckAndAwardRosterMilestones() => Array.Empty<RosterMilestoneRecord>();

    /// <summary>Gets the psychological archetype and personality profile of an NPC.</summary>
    NpcPsycheProfile GetPsycheProfile(long npcId);

    /// <summary>Gets the full canonical or seed-determined traits (1–3) of an NPC.</summary>
    IReadOnlyList<NpcTraitDef> GetTraits(long npcId);

    /// <summary>Gets the UI-visible traits of an NPC, masking secondary traits as "???" when affinity is low.</summary>
    IReadOnlyList<NpcTraitView> GetVisibleTraits(long npcId);

    /// <summary>Awards bonus affinity after the player wins a martial duel against an NPC with the Mê võ trait.</summary>
    int RecordDuelVictoryBonus(long npcId);

    IReadOnlyList<RelationshipDef> AllRelationships();

    IReadOnlyList<MemoryEntry> GetMemories(long npcId, int limit = 20);

    IReadOnlyList<RetainerDef> GetRetainers();

    RetainerDef? GetRetainer(long npcId);

    /// <summary>Gets the list of active sworn brothers.</summary>
    IReadOnlyList<SwornBrotherDef> GetSwornBrothers();

    /// <summary>Checks whether the player has met, interacted with, or established relations with an NPC.</summary>
    bool HasMet(long npcId);

    /// <summary>Evaluates the social/recruitment rank tier of an NPC.</summary>
    NpcRank GetNpcRank(long npcId);

    /// <summary>Calculates the recruitment eligibility and requirements for an NPC.</summary>
    RecruitRequirementDef GetRecruitRequirement(long npcId);

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

    /// <summary>Undergoes intensive mastery training with an NPC who knows the skill, gaining proficiency EXP towards the next level.</summary>
    string TrainSkillWithNpc(long npcId, string skillCode);

    /// <summary>Performs the sworn brotherhood oath (Kết Nghĩa Kim Lan) with an eligible NPC.</summary>
    string SwearBrotherhood(long npcId);

    /// <summary>Hosts a grand banquet in the player's current city, inviting local figures.</summary>
    string HostBanquet();

    /// <summary>Retrieves letters received by the player, optionally filtering for unread only.</summary>
    IReadOnlyList<LetterDef> GetLetters(bool unreadOnly = false, int limit = 30);

    /// <summary>Gets a specific letter by ID.</summary>
    LetterDef? GetLetter(long letterId);

    /// <summary>Reads a letter, marking it as read and returning its full narrative presentation.</summary>
    string ReadLetter(long letterId);

    /// <summary>Claims the gift (copper and/or goods) attached to a letter.</summary>
    string ClaimLetterGift(long letterId);

    /// <summary>Count of unread letters in the player's inbox.</summary>
    int UnreadLetterCount();

    /// <summary>Records a mutually beneficial commercial or commission cooperation with an NPC, boosting affinity and trust.</summary>
    void RecordCooperation(long npcId, int affinityDelta, int trustDelta, string summaryVi);
}
