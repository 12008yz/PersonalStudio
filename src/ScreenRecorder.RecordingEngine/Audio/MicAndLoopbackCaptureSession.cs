using Microsoft.Extensions.Logging;

namespace ScreenRecorder.RecordingEngine.Audio;

/// <summary>
/// Одновременный захват голоса (микрофон) и смеси вывода (WASAPI loopback на выбранном рендер-устройстве).
/// Сценарий продукта: голос одновременно со звуком YouTube/игры с того же ПК без отдельного захвата по приложениям.
/// </summary>
public sealed class MicAndLoopbackCaptureSession : IDisposable
{
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
    /// Запускает оба источника. Порядок: сначала loopback (смешивание вывода), затем микрофон — на части конфигураций
    /// Windows/NAudio второй подряд <c>Initialize</c> на UI-потоке после микрофона даёт HRESULT E_INVALIDARG («Value does not fall within the expected range»).
    /// При ошибке микрофона loopback уже запущенный останавливается перед пробросом исключения.
    /// </summary>
    public void Start(string? microphoneCaptureEndpointId, string? loopbackRenderEndpointId)
    {
        try
        {
            Loopback.Start(loopbackRenderEndpointId);
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException($"WASAPI init failed ({nameof(Loopback)}): " + ex.Message, ex);
        }
        catch (InvalidOperationException ex)
        {
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

            throw new ArgumentException($"WASAPI init failed ({nameof(Microphone)}): " + ex.Message, ex);
        }
        catch (InvalidOperationException ex)
        {
            try { Loopback.Stop(); }
            catch { /* откат; не заменять ошибку микрофона */ }

            throw new InvalidOperationException($"WASAPI init failed ({nameof(Microphone)}): " + ex.Message, ex);
        }
        catch
        {
            try { Loopback.Stop(); }
            catch { /* откат */ }

            throw;
        }
    }

    /// <summary>Остановка зеркально к стабильному teardown: микрофон, затем loopback.</summary>
    public void Stop()
    {
        Microphone.Stop();
        Loopback.Stop();
    }

    public void Dispose() => Stop();
}
