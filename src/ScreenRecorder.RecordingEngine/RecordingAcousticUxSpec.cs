namespace ScreenRecorder.RecordingEngine;

/// <summary>
/// Продуктовые ориентиры по акустике и мониторингу. Фактический флаг в настройках —
/// <see cref="ScreenRecorder.RecordingEngine.Settings.AppSettings.AudioPassthroughMonitoringEnabled"/>.
/// </summary>
public static class RecordingAcousticUxSpec
{
    /// <summary>Pass-through (слышать захваченный звук во время записи) по умолчанию выключен — см. дефолт <see cref="ScreenRecorder.RecordingEngine.Settings.AppSettings.Default"/>.</summary>
    public const bool AudioPassthroughMonitoringDefaultEnabled = false;

    /// <summary>Приглушение (ducking) других потоков при записи — не входит в MVP.</summary>
    public const bool AudioDuckingInMvp = false;
}
