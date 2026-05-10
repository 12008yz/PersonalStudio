using NAudio.CoreAudioApi;

namespace ScreenRecorder.RecordingEngine.Audio;

/// <summary>Перечисление конечных точек звука Windows (MMCSS / WASAPI через NAudio).</summary>
public static class AudioDeviceEnumeration
{
    /// <summary>Захват: микрофоны и прочие устройства ввода (активные).</summary>
    public static IReadOnlyList<AudioEndpointDescriptor> EnumerateCaptureEndpoints()
        => Enumerate(DataFlow.Capture);

    /// <summary>
    /// Воспроизведение (рендер): устройства вывода, к которым в дальнейшем можно привязать WASAPI loopback.
    /// </summary>
    public static IReadOnlyList<AudioEndpointDescriptor> EnumerateRenderEndpoints()
        => Enumerate(DataFlow.Render);

    private static IReadOnlyList<AudioEndpointDescriptor> Enumerate(DataFlow dataFlow)
    {
        if (!OperatingSystem.IsWindows())
            return Array.Empty<AudioEndpointDescriptor>();

        using var enumerator = new MMDeviceEnumerator();
        MMDevice? defaultDevice = null;
        try
        {
            defaultDevice = enumerator.GetDefaultAudioEndpoint(dataFlow, Role.Multimedia);
        }
        catch (Exception)
        {
            /* нет конечной точки по умолчанию — редкая конфигурация */
        }

        string? defaultId = null;
        try
        {
            defaultId = defaultDevice?.ID;
        }
        finally
        {
            defaultDevice?.Dispose();
        }

        var list = new List<AudioEndpointDescriptor>();
        foreach (var d in enumerator.EnumerateAudioEndPoints(dataFlow, DeviceState.Active))
        {
            using (d)
            {
                list.Add(new AudioEndpointDescriptor(d.ID, d.FriendlyName, IsDefaultEndpoint(d.ID, defaultId)));
            }
        }

        static bool IsDefaultEndpoint(string id, string? defaultEndpointId)
        {
            return defaultEndpointId is not null &&
                string.Equals(id, defaultEndpointId, StringComparison.OrdinalIgnoreCase);
        }

        return list;
    }
}
