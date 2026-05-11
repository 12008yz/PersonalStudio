namespace ScreenRecorder.RecordingEngine.Capture;

/// <summary>Lightweight capture health counters (фаза B — без кодирования).</summary>
public readonly struct FrameCaptureMetrics
{
    public FrameCaptureMetrics(
        long framesReceived,
        long emptyFrames,
        TimeSpan elapsed,
        long lastFrameQpcTicks,
        TimeSpan lastFrameSystemRelativeTime,
        double averageFrameHandlerLatencyMs = double.NaN,
        double lastFrameHandlerLatencyMs = double.NaN,
        long poolRecreateFailureCount = 0)
    {
        FramesReceived = framesReceived;
        EmptyFrames = emptyFrames;
        Elapsed = elapsed;
        LastFrameQpcTicks = lastFrameQpcTicks;
        LastFrameSystemRelativeTime = lastFrameSystemRelativeTime;
        AverageFps = elapsed.TotalSeconds > 0.01 ? framesReceived / elapsed.TotalSeconds : 0;
        AverageFrameHandlerLatencyMilliseconds = averageFrameHandlerLatencyMs;
        LastFrameHandlerLatencyMilliseconds = lastFrameHandlerLatencyMs;
        PoolRecreateFailureCount = poolRecreateFailureCount;
    }

    public long FramesReceived { get; }

    public long EmptyFrames { get; }

    public TimeSpan Elapsed { get; }

    /// <summary>Approximate mean FPS since capture started (frames / wall time).</summary>
    public double AverageFps { get; }

    public long LastFrameQpcTicks { get; }

    public TimeSpan LastFrameSystemRelativeTime { get; }

    /// <summary>
    /// Средняя задержка: «стенные» часы QPC после базовой отметки захвата минус <see cref="Windows.Graphics.DirectX.Direct3D11.Direct3D11CaptureFrame.SystemRelativeTime"/>,
    /// в миллисекундах (см. сеанс захвата); кадр на котором выполнялся <c>Recreate</c> пула в среднее не включается. <see cref="double.NaN"/>, если выборок не было.
    /// </summary>
    public double AverageFrameHandlerLatencyMilliseconds { get; }

    /// <summary>Задержка последнего кадра, вошедшего в выборку латентности, мс; <see cref="double.NaN"/>, если таких кадров не было.</summary>
    public double LastFrameHandlerLatencyMilliseconds { get; }

    /// <summary>
    /// Сколько раз не удалось <c>Direct3D11CaptureFramePool.Recreate</c> при смене <c>ContentSize</c> (разрешение, масштаб DPI)
    /// с момента последнего успешного <c>Start</c> сеанса.
    /// </summary>
    public long PoolRecreateFailureCount { get; }
}
