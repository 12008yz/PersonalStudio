using ScreenRecorder.RecordingEngine;
using ScreenRecorder.RecordingEngine.MediaFoundation;
using SharpGen.Runtime;

namespace ScreenRecorder.RecordingEngine.Tests;

[TestClass]
public sealed class Mp4SinkWriterConfigurationTests
{
    [TestMethod]
    public void DefaultKeyframeInterval_YieldsGopOfTwoSecondsAt30Fps()
    {
        var configuration = new Mp4SinkWriterConfiguration { FramesPerSecond = 30 };
        Assert.AreEqual(60u, configuration.ComputeVideoGopSizeFrames());
        Assert.AreEqual(
            configuration.ComputeVideoGopSizeFrames() * Mp4SinkWriterMediaTypes.FrameDurationHns(30),
            configuration.ComputeMaxKeyframeSpacingHns());
    }

    [TestMethod]
    public void PeakBitrate_Default_IsOneAndHalfTimesAverage_ForVbr()
    {
        var configuration = new Mp4SinkWriterConfiguration
        {
            VideoBitrateBps = 4_000_000,
            VideoRateControlMode = H264RateControlMode.PeakConstrainedVbr,
        };

        Assert.AreEqual(6_000_000, configuration.ComputeVideoPeakBitrateBps());
    }

    [TestMethod]
    public void PeakBitrate_ForCbr_EqualsAverage_WhenNotExplicit()
    {
        var configuration = new Mp4SinkWriterConfiguration
        {
            VideoBitrateBps = 3_000_000,
            VideoRateControlMode = H264RateControlMode.ConstantBitrate,
        };

        Assert.AreEqual(3_000_000, configuration.ComputeVideoPeakBitrateBps());
    }

    [TestMethod]
    public void Validate_RejectsPeakBelowAverage()
    {
        var configuration = new Mp4SinkWriterConfiguration
        {
            VideoBitrateBps = 4_000_000,
            VideoPeakBitrateBps = 2_000_000,
        };

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => configuration.Validate());
    }

    [TestMethod]
    public void GopFrameCount_aligns_with_max_keyframe_spacing_for_any_fps()
    {
        const int fps = 24;
        const int intervalSeconds = 3;
        var configuration = new Mp4SinkWriterConfiguration
        {
            FramesPerSecond = fps,
            VideoKeyframeIntervalSeconds = intervalSeconds,
        };

        var frameDurationHns = Mp4SinkWriterMediaTypes.FrameDurationHns(fps);
        var expectedSpacing = configuration.ComputeVideoGopSizeFrames() * frameDurationHns;

        Assert.AreEqual(configuration.ComputeMaxKeyframeSpacingHns(), expectedSpacing);
    }

    [TestMethod]
    public void Validate_RejectsCbrWithMismatchedExplicitPeak()
    {
        var configuration = new Mp4SinkWriterConfiguration
        {
            VideoBitrateBps = 4_000_000,
            VideoPeakBitrateBps = 5_000_000,
            VideoRateControlMode = H264RateControlMode.ConstantBitrate,
        };

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => configuration.Validate());
    }

    [TestMethod]
    public void UnconstrainedVbr_OmitsMaxBitrateCodecAttribute()
    {
        Assert.IsFalse(Mp4H264EncoderParameters.AppliesMaxBitrateCeiling(H264RateControlMode.UnconstrainedVbr));

        var configuration = new Mp4SinkWriterConfiguration
        {
            VideoRateControlMode = H264RateControlMode.UnconstrainedVbr,
        };

        using var attributes = Mp4H264EncoderParameters.CreateEncodingAttributes(configuration);
        Assert.ThrowsException<SharpGenException>(() =>
            attributes.GetUInt32(H264CodecApiGuids.AVEncCommonMaxBitRate));
    }

    [TestMethod]
    public void H264EncoderParameters_ContainGopAndRateControl()
    {
        var configuration = new Mp4SinkWriterConfiguration
        {
            FramesPerSecond = 25,
            VideoKeyframeIntervalSeconds = 2,
            VideoBitrateBps = 2_500_000,
            VideoRateControlMode = H264RateControlMode.PeakConstrainedVbr,
        };
        configuration.Validate();

        using var attributes = Mp4H264EncoderParameters.CreateEncodingAttributes(configuration);
        Assert.AreEqual(50u, attributes.GetUInt32(H264CodecApiGuids.AVEncMPVGOPSize));
        Assert.AreEqual(
            (uint)H264RateControlMode.PeakConstrainedVbr,
            attributes.GetUInt32(H264CodecApiGuids.AVEncCommonRateControlMode));
        Assert.AreEqual(2_500_000u, attributes.GetUInt32(H264CodecApiGuids.AVEncCommonMeanBitRate));
        Assert.AreEqual(3_750_000u, attributes.GetUInt32(H264CodecApiGuids.AVEncCommonMaxBitRate));
    }
}
