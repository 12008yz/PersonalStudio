namespace ScreenRecorder.RecordingEngine.Devices;

public sealed class DeviceTopologyChangedEventArgs : EventArgs
{
    public DeviceTopologyChangedEventArgs(
        DeviceTopologySnapshot previous,
        DeviceTopologySnapshot current,
        DeviceTopologyChangeKind changeKind)
    {
        Previous = previous ?? throw new ArgumentNullException(nameof(previous));
        Current = current ?? throw new ArgumentNullException(nameof(current));
        ChangeKind = changeKind;
    }

    public DeviceTopologySnapshot Previous { get; }

    public DeviceTopologySnapshot Current { get; }

    public DeviceTopologyChangeKind ChangeKind { get; }
}
