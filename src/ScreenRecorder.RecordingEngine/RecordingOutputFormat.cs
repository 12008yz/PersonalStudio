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

    /// <summary>Аудиодорожка: AAC (LC) в MP4.</summary>
    public const string AudioCodecLabel = "AAC-LC";

    /// <summary>Явная политика поставки: в релиз не входят бинарники FFmpeg и зависимости, тянущие внешний FFmpeg.</summary>
    public const bool BundlesFfmpeg = false;
}
