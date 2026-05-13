namespace ScreenRecorder.RecordingEngine.Settings;

/// <summary>Настройки приложения, сериализуемые в JSON.</summary>
public sealed record AppSettings
{
    public int SchemaVersion { get; init; } = 1;

    /// <summary>Последняя выбранная папка сохранения записей (абсолютный путь).</summary>
    public string? LastOutputDirectory { get; init; }

    /// <summary>Идентификатор микрофона (WASAPI / Core Audio endpoint id); null — устройство по умолчанию.</summary>
    public string? PreferredMicrophoneEndpointId { get; init; }

    /// <summary>
    /// Устройство воспроизведения для сквозной записи системного звука (WASAPI loopback привязан к рендер-устройству);
    /// null — системный вывод по умолчанию.
    /// </summary>
    public string? PreferredLoopbackRenderEndpointId { get; init; }

    /// <summary>
    /// Воспроизводить захваченный звук на динамики или в наушники во время записи (мониторинг). По умолчанию выключено из‑за риска
    /// акустической обратной связи при одновременном loopback и микрофоне без наушников. Учитывается движком записи (фаза E+).
    /// </summary>
    public bool AudioPassthroughMonitoringEnabled { get; init; }

    public static AppSettings Default => new();
}
