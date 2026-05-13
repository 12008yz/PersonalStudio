using NAudio.Wave;
using ScreenRecorder.RecordingEngine.Audio;

namespace ScreenRecorder.RecordingEngine.Tests;

[TestClass]
public sealed class PcmIeeeFloatDiagnosticsTests
{
    [TestMethod]
    public void CountAtOrBeyondFullScale_IeeeFloat_AllZeros_ReturnsZero()
    {
        var fmt = WaveFormat.CreateIeeeFloatWaveFormat(48_000, 2);
        var bytes = new byte[8]; // 2 floats 0
        var n = PcmIeeeFloatDiagnostics.CountAtOrBeyondFullScale(bytes, fmt);
        Assert.AreEqual(0, n);
    }

    [TestMethod]
    public void CountAtOrBeyondFullScale_IeeeFloat_PlusOne_ReturnsOne()
    {
        var fmt = WaveFormat.CreateIeeeFloatWaveFormat(48_000, 1);
        var bytes = BitConverter.GetBytes(1f);
        var n = PcmIeeeFloatDiagnostics.CountAtOrBeyondFullScale(bytes, fmt);
        Assert.AreEqual(1, n);
    }

    [TestMethod]
    public void CountAtOrBeyondFullScale_IeeeFloat_MinusOne_ReturnsOne()
    {
        var fmt = WaveFormat.CreateIeeeFloatWaveFormat(48_000, 1);
        var bytes = BitConverter.GetBytes(-1f);
        var n = PcmIeeeFloatDiagnostics.CountAtOrBeyondFullScale(bytes, fmt);
        Assert.AreEqual(1, n);
    }

    [TestMethod]
    public void CountAtOrBeyondFullScale_Pcm16_ReturnsZero()
    {
        var fmt = new WaveFormat(48_000, 16, 2);
        var bytes = new byte[4];
        var n = PcmIeeeFloatDiagnostics.CountAtOrBeyondFullScale(bytes, fmt);
        Assert.AreEqual(0, n);
    }

    [TestMethod]
    public void ApproximateDurationSeconds_FloatStereo48k_MatchesExpected()
    {
        var fmt = WaveFormat.CreateIeeeFloatWaveFormat(48_000, 2);
        var oneSecondBytes = fmt.AverageBytesPerSecond;
        var sec = PcmIeeeFloatDiagnostics.ApproximateDurationSeconds(oneSecondBytes, fmt);
        Assert.AreEqual(1.0, sec, 1e-9);
    }
}
