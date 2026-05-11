using System.Runtime.InteropServices;
using ScreenRecorder.RecordingEngine.Capture;

namespace ScreenRecorder.RecordingEngine.Tests;

[TestClass]
public sealed class ScreenCaptureFailureClassifierTests
{
    [TestMethod]
    public void Classify_Null_ReturnsUnknown()
    {
        Assert.AreEqual(ScreenCaptureFailureKind.Unknown, ScreenCaptureFailureClassifier.Classify(null));
    }

    [TestMethod]
    public void Classify_AccessDenied_Com()
    {
        var ex = new COMException("denied", unchecked((int)0x80070005));
        Assert.AreEqual(ScreenCaptureFailureKind.AccessDenied, ScreenCaptureFailureClassifier.Classify(ex));
    }

    [TestMethod]
    public void Classify_AccessDenied_Unauthorized()
    {
        Assert.AreEqual(
            ScreenCaptureFailureKind.AccessDenied,
            ScreenCaptureFailureClassifier.Classify(new UnauthorizedAccessException()));
    }

    [TestMethod]
    public void Classify_Busy_Com()
    {
        var ex = new COMException("busy", unchecked((int)0x800700AA));
        Assert.AreEqual(ScreenCaptureFailureKind.ResourceBusy, ScreenCaptureFailureClassifier.Classify(ex));
    }

    [TestMethod]
    public void Classify_DxgiAccessLost()
    {
        var ex = new COMException("lost", unchecked((int)0x887A0026));
        Assert.AreEqual(ScreenCaptureFailureKind.AccessLostOrDeviceFailed, ScreenCaptureFailureClassifier.Classify(ex));
    }

    [TestMethod]
    public void Classify_InnerCom_WalksChain()
    {
        var inner = new COMException("e", unchecked((int)0x80070005));
        var outer = new InvalidOperationException("wrap", inner);
        Assert.AreEqual(ScreenCaptureFailureKind.AccessDenied, ScreenCaptureFailureClassifier.Classify(outer));
    }

    [TestMethod]
    public void Classify_Unknown_Win32()
    {
        var ex = new COMException("other", unchecked((int)0x80004005));
        Assert.AreEqual(ScreenCaptureFailureKind.Unknown, ScreenCaptureFailureClassifier.Classify(ex));
    }
}
