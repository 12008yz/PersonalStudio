namespace ScreenRecorder.RecordingEngine;

/// <summary>
/// Единый идентификатор приложения для путей в <c>%LocalAppData%</c> (настройки, кэши).
/// Имя папки согласовано с продуктом; не менять без миграции файлов у пользователей.
/// </summary>
public static class ApplicationIdentity
{
    public const string LocalAppDataFolderName = "PersonalStudio.ScreenRecorder";

    public const string SettingsFileName = "settings.json";

    /// <summary>Полный путь к <c>settings.json</c> в профиле пользователя.</summary>
    public static string DefaultSettingsFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            LocalAppDataFolderName,
            SettingsFileName);
}
