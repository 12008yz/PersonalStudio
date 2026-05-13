namespace ScreenRecorder.RecordingEngine;

/// <summary>
/// Как звук из loopback и микрофона попадает в выходной MP4 (mux в Media Foundation).
/// </summary>
public enum Mp4AudioTrackLayout
{
    /// <summary>
    /// Одна стерео AAC-LC: системный звук и микрофон смешиваются в движке с согласованными таймстемпами и уровнями (фаза MF).
    /// </summary>
    SingleMixedStereoAacLc = 0,

    /// <summary>
    /// Две AAC-LC дорожки: отдельно loopback и микрофон — не MVP; возможная v1.1, если понадобится раздельная пост-обработка без повторного разделения одной смеси.
    /// </summary>
    DualSeparateSystemAndMicrophoneAacLc = 1,
}

/// <summary>
/// Единые ориентиры по аудио для пайплайна записи и MF. Захват WASAPI может идти в частоте устройства; при отличии от
/// <see cref="NominalSampleRateHz"/> движок приводит PCM к номиналу (см. <see cref="Audio.NominalSampleRatePcmConverter"/>).
/// Смена системного default во время записи при выборе «По умолчанию» — <see cref="Audio.RecordingAudioDefaultDevicePolicy"/>.
/// </summary>
public static class RecordingAudioSpec
{
    /// <summary>Целевая номинальная частота дискретизации после выравнивания (AAC/MF и т.д.).</summary>
    public const int NominalSampleRateHz = 48_000;

    /// <summary>MVP v1: одна смешанная стерео AAC-LC дорожка в MP4. Две отдельные дорожки не входят в первую версию.</summary>
    public const Mp4AudioTrackLayout MvpMp4AudioTrackLayout = Mp4AudioTrackLayout.SingleMixedStereoAacLc;
}
