using ScreenRecorder.RecordingEngine.Capture;

namespace ScreenRecorder.RecordingEngine.Tests;

[TestClass]
public sealed class DisplayMonitorEnumerationTests
{
    [TestMethod]
    public void EnumerateMonitors_OnInteractiveWindows_HasPrimaryAndNonEmptyDeviceNames()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10))
            Assert.Inconclusive("Windows 10+ required.");

        var list = DisplayMonitorEnumeration.EnumerateMonitors();
        Assert.IsTrue(list.Count >= 1, "Expected at least one monitor in an interactive Windows session.");

        var primaryCount = list.Count(m => m.IsPrimary);
        Assert.AreEqual(1, primaryCount, "Expected exactly one primary monitor.");

        foreach (var m in list)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(m.DeviceName));
            Assert.IsTrue(m.MonitorHandle != 0);
        }
    }
}
