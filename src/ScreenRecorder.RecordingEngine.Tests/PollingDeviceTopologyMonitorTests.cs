using ScreenRecorder.RecordingEngine.Audio;
using ScreenRecorder.RecordingEngine.Capture;
using ScreenRecorder.RecordingEngine.Devices;

namespace ScreenRecorder.RecordingEngine.Tests;

[TestClass]
public sealed class PollingDeviceTopologyMonitorTests
{
    [TestMethod]
    public void CalculateChangeKind_WhenOnlyMonitorSetDiffers_IncludesMonitorsChanged()
    {
        var before = BuildSnapshot(monitors: ["DISPLAY1"], capture: ["mic1*"], render: ["spk1*"]);
        var after = BuildSnapshot(monitors: ["DISPLAY2"], capture: ["mic1*"], render: ["spk1*"]);

        var kind = PollingDeviceTopologyMonitor.CalculateChangeKind(before, after);

        Assert.IsTrue(kind.HasFlag(DeviceTopologyChangeKind.MonitorsChanged));
        Assert.IsFalse(kind.HasFlag(DeviceTopologyChangeKind.CaptureEndpointsChanged));
    }

    [TestMethod]
    public void CalculateChangeKind_WhenAudioEndpointRemoved_FlagsExpectedChanges()
    {
        var before = BuildSnapshot(monitors: ["DISPLAY1"], capture: ["mic1*", "mic2"], render: ["spk1*"]);
        var after = BuildSnapshot(monitors: ["DISPLAY1"], capture: ["mic2*"], render: ["spk1*"]);

        var kind = PollingDeviceTopologyMonitor.CalculateChangeKind(before, after);

        Assert.IsTrue(kind.HasFlag(DeviceTopologyChangeKind.CaptureEndpointsChanged));
        Assert.IsTrue(kind.HasFlag(DeviceTopologyChangeKind.DefaultCaptureEndpointChanged));
    }

    [TestMethod]
    public async Task CheckForChangesOnceAsync_WhenChanged_RaisesTopologyChanged()
    {
        var sequence = new Queue<DeviceTopologySnapshot>();
        sequence.Enqueue(BuildSnapshot(monitors: ["DISPLAY1"], capture: ["mic1*"], render: ["spk1*"]));
        sequence.Enqueue(BuildSnapshot(monitors: ["DISPLAY1"], capture: ["mic2*"], render: ["spk1*"]));

        var monitor = new PollingDeviceTopologyMonitor(
            snapshotProvider: () => sequence.Dequeue(),
            pollInterval: TimeSpan.FromHours(1));

        monitor.Start();
        DeviceTopologyChangedEventArgs? observed = null;
        monitor.TopologyChanged += (_, e) => observed = e;

        var kind = await monitor.CheckForChangesOnceAsync();
        await monitor.StopAsync();

        Assert.IsNotNull(observed);
        Assert.AreEqual(kind, observed.ChangeKind);
        Assert.IsTrue(kind.HasFlag(DeviceTopologyChangeKind.CaptureEndpointsChanged));
    }

    private static DeviceTopologySnapshot BuildSnapshot(
        string[] monitors,
        string[] capture,
        string[] render)
    {
        var monitorRows = monitors
            .Select((name, i) => new DisplayMonitorDescriptor(
                MonitorHandle: (nint)(100 + i),
                DeviceName: name,
                IsPrimary: i == 0,
                Bounds: new DisplayMonitorBounds(0, 0, 1920, 1080)))
            .ToArray();

        var captureRows = capture.Select(ToEndpoint).ToArray();
        var renderRows = render.Select(ToEndpoint).ToArray();
        return new DeviceTopologySnapshot(monitorRows, captureRows, renderRows);

        static AudioEndpointDescriptor ToEndpoint(string raw)
        {
            var isDefault = raw.EndsWith('*');
            var id = isDefault ? raw.TrimEnd('*') : raw;
            return new AudioEndpointDescriptor(id, id, isDefault);
        }
    }
}
