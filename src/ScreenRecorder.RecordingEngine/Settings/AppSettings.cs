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

    public static AppSettings Default => new();
}
