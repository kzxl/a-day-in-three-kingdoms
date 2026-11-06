namespace SimT.Contracts.Time;

/// <summary>
/// Central time orchestration service providing frame delta time,
/// fixed simulation tick steps, and time scaling.
/// </summary>
public interface ITimeService
{
    /// <summary>Elapsed time in seconds since the previous frame, scaled by <see cref="TimeScale"/>.</summary>
    double DeltaTime { get; }

    /// <summary>Raw unscaled elapsed time in seconds since the previous frame.</summary>
    double UnscaledDeltaTime { get; }

    /// <summary>Fixed delta time step in seconds for deterministic physics/simulation ticks (default 0.02s = 50Hz).</summary>
    double FixedDeltaTime { get; set; }

    /// <summary>Simulation speed multiplier (1.0 = normal, 2.0 = 2x speed, 0.0 = paused).</summary>
    double TimeScale { get; set; }

    /// <summary>Total scaled time in seconds elapsed since engine boot.</summary>
    double TotalTime { get; }

    /// <summary>Total number of frames processed.</summary>
    long FrameCount { get; }

    /// <summary>Updates frame timestamps with the given raw elapsed seconds.</summary>
    void Update(double rawDeltaSeconds);
}

/// <summary>
/// Handle to an active scheduled or periodic task, allowing early cancellation.
/// </summary>
public interface IScheduledTask : IDisposable
{
    bool IsCompleted { get; }
    bool IsCancelled { get; }
    void Cancel();
}

/// <summary>
/// General-purpose engine task scheduler for deferred and periodic execution.
/// </summary>
public interface IEngineScheduler
{
    IScheduledTask ScheduleAfter(double delaySeconds, Action callback);
    IScheduledTask SchedulePeriodic(double intervalSeconds, Action callback);
    void Update(double deltaTimeSeconds);
    void CancelAll();
}
