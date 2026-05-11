namespace ScreenRecorder.RecordingEngine.Devices;

[Flags]
public enum DeviceTopologyChangeKind
{
    None = 0,
    MonitorsChanged = 1 << 0,
    CaptureEndpointsChanged = 1 << 1,
    RenderEndpointsChanged = 1 << 2,
    DefaultCaptureEndpointChanged = 1 << 3,
    DefaultRenderEndpointChanged = 1 << 4,
}
