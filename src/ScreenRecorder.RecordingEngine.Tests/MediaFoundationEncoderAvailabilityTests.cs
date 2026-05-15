using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScreenRecorder.RecordingEngine.MediaFoundation;

namespace ScreenRecorder.RecordingEngine.Tests;

[TestClass]
public sealed class MediaFoundationEncoderAvailabilityTests
{
    [TestMethod]
    public void Probe_reports_sufficient_encoders_on_windows()
    {
        var availability = MediaFoundationEncoderAvailability.Probe();

        Assert.IsTrue(availability.H264VideoEncoderCount > 0);
        Assert.IsTrue(availability.AacAudioEncoderCount > 0);
        Assert.IsTrue(availability.IsSufficientForRecording);
    }
}
