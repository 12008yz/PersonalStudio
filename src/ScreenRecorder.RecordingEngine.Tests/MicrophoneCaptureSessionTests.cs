using ScreenRecorder.RecordingEngine.Audio;

namespace ScreenRecorder.RecordingEngine.Tests;

[TestClass]
public sealed class MicrophoneCaptureSessionTests
{
    [TestMethod]
    public void Start_Twice_ThrowsInvalidOperation()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10))
            Assert.Inconclusive("Windows required.");

        using var session = new MicrophoneCaptureSession();
        try
        {
            session.Start(null);
        }
        catch (InvalidOperationException ex)
        {
            Assert.Inconclusive("No capture endpoint: " + ex.Message);
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
    public void PcmDataAvailable_DeliversBytes_OnDefaultDeviceWhenPresent()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10))
        {
            Assert.Inconclusive("Windows required.");
            return;
        }

        using var session = new MicrophoneCaptureSession();
        var total = 0;
        session.PcmDataAvailable += (_, e) => total += e.PcmSamples.Length;

        try
        {
            session.Start(null);
        }
        catch (InvalidOperationException ex)
        {
            Assert.Inconclusive("No capture endpoint: " + ex.Message);
            return;
        }

        Assert.IsNotNull(session.RecordingWaveFormat);
        Thread.Sleep(400);
        session.Stop();
        Assert.IsTrue(total > 0, "Expected PCM from default microphone in shared mode.");
    }
}
