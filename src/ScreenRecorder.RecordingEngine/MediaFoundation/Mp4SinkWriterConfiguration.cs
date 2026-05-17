namespace ScreenRecorder.RecordingEngine.MediaFoundation;

/// <summary>Параметры выходного MP4 для <see cref="Mp4SinkWriter"/> (H.264 + AAC-LC).</summary>
public sealed class Mp4SinkWriterConfiguration
{
    public int Width { get; init; } = RecordingNfrSpec.ReferenceMaxWidth;

    public int Height { get; init; } = RecordingNfrSpec.ReferenceMaxHeight;

    public int FramesPerSecond { get; init; } = RecordingNfrSpec.ReferenceFramesPerSecond;

    /// <summary>Целевой средний битрейт видео (бит/с).</summary>
    public int VideoBitrateBps { get; init; } = 4_000_000;

    /// <summary>
    /// Потолок битрейта для <see cref="H264RateControlMode.PeakConstrainedVbr"/>.
    /// <c>null</c> — <see cref="ComputeVideoPeakBitrateBps"/> (по умолчанию 1.5× среднего).
    /// </summary>
    public int? VideoPeakBitrateBps { get; init; }

    /// <summary>IDR/keyframe не реже чем раз в N секунд (размер GOP в кадрах = FPS × N).</summary>
    public int VideoKeyframeIntervalSeconds { get; init; } = RecordingVideoEncodingSpec.DefaultKeyframeIntervalSeconds;

    /// <summary>Режим H.264 rate control для MFT. MVP: <see cref="RecordingVideoEncodingSpec.DefaultRateControlMode"/>.</summary>
    public H264RateControlMode VideoRateControlMode { get; init; } = RecordingVideoEncodingSpec.DefaultRateControlMode;

    public int AudioChannels { get; init; } = 2;

    public int AudioSampleRateHz { get; init; } = RecordingAudioSpec.NominalSampleRateHz;

    /// <summary>Целевой битрейт AAC (бит/с).</summary>
    public int AudioBitrateBps { get; init; } = 192_000;

    public bool EnableHardwareTransforms { get; init; } = true;

    public void Validate()
    {
        if (Width <= 0 || Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(Width), "Video dimensions must be positive.");

        if ((Width & 1) != 0 || (Height & 1) != 0)
            throw new ArgumentOutOfRangeException(nameof(Width), "Width and height must be even for NV12/H.264.");

        if (FramesPerSecond <= 0)
            throw new ArgumentOutOfRangeException(nameof(FramesPerSecond), "Frame rate must be positive.");

        if (VideoBitrateBps <= 0)
            throw new ArgumentOutOfRangeException(nameof(VideoBitrateBps), "Video bitrate must be positive.");

        if (VideoPeakBitrateBps is <= 0)
            throw new ArgumentOutOfRangeException(nameof(VideoPeakBitrateBps), "Video peak bitrate must be positive when set.");

        if (VideoPeakBitrateBps is int peak && peak < VideoBitrateBps)
        {
            throw new ArgumentOutOfRangeException(
                nameof(VideoPeakBitrateBps),
                "Peak bitrate must be greater than or equal to average bitrate.");
        }

        if (VideoRateControlMode == H264RateControlMode.ConstantBitrate
            && VideoPeakBitrateBps is int cbrPeak
            && cbrPeak != VideoBitrateBps)
        {
            throw new ArgumentOutOfRangeException(
                nameof(VideoPeakBitrateBps),
                "For CBR, peak bitrate must equal average bitrate when explicitly set.");
        }

        if (VideoKeyframeIntervalSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(VideoKeyframeIntervalSeconds),
                "Keyframe interval must be positive.");
        }

        if (AudioChannels <= 0)
            throw new ArgumentOutOfRangeException(nameof(AudioChannels), "Audio channel count must be positive.");

        if (AudioSampleRateHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(AudioSampleRateHz), "Audio sample rate must be positive.");

        if (AudioBitrateBps <= 0)
            throw new ArgumentOutOfRangeException(nameof(AudioBitrateBps), "Audio bitrate must be positive.");
    }

    /// <summary>Размер GOP в кадрах для <see cref="H264CodecApiGuids.AVEncMPVGOPSize"/>.</summary>
    public uint ComputeVideoGopSizeFrames() =>
        (uint)Math.Max(1, FramesPerSecond * VideoKeyframeIntervalSeconds);

    /// <summary>Максимальный битрейт (бит/с) для CODECAPI_AVEncCommonMaxBitRate (только CBR / peak-constrained VBR).</summary>
    public int ComputeVideoPeakBitrateBps()
    {
        if (!Mp4H264EncoderParameters.AppliesMaxBitrateCeiling(VideoRateControlMode))
        {
            throw new InvalidOperationException(
                $"Peak bitrate is not defined for rate control mode {VideoRateControlMode}.");
        }

        if (VideoPeakBitrateBps is int explicitPeak)
            return explicitPeak;

        if (VideoRateControlMode == H264RateControlMode.ConstantBitrate)
            return VideoBitrateBps;

        return VideoBitrateBps
            * RecordingVideoEncodingSpec.DefaultPeakBitrateNumerator
            / RecordingVideoEncodingSpec.DefaultPeakBitrateDenominator;
    }

    /// <summary>
    /// <see cref="MediaTypeAttributeKeys.MaxKeyframeSpacing"/> (100-ns), согласовано с <see cref="ComputeVideoGopSizeFrames"/>
    /// и фактической длительностью кадра MF (избегает расхождения 24 fps ↔ 10_000_000/24).
    /// </summary>
    public long ComputeMaxKeyframeSpacingHns() =>
        ComputeVideoGopSizeFrames() * Mp4SinkWriterMediaTypes.FrameDurationHns(FramesPerSecond);
}

