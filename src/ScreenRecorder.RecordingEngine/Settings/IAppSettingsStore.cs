namespace ScreenRecorder.RecordingEngine.Settings;

public interface IAppSettingsStore
{
    /// <summary>Загружает настройки или создаёт файл с значениями по умолчанию.</summary>
    Task<AppSettings> LoadOrCreateAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
