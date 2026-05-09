using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ScreenRecorder.RecordingEngine.Settings;

public sealed class JsonAppSettingsStore : IAppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _filePath;
    private readonly ILogger<JsonAppSettingsStore>? _logger;

    public JsonAppSettingsStore(string filePath, ILogger<JsonAppSettingsStore>? logger = null)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _logger = logger;
    }

    public async Task<AppSettings> LoadOrCreateAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            var created = AppSettingsSanitizer.Sanitize(AppSettings.Default);
            await SaveAsync(created, cancellationToken).ConfigureAwait(false);
            _logger?.LogInformation("Created default settings at {Path}", _filePath);
            return created;
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var raw = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            var sanitized = AppSettingsSanitizer.Sanitize(raw);
            _logger?.LogDebug("Loaded settings from {Path}", _filePath);
            return sanitized;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger?.LogWarning(ex, "Failed to read settings; using defaults ({Path})", _filePath);
            var fallback = AppSettingsSanitizer.Sanitize(AppSettings.Default);

            // Битый JSON не должен «вечно» ломать каждый старт — перезаписываем дефолтами, если это возможно.
            if (ex is JsonException)
            {
                try
                {
                    await SaveAsync(fallback, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception saveEx) when (saveEx is IOException or UnauthorizedAccessException)
                {
                    _logger?.LogWarning(saveEx, "Could not repair settings file ({Path})", _filePath);
                }
            }

            return fallback;
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var toWrite = AppSettingsSanitizer.Sanitize(settings);

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await using var stream = new FileStream(
            _filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        await JsonSerializer.SerializeAsync(stream, toWrite, JsonOptions, cancellationToken).ConfigureAwait(false);
        _logger?.LogDebug("Saved settings to {Path}", _filePath);
    }
}
