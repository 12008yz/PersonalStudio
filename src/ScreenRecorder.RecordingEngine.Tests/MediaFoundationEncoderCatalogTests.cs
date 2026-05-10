using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScreenRecorder.RecordingEngine.MediaFoundation;

namespace ScreenRecorder.RecordingEngine.Tests;

[TestClass]
public sealed class MediaFoundationEncoderCatalogTests
{
    [TestMethod]
    public void Catalog_lists_nonempty_h264_and_aac_encoders_on_windows()
    {
        var h264 = MediaFoundationEncoderCatalog.CountH264VideoEncoders();
        var aac = MediaFoundationEncoderCatalog.CountAacEncoders();

        Assert.IsTrue(h264 > 0, "Expected at least one registered H.264 video encoder MFT.");
        Assert.IsTrue(aac > 0, "Expected at least one registered AAC audio encoder MFT.");
    }
}
