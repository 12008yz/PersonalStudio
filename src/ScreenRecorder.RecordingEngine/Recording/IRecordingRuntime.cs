namespace ScreenRecorder.RecordingEngine.Recording;

public interface IRecordingRuntime : IDisposable
{
    RecordingLifecycleState State { get; }

    /// <summary>Ось медиа-времени активной сессии; <c>null</c> в <see cref="RecordingLifecycleState.Idle"/>.</summary>
    RecordingSessionTimebase? SessionTimebase { get; }

    Task StartAsync(RecordingSessionOptions options, CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
