using NAudio.Wave;

namespace ScreenRecorder.RecordingEngine.Audio;

/// <summary>Один буфер PCM из shared WASAPI захвата (копия данных; обработчик не должен блокироваться).</summary>
public sealed class MicrophonePcmDataAvailableEventArgs : EventArgs
{
    public MicrophonePcmDataAvailableEventArgs(byte[] pcmSamples, WaveFormat waveFormat)
    {
        PcmSamples = pcmSamples ?? throw new ArgumentNullException(nameof(pcmSamples));
        WaveFormat = waveFormat ?? throw new ArgumentNullException(nameof(waveFormat));
    }

    public byte[] PcmSamples { get; }

    /// <summary>Формат сэмплов в буфере (частота/каналы/кодирование задаются микшером устройства в shared режиме).</summary>
    public WaveFormat WaveFormat { get; }
}
