namespace ScreenRecorder.RecordingEngine;

/// <summary>
/// Единые ориентиры по аудио для пайплайна записи и MF (реальный захват может идти в формате устройства до ресэмплинга — см. план).
/// </summary>
public static class RecordingAudioSpec
{
    /// <summary>Целевая номинальная частота дискретизации после выравнивания (AAC/MF и т.д.).</summary>
    public const int NominalSampleRateHz = 48_000;
}
