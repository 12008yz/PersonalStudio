using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScreenRecorder.RecordingEngine.MediaFoundation;

namespace ScreenRecorder.RecordingEngine.Tests;

[TestClass]
public sealed class Mp4SinkWriterTests
{
    [TestMethod]
    public void Create_writes_playable_mp4_with_synthetic_nv12_and_pcm()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"ScreenRecorder_Mp4SinkWriter_{Guid.NewGuid():N}.mp4");
        const int width = 640;
        const int height = 360;
        const int fps = 30;
        const double durationSeconds = 2.0;
        var frameCount = (int)(durationSeconds * fps);

        var configuration = new Mp4SinkWriterConfiguration
        {
            Width = width,
            Height = height,
            FramesPerSecond = fps,
            VideoBitrateBps = 2_000_000,
            AudioSampleRateHz = 48_000,
            AudioChannels = 2,
            AudioBitrateBps = 128_000,
        };

        try
        {
            using var writer = Mp4SinkWriter.Create(outputPath, configuration);
            var nv12 = Nv12SolidColorBuffer.Create(width, height, y: 81, u: 90, v: 240);
            var samplesPerFrame = configuration.AudioSampleRateHz / fps;
            var pcmBytesPerFrame = samplesPerFrame * configuration.AudioChannels * 2;
            var pcm = CreateSinePcm16(pcmBytesPerFrame, frequencyHz: 440, sampleRateHz: configuration.AudioSampleRateHz);

            var expectedDurationHns = (long)(durationSeconds * 10_000_000);
            var lastTimestampHns = 0L;

            for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                var timestampHns = frameIndex * writer.VideoFrameDurationHns;
                lastTimestampHns = timestampHns;
                writer.WriteVideoFrame(nv12, timestampHns);
                writer.WriteAudioPcm16(pcm, timestampHns, writer.VideoFrameDurationHns);
            }

            Assert.AreEqual(10_000_000 / fps, writer.VideoFrameDurationHns);
            Assert.IsTrue(lastTimestampHns + writer.VideoFrameDurationHns <= expectedDurationHns + writer.VideoFrameDurationHns);

            writer.FinalizeWriting();
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                var info = new FileInfo(outputPath);
                Assert.IsTrue(info.Length > 16 * 1024, "MP4 output looks too small.");
                try
                {
                    File.Delete(outputPath);
                }
                catch
                {
                    // Temp cleanup is best-effort.
                }
            }
            else
            {
                Assert.Fail("MP4 file was not created.");
            }
        }
    }

    private static byte[] CreateSinePcm16(int byteCount, double frequencyHz, int sampleRateHz)
    {
        var buffer = new byte[byteCount];
        var sampleCount = byteCount / 2;
        for (var i = 0; i < sampleCount; i++)
        {
            var t = (double)i / sampleRateHz;
            var value = (short)(Math.Sin(2 * Math.PI * frequencyHz * t) * 12_000);
            buffer[i * 2] = (byte)(value & 0xFF);
            buffer[i * 2 + 1] = (byte)((value >> 8) & 0xFF);
        }

        return buffer;
    }
}
