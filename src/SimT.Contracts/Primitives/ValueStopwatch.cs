using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace SimT.Contracts.Primitives;

/// <summary>
/// A zero-allocation, lightweight value-type stopwatch for microsecond latency profiling in hot paths.
/// Eliminates heap allocation compared to System.Diagnostics.Stopwatch.
/// </summary>
public readonly struct ValueStopwatch
{
    private static readonly double TimestampToTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;
    private readonly long _startTimestamp;

    public bool IsActive
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _startTimestamp != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ValueStopwatch(long startTimestamp)
    {
        _startTimestamp = startTimestamp;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueStopwatch StartNew() => new(Stopwatch.GetTimestamp());

    public long RawStartTimestamp => _startTimestamp;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TimeSpan GetElapsedTime()
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("An uninitialized ValueStopwatch cannot be used to measure elapsed time.");
        }

        long end = Stopwatch.GetTimestamp();
        long delta = end - _startTimestamp;
        long ticks = (long)(TimestampToTicks * delta);
        return new TimeSpan(ticks);
    }

    public double ElapsedMilliseconds
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => GetElapsedTime().TotalMilliseconds;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan GetElapsedTime(long startingTimestamp)
    {
        long end = Stopwatch.GetTimestamp();
        long delta = end - startingTimestamp;
        long ticks = (long)(TimestampToTicks * delta);
        return new TimeSpan(ticks);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double GetElapsedMilliseconds(long startingTimestamp)
    {
        return GetElapsedTime(startingTimestamp).TotalMilliseconds;
    }
}
