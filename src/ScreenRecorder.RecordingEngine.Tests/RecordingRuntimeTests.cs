using ScreenRecorder.RecordingEngine.Recording;

namespace ScreenRecorder.RecordingEngine.Tests;

[TestClass]
public sealed class RecordingRuntimeTests
{
    [TestMethod]
    public async Task StartAsync_TransitionsToRecording_AndStartsBothSessions()
    {
        var frame = new FakeFrameCaptureSession();
        var audio = new FakeAudioCaptureSession();
        using var runtime = new RecordingRuntime(() => frame, () => audio);

        await runtime.StartAsync(new RecordingSessionOptions((nint)42, "mic-id", "loop-id"));

        Assert.AreEqual(RecordingLifecycleState.Recording, runtime.State);
        Assert.IsNotNull(runtime.SessionTimebase);
        Assert.IsTrue(runtime.SessionTimebase!.IsEstablished);
        Assert.AreEqual((nint)42, frame.StartedMonitorHandle);
        Assert.AreEqual("mic-id", audio.StartedMicrophoneEndpointId);
        Assert.AreEqual("loop-id", audio.StartedLoopbackEndpointId);
        Assert.IsTrue(frame.TimebaseBound);
        Assert.IsTrue(audio.TimebaseBound);
    }

    [TestMethod]
    public async Task StartAsync_WhenAlreadyRecording_ThrowsInvalidOperation()
    {
        var frame = new FakeFrameCaptureSession();
        var audio = new FakeAudioCaptureSession();
        using var runtime = new RecordingRuntime(() => frame, () => audio);
        var options = new RecordingSessionOptions((nint)7, null, null);

        await runtime.StartAsync(options);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => runtime.StartAsync(options));
    }

    [TestMethod]
    public async Task StopAsync_FromRecording_StopsBothSessions_AndReturnsIdle()
    {
        var frame = new FakeFrameCaptureSession();
        var audio = new FakeAudioCaptureSession();
        using var runtime = new RecordingRuntime(() => frame, () => audio);

        await runtime.StartAsync(new RecordingSessionOptions((nint)15, null, null));
        await runtime.StopAsync();

        Assert.AreEqual(RecordingLifecycleState.Idle, runtime.State);
        Assert.IsNull(runtime.SessionTimebase);
        Assert.AreEqual(1, frame.StopCallCount);
        Assert.AreEqual(1, audio.StopCallCount);
    }

    [TestMethod]
    public async Task StartAsync_WhenAudioStartFails_RollsBackFrame_AndReturnsIdle()
    {
        var frame = new FakeFrameCaptureSession();
        var audio = new FakeAudioCaptureSession
        {
            ThrowOnStart = new InvalidOperationException("audio init failed"),
        };
        using var runtime = new RecordingRuntime(() => frame, () => audio);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            runtime.StartAsync(new RecordingSessionOptions((nint)31, null, null)));

        Assert.AreEqual(RecordingLifecycleState.Idle, runtime.State);
        Assert.AreEqual(1, frame.StopCallCount);
        Assert.AreEqual(1, audio.StopCallCount);
    }

    [TestMethod]
    public async Task StartAsync_WithExternalCancellation_TransitionsBackToIdle()
    {
        var frame = new FakeFrameCaptureSession();
        var audio = new FakeAudioCaptureSession();
        using var cts = new CancellationTokenSource();
        using var runtime = new RecordingRuntime(() => frame, () => audio);

        await runtime.StartAsync(new RecordingSessionOptions((nint)64, null, null), cts.Token);
        cts.Cancel();

        await Task.Delay(50);

        Assert.AreEqual(RecordingLifecycleState.Idle, runtime.State);
        Assert.AreEqual(1, frame.StopCallCount);
        Assert.AreEqual(1, audio.StopCallCount);
    }

    [TestMethod]
    public async Task StartAsync_WithAlreadyCancelledToken_ThrowsAndStaysIdle()
    {
        var frame = new FakeFrameCaptureSession();
        var audio = new FakeAudioCaptureSession();
        using var cts = new CancellationTokenSource();
        using var runtime = new RecordingRuntime(() => frame, () => audio);
        cts.Cancel();

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() =>
            runtime.StartAsync(new RecordingSessionOptions((nint)1, null, null), cts.Token));

        Assert.AreEqual(RecordingLifecycleState.Idle, runtime.State);
        Assert.AreEqual(0, frame.StopCallCount);
        Assert.AreEqual(0, audio.StopCallCount);
    }

    private sealed class FakeFrameCaptureSession : RecordingRuntime.IFrameCaptureSession, IRecordingSessionTimebaseConsumer
    {
        public nint StartedMonitorHandle { get; private set; }

        public int StopCallCount { get; private set; }

        public bool TimebaseBound { get; private set; }

        public void BindSessionTimebase(RecordingSessionTimebase timebase) => TimebaseBound = true;

        public void Start(nint monitorHandle)
        {
            StartedMonitorHandle = monitorHandle;
        }

        public void Stop()
        {
            StopCallCount++;
        }
    }

    private sealed class FakeAudioCaptureSession : RecordingRuntime.IAudioCaptureSession, IRecordingSessionTimebaseConsumer
    {
        public string? StartedMicrophoneEndpointId { get; private set; }

        public string? StartedLoopbackEndpointId { get; private set; }

        public int StopCallCount { get; private set; }

        public bool TimebaseBound { get; private set; }

        public Exception? ThrowOnStart { get; init; }

        public void BindSessionTimebase(RecordingSessionTimebase timebase) => TimebaseBound = true;

        public void Start(string? microphoneEndpointId, string? loopbackRenderEndpointId)
        {
            if (ThrowOnStart is not null)
                throw ThrowOnStart;

            StartedMicrophoneEndpointId = microphoneEndpointId;
            StartedLoopbackEndpointId = loopbackRenderEndpointId;
        }

        public void Stop()
        {
            StopCallCount++;
        }
    }
}
