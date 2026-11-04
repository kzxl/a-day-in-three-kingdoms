using SimT.Contracts.Simulation;
using SimT.Contracts.Time;
using SimT.Core.Kernel;

namespace SimT.Core.Simulation;

/// <summary>
/// Executes multi-day batch simulation safely with chunked transactions and progress reporting.
/// </summary>
public sealed class SimulationRunner : ISimulationRunner
{
    private readonly SimKernel _kernel;

    public SimulationRunner(SimKernel kernel)
    {
        _kernel = kernel;
    }

    public async Task<GameDate> AdvanceAsync(
        int totalDays,
        int chunkSize = 1,
        IProgress<SimProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (totalDays <= 0)
        {
            return _kernel.Today;
        }

        var safeChunk = Math.Max(1, chunkSize);
        var completed = 0;

        while (completed < totalDays)
        {
            if (cancellationToken.CanBeCanceled && cancellationToken.WaitHandle.WaitOne(0))
            {
                throw new OperationCanceledException();
            }
            var step = Math.Min(safeChunk, totalDays - completed);

            var today = _kernel.AdvanceDays(step);
            completed += step;

            progress?.Report(new SimProgress(completed, totalDays, today));

            // Yield control back to caller / UI loop between transaction chunks
            await Task.Yield();
        }

        _kernel.Save();
        return _kernel.Today;
    }
}
