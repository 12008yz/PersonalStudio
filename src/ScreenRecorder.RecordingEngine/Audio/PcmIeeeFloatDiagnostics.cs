using System.Buffers.Binary;
using NAudio.Wave;

namespace ScreenRecorder.RecordingEngine.Audio;

/// <summary>
/// Диагностика по буферам PCM в формате IEEE float (как после <see cref="NominalSampleRatePcmConverter"/> при ресэмплинге).
/// </summary>
public static class PcmIeeeFloatDiagnostics
{
    /// <summary>
    /// Считает число сэмплов (скаляров float), для которых |value| ≥ 1.0f — грубый индикатор перегруза/лимитера на пути.
    /// Для других кодирований возвращает 0.
    /// </summary>
    public static long CountAtOrBeyondFullScale(ReadOnlySpan<byte> pcmBytes, WaveFormat format)
    {
        if (format.Encoding is not WaveFormatEncoding.IeeeFloat || format.BitsPerSample != 32)
            return 0;

        if (pcmBytes.Length == 0 || pcmBytes.Length % sizeof(float) != 0)
            return 0;

        long atOrBeyond = 0;
        for (var offset = 0; offset < pcmBytes.Length; offset += sizeof(float))
        {
            var sample = BinaryPrimitives.ReadSingleLittleEndian(pcmBytes.Slice(offset, sizeof(float)));
            if (sample <= -1f || sample >= 1f)
                atOrBeyond++;
        }

        return atOrBeyond;
    }

    /// <summary>
    /// Оценка длительности по накопленному объёму байт и формату (для сравнения двух потоков до mux).
    /// </summary>
    public static double ApproximateDurationSeconds(long totalPcmBytes, WaveFormat format)
    {
        if (totalPcmBytes <= 0)
            return 0;

        if (format.AverageBytesPerSecond <= 0)
            return double.NaN;

        return totalPcmBytes / (double)format.AverageBytesPerSecond;
    }
}
