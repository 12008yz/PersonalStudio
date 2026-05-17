using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace ScreenRecorder.RecordingEngine.Audio;

/// <summary>
/// WASAPI loopback с выбранным устройством вывода → PCM-буферы; при частоте ≠ <see cref="RecordingAudioSpec.NominalSampleRateHz"/>
/// применяется ресэмплинг к номиналу (выход IEEE float).
/// </summary>
/// <remarks>
/// Пока на устройстве нет активного воспроизведения, драйвер часто не отдаёт кадры — это нормальное поведение loopback.
/// </remarks>
public sealed class LoopbackCaptureSession : IDisposable
{
    /// <summary>Длины буфера WASAPI в ms (NAudio по умолчанию — 100; часть драйверов требует другой период).</summary>
    private static readonly int[] LoopbackBufferSizeMillisecondsCandidates =
    [
        0, 100, 200, 500, 50, 33, 20, 16, 10,
    ];

    private static readonly LoopbackInitFlagStyle[] LoopbackFlagStyleCandidates =
    [
        LoopbackInitFlagStyle.MinimalLoopbackOnly,
        LoopbackInitFlagStyle.NoSrcDefaultQuality,
        LoopbackInitFlagStyle.NAudioDefaultWithSrcQuality,
    ];

    private static readonly AudioClientStreamFlags[] LoopbackExtraStreamFlagCandidates =
    [
        default,
        AudioClientStreamFlags.NoPersist,
    ];

    private readonly ILogger<LoopbackCaptureSession>? _logger;
    private readonly object _gate = new();
    private WasapiCapture? _capture;
    private ManualWasapiLoopbackCapture? _manualCapture;
    private WaveFormat? _sourceWaveFormat;
    private WaveFormat? _emittedWaveFormat;
    private NominalSampleRatePcmConverter? _rateConverter;

    public LoopbackCaptureSession(ILogger<LoopbackCaptureSession>? logger = null)
    {
        _logger = logger;
    }

    public event EventHandler<PcmCaptureDataAvailableEventArgs>? PcmDataAvailable;

    /// <summary>Формат данных в <see cref="PcmDataAvailable"/> (после ресэмплинга — 48 kHz IEEE float при необходимости).</summary>
    /// <remarks>Чтение без <c>_gate</c>; см. замечание к аналогу на <see cref="MicrophoneCaptureSession"/>.</remarks>
    public WaveFormat? RecordingWaveFormat => _emittedWaveFormat;

    /// <remarks>Чтение без <c>_gate</c>; см. <see cref="MicrophoneCaptureSession.IsRecording"/>.</remarks>
    public bool IsRecording =>
        (_capture is { CaptureState: CaptureState.Capturing }) ||
        (_manualCapture is { CaptureState: CaptureState.Capturing });

    /// <summary>
    /// Стартует захват смеси вывода. <paramref name="preferredRenderEndpointId"/> — id рендер-устройства (как <c>PreferredLoopbackRenderEndpointId</c> в JSON); <c>null</c> — вывод по умолчанию.
    /// </summary>
    /// <remarks>
    /// Колбэк <see cref="PcmDataAvailable"/> приходит с потока WASAPI; см. ограничения на обработчик в <see cref="MicrophoneCaptureSession.Start(string?)"/>.
    /// </remarks>
    public void Start(string? preferredRenderEndpointId)
    {
        // Весь WASAPI loopback (открытие MMDevice + Initialize) — в MTA; на WinUI STA иначе E_INVALIDARG / E_NOINTERFACE.
        InvokeOnMtaWorker(() => StartCore(preferredRenderEndpointId));
    }

    private void StartCore(string? preferredRenderEndpointId)
    {
        // Каждая попытка открывает новый MMDevice: после Dispose предыдущего WasapiLoopback экземпляр устройства нельзя переиспользовать.

        Exception? lastRecoverable = null;

        if (TryOpenRenderAndStartManual(preferredRenderEndpointId, LoopbackFlagStyleCandidates, out var manualFail))
            return;

        if (manualFail is not null)
            lastRecoverable = manualFail;

        foreach (var bufferMs in LoopbackBufferSizeMillisecondsCandidates)
        {
            foreach (var style in LoopbackFlagStyleCandidates)
            {
                foreach (var extraFlags in LoopbackExtraStreamFlagCandidates)
                {
                    var b = bufferMs;
                    var s = style;
                    var xf = extraFlags;
                    if (TryOpenRenderAndStart(
                            preferredRenderEndpointId,
                            d => new ConfigurableLoopbackWasapiCapture(d, b, s, xf),
                            out var fail))
                    {
                        return;
                    }

                    lastRecoverable = fail;
                    _logger?.LogDebug(
                        "Loopback WASAPI init failed (buffer {BufferMs} ms, flags {FlagStyle}, extra {ExtraFlags}): {Message}",
                        b,
                        s,
                        xf,
                        fail?.Message);
                }
            }
        }

        var detail = DescribeLoopbackInitFailure(lastRecoverable);
        detail += DescribeRenderMixFormat(preferredRenderEndpointId);
        throw new ArgumentException(
            "Loopback WASAPI initialization failed after compatibility retries. "
            + detail
            + " Check the playback device, drivers, "
            + "and that exclusive mode elsewhere is not blocking shared stream creation.",
            lastRecoverable);
    }

