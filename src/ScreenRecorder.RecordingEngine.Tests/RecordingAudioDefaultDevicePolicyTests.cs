using ScreenRecorder.RecordingEngine.Audio;
using ScreenRecorder.RecordingEngine.Devices;

namespace ScreenRecorder.RecordingEngine.Tests;

[TestClass]
public sealed class RecordingAudioDefaultDevicePolicyTests
{
    [TestMethod]
    public void GetRestartMask_WhenNoDefaultFlags_ReturnsNone()
    {
        var mask = RecordingAudioDefaultDevicePolicy.GetRestartMask(
            microphoneCaptureEndpointId: null,
            loopbackRenderEndpointId: null,
            DeviceTopologyChangeKind.RenderEndpointsChanged);

        Assert.AreEqual(AudioDefaultDeviceRestartMask.None, mask);
    }

    [TestMethod]
    public void GetRestartMask_WhenExplicitMic_IgnoresDefaultCaptureChange()
    {
        var mask = RecordingAudioDefaultDevicePolicy.GetRestartMask(
            microphoneCaptureEndpointId: "{explicit-mic}",
            loopbackRenderEndpointId: null,
            DeviceTopologyChangeKind.DefaultCaptureEndpointChanged);

        Assert.AreEqual(AudioDefaultDeviceRestartMask.None, mask);
    }

    [TestMethod]
    public void GetRestartMask_WhenExplicitLoopback_IgnoresDefaultRenderChange()
    {
        var mask = RecordingAudioDefaultDevicePolicy.GetRestartMask(
            microphoneCaptureEndpointId: null,
            loopbackRenderEndpointId: "{explicit-spk}",
            DeviceTopologyChangeKind.DefaultRenderEndpointChanged);

        Assert.AreEqual(AudioDefaultDeviceRestartMask.None, mask);
    }

    [TestMethod]
    public void GetRestartMask_WhenMicFollowsDefault_AndDefaultCaptureChanged_IncludesMicrophone()
    {
        var mask = RecordingAudioDefaultDevicePolicy.GetRestartMask(
            microphoneCaptureEndpointId: null,
            loopbackRenderEndpointId: "{explicit-spk}",
            DeviceTopologyChangeKind.DefaultCaptureEndpointChanged);

        Assert.AreEqual(AudioDefaultDeviceRestartMask.Microphone, mask);
    }

    [TestMethod]
    public void GetRestartMask_WhenLoopbackFollowsDefault_AndDefaultRenderChanged_IncludesLoopback()
    {
        var mask = RecordingAudioDefaultDevicePolicy.GetRestartMask(
            microphoneCaptureEndpointId: "{explicit-mic}",
            loopbackRenderEndpointId: null,
            DeviceTopologyChangeKind.DefaultRenderEndpointChanged);

        Assert.AreEqual(AudioDefaultDeviceRestartMask.Loopback, mask);
    }

    [TestMethod]
    public void GetRestartMask_WhenBothFollowDefault_AndBothDefaultsChanged_IncludesBoth()
    {
        var kind = DeviceTopologyChangeKind.DefaultCaptureEndpointChanged |
                   DeviceTopologyChangeKind.DefaultRenderEndpointChanged;

        var mask = RecordingAudioDefaultDevicePolicy.GetRestartMask(
            microphoneCaptureEndpointId: null,
            loopbackRenderEndpointId: null,
            kind);

        Assert.IsTrue(mask.HasFlag(AudioDefaultDeviceRestartMask.Microphone));
        Assert.IsTrue(mask.HasFlag(AudioDefaultDeviceRestartMask.Loopback));
    }

    [TestMethod]
    public void GetRestartMask_DefaultRenderChange_DetectedAlongsideUnrelatedMonitorFlag()
    {
        var kind = DeviceTopologyChangeKind.MonitorsChanged |
                   DeviceTopologyChangeKind.DefaultRenderEndpointChanged;

        var mask = RecordingAudioDefaultDevicePolicy.GetRestartMask(null, null, kind);

        Assert.AreEqual(AudioDefaultDeviceRestartMask.Loopback, mask);
    }
}
