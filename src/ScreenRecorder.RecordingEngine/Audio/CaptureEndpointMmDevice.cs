using NAudio.CoreAudioApi;

namespace ScreenRecorder.RecordingEngine.Audio;

/// <summary>Открытие WASAPI-конечных точек захвата по id из настроек или по умолчанию.</summary>
public static class CaptureEndpointMmDevice
{
    /// <summary>
    /// Открывает устройство захвата. Вызывающий владеет возвращённым <see cref="MMDevice"/> и обязан освободить его
    /// (в т.ч. передаёт владение <see cref="NAudio.Wave.WasapiCapture"/>, который освободит при <see cref="IDisposable.Dispose"/>).
    /// </summary>
    public static MMDevice OpenCapture(string? endpointIdOrNull)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException();

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            if (string.IsNullOrWhiteSpace(endpointIdOrNull))
                return enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);

            return enumerator.GetDevice(endpointIdOrNull);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to open capture audio endpoint.", ex);
        }
    }
}
