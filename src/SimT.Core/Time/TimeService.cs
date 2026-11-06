using SimT.Contracts.Time;

namespace SimT.Core.Time;

/// <summary>
/// Sovereign implementation of <see cref="ITimeService"/>.
/// Tracks high-resolution frame delta times, fixed simulation steps, and time scale.
/// </summary>
public sealed class TimeService : ITimeService
{
    private double _deltaTime;
    private double _unscaledDeltaTime;
    private double _fixedDeltaTime = 0.02; // Default 50Hz (20ms)
    private double _timeScale = 1.0;
    private double _totalTime;
    private long _frameCount;

    public double DeltaTime => _deltaTime;

    public double UnscaledDeltaTime => _unscaledDeltaTime;

    public double FixedDeltaTime
    {
        get => _fixedDeltaTime;
        set => _fixedDeltaTime = value > 0 ? value : 0.02;
    }

    public double TimeScale
    {
        get => _timeScale;
        set => _timeScale = value >= 0 ? value : 0.0;
    }

    public double TotalTime => _totalTime;

    public long FrameCount => _frameCount;

    public void Update(double rawDeltaSeconds)
    {
        if (rawDeltaSeconds < 0) rawDeltaSeconds = 0;

        _unscaledDeltaTime = rawDeltaSeconds;
        _deltaTime = rawDeltaSeconds * _timeScale;
        _totalTime += _deltaTime;
        _frameCount++;
    }
}
