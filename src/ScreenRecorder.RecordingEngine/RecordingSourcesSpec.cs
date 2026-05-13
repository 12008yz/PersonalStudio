namespace ScreenRecorder.RecordingEngine;

/// <summary>
/// Какие источники входят в продукт и что именно в MVP. Технологии — по плану: <c>Windows.Graphics.Capture</c> для видео, WASAPI для аудио.
/// Укладка звука в файл — <see cref="RecordingAudioSpec.MvpMp4AudioTrackLayout"/> (MVP: одна смешанная AAC-LC дорожка).
/// </summary>
public static class RecordingSourcesSpec
{
    /// <summary>MVP: один выбранный монитор целиком (вся поверхность дисплея). Произвольная область экрана (crop/регион) — после MVP.</summary>
    public const bool MvpFullMonitorOnly = true;

    /// <summary>Системный звук: WASAPI loopback (микшер вывода).</summary>
    public const bool IncludesSystemAudioLoopback = true;

    /// <summary>Микрофон: WASAPI capture (устройство по умолчанию или выбор в UI).</summary>
    public const bool IncludesMicrophone = true;
}
