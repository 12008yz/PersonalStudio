namespace ScreenRecorder.RecordingEngine.Capture;

/// <summary>One physical monitor as seen by Win32 display enumeration.</summary>
public sealed record DisplayMonitorDescriptor(
    nint MonitorHandle,
    string DeviceName,
    bool IsPrimary,
    DisplayMonitorBounds Bounds);