    private static string DescribeRenderMixFormat(string? preferredRenderEndpointId)
    {
        try
        {
            using var device = RenderEndpointMmDevice.OpenRender(preferredRenderEndpointId);
            using var client = device.AudioClient;
            var mix = client.MixFormat;
            return $" Render device mix: {mix.SampleRate} Hz, {mix.Channels} ch, {mix.BitsPerSample}-bit {mix.Encoding}. ";
        }
        catch (Exception ex)
        {
            return $" Could not read render mix format ({ex.Message}). ";
        }
    }

    private static string DescribeLoopbackInitFailure(Exception? ex)
    {
        if (ex is null)
            return string.Empty;

        for (var cur = ex; cur is not null; cur = cur.InnerException)
        {
            if (cur is COMException com)
            {
                var code = (uint)unchecked((int)com.HResult);
                return $"{ex.Message.TrimEnd()} (HRESULT 0x{code:X8}). ";
            }
        }

        return ex.Message.TrimEnd() + ". ";
    }

    /// <summary>
    /// Персонализированная инициализация: длительности в REFERENCE_TIME (включая <c>0</c> и кратности <see cref="AudioClient.DefaultDevicePeriod"/>), как в базе класса WASAPI —
    /// обходит жёсткую связку NAudio «ms → Initialize» для части связок Intel/Realtek/USB.
    /// </summary>
    private bool TryOpenRenderAndStartManual(
        string? preferredRenderEndpointId,
        LoopbackInitFlagStyle[] styles,
        out Exception? recoverableInitFailure)
    {
        recoverableInitFailure = null;

        var device = RenderEndpointMmDevice.OpenRender(preferredRenderEndpointId);
        ManualWasapiLoopbackCapture? manual;
        try
        {
            manual = ManualWasapiLoopbackCapture.TryInitializeWithRetries(device, styles, out recoverableInitFailure);
            if (manual is null)
            {
                device.Dispose();
                return false;
            }
        }
        catch
        {
            device.Dispose();
            throw;
        }

        lock (_gate)
        {
            if (_capture is not null || _manualCapture is not null)
                throw new InvalidOperationException("Loopback capture is already running.");

            manual.DataAvailable += OnLoopbackWaveInput;
            manual.RecordingStopped += OnRecordingStopped;
            var deviceFormat = manual.WaveFormat;
            AssignWaveFormatsForActiveCapture(deviceFormat);
            _logger?.LogInformation(
                "Loopback capture (manual init): {SampleRate} Hz, {Channels} channel(s), encoding {Encoding} (emitted {EmittedRate} Hz{ResampleNote})",
                deviceFormat.SampleRate,
                deviceFormat.Channels,
                deviceFormat.Encoding,
                _emittedWaveFormat!.SampleRate,
                _rateConverter is null ? string.Empty : ", resampled to nominal");

            _manualCapture = manual;
        }

        try
        {
            manual.StartRecording();
            recoverableInitFailure = null;
            return true;
        }
        catch (ArgumentException ex)
        {
            recoverableInitFailure = ex;
            AbandonFailedManualStart(manual);
            return false;
        }
        catch (COMException ce) when ((uint)unchecked((int)ce.HResult) is 0x80070057U or 0x88890019U)
        {
            recoverableInitFailure = ce;
            AbandonFailedManualStart(manual);
            return false;
        }
        catch
        {
            AbandonFailedManualStart(manual);
            throw;
        }
    }

    /// <summary>Под капотом: новое устройство + экземпляр захвата, регистрация под <c>_gate</c>, <see cref="WasapiCapture.StartRecording"/>.</summary>
    /// <returns><c>true</c> при успехе; при восстановимых ошибках <c>Initialize</c> делает abandon и возвращает <c>false</c>.</returns>
    private bool TryOpenRenderAndStart(
        string? preferredRenderEndpointId,
        Func<MMDevice, WasapiCapture> createCapture,
        out Exception? recoverableInitFailure)
    {
        recoverableInitFailure = null;
        var device = RenderEndpointMmDevice.OpenRender(preferredRenderEndpointId);
        WasapiCapture capture;
        try
        {
            capture = createCapture(device);
        }
        catch
        {
            device.Dispose();
            throw;
        }

        capture.ShareMode = AudioClientShareMode.Shared;

        lock (_gate)
        {
            if (_capture is not null || _manualCapture is not null)
                throw new InvalidOperationException("Loopback capture is already running.");

            capture.DataAvailable += OnLoopbackWaveInput;
            capture.RecordingStopped += OnRecordingStopped;

            var deviceFormat = capture.WaveFormat;
            AssignWaveFormatsForActiveCapture(deviceFormat);
            _logger?.LogInformation(
                "Loopback capture: {SampleRate} Hz, {Channels} channel(s), encoding {Encoding} (emitted {EmittedRate} Hz{ResampleNote})",
                deviceFormat.SampleRate,
                deviceFormat.Channels,
                deviceFormat.Encoding,
                _emittedWaveFormat!.SampleRate,
                _rateConverter is null ? string.Empty : ", resampled to nominal");

            _capture = capture;
        }

        try
        {
            capture.StartRecording();
            recoverableInitFailure = null;
            return true;
        }
        catch (ArgumentException ex)
        {
            recoverableInitFailure = ex;
            AbandonFailedStart(capture);
            return false;
        }
        catch (COMException ce) when ((uint)unchecked((int)ce.HResult) is 0x80070057U or 0x88890019U)
        {
            // E_INVALIDARG, AUDCLNT_E_BUFFER_SIZE_NOT_ALIGNED — пробуем следующий размер буфера / флаги.
            recoverableInitFailure = ce;
            AbandonFailedStart(capture);
            return false;
        }
        catch
        {
            AbandonFailedStart(capture);
            throw;
        }
    }

