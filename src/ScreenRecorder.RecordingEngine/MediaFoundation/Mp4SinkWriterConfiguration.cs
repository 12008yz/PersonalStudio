namespace ScreenRecorder.RecordingEngine.MediaFoundation;

/// <summary>Параметры выходного MP4 для <see cref="Mp4SinkWriter"/> (H.264 + AAC-LC).</summary>
public sealed class Mp4SinkWriterConfiguration
{
    public int Width { get; init; } = RecordingNfrSpec.ReferenceMaxWidth;

    public int Height { get; init; } = RecordingNfrSpec.ReferenceMaxHeight;

    public int FramesPerSecond { get; init; } = RecordingNfrSpec.ReferenceFramesPerSecond;

    /// <summary>Целевой битрейт видео (бит/с).</summary>
    public int VideoBitrateBps { get; init; } = 4_000_000;

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

        if (AudioChannels <= 0)
            throw new ArgumentOutOfRangeException(nameof(AudioChannels), "Audio channel count must be positive.");

        if (AudioSampleRateHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(AudioSampleRateHz), "Audio sample rate must be positive.");

        if (AudioBitrateBps <= 0)
            throw new ArgumentOutOfRangeException(nameof(AudioBitrateBps), "Audio bitrate must be positive.");
    }
}
