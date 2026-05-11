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
}
