namespace ScreenRecorder.RecordingEngine.MediaFoundation;

/// <summary>
/// Результат проверки наличия системных MFT-энкодеров под выход <see cref="RecordingOutputFormat"/>.
/// </summary>
public readonly record struct MediaFoundationEncoderAvailability(
    int H264VideoEncoderCount,
    int AacAudioEncoderCount)
{
    /// <summary>Достаточно для записи MP4 (H.264 + AAC-LC) через <c>IMFSinkWriter</c>.</summary>
    public bool IsSufficientForRecording =>
        H264VideoEncoderCount > 0 && AacAudioEncoderCount > 0;

    /// <summary>Перечисляет зарегистрированные энкодеры (внутри — парные <see cref="MediaFoundationLifetime.AddRef"/>/<see cref="MediaFoundationLifetime.Release"/>).</summary>
    public static MediaFoundationEncoderAvailability Probe()
    {
        var report = MediaFoundationEncoderReport.Probe();
        return new MediaFoundationEncoderAvailability(report.H264Encoders.Count, report.AacEncoders.Count);
    }
}
