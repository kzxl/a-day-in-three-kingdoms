using SimT.Contracts.Time;

namespace SimT.Contracts.Playing;

/// <summary>
/// A single living-world event recorded in the realm's chronicle.
/// </summary>
public sealed record SocietyLogEntry(
    long Id,
    GameDate Date,
    long NpcId,
    string NpcNameVi,
    long CitadelId,
    string CitadelNameVi,
    string EventKind, // "martial", "commerce", "governance", "scholarly", "travel"
    string HeadlineVi,
    string DetailVi,
    string? StatImpact);

/// <summary>
/// Autonomous state and current career focus of an NPC in the living world.
/// </summary>
public sealed record NpcActivityDef(
    long NpcId,
    long CurrentCitadelId,
    long CurrentWealth,
    string FocusPath, // "martial", "merchant", "strategist", "governor", "wanderer"
    string CurrentAction,
    int Stamina,
    int Reputation,
    GameDate UpdatedDate);

/// <summary>
/// Accumulated attribute growth for an NPC earned through autonomous practice and feats.
/// </summary>
public sealed record NpcStatDelta(
    long NpcId,
    int WarDelta,
    int LdrDelta,
    int IntDelta,
    int PolDelta,
    int DipDelta,
    int ComDelta,
    int LogDelta,
    int CraDelta,
    GameDate UpdatedDate)
{
    public static NpcStatDelta Empty(long npcId, GameDate date) =>
        new(npcId, 0, 0, 0, 0, 0, 0, 0, 0, date);

    public int TotalBonus => WarDelta + LdrDelta + IntDelta + PolDelta + DipDelta + ComDelta + LogDelta + CraDelta;
}

/// <summary>
/// Living world chronicle and autonomous NPC simulation surface owned by <c>dlc.society</c>.
/// </summary>
public interface ISocietyChronicle
{
    /// <summary>Chronicle feed of recent events across the realm or at a specific citadel.</summary>
    IReadOnlyList<SocietyLogEntry> RecentEvents(int limit = 50, string? category = null, long? citadelId = null);

    /// <summary>Gets the current autonomous activity and location of an NPC.</summary>
    NpcActivityDef? GetNpcActivity(long npcId);

    /// <summary>Gets all active NPC profiles.</summary>
    IReadOnlyList<NpcActivityDef> AllActivities();

    /// <summary>Gets the accumulated stat growth for an NPC.</summary>
    NpcStatDelta GetStatDeltas(long npcId);

    /// <summary>Manually steps the autonomous simulation for a new month.</summary>
    void SimulateTurn(GameDate date);

    /// <summary>Replenishes or spawns procedural NPCs into underpopulated or thriving citadels.</summary>
    IReadOnlyList<NpcDef> ReplenishNpcs(GameDate date, int count = 1);

    /// <summary>Direct access to AI merchant rivalry engine.</summary>
    IMerchantRivalry Rivals { get; }

    /// <summary>Direct access to branching karma & causality event engine.</summary>
    IKarmaEngine Karma { get; }
}

/// <summary>
/// Autonomous AI Merchant Rival managing an independent trading enterprise.
/// </summary>
public sealed record MerchantRivalDef(
    long NpcId,
    string NpcNameVi,
    string EnterpriseName,
    int MerchantTier,
    string TierTitleVi,
    long CapitalQuan,
    int FleetCarts,
    long PrimaryGoodId,
    string PrimaryGoodNameVi,
    long HeadquartersCitadelId,
    string HeadquartersNameVi,
    long CurrentCitadelId,
    string CurrentCitadelNameVi,
    long TargetCitadelId,
    string TargetCitadelNameVi,
    int Reputation,
    int MarketInfluencePermille,
    long MonthlyProfit,
    long TotalTradeVolume,
    string LastActionDesc,
    GameDate UpdatedDate)
{
    public static string TierToTitle(int tier) => tier switch
    {
        >= 5 => "Cự Phú Địch Quốc",
        4 => "Đại Hào Thương",
        3 => "Phường Buôn Châu Huyện",
        2 => "Cửa Hiệu Thành Trấn",
        _ => "Tiểu Thương Hành"
    };
}

/// <summary>
/// Entry in the Realm's Merchant Leaderboard (Bảng Phú Hào Thiên Hạ).
/// </summary>
public sealed record MerchantLeaderboardEntry(
    int Rank,
    long EntityId,
    string Name,
    bool IsPlayer,
    string EnterpriseName,
    int Tier,
    string TierTitle,
    long EstimatedNetWorth,
    string BaseCitadel,
    string PrimarySpecialty,
    string Status);

/// <summary>
/// Engine managing autonomous AI commercial competitors, market pressure, and rivalry.
/// </summary>
public interface IMerchantRivalry
{
    IReadOnlyList<MerchantRivalDef> GetAllRivals();
    MerchantRivalDef? GetRival(long npcId);
    IReadOnlyList<MerchantLeaderboardEntry> GetLeaderboard();
    void SimulateMerchantTurn(GameDate date);
}

/// <summary>
/// Active karma seed incubating in the living world.
/// </summary>
public sealed record KarmaSeedDef(
    long Id,
    string ChainCode,
    string TitleVi,
    int CurrentStage,
    int MaxStage,
    string BranchChoice,
    int PlantedDay,
    int TriggerAfterDay,
    long CitadelId,
    string CitadelNameVi,
    long? TargetNpcId,
    string? TargetNpcNameVi,
    string KarmaAlignment, // "virtue", "ruthless", "neutral"
    bool IsActive,
    bool IsResolved,
    string LastOutcomeVi,
    GameDate CreatedDate);

/// <summary>
/// Historical record of a karmic turning point and player decision.
/// </summary>
public sealed record KarmaHistoryDef(
    long Id,
    long SeedId,
    string ChainCode,
    int Stage,
    string DecisionVi,
    string ConsequenceVi,
    string? StatDeltaSummary,
    int RecordedDay,
    GameDate RecordedDate);

/// <summary>
/// A narrative decision prompt presented when a karma event chain triggers.
/// </summary>
public sealed record KarmaPromptOption(
    string ChoiceKey,
    string LabelVi,
    string DescriptionVi,
    string KarmaAlignment); // "virtue", "ruthless", "neutral"

/// <summary>
/// An actionable karma event whose conditions and timing have matured.
/// </summary>
public sealed record KarmaPendingEvent(
    long SeedId,
    string ChainCode,
    string TitleVi,
    string NarrativeVi,
    int Stage,
    long CitadelId,
    string CitadelNameVi,
    IReadOnlyList<KarmaPromptOption> Options);

/// <summary>
/// Consequence outcome and economic/reputation impact of a karmic choice.
/// </summary>
public sealed record KarmaResolutionResult(
    bool Success,
    string HeadlineVi,
    string OutcomeVi,
    long MoneyChange,
    int FameChange,
    int VirtueChange,
    bool ChainCompleted);

/// <summary>
/// Engine managing delayed causality, incubation, branching event paths, and karma fruition.
/// </summary>
public interface IKarmaEngine
{
    IReadOnlyList<KarmaSeedDef> GetActiveSeeds();
    IReadOnlyList<KarmaSeedDef> GetAllSeeds();
    IReadOnlyList<KarmaHistoryDef> GetHistory(long? seedId = null);
    IReadOnlyList<KarmaPendingEvent> CheckPendingEvents(long currentCitadelId, int currentDay);
    KarmaResolutionResult ResolveChoice(long seedId, string choiceKey);
    long PlantSeed(string chainCode, string titleVi, string branchChoice, int triggerDelayDays, long citadelId, string karmaAlignment, long? targetNpcId = null);
}
