using System.Diagnostics;
using NAudio.Wave;
using ScreenRecorder.RecordingEngine.Audio;
using ScreenRecorder.RecordingEngine.Recording;

namespace ScreenRecorder.RecordingEngine.Tests;

[TestClass]
public sealed class RecordingSessionTimebaseTests
{
    [TestMethod]
    public void Establish_SetsOrigin_AndResetsAudioClocks()
    {
        var timebase = new RecordingSessionTimebase();
        timebase.Establish();

        Assert.IsTrue(timebase.IsEstablished);
        var origin = timebase.OriginQpcTicks;
        Assert.IsTrue(origin > 0);

        timebase.MicrophoneClock.Allocate(480);
        timebase.MicrophoneClock.Reset();
        var (start, duration) = timebase.MicrophoneClock.Allocate(480);
        Assert.AreEqual(0, start);
        Assert.AreEqual(100_000, duration); // 480 samples @ 48 kHz = 10 ms
    }

    [TestMethod]
    public void QpcToMediaTimestampHns_IsMonotonic_ForLaterQpc()
    {
        var timebase = new RecordingSessionTimebase();
        timebase.Establish();
        var origin = timebase.OriginQpcTicks;

        var t0 = timebase.QpcToMediaTimestampHns(origin);
        var later = Stopwatch.GetTimestamp();
        var t1 = timebase.QpcToMediaTimestampHns(later);

        Assert.AreEqual(0, t0);
        Assert.IsTrue(t1 >= t0);
    }

    [TestMethod]
    public void EstimateCaptureQpcTicks_SubtractsLatency()
    {
        var handler = Stopwatch.GetTimestamp();
        var latency = TimeSpan.FromMilliseconds(8);
        var capture = RecordingSessionTimebase.EstimateCaptureQpcTicks(handler, latency);
        var captureMedia = Stopwatch.GetElapsedTime(handler, capture);

        Assert.IsTrue(capture < handler);
        Assert.AreEqual(-latency.Ticks, captureMedia.Ticks, TimeSpan.FromMilliseconds(1).Ticks);
    }

    [TestMethod]
    public void VideoCaptureToMediaTimestampHns_MatchesQpcConversion()
    {
        var timebase = new RecordingSessionTimebase();
        timebase.Establish();
        var handler = Stopwatch.GetTimestamp();
        var latency = TimeSpan.FromMilliseconds(5);

        var viaVideo = timebase.VideoCaptureToMediaTimestampHns(handler, latency);
        var viaQpc = timebase.QpcToMediaTimestampHns(
            RecordingSessionTimebase.EstimateCaptureQpcTicks(handler, latency));

        Assert.AreEqual(viaQpc, viaVideo);
    }

    [TestMethod]
    public void AudioTrackClock_Allocations_AreContiguous()
    {
        var clock = new RecordingSessionAudioTrackClock();
        var (aStart, aDur) = clock.Allocate(480);
        var (bStart, bDur) = clock.Allocate(960);

        Assert.AreEqual(0, aStart);
        Assert.AreEqual(100_000, aDur);
        Assert.AreEqual(100_000, bStart);
        Assert.AreEqual(200_000, bDur);
    }

    [TestMethod]
    public void AudioTrackClock_FirstAllocate_WithTimebase_SkipsStartupGap()
    {
        var timebase = new RecordingSessionTimebase();
        timebase.Establish();
        Thread.Sleep(25);

        var (start, duration) = timebase.MicrophoneClock.Allocate(480, timebase);

        Assert.IsTrue(start >= 150_000, $"Expected >=15 ms wall offset, got {start} hns");
        Assert.AreEqual(100_000, duration);
    }

    [TestMethod]
    public void VideoAndAudio_FirstMarks_AreWithinReasonableSkew_AfterStartupDelay()
    {
        var timebase = new RecordingSessionTimebase();
        timebase.Establish();
        Thread.Sleep(20);

        var handler = Stopwatch.GetTimestamp();
        var videoHns = timebase.VideoCaptureToMediaTimestampHns(handler, TimeSpan.FromMilliseconds(4));
        var (audioStart, _) = timebase.MicrophoneClock.Allocate(480, timebase);

        var skewMs = Math.Abs(videoHns - audioStart) / 10_000.0;
        Assert.IsTrue(skewMs < 30, $"Video/audio skew {skewMs:F1} ms exceeds 30 ms tolerance");
    }

    [TestMethod]
    public void PcmTiming_CountSamples_IeeeFloatStereo()
    {
        var fmt = WaveFormat.CreateIeeeFloatWaveFormat(48_000, 2);
        var pcm = new byte[fmt.AverageBytesPerSecond / 10];
        var samples = RecordingSessionPcmTiming.CountSamples(pcm, fmt);
        Assert.AreEqual(pcm.Length / (fmt.Channels * 4), samples);
    }

    [TestMethod]
    public void MicAndLoopback_ForwardPcm_StampedWhenTimebaseBound()
    {
        var timebase = new RecordingSessionTimebase();
        timebase.Establish();
        using var duo = new MicAndLoopbackCaptureSession();
        duo.BindSessionTimebase(timebase);

        SourcedPcmCaptureDataAvailableEventArgs? micArgs = null;
        duo.PcmDataAvailable += (_, e) =>
        {
            if (e.SourceKind == PcmCaptureSourceKind.Microphone)
                micArgs = e;
        };

        var fmt = WaveFormat.CreateIeeeFloatWaveFormat(RecordingAudioSpec.NominalSampleRateHz, 2);
        var pcm = new byte[fmt.AverageBytesPerSecond / 100];

        typeof(MicAndLoopbackCaptureSession)
            .GetMethod("AttachForwardingHandlers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(duo, null);

        typeof(MicAndLoopbackCaptureSession)
            .GetMethod("OnMicrophonePcmDataAvailable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(duo, [duo.Microphone, new PcmCaptureDataAvailableEventArgs(pcm, fmt)]);

        Assert.IsNotNull(micArgs);
        Assert.IsTrue(micArgs!.HasSessionTiming);
        Assert.IsTrue(micArgs.SessionMediaTimestampHns >= 0);
        Assert.IsTrue(micArgs.SessionMediaDurationHns > 0);
    }
}
