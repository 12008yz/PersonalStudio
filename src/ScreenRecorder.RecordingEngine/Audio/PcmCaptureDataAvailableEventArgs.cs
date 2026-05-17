using NAudio.Wave;

namespace ScreenRecorder.RecordingEngine.Audio;

/// <summary>Один PCM-буфер из WASAPI shared capture или loopback (копия данных; обработчик не должен блокироваться).</summary>
public sealed class PcmCaptureDataAvailableEventArgs : EventArgs
{
    public PcmCaptureDataAvailableEventArgs(byte[] pcmSamples, WaveFormat waveFormat)
        : this(pcmSamples, waveFormat, sessionMediaTimestampHns: null, sessionMediaDurationHns: null)
    {
    }

    public PcmCaptureDataAvailableEventArgs(
        byte[] pcmSamples,
        WaveFormat waveFormat,
        long? sessionMediaTimestampHns,
        long? sessionMediaDurationHns)
    {
        PcmSamples = pcmSamples ?? throw new ArgumentNullException(nameof(pcmSamples));
        WaveFormat = waveFormat ?? throw new ArgumentNullException(nameof(waveFormat));
        SessionMediaTimestampHns = sessionMediaTimestampHns;
        SessionMediaDurationHns = sessionMediaDurationHns;
    }

    public byte[] PcmSamples { get; }

    /// <summary>Формат сэмплов (частота/каналы/кодирование задаёт ОС/WASAPI shared-микшер).</summary>
    public WaveFormat WaveFormat { get; }

    /// <summary>Начало блока на оси сессии (100-ns); <c>null</c>, если ось не привязана.</summary>
    public long? SessionMediaTimestampHns { get; }

    /// <summary>Длительность блока на оси сессии (100-ns); <c>null</c>, если ось не привязана.</summary>
    public long? SessionMediaDurationHns { get; }

    public bool HasSessionTiming => SessionMediaTimestampHns is not null && SessionMediaDurationHns is not null;
}
