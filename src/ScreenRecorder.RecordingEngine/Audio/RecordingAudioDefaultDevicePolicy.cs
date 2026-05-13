using ScreenRecorder.RecordingEngine.Devices;

namespace ScreenRecorder.RecordingEngine.Audio;

/// <summary>
/// Какая нога WASAPI должна быть перезапущена после <see cref="IDeviceTopologyMonitor.TopologyChanged"/>,
/// если в настройках сессии для неё выбрано «системное устройство по умолчанию» (<c>null</c> endpoint id).
/// </summary>
[Flags]
public enum AudioDefaultDeviceRestartMask
{
    None = 0,
    Microphone = 1 << 0,
    Loopback = 1 << 1,
}

/// <summary>
/// Политика MVP при смене default audio во время активной записи (или теста захвата).
/// </summary>
/// <remarks>
/// <para>
/// WASAPI открывает конкретный <c>MMDevice</c> на старте. Для строки «По умолчанию» в UI мы передаём
/// <c>null</c> — это устройство, которое было default в момент <see cref="MicAndLoopbackCaptureSession.Start"/>;
/// оно <b>не</b> «переезжает» на новый default автоматически. Чтобы запись соответствовала ожиданию
/// «следовать системному default», при <see cref="DeviceTopologyChangeKind.DefaultCaptureEndpointChanged"/> /
/// <see cref="DeviceTopologyChangeKind.DefaultRenderEndpointChanged"/> нужно перезапустить соответствующую ногу.
/// </para>
/// <para>
/// Явно выбранный endpoint (не <c>null</c>) не перезапускаем из‑за смены default — пользователь привязан к устройству.
/// Отключение выбранного устройства — отдельный сценарий (фаза F / оркестратор записи).
/// </para>
/// <para>
/// Если перезапуск бросает исключение (драйвер, exclusive mode, нет default) — трактуем как фатальную ошибку
/// записи: остановить сессию, показать сообщение; автоматических бесконечных ретраев в MVP нет.
/// </para>
/// </remarks>
public static class RecordingAudioDefaultDevicePolicy
{
    /// <summary>
    /// Вычисляет, какие ноги перезапустить. Учитываются только флаги смены <b>default</b>, не просто список endpoints.
    /// </summary>
    public static AudioDefaultDeviceRestartMask GetRestartMask(
        string? microphoneCaptureEndpointId,
        string? loopbackRenderEndpointId,
        DeviceTopologyChangeKind changeKind)
    {
        if (changeKind == DeviceTopologyChangeKind.None)
            return AudioDefaultDeviceRestartMask.None;

        var mask = AudioDefaultDeviceRestartMask.None;

        if (microphoneCaptureEndpointId is null &&
            changeKind.HasFlag(DeviceTopologyChangeKind.DefaultCaptureEndpointChanged))
        {
            mask |= AudioDefaultDeviceRestartMask.Microphone;
        }

        if (loopbackRenderEndpointId is null &&
            changeKind.HasFlag(DeviceTopologyChangeKind.DefaultRenderEndpointChanged))
        {
            mask |= AudioDefaultDeviceRestartMask.Loopback;
        }

        return mask;
    }

    /// <summary>
    /// Выполняет перезапуск ног в порядке, согласованном с <see cref="MicAndLoopbackCaptureSession.Start"/>
    /// (сначала loopback, затем микрофон), чтобы снизить риск E_INVALIDARG на части конфигураций.
    /// Если нужно перезапустить обе ноги, вызывается <see cref="MicAndLoopbackCaptureSession.RestartBoth"/>:
    /// обе останавливаются перед повторным стартом loopback — как при «холодном» старте, а не loopback при ещё работающем микрофоне.
    /// </summary>
    public static void ApplyRestartMask(
        MicAndLoopbackCaptureSession session,
        AudioDefaultDeviceRestartMask mask,
        string? microphoneCaptureEndpointId,
        string? loopbackRenderEndpointId)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (mask == AudioDefaultDeviceRestartMask.None)
            return;

        var loop = mask.HasFlag(AudioDefaultDeviceRestartMask.Loopback);
        var mic = mask.HasFlag(AudioDefaultDeviceRestartMask.Microphone);

        if (loop && mic)
        {
            session.RestartBoth(microphoneCaptureEndpointId, loopbackRenderEndpointId);
            return;
        }

        if (loop)
            session.RestartLoopback(loopbackRenderEndpointId);

        if (mic)
            session.RestartMicrophone(microphoneCaptureEndpointId);
    }
}
