using SimT.Contracts.Time;

namespace SimT.Contracts.Playing;

/// <summary>
/// Player choice definition attached to a historical chronicle event.
/// </summary>
public sealed record HistoricalEventChoiceDef(
    string Key,
    string LabelVi,
    string HintVi,
    long CostCopper = 0,
    long RewardCopper = 0,
    int FameDelta = 0,
    int VirtueDelta = 0);

/// <summary>
/// Geopolitical, economic, or demographic impact of a historical event.
/// </summary>
public sealed record HistoricalEventImpactDef(
    string Kind, // "price_shock", "route_danger", "route_block", "city_owner", "npc_move", "npc_allegiance", "npc_death"
    long CitadelId,
    string NoteVi,
    long? FactionId = null,
    string? NpcCode = null,
    long? TargetCitadelId = null,
    string? ShockType = null,
    int DurationDays = 0,
    int SeverityPermille = 1000);

/// <summary>
/// Snapshot of a scheduled or occurred Tam Quốc historical event (184–220).
/// </summary>
public sealed record HistoricalEventView(
    string Code,
    GameDate ScheduledDate,
    string TitleVi,
    string NarrativeVi,
    long PrimaryCitadelId,
    IReadOnlyList<long> RelatedCitadelIds,
    IReadOnlyList<string> RelatedNpcCodes,
    string BackgroundKey,
    IReadOnlyList<HistoricalEventChoiceDef> Choices,
    IReadOnlyList<HistoricalEventImpactDef> Impacts,
    string Status, // "pending", "occurred", "viewed", "diverged"
    GameDate? OccurredDate,
    string? ChoiceCode,
    string EventKey,
    IReadOnlyList<EventPreconditionDef>? Preconditions = null,
    bool IsDiverged = false,
    string? DivergenceCode = null,
    string? DivergenceTitleVi = null,
    string? DivergenceNarrativeVi = null)
{
    public SandboxEventTier Tier => SandboxEventTier.Historical;
    public IReadOnlyList<EventPreconditionDef> Preconditions { get; init; } = Preconditions ?? Array.Empty<EventPreconditionDef>();
    public bool IsDiverged { get; init; } = IsDiverged;
    public string? DivergenceCode { get; init; } = DivergenceCode;
    public string? DivergenceTitleVi { get; init; } = DivergenceTitleVi;
    public string? DivergenceNarrativeVi { get; init; } = DivergenceNarrativeVi;

    public IReadOnlyDictionary<string, IReadOnlyList<string>> PortraitExpressions { get; init; }
        = new Dictionary<string, IReadOnlyList<string>>();
}

/// <summary>
/// Outcome of resolving a player choice on a historical event scene.
/// </summary>
public sealed record HistoricalChoiceOutcome(
    bool Ok,
    string MessageVi,
    long CopperDelta,
    int FameDelta,
    int VirtueDelta);

/// <summary>
/// Cross-module contract implemented by <c>MarketShockEngine</c> (<see cref="CapabilityIds.MarketRumorEvents"/>)
/// so historical events can trigger market price shocks when <c>dlc.economy</c> is enabled.
/// </summary>
public interface IMarketShockTrigger
{
    void TriggerShock(long citadelId, string shockType, long startDay, int durationDays, int severityPermille, string noteVi);
}
