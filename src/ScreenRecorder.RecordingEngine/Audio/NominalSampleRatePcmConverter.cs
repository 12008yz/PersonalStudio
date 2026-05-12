using System.Buffers;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace ScreenRecorder.RecordingEngine.Audio;

/// <summary>
/// Приводит PCM к <see cref="RecordingAudioSpec.NominalSampleRateHz"/> Гц (выход IEEE float), если устройство отдаёт другую частоту.
/// Реализация: <see cref="BufferedWaveProvider"/> + <see cref="WdlResamplingSampleProvider"/> (NAudio / libsamplerate WDL).
/// </summary>
internal sealed class NominalSampleRatePcmConverter : IDisposable
{
    private readonly WaveFormat _sourceFormat;
    private readonly WaveFormat _outputFormat;
    private readonly BufferedWaveProvider _buffered;
    private readonly WdlResamplingSampleProvider _resampler;
    private readonly float[] _floatScratch;
    private readonly byte[] _alignmentCarry;
    private int _carryCount;

    private NominalSampleRatePcmConverter(WaveFormat sourceFormat)
    {
        if (sourceFormat.Channels <= 0)
            throw new ArgumentOutOfRangeException(nameof(sourceFormat), "Channels must be positive.");
        if (sourceFormat.BlockAlign <= 0)
            throw new ArgumentOutOfRangeException(nameof(sourceFormat), "BlockAlign must be positive.");

        _sourceFormat = sourceFormat;
        _outputFormat = WaveFormat.CreateIeeeFloatWaveFormat(RecordingAudioSpec.NominalSampleRateHz, sourceFormat.Channels);

        _buffered = new BufferedWaveProvider(sourceFormat)
        {
            BufferLength = Math.Clamp(sourceFormat.AverageBytesPerSecond * 4, 256 * 1024, 8 * 1024 * 1024),
            DiscardOnBufferOverflow = false,
        };

        var inSamples = _buffered.ToSampleProvider();
        _resampler = new WdlResamplingSampleProvider(inSamples, RecordingAudioSpec.NominalSampleRateHz);

        _floatScratch = new float[8192 * sourceFormat.Channels];
        _alignmentCarry = new byte[sourceFormat.BlockAlign];
    }

    public WaveFormat OutputWaveFormat => _outputFormat;

    /// <summary>Создаёт конвертер только если частота источника отличается от номинальной.</summary>
    public static NominalSampleRatePcmConverter? TryCreateIfNeeded(WaveFormat sourceFormat)
    {
        if (sourceFormat.SampleRate == RecordingAudioSpec.NominalSampleRateHz)
            return null;

        return new NominalSampleRatePcmConverter(sourceFormat);
    }

    /// <summary>Подаёт кадр PCM в формате источника; для подписчиков может вызывать <paramref name="emit"/> несколько раз.</summary>
    public void Process(ReadOnlySpan<byte> sourcePcm, Action<byte[], WaveFormat> emit)
    {
        if (sourcePcm.IsEmpty)
            return;

        var block = _sourceFormat.BlockAlign;
        var prefix = _alignmentCarry.AsSpan(0, _carryCount);
        var total = prefix.Length + sourcePcm.Length;
        var rented = ArrayPool<byte>.Shared.Rent(total);
        var alignedBytes = 0;
        try
        {
            prefix.CopyTo(rented);
            sourcePcm.CopyTo(rented.AsSpan(prefix.Length));
            var combined = rented.AsSpan(0, total);

            alignedBytes = combined.Length / block * block;
            var remainder = combined.Length - alignedBytes;
            if (remainder > 0)
                combined.Slice(alignedBytes, remainder).CopyTo(_alignmentCarry);
            _carryCount = remainder;

            if (alignedBytes > 0)
                _buffered.AddSamples(rented, 0, alignedBytes);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }

        DrainResampler(emit);
    }

    /// <summary>Сбрасывает задержку ресэмплера (короткий хвост тишины на входе).</summary>
    public void Flush(Action<byte[], WaveFormat> emit)
    {
        var pad = Math.Max(_sourceFormat.BlockAlign, _sourceFormat.AverageBytesPerSecond / 20);
        var silence = ArrayPool<byte>.Shared.Rent(pad);
        try
        {
            silence.AsSpan(0, pad).Clear();
            for (var i = 0; i < 4; i++)
                Process(silence.AsSpan(0, pad), emit);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(silence);
        }
    }

    private void DrainResampler(Action<byte[], WaveFormat> emit)
    {
        var block = _sourceFormat.BlockAlign;
        var channels = _outputFormat.Channels;
        var queuedFrames = _buffered.BufferedBytes / block;
        if (queuedFrames <= 0)
            return;

        // Верхняя оценка числа float-сэмплов на выходе по тому, что сейчас лежит во входной очереди
        // (на момент начала дренажа), плюс запас под задержку WDL.
        var maxFloatSamples =
            (long)Math.Ceiling(queuedFrames * (double)_outputFormat.SampleRate / _sourceFormat.SampleRate) * channels
            + channels * 256;

        long emitted = 0;
        for (var iter = 0; iter < 200_000 && emitted < maxFloatSamples; iter++)
        {
            var remaining = maxFloatSamples - emitted;
            if (remaining <= 0)
                break;

            var toRequest = (int)Math.Min(_floatScratch.Length, remaining);
            if (toRequest <= 0)
                break;

            var read = _resampler.Read(_floatScratch, 0, toRequest);
            if (read <= 0)
                break;

            emitted += read;
            var byteCount = read * sizeof(float);
            var bytes = new byte[byteCount];
            Buffer.BlockCopy(_floatScratch, 0, bytes, 0, byteCount);
            emit(bytes, _outputFormat);
        }
    }

    public void Dispose()
    {
        _carryCount = 0;
    }
}
