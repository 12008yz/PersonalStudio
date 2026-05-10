namespace ScreenRecorder.RecordingEngine.Capture;

/// <summary>Lightweight capture health counters (фаза B — без кодирования).</summary>
public readonly struct FrameCaptureMetrics
{
    public FrameCaptureMetrics(
        long framesReceived,
        long emptyFrames,
        TimeSpan elapsed,
        long lastFrameQpcTicks,
        TimeSpan lastFrameSystemRelativeTime)
    {
        FramesReceived = framesReceived;
        EmptyFrames = emptyFrames;
        Elapsed = elapsed;
        LastFrameQpcTicks = lastFrameQpcTicks;
        LastFrameSystemRelativeTime = lastFrameSystemRelativeTime;
        AverageFps = elapsed.TotalSeconds > 0.01 ? framesReceived / elapsed.TotalSeconds : 0;
    }

    public long FramesReceived { get; }

    public long EmptyFrames { get; }

    public TimeSpan Elapsed { get; }

    /// <summary>Approximate mean FPS since capture started (frames / wall time).</summary>
    public double AverageFps { get; }

    public long LastFrameQpcTicks { get; }

    public TimeSpan LastFrameSystemRelativeTime { get; }
}
