namespace ScreenRecorder.RecordingEngine.Devices;

public interface IDeviceTopologyMonitor : IDisposable
{
    event EventHandler<DeviceTopologyChangedEventArgs>? TopologyChanged;

    DeviceTopologySnapshot? CurrentSnapshot { get; }

    bool IsRunning { get; }

    void Start();

    Task StopAsync(CancellationToken cancellationToken = default);

    Task<DeviceTopologyChangeKind> CheckForChangesOnceAsync(CancellationToken cancellationToken = default);
}
