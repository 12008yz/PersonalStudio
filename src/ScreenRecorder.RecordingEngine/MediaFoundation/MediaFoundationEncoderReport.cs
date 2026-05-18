namespace ScreenRecorder.RecordingEngine.MediaFoundation;

/// <summary>Снимок доступных MFT-энкодеров на машине (для логов и <c>docs/HARDWARE_CODEC_MATRIX.md</c>).</summary>
public sealed class MediaFoundationEncoderReport
{
    public MediaFoundationEncoderReport(
        IReadOnlyList<MediaFoundationEncoderInfo> h264Encoders,
        IReadOnlyList<MediaFoundationEncoderInfo> aacEncoders)
    {
        H264Encoders = h264Encoders ?? throw new ArgumentNullException(nameof(h264Encoders));
        AacEncoders = aacEncoders ?? throw new ArgumentNullException(nameof(aacEncoders));
    }

    public IReadOnlyList<MediaFoundationEncoderInfo> H264Encoders { get; }

    public IReadOnlyList<MediaFoundationEncoderInfo> AacEncoders { get; }

    public int H264HardwareCount => H264Encoders.Count(e => e.IsHardware);

    public int H264SoftwareCount => H264Encoders.Count(e => !e.IsHardware);

    public bool IsSufficientForRecording =>
        H264Encoders.Count > 0 && AacEncoders.Count > 0;

    public static MediaFoundationEncoderReport Probe() =>
        new(
            MediaFoundationEncoderCatalog.ListH264VideoEncoders(),
            MediaFoundationEncoderCatalog.ListAacEncoders());

    public string ToDiagnosticMultiline()
    {
        static string Line(MediaFoundationEncoderInfo e) =>
            $"  - {(e.IsHardware ? "HW" : "SW")} {e.FriendlyName} ({e.TransformClsid:D})";

        var h264 = H264Encoders.Count == 0
            ? "  (none)"
            : string.Join(Environment.NewLine, H264Encoders.Select(Line));
        var aac = AacEncoders.Count == 0
            ? "  (none)"
            : string.Join(Environment.NewLine, AacEncoders.Select(Line));

        return string.Join(Environment.NewLine, new[]
        {
            $"H.264 encoders: {H264Encoders.Count} (HW {H264HardwareCount}, SW {H264SoftwareCount})",
            h264,
            $"AAC encoders: {AacEncoders.Count}",
            aac,
        });
    }
}
