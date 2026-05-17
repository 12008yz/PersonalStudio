using ScreenRecorder.RecordingEngine.Recording;

namespace ScreenRecorder.RecordingEngine.Tests;

[TestClass]
public sealed class RecordingSessionTests
{
    [TestMethod]
    public async Task StartAsync_DelegatesToRuntime_AndStoresOptions()
    {
        var runtime = new FakeRecordingRuntime();
        using var session = new RecordingSession(runtime);
        var options = new RecordingSessionOptions((nint)11, "mic-1", "loop-1");

        await session.StartAsync(options);

        Assert.AreEqual(RecordingLifecycleState.Recording, session.State);
        Assert.AreEqual(options, session.CurrentOptions);
        Assert.AreEqual(1, runtime.StartCallCount);
        Assert.AreEqual(options, runtime.LastStartedOptions);
    }

    [TestMethod]
    public async Task StopAsync_DelegatesToRuntime_AndClearsOptions()
    {
        var runtime = new FakeRecordingRuntime();
        using var session = new RecordingSession(runtime);
        await session.StartAsync(new RecordingSessionOptions((nint)12, null, null));

        await session.StopAsync();

        Assert.AreEqual(RecordingLifecycleState.Idle, session.State);
        Assert.IsNull(session.CurrentOptions);
        Assert.AreEqual(1, runtime.StopCallCount);
    }

    [TestMethod]
    public async Task ChangeAudioDevicesAsync_WhenRecording_RestartsWithUpdatedAudioOptions()
    {
        var runtime = new FakeRecordingRuntime();
        using var session = new RecordingSession(runtime);
        await session.StartAsync(new RecordingSessionOptions((nint)13, "mic-a", "loop-a"));

        await session.ChangeAudioDevicesAsync("mic-b", "loop-b");

        Assert.AreEqual(2, runtime.StartCallCount);
        Assert.AreEqual(1, runtime.StopCallCount);
        Assert.AreEqual("mic-b", session.CurrentOptions?.PreferredMicrophoneEndpointId);
        Assert.AreEqual("loop-b", session.CurrentOptions?.PreferredLoopbackRenderEndpointId);
    }

    [TestMethod]
    public async Task ChangeMonitorAsync_WhenRecording_RestartsWithUpdatedMonitor()
    {
        var runtime = new FakeRecordingRuntime();
        using var session = new RecordingSession(runtime);
        await session.StartAsync(new RecordingSessionOptions((nint)21, null, null));

        await session.ChangeMonitorAsync((nint)22);

        Assert.AreEqual(2, runtime.StartCallCount);
        Assert.AreEqual(1, runtime.StopCallCount);
        Assert.AreEqual((nint)22, session.CurrentOptions?.MonitorHandle);
    }

    [TestMethod]
    public async Task ChangeAudioDevicesAsync_WhenNotRecording_Throws()
    {
        var runtime = new FakeRecordingRuntime();
        using var session = new RecordingSession(runtime);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            session.ChangeAudioDevicesAsync("mic-x", "loop-x"));
    }

    [TestMethod]
    public async Task ChangeAudioDevicesAsync_WhenRestartStartFails_ClearsCurrentOptions()
    {
        var runtime = new FakeRecordingRuntime { ThrowOnSecondStart = true };
        using var session = new RecordingSession(runtime);
        await session.StartAsync(new RecordingSessionOptions((nint)30, "mic-a", "loop-a"));

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            session.ChangeAudioDevicesAsync("mic-b", "loop-b"));

        Assert.AreEqual(RecordingLifecycleState.Idle, session.State);
        Assert.IsNull(session.CurrentOptions);
    }

    private sealed class FakeRecordingRuntime : IRecordingRuntime
    {
        public RecordingLifecycleState State { get; private set; } = RecordingLifecycleState.Idle;

        private RecordingSessionTimebase? _sessionTimebase;

        public RecordingSessionTimebase? SessionTimebase => _sessionTimebase;

        public int StartCallCount { get; private set; }

        public int StopCallCount { get; private set; }

        public RecordingSessionOptions? LastStartedOptions { get; private set; }
        public bool ThrowOnSecondStart { get; init; }

        public Task StartAsync(RecordingSessionOptions options, CancellationToken cancellationToken = default)
        {
            if (State != RecordingLifecycleState.Idle)
                throw new InvalidOperationException("Already running.");

            cancellationToken.ThrowIfCancellationRequested();
            StartCallCount++;
            if (ThrowOnSecondStart && StartCallCount >= 2)
                throw new InvalidOperationException("Simulated runtime start failure.");
            LastStartedOptions = options;
            _sessionTimebase = new RecordingSessionTimebase();
            _sessionTimebase.Establish();
            State = RecordingLifecycleState.Recording;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StopCallCount++;
            _sessionTimebase = null;
            State = RecordingLifecycleState.Idle;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}
