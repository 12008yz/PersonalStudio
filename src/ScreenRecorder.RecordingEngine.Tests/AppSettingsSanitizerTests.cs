using ScreenRecorder.RecordingEngine;
using ScreenRecorder.RecordingEngine.Settings;

namespace ScreenRecorder.RecordingEngine.Tests;

[TestClass]
public sealed class AppSettingsSanitizerTests
{
    [TestMethod]
    public void Sanitize_EndpointIdWithNewline_ClearsValue()
    {
        var input = new AppSettings { PreferredMicrophoneEndpointId = "bad\nid" };
        var sanitized = AppSettingsSanitizer.Sanitize(input);
        Assert.IsNull(sanitized.PreferredMicrophoneEndpointId);
    }

    [TestMethod]
    public void Sanitize_EndpointIdTooLong_ClearsValue()
    {
        var longId = new string('a', 4096);
        var input = new AppSettings { PreferredLoopbackRenderEndpointId = longId };
        var sanitized = AppSettingsSanitizer.Sanitize(input);
        Assert.IsNull(sanitized.PreferredLoopbackRenderEndpointId);
    }

    [TestMethod]
    public void Sanitize_ValidEndpointId_Preserved()
    {
        var input = new AppSettings { PreferredMicrophoneEndpointId = @"{GUID}" };
        var sanitized = AppSettingsSanitizer.Sanitize(input);
        Assert.AreEqual(@"{GUID}", sanitized.PreferredMicrophoneEndpointId);
    }

    [TestMethod]
    public void AppSettingsDefault_MatchesAcousticUxSpecMonitoringDefault()
    {
        Assert.AreEqual(
            RecordingAcousticUxSpec.AudioPassthroughMonitoringDefaultEnabled,
            AppSettings.Default.AudioPassthroughMonitoringEnabled);
        Assert.IsFalse(RecordingAcousticUxSpec.AudioDuckingInMvp);
    }

    [TestMethod]
    public void Sanitize_PreservesAudioPassthroughMonitoringEnabled()
    {
        var on = AppSettingsSanitizer.Sanitize(new AppSettings { AudioPassthroughMonitoringEnabled = true });
        Assert.IsTrue(on.AudioPassthroughMonitoringEnabled);

        var off = AppSettingsSanitizer.Sanitize(new AppSettings());
        Assert.IsFalse(off.AudioPassthroughMonitoringEnabled);
    }
}
