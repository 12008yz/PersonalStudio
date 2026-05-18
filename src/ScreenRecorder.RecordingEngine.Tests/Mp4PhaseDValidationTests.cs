using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScreenRecorder.RecordingEngine;
using ScreenRecorder.RecordingEngine.MediaFoundation;

namespace ScreenRecorder.RecordingEngine.Tests;

/// <summary>
/// Автоматическая часть критерия «Готово» фазы D: синтетический A/V → MP4, ftyp, отчёт MFT.
/// Прослушивание синхрона — <c>docs/PHASE_D_MP4_MANUAL_VALIDATION_CHECKLIST.md</c>.
/// </summary>
[TestClass]
public sealed class Mp4PhaseDValidationTests
{
    public TestContext? TestContext { get; set; }

    [TestMethod]
    public void Synthetic_av_mp4_passes_container_validation_and_encoder_report()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"ScreenRecorder_PhaseD_{Guid.NewGuid():N}.mp4");
        var configuration = new Mp4SinkWriterConfiguration
        {
            Width = RecordingNfrSpec.ReferenceMaxWidth,
            Height = RecordingNfrSpec.ReferenceMaxHeight,
            FramesPerSecond = RecordingNfrSpec.ReferenceFramesPerSecond,
            VideoBitrateBps = 4_000_000,
            VideoRateControlMode = RecordingVideoEncodingSpec.DefaultRateControlMode,
            VideoKeyframeIntervalSeconds = RecordingVideoEncodingSpec.DefaultKeyframeIntervalSeconds,
            AudioSampleRateHz = RecordingAudioSpec.NominalSampleRateHz,
            AudioChannels = 2,
            AudioBitrateBps = 192_000,
        };

        var report = MediaFoundationEncoderReport.Probe();
        TestContext?.WriteLine(report.ToDiagnosticMultiline());

        Assert.IsTrue(report.IsSufficientForRecording, report.ToDiagnosticMultiline());

        try
        {
            using (var writer = Mp4SinkWriter.Create(outputPath, configuration))
            {
                WriteFiveSecondAvClip(writer, configuration);
                var shutdown = writer.Shutdown(Mp4SinkWriterShutdownKind.Complete);
                Assert.IsTrue(shutdown.FinalizeSucceeded);
                Assert.IsTrue(shutdown.OutputFileRetained);
            }

            Assert.IsTrue(
                Mp4ContainerValidation.TryValidatePlayableFile(outputPath, out var error),
                error);

            TestContext?.WriteLine($"Phase D validation MP4: {outputPath}");
        }
        finally
        {
            if (!ShouldKeepPhaseDOutputForManualValidation())
                TryDelete(outputPath);
            else
                TestContext?.WriteLine($"Kept for manual validation (SCREENRECORDER_KEEP_PHASED_MP4=1): {outputPath}");
        }
    }

    private static bool ShouldKeepPhaseDOutputForManualValidation() =>
        string.Equals(
            Environment.GetEnvironmentVariable("SCREENRECORDER_KEEP_PHASED_MP4"),
            "1",
            StringComparison.Ordinal);

    [TestMethod]
    public void Encoder_report_lists_h264_and_aac_on_windows()
    {
        var report = MediaFoundationEncoderReport.Probe();
        TestContext?.WriteLine(report.ToDiagnosticMultiline());

        Assert.IsTrue(report.H264Encoders.Count > 0);
        Assert.IsTrue(report.AacEncoders.Count > 0);
    }

    private static void WriteFiveSecondAvClip(Mp4SinkWriter writer, Mp4SinkWriterConfiguration configuration)
    {
        var frameCount = configuration.FramesPerSecond * 5;
        var nv12 = Nv12SolidColorBuffer.Create(configuration.Width, configuration.Height, y: 90, u: 128, v: 128);
        var samplesPerFrame = configuration.AudioSampleRateHz / configuration.FramesPerSecond;
        var pcmBytesPerFrame = samplesPerFrame * configuration.AudioChannels * 2;
        var pcm = CreateSinePcm16(pcmBytesPerFrame, frequencyHz: 440, sampleRateHz: configuration.AudioSampleRateHz);

        for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            var timestampHns = frameIndex * writer.VideoFrameDurationHns;
            writer.WriteVideoFrame(nv12, timestampHns);
            writer.WriteAudioPcm16(pcm, timestampHns, writer.VideoFrameDurationHns);
        }
    }

    private static byte[] CreateSinePcm16(int byteCount, double frequencyHz, int sampleRateHz)
    {
        var buffer = new byte[byteCount];
        var sampleCount = byteCount / 2;
        for (var i = 0; i < sampleCount; i++)
        {
            var t = (double)i / sampleRateHz;
            var value = (short)(Math.Sin(2 * Math.PI * frequencyHz * t) * 10_000);
            buffer[i * 2] = (byte)(value & 0xFF);
            buffer[i * 2 + 1] = (byte)((value >> 8) & 0xFF);
        }

        return buffer;
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
            // Best-effort.
        }
    }
}
