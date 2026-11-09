using SimT.Contracts.Time;

namespace SimT.Contracts.Playing;

/// <summary>
/// 3-Tier classification for sandbox narrative events.
/// Distinguishes fixed historical milestones from dynamic emergent world crises and interpersonal NPC events.
/// </summary>
public enum SandboxEventTier
{
    /// <summary>Canonical historical milestones (184–220) with alternate branching divergence.</summary>
    Historical = 0,

    /// <summary>Emergent events driven by simulation state thresholds (economy, food, order, epidemics, harvests).</summary>
    Dynamic = 1,

    /// <summary>Interpersonal events emerging from NPC relationship networks, affinity, loyalty, and ambition.</summary>
    Personal = 2
}

/// <summary>
/// Precondition evaluated before a historical event executes.
/// If any precondition is broken by player agency or emergent world state,
/// history diverges into alternate sandbox reality.
/// </summary>
public sealed record EventPreconditionDef(
    string Type, // "npc_alive", "npc_dead", "citadel_owner", "faction_exists"
    string? NpcCode = null,
    long? CitadelId = null,
    long? FactionId = null,
    string? ReasonVi = null);

/// <summary>
/// Dynamic world event triggered procedurally by state thresholds (famine, epidemic, bandit uprising, bumper harvest).
/// </summary>
public sealed record DynamicWorldEventDef(
    string Code,
    string Category, // "famine", "order", "epidemic", "market_crisis", "harvest"
    string TitleVi,
    string NarrativeVi,
    long CitadelId,
    GameDate OccurredDate,
    int DurationDays = 30,
    IReadOnlyList<HistoricalEventImpactDef>? Impacts = null,
    string Status = "active")
{
    public SandboxEventTier Tier => SandboxEventTier.Dynamic;
}

/// <summary>
/// Personal NPC event emerging from interpersonal relationships and social network state.
/// </summary>
public sealed record PersonalNpcEventDef(
    string Code,
    string Kind, // "defection", "vendetta", "sworn_brother", "courtship", "talent_emergence"
    long NpcId,
    long? TargetNpcId,
    long CitadelId,
    GameDate OccurredDate,
    string TitleVi,
    string NarrativeVi,
    string? ImpactNoteVi = null)
{
    public SandboxEventTier Tier => SandboxEventTier.Personal;
}

/// <summary>
/// Unified view of any event across all 3 tiers for chronicle feeds, status displays, and UI rendering.
/// </summary>
public sealed record UnifiedSandboxEventView(
    string Code,
    SandboxEventTier Tier,
    GameDate Date,
    string TitleVi,
    string NarrativeVi,
    long PrimaryCitadelId,
    string Status, // "occurred", "diverged", "active", "resolved"
    bool IsDiverged = false,
    string? DivergenceReasonVi = null);
