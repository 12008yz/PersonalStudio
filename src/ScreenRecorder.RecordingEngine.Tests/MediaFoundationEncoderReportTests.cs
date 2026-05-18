using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScreenRecorder.RecordingEngine.MediaFoundation;

namespace ScreenRecorder.RecordingEngine.Tests;

[TestClass]
public sealed class MediaFoundationEncoderReportTests
{
    [TestMethod]
    public void Probe_encoder_counts_match_catalog_list_counts()
    {
        var report = MediaFoundationEncoderReport.Probe();

        Assert.AreEqual(MediaFoundationEncoderCatalog.CountH264VideoEncoders(), report.H264Encoders.Count);
        Assert.AreEqual(MediaFoundationEncoderCatalog.CountAacEncoders(), report.AacEncoders.Count);
    }

    [TestMethod]
    public void List_encoders_have_unique_transform_clsid()
    {
        var report = MediaFoundationEncoderReport.Probe();

        Assert.AreEqual(
            report.H264Encoders.Count,
            report.H264Encoders.Select(e => e.TransformClsid).Distinct().Count());
        Assert.AreEqual(
            report.AacEncoders.Count,
            report.AacEncoders.Select(e => e.TransformClsid).Distinct().Count());
    }
}
