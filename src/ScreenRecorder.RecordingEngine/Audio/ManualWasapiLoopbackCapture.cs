using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace ScreenRecorder.RecordingEngine.Audio;

/// <summary>
/// После успешного <see cref="AudioClient.Initialize"/> для loopback — поток захвата по образцу NAudio <see cref="WasapiCapture"/>.
/// Используется когда NAudio задаёт недопустимую для драйвера длительность буфера (только перевод через ms).
/// </summary>
internal sealed class ManualWasapiLoopbackCapture : IDisposable
{
    private const long ReftimesPerSec = 10_000_000;
    private const long ReftimesPerMillisec = 10_000;

    private readonly MMDevice _device;
    private readonly AudioClient _audioClient;
    private readonly WaveFormat _waveFormat;
    private readonly SynchronizationContext? _syncContext;
    private readonly int _bytesPerFrame;

    private volatile CaptureState _captureState;
    private byte[] _recordBuffer;
    private Thread? _captureThread;

    /// <summary>
    /// <paramref name="device"/> и уже инициализированный <paramref name="initializedClient"/> переходят во владение.
    /// </summary>
    public ManualWasapiLoopbackCapture(MMDevice device, AudioClient initializedClient, WaveFormat mixFormatFromInitialize)
    {
        _device = device;
        _audioClient = initializedClient;
        _waveFormat = mixFormatFromInitialize.AsStandardWaveFormat();
        _syncContext = SynchronizationContext.Current;
        _bytesPerFrame = mixFormatFromInitialize.Channels * mixFormatFromInitialize.BitsPerSample / 8;

        var bufferFrames = initializedClient.BufferSize;
        _recordBuffer = new byte[bufferFrames * _bytesPerFrame];
    }

    public WaveFormat WaveFormat => _waveFormat;

    public CaptureState CaptureState => _captureState;

    public event EventHandler? DataAvailable;

    public event EventHandler? RecordingStopped;

    internal static IReadOnlyList<long> BuildReferenceDurationList(long defaultDevicePeriod, long minimumDevicePeriod)
    {
        var orderedUnique = new List<long>();

        void tryAdd(long hns)
        {
            if (hns == 0)
            {
                if (!orderedUnique.Contains(0L))
                    orderedUnique.Add(0);
                return;
            }

            if (hns > 0 && !orderedUnique.Contains(hns))
                orderedUnique.Add(hns);
        }

        tryAdd(0);

        if (minimumDevicePeriod > 0 && defaultDevicePeriod > 0)
        {
            for (var mul = 1; mul <= 6; mul++)
            {
                tryAdd(defaultDevicePeriod * mul);
                tryAdd(minimumDevicePeriod * mul);
                tryAdd(defaultDevicePeriod * mul + minimumDevicePeriod);
            }
        }

        foreach (var ms in new[] { 10, 16, 20, 33, 50, 100, 200, 500 })
            tryAdd(ms * ReftimesPerMillisec);

        return orderedUnique;
    }

    /// <summary>Подстановки формата для Shared loopback при отказах драйвера на MIX-format.</summary>
    internal static List<WaveFormat> BuildWaveFormatCandidates(WaveFormat mix)
    {
        static bool Equivalent(WaveFormat a, WaveFormat b) =>
            a.SampleRate == b.SampleRate &&
            a.Channels == b.Channels &&
            a.BitsPerSample == b.BitsPerSample &&
            a.Encoding == b.Encoding;

        var result = new List<WaveFormat>();

        void tryAdd(WaveFormat? wf)
        {
            if (wf is null || wf.SampleRate <= 0 || wf.Channels <= 0)
                return;

            foreach (var existing in result)
            {
                if (Equivalent(existing, wf))
                    return;
            }

            result.Add(wf);
        }

        tryAdd(mix);
        tryAdd(WaveFormat.CreateIeeeFloatWaveFormat(mix.SampleRate, mix.Channels));
        tryAdd(new WaveFormat(mix.SampleRate, 16, mix.Channels));
        return result;
    }

    internal static bool IsRecoverableWaveInitializeFailure(Exception ex)
    {
        for (Exception? cur = ex; cur is not null; cur = cur.InnerException)
        {
            // NAudio переводит ряд кодов аудиоклиента без COMException во внутреннем слое — ArgumentException здесь воспринимаем как «пробуй дальше».
            if (cur is ArgumentException or ArgumentNullException or ArgumentOutOfRangeException)
                return true;

            if (cur is COMException cx)
            {
                var u = unchecked((uint)unchecked((int)cx.HResult));
                if (u is 0x80070057U or 0x88890019U)
                    return true;
            }
        }

        return false;
    }

