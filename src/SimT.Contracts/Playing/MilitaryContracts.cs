using SimT.Contracts.Time;

namespace SimT.Contracts.Playing;

/// <summary>
/// The three iconic military troop branches forming the rock-paper-scissors counter system.
/// </summary>
public enum TroopBranch
{
    /// <summary>Infantry (Bộ Binh) — High defense and shield formations. Counters Archers (+25% dmg); vulnerable to Cavalry.</summary>
    Infantry = 0,

    /// <summary>Cavalry (Kỵ Binh) — High speed and charge momentum. Counters Infantry (+25% dmg); vulnerable to Archers.</summary>
    Cavalry = 1,

    /// <summary>Archers (Cung Binh) — Long-range precision volleys. Counters Cavalry (+25% dmg); vulnerable to Infantry.</summary>
    Archers = 2
}

/// <summary>
/// Helper methods and descriptions for troop branches and affinity counter mechanics.
/// </summary>
public static class TroopBranchHelper
{
    public static string NameVi(TroopBranch branch) => branch switch
    {
        TroopBranch.Infantry => "Bộ Binh",
        TroopBranch.Cavalry => "Kỵ Binh",
        TroopBranch.Archers => "Cung Binh",
        _ => "Bộ Binh"
    };

    public static string Icon(TroopBranch branch) => branch switch
    {
        TroopBranch.Infantry => "🛡️",
        TroopBranch.Cavalry => "🐎",
        TroopBranch.Archers => "🏹",
        _ => "🛡️"
    };

    /// <summary>
    /// Checks if <paramref name="attacker"/> holds counter-advantage over <paramref name="defender"/>.
    /// Cavalry beats Infantry, Infantry beats Archers, Archers beat Cavalry.
    /// </summary>
    public static bool Counters(TroopBranch attacker, TroopBranch defender) =>
        (attacker == TroopBranch.Cavalry && defender == TroopBranch.Infantry) ||
        (attacker == TroopBranch.Infantry && defender == TroopBranch.Archers) ||
        (attacker == TroopBranch.Archers && defender == TroopBranch.Cavalry);

    public static string MatchupSummaryVi(TroopBranch player, TroopBranch opponent)
    {
        if (Counters(player, opponent))
        {
            return $"{Icon(player)} {NameVi(player)} KHẮC CHẾ {Icon(opponent)} {NameVi(opponent)} (+25% Sát Thương!)";
        }
        if (Counters(opponent, player))
        {
            return $"{Icon(opponent)} {NameVi(opponent)} BỊ KHẮC BỞI {Icon(player)} {NameVi(player)} (-15% Giảm Thủ!)";
        }
        return $"{Icon(player)} {NameVi(player)} ngang sức ngang tài {Icon(opponent)} {NameVi(opponent)}";
    }
}

/// <summary>
/// 4 asymmetric tactical combat commands for single-combat duels.
/// </summary>
public enum TacticalDuelAction
{
    /// <summary>Fierce offensive strike, interrupts Focus Spirit (+30% crush damage).</summary>
    Attack = 0,

    /// <summary>Defensive parry, negates 75% attack damage, parry-counters Special Strike.</summary>
    Defend = 1,

    /// <summary>Devastating signature martial technique, consumes 30 Spirit.</summary>
    SpecialStrike = 2,

    /// <summary>Channels inner qi (+35 Spirit, +30% damage buff on subsequent round).</summary>
    FocusSpirit = 3
}

public static class TacticalDuelActionHelper
{
    public static string NameVi(TacticalDuelAction action) => action switch
    {
        TacticalDuelAction.Attack => "CÔNG",
        TacticalDuelAction.Defend => "THỦ",
        TacticalDuelAction.SpecialStrike => "SÁT",
        TacticalDuelAction.FocusSpirit => "KHÍ",
        _ => "CÔNG"
    };

    public static string Icon(TacticalDuelAction action) => action switch
    {
        TacticalDuelAction.Attack => "⚔️",
        TacticalDuelAction.Defend => "🛡️",
        TacticalDuelAction.SpecialStrike => "💥",
        TacticalDuelAction.FocusSpirit => "🧘",
        _ => "⚔️"
    };

    public static string DescriptionVi(TacticalDuelAction action) => action switch
    {
        TacticalDuelAction.Attack => "Công: Tấn công dũng mãnh, phá chiêu Tụ Khí (+30% Sát thương)",
        TacticalDuelAction.Defend => "Thủ: Phòng ngự đỡ gạt, hóa giải và phản kích Tuyệt Kỹ (-75% sát thương)",
        TacticalDuelAction.SpecialStrike => "Sát: Tuyệt kỹ võ tướng uy lực kinh hoàng (tiêu hao 30 Khí lực)",
        TacticalDuelAction.FocusSpirit => "Khí: Tụ khí quy nguyên (+35 Khí lực, buff +30% sát thương hiệp sau)",
        _ => ""
    };
}

/// <summary>
/// Record of a single tactical round clash between duelists.
/// </summary>
public sealed record DuelRoundLog(
    int RoundNumber,
    TacticalDuelAction PlayerAction,
    TacticalDuelAction OpponentAction,
    int PlayerDamageDealt,
    int OpponentDamageDealt,
    int PlayerHpAfter,
    int OpponentHpAfter,
    int PlayerSpiritAfter,
    int OpponentSpiritAfter,
    bool IsCritical,
    bool IsParried,
    string NarrativeVi);

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
    GameDate? LastClearedDate,
    TroopBranch Branch = TroopBranch.Infantry);

