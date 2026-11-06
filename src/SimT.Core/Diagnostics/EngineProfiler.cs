using System.Collections.Concurrent;
using SimT.Contracts.Diagnostics;
using SimT.Contracts.Primitives;

namespace SimT.Core.Diagnostics;

/// <summary>
/// Sovereign high-resolution diagnostics profiler.
/// Measures operation latencies with low allocation overhead using high-frequency timestamps and ValueStopwatch.
/// </summary>
public sealed class EngineProfiler : IEngineProfiler
{
    private sealed class OperationTracker
    {
        public readonly string Category;
        public readonly string Operation;
        private long _callCount;
        private long _totalStopwatchTicks;
        private long _maxStopwatchTicks;

        public OperationTracker(string category, string operation)
        {
            Category = category;
            Operation = operation;
        }

        public void Record(long elapsedTicks)
        {
            Interlocked.Increment(ref _callCount);
            Interlocked.Add(ref _totalStopwatchTicks, elapsedTicks);

            long currentMax = Volatile.Read(ref _maxStopwatchTicks);
            while (elapsedTicks > currentMax)
            {
                long prev = Interlocked.CompareExchange(ref _maxStopwatchTicks, elapsedTicks, currentMax);
                if (prev == currentMax)
                {
                    break;
                }
                currentMax = Volatile.Read(ref _maxStopwatchTicks);
            }
        }

        public ProfileMetricSnapshot ToSnapshot()
        {
            long count = Volatile.Read(ref _callCount);
            long totalTicks = Volatile.Read(ref _totalStopwatchTicks);
            long maxTicks = Volatile.Read(ref _maxStopwatchTicks);

            double totalMs = TimeSpan.FromTicks(totalTicks).TotalMilliseconds;
            double maxMs = TimeSpan.FromTicks(maxTicks).TotalMilliseconds;
            double avgMs = count > 0 ? totalMs / count : 0.0;

            return new ProfileMetricSnapshot(Category, Operation, count, totalMs, maxMs, avgMs);
        }
    }

    private readonly ConcurrentDictionary<string, OperationTracker> _trackers = new();

    public IDisposable Scope(string category, string operation)
    {
        var key = $"{category}::{operation}";
        var tracker = _trackers.GetOrAdd(key, _ => new OperationTracker(category, operation));
        return new ProfilerScope(tracker);
    }

    public IReadOnlyList<ProfileMetricSnapshot> GetSnapshots()
    {
        var list = new List<ProfileMetricSnapshot>(_trackers.Count);
        foreach (var tracker in _trackers.Values)
        {
            list.Add(tracker.ToSnapshot());
        }
        return list;
    }

    public ProfileMetricSnapshot? GetSnapshot(string category, string operation)
    {
        var key = $"{category}::{operation}";
        return _trackers.TryGetValue(key, out var tracker) ? tracker.ToSnapshot() : null;
    }

    public void Reset()
    {
        _trackers.Clear();
    }

    private sealed class ProfilerScope : IDisposable
    {
        private readonly OperationTracker _tracker;
        private readonly ValueStopwatch _stopwatch;
        private bool _disposed;

        public ProfilerScope(OperationTracker tracker)
        {
            _tracker = tracker;
            _stopwatch = ValueStopwatch.StartNew();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _tracker.Record(_stopwatch.GetElapsedTime().Ticks);
            }
        }
    }
}
