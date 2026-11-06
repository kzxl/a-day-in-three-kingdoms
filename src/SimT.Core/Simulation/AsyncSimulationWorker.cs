using SimT.Contracts.Logging;
using SimT.Contracts.Simulation;
using SimT.Contracts.Time;

namespace SimT.Core.Simulation;

/// <summary>
/// Sovereign asynchronous simulation pipeline.
/// Advances game clock on background threads, throttles according to speed tiers,
/// reports real-time progress, and triggers emergency pauses on critical conditions.
/// </summary>
public sealed class AsyncSimulationWorker : IAsyncSimulationWorker
{
    private readonly IGameClock _clock;
    private readonly ILog _log;
    private readonly List<Func<GameDate, (bool ShouldPause, string Reason)>> _criticalConditions = new();
    private volatile bool _isRunning;
    private volatile bool _pauseSignaled;
    private string? _pauseReason;

    public SimulationSpeed Speed { get; set; } = SimulationSpeed.Normal;

    public bool IsRunning => _isRunning;

    public AsyncSimulationWorker(IGameClock clock, ILog log)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public void SignalPause(string? reason = null)
    {
        _pauseSignaled = true;
        _pauseReason = reason;
        Speed = SimulationSpeed.Paused;
    }

    public void RegisterCriticalPauseCondition(Func<GameDate, (bool ShouldPause, string Reason)> condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        _criticalConditions.Add(condition);
    }

    public async Task<SimulationTickResult> AdvanceDaysAsync(
        int totalDays,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (totalDays <= 0)
        {
            return new SimulationTickResult(_clock.Today, 0, false);
        }

        if (_isRunning)
        {
            throw new InvalidOperationException("Mô phỏng đang chạy trong một tác vụ khác.");
        }

        _isRunning = true;
        _pauseSignaled = false;
        _pauseReason = null;

        int daysCompleted = 0;
        bool pausedByCritical = false;
        string? criticalReason = null;

        try
        {
            for (int i = 0; i < totalDays; i++)
            {
                if ((cancellationToken.CanBeCanceled && cancellationToken.WaitHandle.WaitOne(0)) || _pauseSignaled)
                {
                    break;
                }

                // Advance 1 day synchronously through engine clock and event publishers
                var newDate = _clock.Advance(1);
                daysCompleted++;
                progress?.Report(daysCompleted);

                // Evaluate critical interrupt conditions (e.g. emergencies, major crises)
                foreach (var condition in _criticalConditions)
                {
                    var (shouldPause, reason) = condition(newDate);
                    if (shouldPause)
                    {
                        pausedByCritical = true;
                        criticalReason = reason;
                        _pauseSignaled = true;
                        _pauseReason = reason;
                        _log.Info($"[SimulationWorker] Tự động ngắt tua ngày: {reason}");
                        break;
                    }
                }

                if (pausedByCritical)
                {
                    break;
                }

                // Throttle pace according to selected SimulationSpeed
                var delayMs = GetDelayMilliseconds(Speed);
                if (delayMs > 0 && i < totalDays - 1)
                {
                    await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _isRunning = false;
        }

        return new SimulationTickResult(
            _clock.Today,
            daysCompleted,
            pausedByCritical,
            criticalReason ?? _pauseReason);
    }

    private static int GetDelayMilliseconds(SimulationSpeed speed) => speed switch
    {
        SimulationSpeed.Paused => 1000,
        SimulationSpeed.Normal => 150,
        SimulationSpeed.Fast => 40,
        SimulationSpeed.Ultra => 5,
        _ => 150
    };
}
