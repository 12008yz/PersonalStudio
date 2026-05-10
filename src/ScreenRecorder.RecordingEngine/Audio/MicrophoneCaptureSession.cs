using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace ScreenRecorder.RecordingEngine.Audio;

/// <summary>
/// WASAPI capture в shared режиме → PCM буферы (без ресэмплинга; см. <see cref="RecordingAudioSpec.NominalSampleRateHz"/> для цели пайплайна).
/// </summary>
public sealed class MicrophoneCaptureSession : IDisposable
{
    private readonly ILogger<MicrophoneCaptureSession>? _logger;
    private readonly object _gate = new();
    private WasapiCapture? _capture;
    private WaveFormat? _waveFormat;

    public MicrophoneCaptureSession(ILogger<MicrophoneCaptureSession>? logger = null)
    {
        _logger = logger;
    }

    public event EventHandler<MicrophonePcmDataAvailableEventArgs>? PcmDataAvailable;

    /// <summary>Формат текущего потока; после <see cref="Stop"/> или до <see cref="Start"/> — <c>null</c>.</summary>
    /// <remarks>Без <c>_gate</c>: иначе взаимная блокировка с <see cref="Start"/> при вызове из <see cref="PcmDataAvailable"/>.</remarks>
    public WaveFormat? RecordingWaveFormat => _waveFormat;

    /// <remarks>Без <c>_gate</c> по той же причине, что и <see cref="RecordingWaveFormat"/>.</remarks>
    public bool IsRecording => _capture is { CaptureState: CaptureState.Capturing };

    /// <summary>
    /// Стартует захват. <paramref name="preferredCaptureEndpointId"/> — <c>null</c> для системного устройства по умолчанию.
    /// </summary>
    /// <remarks>
    /// Колбэк <see cref="PcmDataAvailable"/> может приходить с потока WASAPI; обработчик не должен блокироваться надолго
    /// и не должен вызывать <see cref="Start"/> рекурсивно. Вызов <see cref="Stop"/> из обработчика допустим, но избегайте ожиданий UI.
    /// </remarks>
    public void Start(string? preferredCaptureEndpointId)
    {
        WasapiCapture capture;
        lock (_gate)
        {
            if (_capture is not null)
                throw new InvalidOperationException("Microphone capture is already running.");

            MMDevice device = CaptureEndpointMmDevice.OpenCapture(preferredCaptureEndpointId);

            capture = new WasapiCapture(device)
            {
                ShareMode = AudioClientShareMode.Shared,
            };

            capture.DataAvailable += OnDataAvailable;
            capture.RecordingStopped += OnRecordingStopped;

            _waveFormat = capture.WaveFormat;
            _logger?.LogInformation(
                "Microphone capture: {SampleRate} Hz, {Channels} channel(s), encoding {Encoding} (nominal pipeline target {NominalHz} Hz)",
                _waveFormat.SampleRate,
                _waveFormat.Channels,
                _waveFormat.Encoding,
                RecordingAudioSpec.NominalSampleRateHz);

            _capture = capture;
        }

        try
        {
            capture.StartRecording();
        }
        catch
        {
            AbandonFailedStart(capture);
            throw;
        }
    }

    /// <summary>Старт упал после привязки <paramref name="capture"/> к полю — откатываем под <c>_gate</c>.</summary>
    private void AbandonFailedStart(WasapiCapture capture)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_capture, capture))
                return;

            capture.DataAvailable -= OnDataAvailable;
            capture.RecordingStopped -= OnRecordingStopped;
            capture.Dispose();
            _capture = null;
            _waveFormat = null;
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
            _logger?.LogError(e.Exception, "Microphone capture stopped with error");
    }

    /// <summary>
    /// Без захвата <c>_gate</c>: NAudio может вызывать обработчик с того же потока, где идёт <see cref="WasapiCapture.StartRecording"/> / остановка;
    /// удержание <c>_gate</c> здесь давало бы взаимную блокировку с <see cref="Stop"/> или с подписчиками, читающими состояние под <c>_gate</c>.
    /// </summary>
    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        var fmt = _waveFormat;
        var handler = PcmDataAvailable;

        if (fmt is null || handler is null || e.BytesRecorded <= 0)
            return;

        var copy = new byte[e.BytesRecorded];
        Array.Copy(e.Buffer, copy, e.BytesRecorded);

        handler.Invoke(this, new MicrophonePcmDataAvailableEventArgs(copy, fmt));
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (_capture is null)
                return;

            _capture.DataAvailable -= OnDataAvailable;
            _capture.RecordingStopped -= OnRecordingStopped;

            if (_capture.CaptureState == CaptureState.Capturing)
                _capture.StopRecording();

            _capture.Dispose();
            _capture = null;
            _waveFormat = null;
        }
    }

    public void Dispose() => Stop();
}
