using System.Runtime.InteropServices;
using ScreenRecorder.RecordingEngine.Capture;

namespace ScreenRecorder.RecordingEngine.Tests;

[TestClass]
public sealed class ScreenCaptureFailureClassifierAggregateTests
{
    [TestMethod]
    public void Classify_Aggregate_FirstKnownInner_Wins()
    {
        var inner = new COMException("e", unchecked((int)0x80070005));
        var agg = new AggregateException("wrap", new Exception("noise"), inner);
        Assert.AreEqual(ScreenCaptureFailureKind.AccessDenied, ScreenCaptureFailureClassifier.Classify(agg));
    }
}
