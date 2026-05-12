using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace ScreenRecorder.RecordingEngine.Audio;

/// <summary>
/// WASAPI capture в shared режиме → PCM буферы; при частоте ≠ <see cref="RecordingAudioSpec.NominalSampleRateHz"/>
/// применяется ресэмплинг к номиналу (выход IEEE float).
/// </summary>
public sealed class MicrophoneCaptureSession : IDisposable
{
    private readonly ILogger<MicrophoneCaptureSession>? _logger;
    private readonly object _gate = new();
    private WasapiCapture? _capture;
    private WaveFormat? _sourceWaveFormat;
    private WaveFormat? _emittedWaveFormat;
    private NominalSampleRatePcmConverter? _rateConverter;

    public MicrophoneCaptureSession(ILogger<MicrophoneCaptureSession>? logger = null)
    {
        _logger = logger;
    }

    public event EventHandler<PcmCaptureDataAvailableEventArgs>? PcmDataAvailable;

    /// <summary>Формат данных в <see cref="PcmDataAvailable"/> (после ресэмплинга — 48 kHz IEEE float при необходимости).</summary>
    /// <remarks>Без <c>_gate</c>: иначе взаимная блокировка с <see cref="Start"/> при вызове из <see cref="PcmDataAvailable"/>.</remarks>
    public WaveFormat? RecordingWaveFormat => _emittedWaveFormat;

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

            _sourceWaveFormat = capture.WaveFormat;
            _rateConverter = NominalSampleRatePcmConverter.TryCreateIfNeeded(_sourceWaveFormat);
            _emittedWaveFormat = _rateConverter?.OutputWaveFormat ?? _sourceWaveFormat;
            _logger?.LogInformation(
                "Microphone capture: {SampleRate} Hz, {Channels} channel(s), encoding {Encoding} (emitted {EmittedRate} Hz{ResampleNote})",
                _sourceWaveFormat.SampleRate,
                _sourceWaveFormat.Channels,
                _sourceWaveFormat.Encoding,
                _emittedWaveFormat.SampleRate,
                _rateConverter is null ? string.Empty : ", resampled to nominal");

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
            _sourceWaveFormat = null;
            _emittedWaveFormat = null;
            _rateConverter?.Dispose();
            _rateConverter = null;
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
        var srcFmt = _sourceWaveFormat;
        var handler = PcmDataAvailable;
        var converter = _rateConverter;

        if (srcFmt is null || handler is null || e.BytesRecorded <= 0)
            return;

        var copy = new byte[e.BytesRecorded];
        Array.Copy(e.Buffer, copy, e.BytesRecorded);

        if (converter is not null)
        {
            converter.Process(
                copy,
                (outBuf, outFmt) => handler.Invoke(this, new PcmCaptureDataAvailableEventArgs(outBuf, outFmt)));
        }
        else
        {
            handler.Invoke(this, new PcmCaptureDataAvailableEventArgs(copy, srcFmt));
        }
    }

    public void Stop()
    {
        EventHandler<PcmCaptureDataAvailableEventArgs>? handler;
        NominalSampleRatePcmConverter? converter;
        lock (_gate)
        {
            if (_capture is null)
                return;

            handler = PcmDataAvailable;
            converter = _rateConverter;

            _capture.DataAvailable -= OnDataAvailable;
            _capture.RecordingStopped -= OnRecordingStopped;

            if (_capture.CaptureState == CaptureState.Capturing)
                _capture.StopRecording();

            _capture.Dispose();
            _capture = null;
            _sourceWaveFormat = null;
            _emittedWaveFormat = null;
            _rateConverter = null;
        }

        converter?.Flush((outBuf, outFmt) => handler?.Invoke(this, new PcmCaptureDataAvailableEventArgs(outBuf, outFmt)));
        converter?.Dispose();
    }

    public void Dispose() => Stop();
}
