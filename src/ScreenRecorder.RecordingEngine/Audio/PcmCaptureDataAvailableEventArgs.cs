using NAudio.Wave;

namespace ScreenRecorder.RecordingEngine.Audio;

/// <summary>Один PCM-буфер из WASAPI shared capture или loopback (копия данных; обработчик не должен блокироваться).</summary>
public sealed class PcmCaptureDataAvailableEventArgs : EventArgs
{
    public PcmCaptureDataAvailableEventArgs(byte[] pcmSamples, WaveFormat waveFormat)
    {
        PcmSamples = pcmSamples ?? throw new ArgumentNullException(nameof(pcmSamples));
        WaveFormat = waveFormat ?? throw new ArgumentNullException(nameof(waveFormat));
    }

    public byte[] PcmSamples { get; }

    /// <summary>Формат сэмплов (частота/каналы/кодирование задаёт ОС/WASAPI shared-микшер).</summary>
    public WaveFormat WaveFormat { get; }
}
