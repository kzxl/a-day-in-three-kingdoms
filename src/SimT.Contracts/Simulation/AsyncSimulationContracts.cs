using SimT.Contracts.Time;

namespace SimT.Contracts.Simulation;

/// <summary>
/// Simulation clock progression speed tier.
/// </summary>
public enum SimulationSpeed
{
    Paused = 0,
    Normal = 1,     // ~600ms per day
    Fast = 2,       // ~200ms per day
    Ultra = 5       // ~40ms per day
}

/// <summary>
/// Summary report of an asynchronous simulation progression batch.
/// </summary>
public sealed record SimulationTickResult(
    GameDate FinalDate,
    int DaysAdvanced,
    bool PausedByCriticalCondition,
    string? PauseReason = null);

/// <summary>
/// Asynchronous simulation execution pipeline allowing background time advancement
/// with non-blocking UI, progress notifications, and emergency pause triggers.
/// </summary>
public interface IAsyncSimulationWorker
{
    SimulationSpeed Speed { get; set; }
    bool IsRunning { get; }

    Task<SimulationTickResult> AdvanceDaysAsync(
        int totalDays,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);

    void SignalPause(string? reason = null);

    void RegisterCriticalPauseCondition(Func<GameDate, (bool ShouldPause, string Reason)> condition);
}
