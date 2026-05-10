using ScreenRecorder.RecordingEngine.Audio;

namespace ScreenRecorder.RecordingEngine.Tests;

[TestClass]
public sealed class AudioDeviceEnumerationTests
{
    [TestMethod]
    public void EnumerateCaptureEndpoints_OnInteractiveWindows_ReturnsValidDescriptorsWhenPresent()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10))
            Assert.Inconclusive("Windows required.");

        var list = AudioDeviceEnumeration.EnumerateCaptureEndpoints();

        foreach (var d in list)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(d.DeviceId));
            Assert.IsFalse(string.IsNullOrWhiteSpace(d.DisplayName));

            Assert.IsFalse(d.DeviceId.Contains('\0'));
        }
    }

    [TestMethod]
    public void EnumerateRenderEndpoints_OnInteractiveWindows_ReturnsValidDescriptorsWhenPresent()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10))
            Assert.Inconclusive("Windows required.");

        var list = AudioDeviceEnumeration.EnumerateRenderEndpoints();

        foreach (var d in list)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(d.DeviceId));
            Assert.IsFalse(string.IsNullOrWhiteSpace(d.DisplayName));
        }
    }
}
