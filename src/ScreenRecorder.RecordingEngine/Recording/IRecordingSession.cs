namespace ScreenRecorder.RecordingEngine.Recording;

public interface IRecordingSession
{
    RecordingLifecycleState State { get; }

    RecordingSessionOptions? CurrentOptions { get; }

    Task StartAsync(RecordingSessionOptions options, CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    Task ChangeAudioDevicesAsync(string? microphoneEndpointId, string? loopbackRenderEndpointId, CancellationToken cancellationToken = default);

    Task ChangeMonitorAsync(nint monitorHandle, CancellationToken cancellationToken = default);
}
