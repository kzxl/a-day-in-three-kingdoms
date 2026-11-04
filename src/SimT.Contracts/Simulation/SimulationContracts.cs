using SimT.Contracts.Time;

namespace SimT.Contracts.Simulation;

/// <summary>
/// Granular progress report emitted during batch simulation / fast-forward ticks.
/// </summary>
public sealed record SimProgress(
    int CurrentStep,
    int TotalSteps,
    GameDate CurrentDate,
    string? Message = null);

/// <summary>
/// Engine-level simulation loop runner for advancing multiple days/months safely
/// with chunked transactions, progress reporting, and cooperative cancellation.
/// </summary>
public interface ISimulationRunner
{
    Task<GameDate> AdvanceAsync(
        int totalDays,
        int chunkSize = 1,
        IProgress<SimProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
