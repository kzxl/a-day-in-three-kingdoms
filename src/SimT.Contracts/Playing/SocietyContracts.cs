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
}
