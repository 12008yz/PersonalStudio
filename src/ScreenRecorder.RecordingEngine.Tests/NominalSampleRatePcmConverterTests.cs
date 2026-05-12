using System.Buffers.Binary;
using NAudio.Wave;
using ScreenRecorder.RecordingEngine.Audio;

namespace ScreenRecorder.RecordingEngine.Tests;

[TestClass]
public sealed class NominalSampleRatePcmConverterTests
{
    [TestMethod]
    public void TryCreateIfNeeded_ReturnsNull_WhenAlreadyNominalRate()
    {
        var wf = WaveFormat.CreateIeeeFloatWaveFormat(RecordingAudioSpec.NominalSampleRateHz, 2);
        Assert.IsNull(NominalSampleRatePcmConverter.TryCreateIfNeeded(wf));
    }

    [TestMethod]
    public void Process_44100FloatStereo_Yields48000Float_AndRoughlyExpectedLength()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10))
        {
            Assert.Inconclusive("Windows 10+ required for Media Foundation resampler.");
            return;
        }

        var source = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        var created = NominalSampleRatePcmConverter.TryCreateIfNeeded(source);
        Assert.IsNotNull(created);
        using var converter = created!;
        Assert.AreEqual(RecordingAudioSpec.NominalSampleRateHz, converter.OutputWaveFormat.SampleRate);
        Assert.AreEqual(2, converter.OutputWaveFormat.Channels);

        const int frames = 4410;
        var input = new byte[frames * source.BlockAlign];
        for (var i = 0; i < frames * 2; i++)
            BinaryPrimitives.WriteSingleLittleEndian(input.AsSpan(i * 4, 4), 0.1f);

        long outBytes = 0;
        converter.Process(input, (buf, fmt) =>
        {
            outBytes += buf.Length;
            Assert.AreEqual(RecordingAudioSpec.NominalSampleRateHz, fmt.SampleRate);
            Assert.AreEqual(WaveFormatEncoding.IeeeFloat, fmt.Encoding);
        });

        var expectedApprox = (long)(frames * (48000.0 / 44100.0) * converter.OutputWaveFormat.BlockAlign);
        Assert.IsTrue(
            outBytes >= expectedApprox * 85 / 100 && outBytes <= expectedApprox * 125 / 100,
            $"Expected output length near {expectedApprox} bytes from Process, got {outBytes}.");

        long flushBytes = 0;
        converter.Flush((buf, fmt) =>
        {
            flushBytes += buf.Length;
            Assert.AreEqual(RecordingAudioSpec.NominalSampleRateHz, fmt.SampleRate);
        });

        Assert.IsTrue(flushBytes > 0, "Flush should emit resampler tail.");
    }
}
