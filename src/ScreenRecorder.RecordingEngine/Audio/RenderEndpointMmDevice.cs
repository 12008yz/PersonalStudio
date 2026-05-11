using NAudio.CoreAudioApi;

namespace ScreenRecorder.RecordingEngine.Audio;

/// <summary>
/// Открытие WASAPI конечной точки <b>вывода</b> для loopback-захвата (см. <see cref="NAudio.Wave.WasapiLoopbackCapture"/>).
/// </summary>
public static class RenderEndpointMmDevice
{
    /// <summary>
    /// Открывает устройство воспроизведения. Вызывающий обычно передаёт объект владению экземпляру <see cref="NAudio.Wave.WasapiLoopbackCapture"/>.
    /// </summary>
    public static MMDevice OpenRender(string? endpointIdOrNull)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException();

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            if (string.IsNullOrWhiteSpace(endpointIdOrNull))
            {
                // Разная политика Windows: Multimedia обычен для loopback, но активный вывод иногда висит только на Console/Communications.
                Exception? last = null;
                foreach (var role in new[] { Role.Multimedia, Role.Console, Role.Communications })
                {
                    try
                    {
                        return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, role);
                    }
                    catch (Exception ex)
                    {
                        last = ex;
                    }
                }

                throw new InvalidOperationException(
                    "No default render audio endpoint available for Multimedia, Console, or Communications role.",
                    last);
            }

            var device = enumerator.GetDevice(endpointIdOrNull);
            if (device.DataFlow != DataFlow.Render)
            {
                device.Dispose();
                throw new InvalidOperationException("Endpoint is not an audio rendering device.");
            }

            return device;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to open render audio endpoint.", ex);
        }
    }
}