/// <summary>
/// Dossier of a bandit warlord / chief commanding a stronghold.
/// </summary>
public sealed record BanditChiefDef(
    string NameVi,
    string TitleVi,
    string AvatarChar,
    string FactionVi,
    TroopBranch Branch,
    int War,
    int Ldr,
    int Int,
    int TroopCount,
    int MaxHp,
    int TerrainDanger,
    string TerrainNameVi,
    string DescriptionVi);

/// <summary>
/// Log of an individual combat round clash between the expeditionary forces and bandit horde.
/// </summary>
public sealed record BanditBattleRoundLog(
    int RoundNumber,
    int PlayerDamageDealt,
    int BanditDamageDealt,
    int PlayerTroopsRemaining,
    int BanditTroopsRemaining,
    int PlayerHpRemaining,
    int BanditHpRemaining,
    string? SkillTriggeredVi,
    string NarrativeVi,
    bool IsDecisive);

/// <summary>
/// Complete outcome of a visual expedition clash against an outlaw stronghold.
/// </summary>
public sealed record BanditExpeditionResult(
    bool Won,
    int Rounds,
    int LootGold,
    string ItemRewardCode,
    string ItemRewardName,
    int FameGained,
    int VirtueGained,
    int PlayerInitialTroops,
    int PlayerFinalTroops,
    int BanditInitialTroops,
    int BanditFinalTroops,
    BanditChiefDef Chief,
    StrongholdDef Stronghold,
    IReadOnlyList<BanditBattleRoundLog> RoundLogs,
    string SummaryVi);

/// <summary>
/// The outcome of an arena duel between the player and an NPC.
/// </summary>
public sealed record DuelResult(
    bool Won,
    int Rounds,
    int GoldReward,
    string OpponentName,
    string CombatSummaryVi,
    TroopBranch PlayerBranch = TroopBranch.Infantry,
    TroopBranch OpponentBranch = TroopBranch.Infantry,
    int PlayerMaxHp = 100,
    int OpponentMaxHp = 100,
    int FinalPlayerHp = 100,
    int FinalOpponentHp = 0,
    IReadOnlyList<DuelRoundLog>? RoundLogs = null,
    string OpponentAvatarChar = "武",
    string OpponentRole = "Võ tướng",
    int OpponentWar = 60);

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
/// State of a citadel military armory, connecting iron value chain to garrison defense strength.
/// </summary>
public sealed record CitadelArmoryDef(
    long CitadelId,
    int WeaponsStock,
    int WeaponsCapacity,
    int MonthlyIronIn,
    int MonthlyArmsOut,
    string StatusVi,
    int UpdatedDayIndex);

/// <summary>
/// Outcome of a monthly armory forge production and garrison procurement simulation.
/// </summary>
public sealed record ArmorySimulationResult(
    long CitadelId,
    int InitialWeapons,
    int IronConsumed,
    int ArmsForged,
    int GarrisonDemand,
    int ArmsSupplied,
    int WeaponsDeficit,
    int FinalWeapons,
    int OldDefenseStrength,
    int NewDefenseStrength,
    string StatusVi);

/// <summary>
/// Military, martial arena, and expedition surface owned by <c>dlc.military</c>.
/// </summary>
public interface IMilitaryCommand
{
    IReadOnlyList<StrongholdDef> StrongholdsAt(long citadelId);

    IReadOnlyList<StrongholdDef> AllStrongholds();

    IReadOnlyList<SupplyRequisitionDef> ActiveRequisitions(long citadelId);

    /// <summary>Calculates the route and peace security index (0 to 100%) for a citadel.</summary>
    int GetCitadelSecurity(long citadelId);

    /// <summary>Queries the military armory state of a citadel.</summary>
    CitadelArmoryDef GetCitadelArmory(long citadelId);

    /// <summary>Simulates monthly city forge production and garrison weapon procurement.</summary>
    ArmorySimulationResult SimulateMonthlyArmoryAndProcurement(long citadelId, GameDate date);

    /// <summary>Challenges an NPC warrior to a duel in the local arena with default branch.</summary>
    DuelResult ChallengeArena(long npcId);

    /// <summary>Challenges an NPC warrior to a duel in the local arena with chosen troop branch.</summary>
    DuelResult ChallengeArena(long npcId, TroopBranch playerBranch);

    /// <summary>Resolves the martial troop branch preferred by an NPC.</summary>
    TroopBranch ResolveNpcBranch(long npcId);

    /// <summary>Leads an expedition against an outlaw stronghold.</summary>
    string AssaultStronghold(long strongholdId);

    /// <summary>Resolves the outlaw chief dossier commanding a stronghold.</summary>
    BanditChiefDef GetStrongholdChief(long strongholdId);

    /// <summary>Simulates a visual multi-round expedition clash against a stronghold without committing side effects.</summary>
    BanditExpeditionResult SimulateBanditExpedition(long strongholdId, TroopBranch playerBranch);

    /// <summary>Executes a full expedition against an outlaw stronghold, committing rewards, loot, fatigue, and logs.</summary>
    BanditExpeditionResult ExecuteBanditExpedition(long strongholdId, TroopBranch playerBranch);

    /// <summary>Fulfills a military garrison requisition from local bag stock.</summary>
    string FulfillRequisition(long requisitionId);
}
