using NAudio.Wave;

namespace ScreenRecorder.RecordingEngine.Audio;

/// <summary>
/// Потокобезопасная дозапись PCM в один WAV (ленивое создание файла с первого непустого буфера).
/// Используется для отладочных дампов до кодирования в MP4.
/// </summary>
public sealed class AppendOnlyPcmWaveFileWriter : IDisposable
{
    private readonly string _filePath;
    private readonly object _sync = new();
    private WaveFileWriter? _writer;
    private WaveFormat? _format;

    public AppendOnlyPcmWaveFileWriter(string filePath) =>
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));

    public long TotalBytesWritten { get; private set; }

    public void Append(byte[] pcmSamples, WaveFormat format)
    {
        ArgumentNullException.ThrowIfNull(pcmSamples);
        ArgumentNullException.ThrowIfNull(format);

        if (pcmSamples.Length == 0)
            return;

        lock (_sync)
        {
            if (_writer is null)
            {
                _writer = new WaveFileWriter(_filePath, format);
                _format = format;
            }
            else if (!SamePcmLayout(_format!, format))
            {
                throw new InvalidOperationException(
                    "PCM wave format changed mid-capture; cannot append to a single WAV file.");
            }

            _writer.Write(pcmSamples, 0, pcmSamples.Length);
            TotalBytesWritten += pcmSamples.Length;
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _writer?.Dispose();
            _writer = null;
            _format = null;
        }
    }

    private static bool SamePcmLayout(WaveFormat a, WaveFormat b) =>
        a.SampleRate == b.SampleRate &&
        a.Channels == b.Channels &&
        a.BitsPerSample == b.BitsPerSample &&
        a.Encoding == b.Encoding;
}
