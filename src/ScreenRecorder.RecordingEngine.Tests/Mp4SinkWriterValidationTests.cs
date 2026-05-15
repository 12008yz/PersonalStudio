using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScreenRecorder.RecordingEngine.MediaFoundation;

namespace ScreenRecorder.RecordingEngine.Tests;

[TestClass]
public sealed class Mp4SinkWriterValidationTests
{
    [TestMethod]
    public void Validate_rejects_odd_video_dimensions()
    {
        var configuration = new Mp4SinkWriterConfiguration { Width = 641, Height = 360 };
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => configuration.Validate());
    }

    [TestMethod]
    public void WriteVideoFrame_rejects_wrong_nv12_size()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"ScreenRecorder_Mp4SinkWriter_{Guid.NewGuid():N}.mp4");
        var configuration = new Mp4SinkWriterConfiguration
        {
            Width = 640,
            Height = 360,
            FramesPerSecond = 30,
        };

        try
        {
            using var writer = Mp4SinkWriter.Create(outputPath, configuration);
            Assert.ThrowsException<ArgumentException>(() =>
                writer.WriteVideoFrame(new byte[100], timestampHns: 0));
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                try
                {
                    File.Delete(outputPath);
                }
                catch
                {
                    // Best-effort cleanup.
                }
            }
        }
    }
}