    private static T InvokeOnMtaWorker<T>(Func<T> work)
    {
        T? result = default;
        Exception? failure = null;
        using var done = new ManualResetEventSlim(false);

        void Work()
        {
            try
            {
                result = work();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                done.Set();
            }
        }

        Thread th = new Thread(_ => Work())
        {
            IsBackground = true,
            Name = "WasapiLoopbackMta",
        };

        try
        {
            th.SetApartmentState(ApartmentState.MTA);
        }
        catch (PlatformNotSupportedException)
        {
            return work();
        }

        th.Start();

        const int workerTimeoutSeconds = 30;
        if (!done.Wait(TimeSpan.FromSeconds(workerTimeoutSeconds)))
            throw new TimeoutException(
                $"WASAPI loopback MTA work timed out after {workerTimeoutSeconds} seconds.");

        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();

        return result!;
    }

    private static void InvokeOnMtaWorker(Action work) =>
        InvokeOnMtaWorker<object?>(() =>
        {
            work();
            return null;
        });

    private void AssignWaveFormatsForActiveCapture(WaveFormat deviceFormat)
    {
        _rateConverter?.Dispose();
        _sourceWaveFormat = deviceFormat;
        _rateConverter = NominalSampleRatePcmConverter.TryCreateIfNeeded(deviceFormat);
        _emittedWaveFormat = _rateConverter?.OutputWaveFormat ?? deviceFormat;
    }

    private void AbandonFailedStart(WasapiCapture capture)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_capture, capture))
                return;

            capture.DataAvailable -= OnLoopbackWaveInput;
            capture.RecordingStopped -= OnRecordingStopped;
            capture.Dispose();
            _capture = null;
            _sourceWaveFormat = null;
            _emittedWaveFormat = null;
            _rateConverter?.Dispose();
            _rateConverter = null;
        }
    }

    private void AbandonFailedManualStart(ManualWasapiLoopbackCapture capture)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_manualCapture, capture))
                return;

            capture.DataAvailable -= OnLoopbackWaveInput;
            capture.RecordingStopped -= OnRecordingStopped;
            capture.Dispose();
            _manualCapture = null;
            _sourceWaveFormat = null;
            _emittedWaveFormat = null;
            _rateConverter?.Dispose();
            _rateConverter = null;
        }
    }

    private void OnRecordingStopped(object? sender, EventArgs e)
    {
        if (e is not StoppedEventArgs se || se.Exception is null)
            return;

        _logger?.LogError(se.Exception, "Loopback capture stopped with error");
    }

    private void OnLoopbackWaveInput(object? sender, EventArgs e)
    {
        if (e is not WaveInEventArgs waveInArgs)
            return;

        var srcFmt = _sourceWaveFormat;
        var handler = PcmDataAvailable;
        var converter = _rateConverter;

        if (srcFmt is null || handler is null || waveInArgs.BytesRecorded <= 0)
            return;

        var copy = new byte[waveInArgs.BytesRecorded];
        Array.Copy(waveInArgs.Buffer, copy, waveInArgs.BytesRecorded);

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
            if (_capture is null && _manualCapture is null)
                return;

            handler = PcmDataAvailable;
            converter = _rateConverter;

            if (_capture is not null)
            {
                _capture.DataAvailable -= OnLoopbackWaveInput;
                _capture.RecordingStopped -= OnRecordingStopped;

                if (_capture.CaptureState == CaptureState.Capturing)
                    _capture.StopRecording();

                _capture.Dispose();
                _capture = null;
            }

            if (_manualCapture is not null)
            {
                _manualCapture.DataAvailable -= OnLoopbackWaveInput;
                _manualCapture.RecordingStopped -= OnRecordingStopped;

                if (_manualCapture.CaptureState == CaptureState.Capturing)
                    _manualCapture.StopRecording();

                _manualCapture.Dispose();
                _manualCapture = null;
            }

            _sourceWaveFormat = null;
            _emittedWaveFormat = null;
            _rateConverter = null;
        }

        converter?.Flush((outBuf, outFmt) => handler?.Invoke(this, new PcmCaptureDataAvailableEventArgs(outBuf, outFmt)));
        converter?.Dispose();
    }

    public void Dispose() => Stop();
}
