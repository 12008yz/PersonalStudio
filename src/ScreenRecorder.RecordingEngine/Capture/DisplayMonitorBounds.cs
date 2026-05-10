namespace ScreenRecorder.RecordingEngine.Capture;

/// <summary>Logical bounds from <c>GetMonitorInfo</c> (virtual screen coordinates).</summary>
public readonly record struct DisplayMonitorBounds(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;

    public int Height => Bottom - Top;
}
