namespace ScreenRecorder.RecordingEngine.Audio;

/// <summary>Аудиоустройство Core Audio (рендер для loopback или захват для микрофона).</summary>
/// <param name="DeviceId">Стабильный идентификатор WASAPI (можно сохранять в настройках).</param>
/// <param name="DisplayName">Отображаемое имя (локализованное ОС).</param>
/// <param name="IsSystemDefault">Является ли устройством по умолчанию для роли Multimedia.</param>
public sealed record AudioEndpointDescriptor(string DeviceId, string DisplayName, bool IsSystemDefault);
