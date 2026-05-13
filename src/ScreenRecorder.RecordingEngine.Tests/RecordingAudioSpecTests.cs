namespace ScreenRecorder.RecordingEngine.Tests;

[TestClass]
public sealed class RecordingAudioSpecTests
{
    [TestMethod]
    public void NominalSampleRateHz_Is48K()
    {
        Assert.AreEqual(48_000, RecordingAudioSpec.NominalSampleRateHz);
    }

    [TestMethod]
    public void MvpMp4AudioTrackLayout_IsSingleMixedStereoAacLc()
    {
        Assert.AreEqual(Mp4AudioTrackLayout.SingleMixedStereoAacLc, RecordingAudioSpec.MvpMp4AudioTrackLayout);
    }
}
