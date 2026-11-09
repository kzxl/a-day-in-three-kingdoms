namespace SimT.Contracts.Playing;

/// <summary>
/// Active frontline conflict between rival factions contesting a strategic citadel.
/// Part of the ambient macro geopolitics simulation driving market shocks, trade hazards, and sovereign loans.
/// </summary>
public sealed record FrontlineConflictView(
    long Id,
    long AttackerFactionId,
    string AttackerFactionNameVi,
    long DefenderFactionId,
    string DefenderFactionNameVi,
    long TargetCitadelId,
    string TargetCitadelNameVi,
    int Intensity,          // 1: Skirmish, 2: Clashing, 3: Full Siege
    string WarStage,        // "SKIRMISH", "SIEGE", "BREACH", "PACIFIED"
    long StartedDayIndex,
    int SiegeProgress,      // 0..100
    int SupplyAttrition,    // 0..100
    string Status);         // "ACTIVE", "RESOLVED_VICTORY", "RESOLVED_REPULSED", "TRUCE"

/// <summary>
/// Status summary for whether a citadel is on the active frontline, besieged, or neutral.
/// </summary>
public sealed record CitadelWarStatus(
    long CitadelId,
    bool IsFrontline,
    bool IsBesieged,
    int Intensity,
    long? ActiveConflictId);

/// <summary>
/// Cross-module capability for living war and macro geopolitics.
/// Provided by <c>dlc.power</c> as <c>power.geopolitical-war</c>.
/// </summary>
public interface IGeopoliticalWar
{
    /// <summary>Returns all currently active frontline conflicts across the realm.</summary>
    IReadOnlyList<FrontlineConflictView> ActiveConflicts();

    /// <summary>Returns active conflict details for a given citadel if currently contested.</summary>
    FrontlineConflictView? GetConflictForCitadel(long citadelId);

    /// <summary>Returns war state (frontline, siege, intensity) for a given citadel.</summary>
    CitadelWarStatus GetCitadelWarStatus(long citadelId);

    /// <summary>True if the citadel is currently besieged (intensity >= 2, stage SIEGE or BREACH).</summary>
    bool IsCitadelUnderSiege(long citadelId);

    /// <summary>True if trade route connecting two citadels is blockaded or hazardous due to active warfare.</summary>
    bool IsRouteBlockaded(long originCitadelId, long destinationCitadelId);

    /// <summary>Programmatically initiates or escalates a conflict against a target citadel.</summary>
    void TriggerConflict(long attackerFactionId, long defenderFactionId, long targetCitadelId, int initialIntensity = 1);

    /// <summary>Simulates monthly pulse of ongoing conflicts, sieges, and territorial handovers.</summary>
    void AdvanceConflictMonth(long dayIndex, ulong seed);
}
