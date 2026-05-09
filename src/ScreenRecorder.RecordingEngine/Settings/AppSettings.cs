namespace ScreenRecorder.RecordingEngine.Settings;

/// <summary>Настройки приложения, сериализуемые в JSON.</summary>
public sealed record AppSettings
{
    public int SchemaVersion { get; init; } = 1;

    /// <summary>Последняя выбранная папка сохранения записей (абсолютный путь).</summary>
    public string? LastOutputDirectory { get; init; }

    public static AppSettings Default => new();
}
