using ScreenRecorder.RecordingEngine.Audio;

namespace ScreenRecorder.RecordingEngine.Tests;

[TestClass]
public sealed class MicAndLoopbackCaptureSessionTests
{
    [TestMethod]
    public void StartThenStop_OnWindows_DoesNotThrowWhenAudioAvailable()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10))
            Assert.Inconclusive("Windows required.");

        using var session = new MicAndLoopbackCaptureSession();
        try
        {
            session.Start(null, null);
        }
        catch (InvalidOperationException ex)
        {
            Assert.Inconclusive("Audio endpoint unavailable: " + ex.Message);
            return;
        }
        catch (ArgumentException ex)
        {
            Assert.Inconclusive("WASAPI init failed: " + ex.Message);
            return;
        }

        Thread.Sleep(150);
        session.Stop();
    }

    [TestMethod]
    public void UnifiedPcmContract_ProvidesSourceKindAndWaveFormat_WhenAudioAvailable()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10))
            Assert.Inconclusive("Windows required.");

        using var session = new MicAndLoopbackCaptureSession();
        var observed = 0;
        var sawKnownSource = false;
        var sawWaveFormat = false;

        session.PcmDataAvailable += (_, e) =>
        {
            Interlocked.Increment(ref observed);
            if (e.SourceKind is PcmCaptureSourceKind.Microphone or PcmCaptureSourceKind.Loopback)
                sawKnownSource = true;
            if (e.WaveFormat is not null)
                sawWaveFormat = true;
        };

        try
        {
            session.Start(null, null);
        }
        catch (InvalidOperationException ex)
        {
            Assert.Inconclusive("Audio endpoint unavailable: " + ex.Message);
            return;
        }
        catch (ArgumentException ex)
        {
            Assert.Inconclusive("WASAPI init failed: " + ex.Message);
            return;
        }

        Thread.Sleep(600);
        session.Stop();

        if (observed == 0)
            Assert.Inconclusive("No PCM observed during the short capture window.");

        Assert.IsTrue(sawKnownSource, "Expected SourceKind to be microphone or loopback.");
        Assert.IsTrue(sawWaveFormat, "Expected WaveFormat to be present in unified PCM contract.");
    }
}
