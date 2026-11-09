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
    GameDate UpdatedDate,
    bool IsDeceased = false,
    long AllegianceFactionId = 0);

/// <summary>
/// Dynamic psycho-social and survival drives directing an autonomous NPC's long-term behavior.
/// </summary>
public sealed record NpcNeedsDef(
    long NpcId,
    int SafetyNeed,
    int WealthNeed,
    int FameNeed,
    int PowerNeed,
    int SocialNeed,
    GameDate UpdatedDate);

/// <summary>
/// Long-term multi-step goal actively pursued by an autonomous NPC.
/// </summary>
public sealed record NpcGoalDef(
    long NpcId,
    string GoalType, // "TradeExpedition", "PacifyBandits", "MilitaryDrill", "SeekPatron", "CityGovernance", "ScholarTour", "FleeUnrest", "WandererLife"
    string Stage,    // "Planning", "Preparing", "Executing", "Completing"
    int CurrentStep,
    int TotalSteps,
    long? TargetCitadelId,
    long? TargetNpcId,
    long? TargetItemId,
    long CapitalAllocated,
    int Progress,
    string PlanDesc,
    long StartedDayIndex,
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
    void RecordGovernanceEvent(GameDate date, long npcId, long cityId, string headlineVi) { }
    /// <summary>Returns an NPC to its registry hometown using the living world's location state.</summary>
    void ReturnNpcHome(long npcId, GameDate date) { }

    void RecordRelationshipEvent(GameDate date, long npcId, string headlineVi) { }

    void RecordMilestoneEvent(GameDate date, string eventKind, string headlineVi, string detailVi, string statImpact) { }

    /// <summary>Chronicle feed of recent events across the realm or at a specific citadel.</summary>
    IReadOnlyList<SocietyLogEntry> RecentEvents(int limit = 50, string? category = null, long? citadelId = null);

    /// <summary>Records an external event or heroic feat into the living world chronicle.</summary>
    void RecordEvent(GameDate date, long npcId, string npcNameVi, long citadelId, string headlineVi, string detailVi, string category = "travel", string statImpact = "");

    /// <summary>Gets the current autonomous activity and location of an NPC.</summary>
    NpcActivityDef? GetNpcActivity(long npcId);

    /// <summary>Gets all active NPC profiles.</summary>
    IReadOnlyList<NpcActivityDef> AllActivities();

    /// <summary>Gets the accumulated stat growth for an NPC.</summary>
    NpcStatDelta GetStatDeltas(long npcId);

    /// <summary>Gets the current psychological and survival needs of an NPC.</summary>
    NpcNeedsDef? GetNpcNeeds(long npcId) => null;

    /// <summary>Gets the active multi-step goal pursued by an NPC.</summary>
    NpcGoalDef? GetNpcGoal(long npcId) => null;

    /// <summary>Manually steps the autonomous simulation for a new month.</summary>
    void SimulateTurn(GameDate date);

    /// <summary>Replenishes or spawns procedural NPCs into underpopulated or thriving citadels.</summary>
    IReadOnlyList<NpcDef> ReplenishNpcs(GameDate date, int count = 1);

    /// <summary>Direct access to AI merchant rivalry engine.</summary>
    IMerchantRivalry Rivals { get; }

    /// <summary>Direct access to branching karma & causality event engine.</summary>
    IKarmaEngine Karma { get; }

    /// <summary>Lists all canonical historical events (184–220) and their current states.</summary>
    IReadOnlyList<HistoricalEventView> GetHistoricalEvents(bool occurredOnly = false) => Array.Empty<HistoricalEventView>();

    /// <summary>Finds a historical event by code.</summary>
    HistoricalEventView? FindHistoricalEvent(string code) => null;

    /// <summary>Dequeues the next historical event that occurred at the player's current city.</summary>
    HistoricalEventView? DequeuePendingHistoricalEvent(long playerCitadelId) => null;

    /// <summary>Dequeues the next status-bar notice for a historical event that occurred at a distant city.</summary>
    string? ConsumeLastHistoricalStatus() => null;

    /// <summary>Resolves a player choice on a historical event.</summary>
    HistoricalChoiceOutcome ResolveHistoricalEventChoice(string eventCode, string choiceKey) => new(false, string.Empty, 0, 0, 0);

    /// <summary>Lists dynamic world events.</summary>
    IReadOnlyList<DynamicWorldEventDef> GetDynamicEvents(bool activeOnly = true) => Array.Empty<DynamicWorldEventDef>();

    /// <summary>Lists personal NPC events.</summary>
    IReadOnlyList<PersonalNpcEventDef> GetPersonalEvents(int limit = 50) => Array.Empty<PersonalNpcEventDef>();

    /// <summary>Lists all 3 tiers of sandbox events in unified timeline order.</summary>
    IReadOnlyList<UnifiedSandboxEventView> GetUnifiedEvents(SandboxEventTier? tierFilter = null, int limit = 50) => Array.Empty<UnifiedSandboxEventView>();

    /// <summary>Evaluates world state thresholds and triggers dynamic crises/boons.</summary>
    DynamicWorldEventDef? EvaluateAndTriggerDynamicEvents(GameDate date) => null;

    /// <summary>Evaluates NPC relationships and triggers personal bonding/rivalry events.</summary>
    PersonalNpcEventDef? EvaluateAndTriggerPersonalEvents(GameDate date) => null;
}

