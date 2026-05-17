namespace ScreenRecorder.RecordingEngine;

/// <summary>
/// Продуктовая спецификация выходного файла v1. Кодирование и контейнер — через Media Foundation
/// (например <c>IMFSinkWriter</c> и MFT для H.264/AAC), без поставки FFmpeg и без вызова внешнего <c>ffmpeg.exe</c>.
/// </summary>
public static class RecordingOutputFormat
{
    public const string FileExtension = ".mp4";

    /// <summary>Видеодорожка: H.264 (AVC) в MP4.</summary>
    public const string VideoCodecLabel = "H.264";

    /// <summary>Аудиокодек в MP4: AAC (LC). Сколько дорожек и как смешиваются loopback и микрофон — <see cref="RecordingAudioSpec.MvpMp4AudioTrackLayout"/>.</summary>
    public const string AudioCodecLabel = "AAC-LC";

    /// <summary>Явная политика поставки: в релиз не входят бинарники FFmpeg и зависимости, тянущие внешний FFmpeg.</summary>
    public const bool BundlesFfmpeg = false;

    /// <summary>H.264: GOP и rate control — см. <see cref="RecordingVideoEncodingSpec"/> и <c>Mp4SinkWriterConfiguration</c>.</summary>
    public const string VideoRateControlLabel = "H.264 peak-constrained VBR (CBR optional)";
}
