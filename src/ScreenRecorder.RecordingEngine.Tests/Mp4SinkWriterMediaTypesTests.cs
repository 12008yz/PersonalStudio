using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScreenRecorder.RecordingEngine.MediaFoundation;

namespace ScreenRecorder.RecordingEngine.Tests;

[TestClass]
public sealed class Mp4SinkWriterMediaTypesTests
{
    [TestMethod]
    public void FrameDurationHns_at_30fps_is_one_thirtieth_of_a_second()
    {
        const long hnsPerSecond = 10_000_000;
        var duration = Mp4SinkWriterMediaTypes.FrameDurationHns(30);

        Assert.AreEqual(hnsPerSecond / 30, duration);
    }

    [TestMethod]
    public void CalculateNv12BufferSize_matches_tight_packed_layout()
    {
        Assert.AreEqual(640 * 360 * 3 / 2, Mp4SinkWriterMediaTypes.CalculateNv12BufferSize(640, 360));
    }
}
