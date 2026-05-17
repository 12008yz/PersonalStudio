using Vortice.MediaFoundation;

namespace ScreenRecorder.RecordingEngine.MediaFoundation;

/// <summary>Сборка IMFAttributes для H.264 encoder MFT (GOP + rate control).</summary>
internal static class Mp4H264EncoderParameters
{
    /// <summary>
    /// MaxBitRate имеет смысл только для CBR (потолок = средний) и peak-constrained VBR.
    /// Для unconstrained/quality потолок через CODECAPI только искажает выбранный режим.
    /// </summary>
    internal static bool AppliesMaxBitrateCeiling(H264RateControlMode mode) =>
        mode is H264RateControlMode.ConstantBitrate or H264RateControlMode.PeakConstrainedVbr;

    public static IMFAttributes CreateEncodingAttributes(Mp4SinkWriterConfiguration configuration)
    {
        var gopFrames = configuration.ComputeVideoGopSizeFrames();
        var meanBitrate = (uint)configuration.VideoBitrateBps;
        var rateMode = (uint)configuration.VideoRateControlMode;
        var attributeCount = AppliesMaxBitrateCeiling(configuration.VideoRateControlMode) ? 4u : 3u;

        var attributes = MediaFactory.MFCreateAttributes(attributeCount);
        attributes.Set(H264CodecApiGuids.AVEncMPVGOPSize, gopFrames);
        attributes.Set(H264CodecApiGuids.AVEncCommonRateControlMode, rateMode);
        attributes.Set(H264CodecApiGuids.AVEncCommonMeanBitRate, meanBitrate);

        if (AppliesMaxBitrateCeiling(configuration.VideoRateControlMode))
        {
            attributes.Set(
                H264CodecApiGuids.AVEncCommonMaxBitRate,
                (uint)configuration.ComputeVideoPeakBitrateBps());
        }

        return attributes;
    }
}
