using ScreenRecorder.RecordingEngine.Audio;

namespace ScreenRecorder.RecordingEngine.Tests;

[TestClass]
public sealed class LoopbackCaptureSessionTests
{
    [TestMethod]
    public void Start_Twice_ThrowsInvalidOperation()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10))
            Assert.Inconclusive("Windows required.");

        using var session = new LoopbackCaptureSession();
        try
        {
            session.Start(null);
        }
        catch (InvalidOperationException ex)
        {
            Assert.Inconclusive("No render endpoint: " + ex.Message);
            return;
        }
        catch (ArgumentException ex)
        {
            Assert.Inconclusive("WASAPI loopback init failed (offline/headless/unsupported audio): " + ex.Message);
            return;
        }

        try
        {
            Assert.ThrowsException<InvalidOperationException>(() => session.Start(null));
        }
        finally
        {
            session.Stop();
        }
    }

    [TestMethod]
    public void PcmDataAvailable_MayDeliverBytes_WhenOutputIsActive()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10))
        {
            Assert.Inconclusive("Windows required.");
            return;
        }

        using var session = new LoopbackCaptureSession();
        var total = 0;
        session.PcmDataAvailable += (_, e) => total += e.PcmSamples.Length;

        try
        {
            session.Start(null);
        }
        catch (InvalidOperationException ex)
        {
            Assert.Inconclusive("No render endpoint: " + ex.Message);
            return;
        }
        catch (ArgumentException ex)
        {
            Assert.Inconclusive("WASAPI loopback init failed (offline/headless/unsupported audio): " + ex.Message);
            return;
        }

        Assert.IsNotNull(session.RecordingWaveFormat);
        Thread.Sleep(400);
        session.Stop();

        if (total == 0)
        {
            Assert.Inconclusive(
                "Loopback produced no PCM in the window; this is typical when nothing is playing on the default render device.");
        }

        Assert.IsTrue(total > 0);
    }
}
