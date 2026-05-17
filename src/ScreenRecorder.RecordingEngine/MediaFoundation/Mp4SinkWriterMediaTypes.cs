using Vortice;
using Vortice.MediaFoundation;

namespace ScreenRecorder.RecordingEngine.MediaFoundation;

internal static class Mp4SinkWriterMediaTypes
{
    private static readonly Guid Nv12Subtype = VideoFormatGuids.FromFourCC(new FourCC("NV12"));
    private static readonly Guid H264Subtype = VideoFormatGuids.FromFourCC(new FourCC("H264"));

    public static IMFMediaType CreateH264OutputType(Mp4SinkWriterConfiguration configuration)
    {
        var mediaType = MediaFactory.MFCreateMediaType();
        mediaType.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
        mediaType.Set(MediaTypeAttributeKeys.Subtype, H264Subtype);
        mediaType.Set(MediaTypeAttributeKeys.InterlaceMode, (uint)VideoInterlaceMode.Progressive);
        mediaType.Set(MediaTypeAttributeKeys.FrameSize, PackSize((uint)configuration.Width, (uint)configuration.Height));
        mediaType.Set(MediaTypeAttributeKeys.FrameRate, PackRatio((uint)configuration.FramesPerSecond, 1));
        mediaType.Set(MediaTypeAttributeKeys.PixelAspectRatio, PackRatio(1, 1));
        mediaType.Set(MediaTypeAttributeKeys.AvgBitrate, (uint)configuration.VideoBitrateBps);
        mediaType.Set(MediaTypeAttributeKeys.MaxKeyframeSpacing, (ulong)configuration.ComputeMaxKeyframeSpacingHns());
        mediaType.Set(MediaTypeAttributeKeys.H264RateControlModes, (uint)configuration.VideoRateControlMode);
        return mediaType;
    }

    public static IMFMediaType CreateNv12InputType(Mp4SinkWriterConfiguration configuration)
    {
        var mediaType = MediaFactory.MFCreateMediaType();
        mediaType.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
        mediaType.Set(MediaTypeAttributeKeys.Subtype, Nv12Subtype);
        mediaType.Set(MediaTypeAttributeKeys.InterlaceMode, (uint)VideoInterlaceMode.Progressive);
        mediaType.Set(MediaTypeAttributeKeys.FrameSize, PackSize((uint)configuration.Width, (uint)configuration.Height));
        mediaType.Set(MediaTypeAttributeKeys.FrameRate, PackRatio((uint)configuration.FramesPerSecond, 1));
        mediaType.Set(MediaTypeAttributeKeys.PixelAspectRatio, PackRatio(1, 1));
        mediaType.Set(MediaTypeAttributeKeys.DefaultStride, (uint)configuration.Width);
        return mediaType;
    }

    public static IMFMediaType CreateAacOutputType(Mp4SinkWriterConfiguration configuration)
    {
        var mediaType = MediaFactory.MFCreateMediaType();
        mediaType.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Audio);
        mediaType.Set(MediaTypeAttributeKeys.Subtype, AudioFormatGuids.Aac);
        mediaType.Set(MediaTypeAttributeKeys.AudioNumChannels, (uint)configuration.AudioChannels);
        mediaType.Set(MediaTypeAttributeKeys.AudioSamplesPerSecond, (uint)configuration.AudioSampleRateHz);
        mediaType.Set(MediaTypeAttributeKeys.AudioBitsPerSample, 16u);
        mediaType.Set(MediaTypeAttributeKeys.AvgBitrate, (uint)configuration.AudioBitrateBps);
        return mediaType;
    }

    public static IMFMediaType CreatePcmInputType(Mp4SinkWriterConfiguration configuration)
    {
        var mediaType = MediaFactory.MFCreateMediaType();
        mediaType.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Audio);
        mediaType.Set(MediaTypeAttributeKeys.Subtype, AudioFormatGuids.Pcm);
        mediaType.Set(MediaTypeAttributeKeys.AudioNumChannels, (uint)configuration.AudioChannels);
        mediaType.Set(MediaTypeAttributeKeys.AudioSamplesPerSecond, (uint)configuration.AudioSampleRateHz);
        mediaType.Set(MediaTypeAttributeKeys.AudioBitsPerSample, 16u);
        return mediaType;
    }

    /// <summary>Длительность одного кадра в 100-нс единицах (MF) для заданного FPS.</summary>
    public static long FrameDurationHns(int framesPerSecond) =>
        (long)MediaFactory.MFFrameRateToAverageTimePerFrame((uint)framesPerSecond, 1);

    public static int CalculateNv12BufferSize(int width, int height) =>
        width * height * 3 / 2;

    public static ulong PackSize(uint width, uint height) =>
        ((ulong)width << 32) | height;

    private static ulong PackRatio(uint numerator, uint denominator) =>
        ((ulong)numerator << 32) | denominator;
}
