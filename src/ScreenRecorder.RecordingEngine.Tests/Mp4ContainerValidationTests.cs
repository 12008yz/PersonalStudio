using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScreenRecorder.RecordingEngine.MediaFoundation;

namespace ScreenRecorder.RecordingEngine.Tests;

[TestClass]
public sealed class Mp4ContainerValidationTests
{
    [TestMethod]
    public void HasFtypBox_detects_mp4_signature()
    {
        Span<byte> header = stackalloc byte[12];
        "ftyp"u8.CopyTo(header[4..]);
        Assert.IsTrue(Mp4ContainerValidation.HasFtypBox(header));
    }

    [TestMethod]
    public void HasFtypBox_rejects_non_mp4_header()
    {
        Span<byte> header = stackalloc byte[12];
        header.Clear();
        Assert.IsFalse(Mp4ContainerValidation.HasFtypBox(header));
    }
}
