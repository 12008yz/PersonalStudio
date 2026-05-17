using Microsoft.Extensions.Logging;
using ScreenRecorder.RecordingEngine.Recording;

namespace ScreenRecorder.RecordingEngine.Audio;

/// <summary>
/// Одновременный захват голоса (микрофон) и смеси вывода (WASAPI loopback на выбранном рендер-устройстве).
/// Сценарий продукта: голос одновременно со звуком YouTube/игры с того же ПК без отдельного захвата по приложениям.
/// В MP4 для MVP смешивание в одну стерео AAC-LC дорожку — в движке MF (<see cref="RecordingAudioSpec.MvpMp4AudioTrackLayout"/>); здесь только раздельные PCM-потоки с меткой источника.
/// </summary>
public sealed class MicAndLoopbackCaptureSession : IDisposable, IRecordingSessionTimebaseConsumer
{
    private readonly object _gate = new();
    private bool _forwardingAttached;
    private volatile RecordingSessionTimebase? _sessionTimebase;

    public MicAndLoopbackCaptureSession(
        ILogger<MicrophoneCaptureSession>? microphoneLogger = null,
        ILogger<LoopbackCaptureSession>? loopbackLogger = null)
    {
        Microphone = new MicrophoneCaptureSession(microphoneLogger);
        Loopback = new LoopbackCaptureSession(loopbackLogger);
    }

    public MicrophoneCaptureSession Microphone { get; }

    public LoopbackCaptureSession Loopback { get; }

    /// <summary>
    /// Унифицированный PCM-поток для двух источников: в каждом событии есть и буфер, и wave format,
    /// и явная метка источника (<see cref="PcmCaptureSourceKind"/>).
    /// </summary>
    public event EventHandler<SourcedPcmCaptureDataAvailableEventArgs>? PcmDataAvailable;

    public void BindSessionTimebase(RecordingSessionTimebase timebase) =>
        _sessionTimebase = timebase ?? throw new ArgumentNullException(nameof(timebase));

    /// <summary>
    /// Запускает оба источника. Порядок: сначала loopback (WASAPI на MTA-воркере), затем микрофон на потоке вызывающего —
    /// на части конфигураций обратный порядок (микрофон до loopback) давал E_INVALIDARG при loopback <c>Initialize</c> на UI STA.
    /// При ошибке микрофона loopback уже запущенный останавливается перед пробросом исключения.
    /// </summary>
    public void Start(string? microphoneCaptureEndpointId, string? loopbackRenderEndpointId)
    {
        var attachedForThisStart = AttachForwardingHandlers();
        try
        {
            Loopback.Start(loopbackRenderEndpointId);
        }
        catch (ArgumentException ex)
        {
            if (attachedForThisStart)
                DetachForwardingHandlers();
            throw new ArgumentException($"WASAPI init failed ({nameof(Loopback)}): " + ex.Message, ex);
        }
        catch (InvalidOperationException ex)
        {
            if (attachedForThisStart)
                DetachForwardingHandlers();
            throw new InvalidOperationException($"WASAPI init failed ({nameof(Loopback)}): " + ex.Message, ex);
        }

        try
        {
            Microphone.Start(microphoneCaptureEndpointId);
        }
        catch (ArgumentException ex)
        {
            try { Loopback.Stop(); }
            catch { /* откат; не заменять ошибку микрофона */ }
            if (attachedForThisStart)
                DetachForwardingHandlers();

            throw new ArgumentException($"WASAPI init failed ({nameof(Microphone)}): " + ex.Message, ex);
        }
        catch (InvalidOperationException ex)
        {
            try { Loopback.Stop(); }
            catch { /* откат; не заменять ошибку микрофона */ }
            if (attachedForThisStart)
                DetachForwardingHandlers();

            throw new InvalidOperationException($"WASAPI init failed ({nameof(Microphone)}): " + ex.Message, ex);
        }
        catch
        {
            try { Loopback.Stop(); }
            catch { /* откат */ }
            if (attachedForThisStart)
                DetachForwardingHandlers();

            throw;
        }
    }

