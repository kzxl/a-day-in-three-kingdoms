namespace SimT.Contracts.Diagnostics;

/// <summary>
/// Sovereign high-resolution frame telemetry tracker.
/// Measures instantaneous and rolling FPS, frame time, draw calls,
/// visible entities, and GC memory allocation deltas per frame.
/// </summary>
public sealed class FrameDiagnostics
{
    private long _lastThreadAllocatedBytes;
    private double _accumulatedTime;
    private int _accumulatedFrames;

    public float CurrentFps { get; private set; }
    public float FrameTimeMs { get; private set; }
    public long BytesAllocatedThisFrame { get; private set; }
    public int DrawCalls { get; set; }
    public int VisibleEntities { get; set; }
    public int ActiveGameObjects { get; set; }

    public void BeginFrame()
    {
        _lastThreadAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
        DrawCalls = 0;
    }

    public void EndFrame(double rawDeltaSeconds)
    {
        var currentAllocated = GC.GetAllocatedBytesForCurrentThread();
        BytesAllocatedThisFrame = Math.Max(0, currentAllocated - _lastThreadAllocatedBytes);

        _accumulatedFrames++;
        _accumulatedTime += rawDeltaSeconds;

        // Refresh FPS display every 250ms to maintain readability
        if (_accumulatedTime >= 0.25)
        {
            CurrentFps = (float)(_accumulatedFrames / _accumulatedTime);
            FrameTimeMs = (float)(_accumulatedTime / _accumulatedFrames * 1000.0);
            _accumulatedFrames = 0;
            _accumulatedTime = 0.0;
        }
    }
}
