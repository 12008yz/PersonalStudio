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

    [TestMethod]
    public void Constructor_DefaultLatencyFields_AreNaN()
    {
        var m = new FrameCaptureMetrics(1, 0, TimeSpan.FromSeconds(1), 0, TimeSpan.Zero);
        Assert.IsTrue(double.IsNaN(m.AverageFrameHandlerLatencyMilliseconds));
        Assert.IsTrue(double.IsNaN(m.LastFrameHandlerLatencyMilliseconds));
    }

    [TestMethod]
    public void Constructor_WithLatency_PreservesPassedValues()
    {
        var m = new FrameCaptureMetrics(2, 0, TimeSpan.FromSeconds(1), 0, TimeSpan.Zero, 4.25, 10);
        Assert.AreEqual(4.25, m.AverageFrameHandlerLatencyMilliseconds, 1e-9);
        Assert.AreEqual(10, m.LastFrameHandlerLatencyMilliseconds, 1e-9);
    }
}
