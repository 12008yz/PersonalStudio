namespace ScreenRecorder.RecordingEngine;

/// <summary>
/// Единые ориентиры по аудио для пайплайна записи и MF. Захват WASAPI может идти в частоте устройства; при отличии от
/// <see cref="NominalSampleRateHz"/> движок приводит PCM к номиналу (см. <see cref="Audio.NominalSampleRatePcmConverter"/>).
/// </summary>
public static class RecordingAudioSpec
{
    /// <summary>Целевая номинальная частота дискретизации после выравнивания (AAC/MF и т.д.).</summary>
    public const int NominalSampleRateHz = 48_000;
}
