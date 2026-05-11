using ScreenRecorder.RecordingEngine.Audio;
using ScreenRecorder.RecordingEngine.Capture;

namespace ScreenRecorder.RecordingEngine.Devices;

public sealed record DeviceTopologySnapshot(
    IReadOnlyList<DisplayMonitorDescriptor> Monitors,
    IReadOnlyList<AudioEndpointDescriptor> CaptureEndpoints,
    IReadOnlyList<AudioEndpointDescriptor> RenderEndpoints)
{
    public string? DefaultCaptureEndpointId =>
        CaptureEndpoints.FirstOrDefault(e => e.IsSystemDefault)?.DeviceId;

    public string? DefaultRenderEndpointId =>
        RenderEndpoints.FirstOrDefault(e => e.IsSystemDefault)?.DeviceId;

    public static DeviceTopologySnapshot CaptureCurrent()
    {
        return new DeviceTopologySnapshot(
            DisplayMonitorEnumeration.EnumerateMonitors(),
            AudioDeviceEnumeration.EnumerateCaptureEndpoints(),
            AudioDeviceEnumeration.EnumerateRenderEndpoints());
    }
}
