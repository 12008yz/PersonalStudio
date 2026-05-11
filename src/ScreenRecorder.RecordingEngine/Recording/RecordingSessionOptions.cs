namespace ScreenRecorder.RecordingEngine.Recording;

public sealed record RecordingSessionOptions(
    nint MonitorHandle,
    string? PreferredMicrophoneEndpointId,
    string? PreferredLoopbackRenderEndpointId);