/// <summary>
/// Canonical location and city-roster helper shared across scenes, dialogue, and social services.
/// </summary>
public static class NpcLocation
{
    public static bool IsDeceased(this NpcDef npc, ISocietyChronicle? society = null) =>
        npc.IsDeceasedFlag
        || (npc.BioVi?.Contains("Đã mất", StringComparison.OrdinalIgnoreCase) ?? false)
        || (society?.GetNpcActivity(npc.Id)?.IsDeceased ?? false)
        || (society?.GetNpcActivity(npc.Id)?.CurrentAction?.StartsWith("Đã mất", StringComparison.OrdinalIgnoreCase) ?? false);

    public static long CurrentCitadelId(this NpcDef npc, ISocietyChronicle? society) =>
        society?.GetNpcActivity(npc.Id)?.CurrentCitadelId ?? npc.CitadelId;

    public static long CurrentCitadelId(this NpcDef npc, SimT.Contracts.Modules.ICapabilityBroker? capabilities) =>
        capabilities is not null && capabilities.TryGet<ISocietyChronicle>(CapabilityIds.SocietyChronicle, out var society)
            ? npc.CurrentCitadelId(society)
            : npc.CitadelId;

    public static IReadOnlyList<SimT.Contracts.Scenes.CityNpcSprite> BuildCityNpcs(
        INpcRegistry? reg, ISocietyChronicle? society, long cityId, int maxCount = 6)
    {
        if (reg is null || maxCount <= 0)
            return Array.Empty<SimT.Contracts.Scenes.CityNpcSprite>();

        var list = reg.All
            .Where(npc => !npc.IsDeceased(society) && npc.CurrentCitadelId(society) == cityId)
            .OrderBy(npc =>
            {
                var (animPath, _) = SimT.Contracts.Scenes.SceneArtCatalog.ResolveNpcAnim(npc);
                var isGeneralOrAnimated = !string.IsNullOrEmpty(animPath) ||
                    string.Equals(SimT.Contracts.Scenes.SceneArtCatalog.ClassifyArchetype(npc), "general", StringComparison.OrdinalIgnoreCase);
                return isGeneralOrAnimated ? 0 : 1;
            })
            .ThenBy(npc => npc.Id)
            // Most NPCs share an archetype sprite; show each look once so the street has no clones.
            .DistinctBy(npc => SimT.Contracts.Scenes.SceneArtCatalog.ResolveNpcAnim(npc).AnimPath
                ?? SimT.Contracts.Scenes.SceneArtCatalog.ResolveNpcArt(npc).SpritePath)
            .Take(maxCount)
            .ToList();

        double[] xs = [35, 65, 50, 17, 85, 8];
        double[] ys = [87, 88, 73, 75, 76, 86];
        var sprites = new List<SimT.Contracts.Scenes.CityNpcSprite>(list.Count);
        for (var i = 0; i < list.Count; i++)
        {
            var npc = list[i];
            var (_, facePath, spritePath) = SimT.Contracts.Scenes.SceneArtCatalog.ResolveNpcArt(npc);
            var (animPath, _) = SimT.Contracts.Scenes.SceneArtCatalog.ResolveNpcAnim(npc);
            var shortName = SimT.Contracts.Scenes.RoleTitleVi.ShortName(npc.NameVi);
            if (shortName.Length > 24)
                shortName = shortName[..23] + "…";
            sprites.Add(new SimT.Contracts.Scenes.CityNpcSprite(
                npc.Id, npc.Code, shortName, SimT.Contracts.Scenes.RoleTitleVi.Format(npc.Role),
                spritePath, facePath, xs[i % xs.Length], ys[i % ys.Length], animPath));
        }

        return sprites;
    }
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
    GameDate UpdatedDate,
    int CarriedCargoQty = 0)
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
