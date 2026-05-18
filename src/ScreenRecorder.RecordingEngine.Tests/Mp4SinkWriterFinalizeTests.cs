using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScreenRecorder.RecordingEngine.MediaFoundation;

namespace ScreenRecorder.RecordingEngine.Tests;

[TestClass]
public sealed class Mp4SinkWriterFinalizeTests
{
    [TestMethod]
    public void Shutdown_complete_after_clip_retains_playable_file()
    {
        var outputPath = CreateTempOutputPath();
        var configuration = CreateDefaultConfiguration();

        Mp4SinkWriterShutdownResult result;
        using (var writer = Mp4SinkWriter.Create(outputPath, configuration))
        {
            WriteOneSecondOfMedia(writer, configuration, frameCount: 30);
            result = writer.Shutdown(Mp4SinkWriterShutdownKind.Complete);
        }

        Assert.IsTrue(result.FinalizeSucceeded);
        Assert.IsTrue(result.HasWrittenSamples);
        Assert.IsTrue(result.OutputFileRetained);
        Assert.IsFalse(result.OutputFileDeleted);
        AssertMp4LooksPlayable(outputPath);
        TryDelete(outputPath);
    }

    [TestMethod]
    public void Shutdown_complete_without_samples_deletes_empty_output()
    {
        var outputPath = CreateTempOutputPath();
        var configuration = CreateDefaultConfiguration();

        Mp4SinkWriterShutdownResult result;
        using (var writer = Mp4SinkWriter.Create(outputPath, configuration))
        {
            result = writer.Shutdown(Mp4SinkWriterShutdownKind.Complete);
        }

        Assert.IsFalse(result.HasWrittenSamples);
        Assert.IsTrue(result.OutputFileDeleted);
        Assert.IsFalse(result.OutputFileRetained);
        Assert.IsFalse(File.Exists(outputPath));
    }

    [TestMethod]
    public void Shutdown_abort_after_partial_clip_retains_file_when_finalize_succeeds()
    {
        var outputPath = CreateTempOutputPath();
        var configuration = CreateDefaultConfiguration();

        Mp4SinkWriterShutdownResult result;
        using (var writer = Mp4SinkWriter.Create(outputPath, configuration))
        {
            WriteOneSecondOfMedia(writer, configuration, frameCount: 15);
            result = writer.Shutdown(Mp4SinkWriterShutdownKind.AbortDueToError);
        }

        Assert.IsTrue(result.FinalizeSucceeded);
        Assert.IsTrue(result.HasWrittenSamples);
        Assert.IsTrue(result.OutputFileRetained);
        Assert.IsFalse(result.OutputFileDeleted);
        AssertMp4LooksPlayable(outputPath);
        TryDelete(outputPath);
    }

    [TestMethod]
    public void Shutdown_abort_without_samples_deletes_output()
    {
        var outputPath = CreateTempOutputPath();
        var configuration = CreateDefaultConfiguration();

        var result = ShutdownWithoutUsing(outputPath, configuration, Mp4SinkWriterShutdownKind.AbortDueToError);

        Assert.IsFalse(result.HasWrittenSamples);
        Assert.IsTrue(result.OutputFileDeleted);
        Assert.IsFalse(File.Exists(outputPath));
    }

    [TestMethod]
    public void Dispose_after_partial_clip_retains_file_via_abort_policy()
    {
        var outputPath = CreateTempOutputPath();
        var configuration = CreateDefaultConfiguration();

        using (var writer = Mp4SinkWriter.Create(outputPath, configuration))
        {
            WriteOneSecondOfMedia(writer, configuration, frameCount: 30);
        }

        Assert.IsTrue(File.Exists(outputPath));
        AssertMp4LooksPlayable(outputPath);
        TryDelete(outputPath);
    }

