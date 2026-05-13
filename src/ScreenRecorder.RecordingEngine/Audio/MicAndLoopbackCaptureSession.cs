using Microsoft.Extensions.Logging;

namespace ScreenRecorder.RecordingEngine.Audio;

/// <summary>
/// Одновременный захват голоса (микрофон) и смеси вывода (WASAPI loopback на выбранном рендер-устройстве).
/// Сценарий продукта: голос одновременно со звуком YouTube/игры с того же ПК без отдельного захвата по приложениям.
/// В MP4 для MVP смешивание в одну стерео AAC-LC дорожку — в движке MF (<see cref="RecordingAudioSpec.MvpMp4AudioTrackLayout"/>); здесь только раздельные PCM-потоки с меткой источника.
/// </summary>
public sealed class MicAndLoopbackCaptureSession : IDisposable
{
    private readonly object _gate = new();
    private bool _forwardingAttached;

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

    /// <summary>
    /// Запускает оба источника. Порядок: сначала loopback (смешивание вывода), затем микрофон — на части конфигураций
    /// Windows/NAudio второй подряд <c>Initialize</c> на UI-потоке после микрофона даёт HRESULT E_INVALIDARG («Value does not fall within the expected range»).
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

    private void OnMicrophonePcmDataAvailable(object? sender, PcmCaptureDataAvailableEventArgs e)
    {
        var handler = PcmDataAvailable;
        if (handler is null)
            return;

        handler(this, new SourcedPcmCaptureDataAvailableEventArgs(
            PcmCaptureSourceKind.Microphone,
            e.PcmSamples,
            e.WaveFormat));
    }

    private void OnLoopbackPcmDataAvailable(object? sender, PcmCaptureDataAvailableEventArgs e)
    {
        var handler = PcmDataAvailable;
        if (handler is null)
            return;

        handler(this, new SourcedPcmCaptureDataAvailableEventArgs(
            PcmCaptureSourceKind.Loopback,
            e.PcmSamples,
            e.WaveFormat));
    }
}
