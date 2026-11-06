namespace SimT.Contracts.Diagnostics;

/// <summary>
/// Telemetry metric snapshot for an instrumented engine operation.
/// </summary>
public sealed record ProfileMetricSnapshot(
    string Category,
    string Operation,
    long CallCount,
    double TotalMilliseconds,
    double MaxMilliseconds,
    double AvgMilliseconds);

/// <summary>
/// Low-overhead high-precision diagnostic profiler measuring tick durations,
/// query bottlenecks, and module execution latencies.
/// </summary>
public interface IEngineProfiler
{
    IDisposable Scope(string category, string operation);
    IReadOnlyList<ProfileMetricSnapshot> GetSnapshots();
    ProfileMetricSnapshot? GetSnapshot(string category, string operation);
    void Reset();
}
