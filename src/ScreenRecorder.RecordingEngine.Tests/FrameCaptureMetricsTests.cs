using ScreenRecorder.RecordingEngine.Capture;

namespace ScreenRecorder.RecordingEngine.Tests;

[TestClass]
public sealed class FrameCaptureMetricsTests
{
    [TestMethod]
    public void Constructor_ShortElapsed_YieldsZeroAverageFps()
    {
        var m = new FrameCaptureMetrics(100, 0, TimeSpan.FromMilliseconds(5), 0, TimeSpan.Zero);
        Assert.AreEqual(0, m.AverageFps);
    }

    [TestMethod]
    public void Constructor_TenSecondsThreeHundredFrames_YieldsAboutThirtyFps()
    {
        var m = new FrameCaptureMetrics(300, 2, TimeSpan.FromSeconds(10), 99, TimeSpan.FromSeconds(1));
        Assert.AreEqual(300, m.FramesReceived);
        Assert.AreEqual(2, m.EmptyFrames);
        Assert.AreEqual(30.0, m.AverageFps, 1e-6);
    }
}
