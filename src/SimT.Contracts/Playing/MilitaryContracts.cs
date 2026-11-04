using SimT.Contracts.Time;

namespace SimT.Contracts.Playing;

/// <summary>
/// A hostile bandit stronghold or outlaw encampment menacing a settlement.
/// </summary>
public sealed record StrongholdDef(
    long Id,
    long CitadelId,
    string NameVi,
    string BanditFaction, // "khan_vang", "hac_son", "bach_ba", "tho_phi"
    int DangerLevel,      // 1 (bandits) to 10 (warlord rebel fortress)
    int LootGold,
    string ItemRewardCode,
    int ClearedCount,
    GameDate? LastClearedDate);

/// <summary>
/// The outcome of an arena duel between the player and an NPC.
/// </summary>
public sealed record DuelResult(
    bool Won,
    int Rounds,
    int GoldReward,
    string OpponentName,
    string CombatSummaryVi);

/// <summary>
/// A military commissary requisition for supplies (grain, iron, copper, etc.).
/// </summary>
public sealed record SupplyRequisitionDef(
    long Id,
    long CitadelId,
    string TargetItemCode,
    string TargetItemName,
    int QuantityNeeded,
    int RewardPerUnit,
    GameDate DeadlineDate,
    bool Fulfilled);

/// <summary>
/// Military, martial arena, and expedition surface owned by <c>dlc.military</c>.
/// </summary>
public interface IMilitaryCommand
{
    IReadOnlyList<StrongholdDef> StrongholdsAt(long citadelId);

    IReadOnlyList<StrongholdDef> AllStrongholds();

    IReadOnlyList<SupplyRequisitionDef> ActiveRequisitions(long citadelId);

    /// <summary>Challenges an NPC warrior to a duel in the local arena.</summary>
    DuelResult ChallengeArena(long npcId);

    /// <summary>Leads an expedition against an outlaw stronghold.</summary>
    string AssaultStronghold(long strongholdId);

    /// <summary>Fulfills a military garrison requisition from local bag stock.</summary>
    string FulfillRequisition(long requisitionId);
}
