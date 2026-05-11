namespace ScreenRecorder.RecordingEngine.Recording;

public interface IRecordingRuntime : IDisposable
{
    RecordingLifecycleState State { get; }

    Task StartAsync(RecordingSessionOptions options, CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