    /// <remarks>При ошибке восстанавливаем только <paramref name="lastFailure"/>; устройство инициализатора возвращает <c>null</c> — удалить снаружи.</remarks>
    internal static ManualWasapiLoopbackCapture? TryInitializeWithRetries(
        MMDevice device,
        LoopbackInitFlagStyle[] styles,
        out Exception? lastFailure)
    {
        lastFailure = null;

        WaveFormat mix;
        long defPeriod;
        long minPeriod;

        var probe = device.AudioClient;
        try
        {
            mix = probe.MixFormat;
            defPeriod = probe.DefaultDevicePeriod;
            minPeriod = probe.MinimumDevicePeriod;
        }
        finally
        {
            probe.Dispose();
        }

        var durations = BuildReferenceDurationList(defPeriod, minPeriod);
        var formats = BuildWaveFormatCandidates(mix);
        AudioClientStreamFlags[] noPersistVariants = [default, AudioClientStreamFlags.NoPersist];

        foreach (var wf in formats)
        {
            foreach (var dur in durations)
            {
                foreach (var style in styles)
                {
                    foreach (var noPersistAddon in noPersistVariants)
                    {
                        var client = device.AudioClient;
                        var flags = style.ToLoopbackFlags() | noPersistAddon;
                        try
                        {
                            client.Initialize(AudioClientShareMode.Shared, flags, dur, 0L, wf, Guid.Empty);
                            lastFailure = null;

                            // Успешный режим задаёт свой mix; приложению нужен общий источник для WaveFormat свойств.
                            return new ManualWasapiLoopbackCapture(device, client, wf);
                        }
                        catch (Exception ex) when (IsRecoverableWaveInitializeFailure(ex))
                        {
                            lastFailure = ex;
                            client.Dispose();
                        }
                        catch
                        {
                            client.Dispose();
                            throw;
                        }
                    }
                }
            }
        }

        return null;
    }

    /// <seealso cref="WasapiCapture.StartRecording"/>
    public void StartRecording()
    {
        if (_captureState != CaptureState.Stopped)
            throw new InvalidOperationException("Previous recording still in progress");

        _captureState = CaptureState.Starting;
        _captureThread = new Thread(() => CaptureThread(_audioClient))
        {
            IsBackground = true,
        };
        _captureThread.Start();
    }

    /// <seealso cref="WasapiCapture.StopRecording"/>
    public void StopRecording()
    {
        if (_captureState != CaptureState.Stopped)
            _captureState = CaptureState.Stopping;
    }

    private void CaptureThread(AudioClient client)
    {
        Exception? exception = null;
        try
        {
            DoRecording(client);
        }
        catch (Exception e)
        {
            exception = e;
        }
        finally
        {
            client.Stop();
        }

        _captureThread = null;
        _captureState = CaptureState.Stopped;
        RaiseRecordingStopped(exception);
    }

    private void DoRecording(AudioClient client)
    {
        int bufferFrameCount = client.BufferSize;
        int sr = Math.Max(1, _waveFormat.SampleRate);
        long actualDuration = (long)(ReftimesPerSec * bufferFrameCount / (double)sr);
        int sleepMilliseconds = (int)(actualDuration / ReftimesPerMillisec / 2);
        sleepMilliseconds = Math.Max(1, sleepMilliseconds);

        var captureClient = client.AudioCaptureClient;
        client.Start();

        if (_captureState == CaptureState.Starting)
            _captureState = CaptureState.Capturing;

        while (_captureState == CaptureState.Capturing)
        {
            Thread.Sleep(sleepMilliseconds);
            if (_captureState != CaptureState.Capturing)
                break;

            ReadNextPacket(captureClient);
        }
    }

    private void RaiseRecordingStopped(Exception? e)
    {
        var handler = RecordingStopped;
        if (handler is null)
            return;

        if (_syncContext is null)
            handler(this, new StoppedEventArgs(e));
        else
            _syncContext.Post(_ => handler(this, new StoppedEventArgs(e)), null);
    }

    private void ReadNextPacket(AudioCaptureClient capture)
    {
        int packetSize = capture.GetNextPacketSize();
        int recordBufferOffset = 0;

        while (packetSize != 0)
        {
            IntPtr buffer = capture.GetBuffer(out int framesAvailable, out AudioClientBufferFlags flags);

            int bytesAvailable = framesAvailable * _bytesPerFrame;
            int spaceRemaining = Math.Max(0, _recordBuffer.Length - recordBufferOffset);
            if (spaceRemaining < bytesAvailable && recordBufferOffset > 0)
            {
                DataAvailable?.Invoke(this, new WaveInEventArgs(_recordBuffer, recordBufferOffset));
                recordBufferOffset = 0;
            }

            if ((flags & AudioClientBufferFlags.Silent) != AudioClientBufferFlags.Silent)
                Marshal.Copy(buffer, _recordBuffer, recordBufferOffset, bytesAvailable);
            else
                Array.Clear(_recordBuffer, recordBufferOffset, bytesAvailable);

            recordBufferOffset += bytesAvailable;
            capture.ReleaseBuffer(framesAvailable);
            packetSize = capture.GetNextPacketSize();
        }

        DataAvailable?.Invoke(this, new WaveInEventArgs(_recordBuffer, recordBufferOffset));
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        StopRecording();
        _captureThread?.Join();
        _captureThread = null;
        _audioClient.Dispose();
        _device.Dispose();
    }
}
