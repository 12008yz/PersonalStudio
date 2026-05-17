using Windows.Graphics;

namespace ScreenRecorder.RecordingEngine.Capture;

/// <summary>Кадр WGC с меткой на оси медиа-времени сессии (без readback пикселей).</summary>
public sealed class CapturedVideoFrameEventArgs : EventArgs
{
    public CapturedVideoFrameEventArgs(
        SizeInt32 contentSize,
        long sessionMediaTimestampHns,
        long captureQpcTicks,
        TimeSpan handlerLatency)
    {
        ContentSize = contentSize;
        SessionMediaTimestampHns = sessionMediaTimestampHns;
        CaptureQpcTicks = captureQpcTicks;
        HandlerLatency = handlerLatency;
    }

    public SizeInt32 ContentSize { get; }

    /// <summary>Медиа-время относительно <see cref="Recording.RecordingSessionTimebase.Establish"/> (100-ns).</summary>
    public long SessionMediaTimestampHns { get; }

    public long CaptureQpcTicks { get; }

    public TimeSpan HandlerLatency { get; }
}
