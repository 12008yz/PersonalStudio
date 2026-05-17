using System.Diagnostics;
using ScreenRecorder.RecordingEngine.Audio;

namespace ScreenRecorder.RecordingEngine.Recording;

/// <summary>
/// Единая ось медиа-времени сессии: нулевая точка по QPC и выделение меток для PCM после ресэмплинга к номиналу.
/// См. <c>docs/AV_DRIFT_POLICY.md</c> §3.
/// </summary>
public sealed class RecordingSessionTimebase
{
    private long _originQpcTicks;
    private readonly RecordingSessionAudioTrackClock _microphoneClock = new();
    private readonly RecordingSessionAudioTrackClock _loopbackClock = new();

    public bool IsEstablished => Volatile.Read(ref _originQpcTicks) != 0;

    public long OriginQpcTicks
    {
        get
        {
            var origin = Volatile.Read(ref _originQpcTicks);
            if (origin == 0)
                throw new InvalidOperationException("Session timebase is not established.");

            return origin;
        }
    }

    public RecordingSessionAudioTrackClock MicrophoneClock => _microphoneClock;

    public RecordingSessionAudioTrackClock LoopbackClock => _loopbackClock;

    /// <summary>Фиксирует нулевую точку сессии (QPC) и сбрасывает аудио-счётчики.</summary>
    public void Establish()
    {
        var origin = Stopwatch.GetTimestamp();
        Volatile.Write(ref _originQpcTicks, origin);
        _microphoneClock.Reset();
        _loopbackClock.Reset();
    }

    /// <summary>Переводит отметку QPC в медиа-время относительно <see cref="Establish"/> (100-ns, HNS).</summary>
    public long QpcToMediaTimestampHns(long qpcTicks)
    {
        var origin = OriginQpcTicks;
        var elapsed = Stopwatch.GetElapsedTime(origin, qpcTicks);
        if (elapsed <= TimeSpan.Zero)
            return 0;

        return (long)(elapsed.TotalSeconds * 10_000_000.0);
    }

    /// <summary>
    /// Оценка момента захвата кадра на шкале QPC: время прихода обработчика минус измеренная задержка WGC.
    /// </summary>
    public static long EstimateCaptureQpcTicks(long handlerQpcTicks, TimeSpan handlerLatency)
    {
        if (handlerLatency <= TimeSpan.Zero)
            return handlerQpcTicks;

        var latencyTicks = (long)(handlerLatency.Ticks * (double)Stopwatch.Frequency / TimeSpan.TicksPerSecond);
        return handlerQpcTicks - latencyTicks;
    }

    /// <summary>Медиа-метка кадра по QPC обработчика и задержке относительно <see cref="Direct3D11CaptureFrame.SystemRelativeTime"/>.</summary>
    public long VideoCaptureToMediaTimestampHns(long handlerQpcTicks, TimeSpan handlerLatency) =>
        QpcToMediaTimestampHns(EstimateCaptureQpcTicks(handlerQpcTicks, handlerLatency));
}

/// <summary>Монотонный счётчик сэмплов на одной аудио-ноге (после приведения к <see cref="RecordingAudioSpec.NominalSampleRateHz"/>).</summary>
public sealed class RecordingSessionAudioTrackClock
{
    private long _nextSampleIndex;
    private int _wallAligned;

    public void Reset()
    {
        Interlocked.Exchange(ref _nextSampleIndex, 0);
        Interlocked.Exchange(ref _wallAligned, 0);
    }

    /// <summary>
    /// Выделяет непрерывный интервал медиа-времени для блока PCM.
    /// При первом вызове с <paramref name="sessionTimebase"/> сдвигает ось к текущему QPC,
    /// чтобы первая метка совпадала с видео (оба относительно <see cref="RecordingSessionTimebase.Establish"/>).
    /// </summary>
    public (long StartHns, long DurationHns) Allocate(int sampleCount, RecordingSessionTimebase? sessionTimebase = null)
    {
        if (sampleCount <= 0)
            return (0, 0);

        if (sessionTimebase is { IsEstablished: true })
            EnsureWallAligned(sessionTimebase);

        var startIndex = Interlocked.Add(ref _nextSampleIndex, sampleCount) - sampleCount;
        return (SampleIndexToHns(startIndex), SampleIndexToHns(sampleCount));
    }

    public long SampleIndexToMediaTimestampHns(long sampleIndex) => SampleIndexToHns(sampleIndex);

    private void EnsureWallAligned(RecordingSessionTimebase sessionTimebase)
    {
        if (Interlocked.CompareExchange(ref _wallAligned, 1, 0) != 0)
            return;

        var mediaHns = sessionTimebase.QpcToMediaTimestampHns(Stopwatch.GetTimestamp());
        var sampleIndex = mediaHns * RecordingAudioSpec.NominalSampleRateHz / 10_000_000L;

        while (true)
        {
            var current = Volatile.Read(ref _nextSampleIndex);
            if (current >= sampleIndex)
                return;

            if (Interlocked.CompareExchange(ref _nextSampleIndex, sampleIndex, current) == current)
                return;
        }
    }

    private static long SampleIndexToHns(long sampleCount) =>
        sampleCount * 10_000_000L / RecordingAudioSpec.NominalSampleRateHz;
}

/// <summary>Утилиты для PCM-буферов на оси сессии.</summary>
public static class RecordingSessionPcmTiming
{
    public static int CountSamples(ReadOnlySpan<byte> pcm, NAudio.Wave.WaveFormat waveFormat)
    {
        ArgumentNullException.ThrowIfNull(waveFormat);
        if (pcm.IsEmpty)
            return 0;

        var bytesPerFrame = waveFormat.Channels * (waveFormat.BitsPerSample / 8);
        if (bytesPerFrame <= 0)
            throw new ArgumentException("Invalid wave format for sample counting.", nameof(waveFormat));

        return pcm.Length / bytesPerFrame;
    }

    public static bool IsNominalRate(NAudio.Wave.WaveFormat waveFormat) =>
        waveFormat.SampleRate == RecordingAudioSpec.NominalSampleRateHz;
}
