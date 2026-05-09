namespace ScreenRecorder.RecordingEngine.Settings;

internal static class AppSettingsSanitizer
{
    public static AppSettings Sanitize(AppSettings? input)
    {
        input ??= AppSettings.Default;

        var version = input.SchemaVersion < 1 ? 1 : input.SchemaVersion;
        string? dir = input.LastOutputDirectory;

        if (!string.IsNullOrWhiteSpace(dir))
        {
            try
            {
                if (dir.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                    dir = null;
                else if (!Path.IsPathFullyQualified(dir))
                    dir = null;
            }
            catch (ArgumentException)
            {
                dir = null;
            }
        }

        return new AppSettings
        {
            SchemaVersion = version,
            LastOutputDirectory = dir,
        };
    }
}