    /// <summary>Остановка зеркально к стабильному teardown: микрофон, затем loopback.</summary>
    public void Stop()
    {
        try
        {
            Microphone.Stop();
            Loopback.Stop();
        }
        finally
        {
            DetachForwardingHandlers();
        }
    }

    /// <summary>
    /// Перезапуск loopback с тем же правилом endpoint, что в <see cref="Start"/> (<c>null</c> — текущий default render).
    /// Микрофон не останавливается; агрегирующий <see cref="PcmDataAvailable"/> остаётся подписанным.
    /// См. <see cref="RecordingAudioDefaultDevicePolicy"/>.
    /// </summary>
    public void RestartLoopback(string? loopbackRenderEndpointId)
    {
        Loopback.Stop();
        Loopback.Start(loopbackRenderEndpointId);
    }

    /// <summary>
    /// Перезапуск микрофона (<c>null</c> — текущий default capture). Loopback не трогается.
    /// См. <see cref="RecordingAudioDefaultDevicePolicy"/>.
    /// </summary>
    public void RestartMicrophone(string? microphoneCaptureEndpointId)
    {
        Microphone.Stop();
        Microphone.Start(microphoneCaptureEndpointId);
    }

    /// <summary>
    /// Перезапуск обеих ног в порядке как при <see cref="Start"/> (сначала loopback, затем микрофон).
    /// При ошибке старта микрофона loopback останавливается, как при первичном <see cref="Start"/>.
    /// </summary>
    public void RestartBoth(string? microphoneCaptureEndpointId, string? loopbackRenderEndpointId)
    {
        Loopback.Stop();
        Microphone.Stop();
        Loopback.Start(loopbackRenderEndpointId);
        try
        {
            Microphone.Start(microphoneCaptureEndpointId);
        }
        catch
        {
            try { Loopback.Stop(); }
            catch { /* откат; не маскировать исходную ошибку */ }

            throw;
        }
    }

    public void Dispose() => Stop();

    private bool AttachForwardingHandlers()
    {
        lock (_gate)
        {
            if (_forwardingAttached)
                return false;

            Microphone.PcmDataAvailable += OnMicrophonePcmDataAvailable;
            Loopback.PcmDataAvailable += OnLoopbackPcmDataAvailable;
            _forwardingAttached = true;
            return true;
        }
    }

    private void DetachForwardingHandlers()
    {
        lock (_gate)
        {
            if (!_forwardingAttached)
                return;

            Microphone.PcmDataAvailable -= OnMicrophonePcmDataAvailable;
            Loopback.PcmDataAvailable -= OnLoopbackPcmDataAvailable;
            _forwardingAttached = false;
        }
    }

    private void OnMicrophonePcmDataAvailable(object? sender, PcmCaptureDataAvailableEventArgs e) =>
        ForwardPcm(PcmCaptureSourceKind.Microphone, e);

    private void OnLoopbackPcmDataAvailable(object? sender, PcmCaptureDataAvailableEventArgs e) =>
        ForwardPcm(PcmCaptureSourceKind.Loopback, e);

    private void ForwardPcm(PcmCaptureSourceKind sourceKind, PcmCaptureDataAvailableEventArgs e)
    {
        var handler = PcmDataAvailable;
        if (handler is null)
            return;

        long? startHns = e.SessionMediaTimestampHns;
        long? durationHns = e.SessionMediaDurationHns;
        var timebase = _sessionTimebase;
        if (timebase is { IsEstablished: true } && !e.HasSessionTiming)
        {
            var sampleCount = RecordingSessionPcmTiming.CountSamples(e.PcmSamples, e.WaveFormat);
            if (sampleCount > 0 && RecordingSessionPcmTiming.IsNominalRate(e.WaveFormat))
            {
                var trackClock = sourceKind == PcmCaptureSourceKind.Microphone
                    ? timebase.MicrophoneClock
                    : timebase.LoopbackClock;
                (startHns, durationHns) = trackClock.Allocate(sampleCount, timebase);
            }
        }

        handler(this, new SourcedPcmCaptureDataAvailableEventArgs(
            sourceKind,
            e.PcmSamples,
            e.WaveFormat,
            startHns,
            durationHns));
    }
}