    [TestMethod]
    public void Shutdown_called_twice_is_idempotent_and_keeps_file()
    {
        var outputPath = CreateTempOutputPath();
        var configuration = CreateDefaultConfiguration();

        var writer = Mp4SinkWriter.Create(outputPath, configuration);
        WriteOneSecondOfMedia(writer, configuration, frameCount: 30);

        var first = writer.Shutdown(Mp4SinkWriterShutdownKind.Complete);
        var second = writer.Shutdown(Mp4SinkWriterShutdownKind.Complete);

        Assert.IsTrue(first.FinalizeSucceeded);
        Assert.IsTrue(first.OutputFileRetained);
        Assert.IsTrue(second.FinalizeSucceeded);
        Assert.IsTrue(second.OutputFileRetained);
        Assert.IsFalse(second.OutputFileDeleted);
        writer.Dispose();
        AssertMp4LooksPlayable(outputPath);
        TryDelete(outputPath);
    }

    [TestMethod]
    public void Shutdown_complete_then_dispose_does_not_remove_file()
    {
        var outputPath = CreateTempOutputPath();
        var configuration = CreateDefaultConfiguration();

        using (var writer = Mp4SinkWriter.Create(outputPath, configuration))
        {
            WriteOneSecondOfMedia(writer, configuration, frameCount: 30);
            writer.Shutdown(Mp4SinkWriterShutdownKind.Complete);
        }

        Assert.IsTrue(File.Exists(outputPath));
        AssertMp4LooksPlayable(outputPath);
        TryDelete(outputPath);
    }

    [TestMethod]
    public void FinalizeWriting_still_supported_before_shutdown()
    {
        var outputPath = CreateTempOutputPath();
        var configuration = CreateDefaultConfiguration();

        using (var writer = Mp4SinkWriter.Create(outputPath, configuration))
        {
            WriteOneSecondOfMedia(writer, configuration, frameCount: 30);
            writer.FinalizeWriting();
            Assert.IsTrue(writer.IsFinalized);
            var result = writer.Shutdown(Mp4SinkWriterShutdownKind.Complete);
            Assert.IsTrue(result.FinalizeSucceeded);
            Assert.IsTrue(result.OutputFileRetained);
        }

        AssertMp4LooksPlayable(outputPath);
        TryDelete(outputPath);
    }

    private static Mp4SinkWriterShutdownResult ShutdownWithoutUsing(
        string outputPath,
        Mp4SinkWriterConfiguration configuration,
        Mp4SinkWriterShutdownKind kind)
    {
        var writer = Mp4SinkWriter.Create(outputPath, configuration);
        return writer.Shutdown(kind);
    }

    private static void WriteOneSecondOfMedia(Mp4SinkWriter writer, Mp4SinkWriterConfiguration configuration, int frameCount)
    {
        var nv12 = Nv12SolidColorBuffer.Create(configuration.Width, configuration.Height, y: 81, u: 90, v: 240);
        var samplesPerFrame = configuration.AudioSampleRateHz / configuration.FramesPerSecond;
        var pcmBytesPerFrame = samplesPerFrame * configuration.AudioChannels * 2;
        var pcm = new byte[pcmBytesPerFrame];

        for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            var timestampHns = frameIndex * writer.VideoFrameDurationHns;
            writer.WriteVideoFrame(nv12, timestampHns);
            writer.WriteAudioPcm16(pcm, timestampHns, writer.VideoFrameDurationHns);
        }
    }

    private static Mp4SinkWriterConfiguration CreateDefaultConfiguration() =>
        new()
        {
            Width = 320,
            Height = 180,
            FramesPerSecond = 30,
            VideoBitrateBps = 1_500_000,
        };

    private static string CreateTempOutputPath() =>
        Path.Combine(Path.GetTempPath(), $"ScreenRecorder_Mp4Finalize_{Guid.NewGuid():N}.mp4");

    private static void AssertMp4LooksPlayable(string outputPath)
    {
        Assert.IsTrue(File.Exists(outputPath));
        var info = new FileInfo(outputPath);
        Assert.IsTrue(info.Length > 8 * 1024, "MP4 output looks too small.");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort temp cleanup.
        }
    }
}
