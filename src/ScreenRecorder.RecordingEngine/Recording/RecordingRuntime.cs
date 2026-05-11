using ScreenRecorder.RecordingEngine.Audio;
using ScreenRecorder.RecordingEngine.Capture;

namespace ScreenRecorder.RecordingEngine.Recording;

public sealed class RecordingRuntime : IRecordingRuntime
{
    private readonly object _gate = new();
    private readonly Func<IFrameCaptureSession> _frameFactory;
    private readonly Func<IAudioCaptureSession> _audioFactory;

    private IFrameCaptureSession? _frameSession;
    private IAudioCaptureSession? _audioSession;
    private CancellationTokenRegistration _externalCancellationRegistration;
    private RecordingLifecycleState _state = RecordingLifecycleState.Idle;

    public RecordingRuntime()
        : this(
            () => new MonitorFrameCaptureSessionAdapter(new MonitorFrameCaptureSession()),
            () => new MicAndLoopbackCaptureSessionAdapter(new MicAndLoopbackCaptureSession()))
    {
    }

    internal RecordingRuntime(
        Func<IFrameCaptureSession> frameFactory,
        Func<IAudioCaptureSession> audioFactory)
    {
        _frameFactory = frameFactory ?? throw new ArgumentNullException(nameof(frameFactory));
        _audioFactory = audioFactory ?? throw new ArgumentNullException(nameof(audioFactory));
    }

    public RecordingLifecycleState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public Task StartAsync(RecordingSessionOptions options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IFrameCaptureSession frame;
        IAudioCaptureSession audio;

        lock (_gate)
        {
            if (_state != RecordingLifecycleState.Idle)
                throw new InvalidOperationException($"Cannot start recording while runtime state is {_state}.");

            _state = RecordingLifecycleState.Starting;
            frame = _frameFactory();
            audio = _audioFactory();
            _frameSession = frame;
            _audioSession = audio;
        }

        try
        {
            frame.Start(options.MonitorHandle);
            audio.Start(options.PreferredMicrophoneEndpointId, options.PreferredLoopbackRenderEndpointId);

            lock (_gate)
            {
                _state = RecordingLifecycleState.Recording;
                _externalCancellationRegistration = cancellationToken.Register(StopFromExternalCancellation);
            }

            if (cancellationToken.IsCancellationRequested)
                CleanupSessions();
        }
        catch
        {
            CleanupSessions();
            throw;
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CleanupSessions();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        CleanupSessions();
    }

    private void StopFromExternalCancellation()
    {
        try
        {
            CleanupSessions();
        }
        catch
        {
            // Best-effort shutdown from cancellation callback.
        }
    }

    private void CleanupSessions()
    {
        IFrameCaptureSession? frame;
        IAudioCaptureSession? audio;
        CancellationTokenRegistration registration;

        lock (_gate)
        {
            if (_state == RecordingLifecycleState.Idle)
                return;

            _state = RecordingLifecycleState.Stopping;
            frame = _frameSession;
            audio = _audioSession;
            _frameSession = null;
            _audioSession = null;
            registration = _externalCancellationRegistration;
            _externalCancellationRegistration = default;
        }

        Exception? firstError = null;
        try
        {
            audio?.Stop();
        }
        catch (Exception ex)
        {
            firstError = ex;
        }

        try
        {
            frame?.Stop();
        }
        catch (Exception ex)
        {
            firstError ??= ex;
        }

        registration.Dispose();

        lock (_gate)
        {
            _state = RecordingLifecycleState.Idle;
        }

        if (firstError is not null)
            throw firstError;
    }

    internal interface IFrameCaptureSession
    {
        void Start(nint monitorHandle);

        void Stop();
    }

    internal interface IAudioCaptureSession
    {
        void Start(string? microphoneEndpointId, string? loopbackRenderEndpointId);

        void Stop();
    }

    private sealed class MonitorFrameCaptureSessionAdapter : IFrameCaptureSession
    {
        private readonly MonitorFrameCaptureSession _inner;

        public MonitorFrameCaptureSessionAdapter(MonitorFrameCaptureSession inner)
        {
            _inner = inner;
        }

        public void Start(nint monitorHandle) => _inner.Start(monitorHandle);

        public void Stop() => _inner.Stop();
    }

    private sealed class MicAndLoopbackCaptureSessionAdapter : IAudioCaptureSession
    {
        private readonly MicAndLoopbackCaptureSession _inner;

        public MicAndLoopbackCaptureSessionAdapter(MicAndLoopbackCaptureSession inner)
        {
            _inner = inner;
        }

        public void Start(string? microphoneEndpointId, string? loopbackRenderEndpointId) =>
            _inner.Start(microphoneEndpointId, loopbackRenderEndpointId);

        public void Stop() => _inner.Stop();
    }
}
