namespace ScreenRecorder.RecordingEngine.Recording;

/// <summary>
/// Единый orchestration-слой для управления runtime записи:
/// Start/Stop и безопасная смена устройств через restart активной сессии.
/// </summary>
public sealed class RecordingSession : IRecordingSession, IDisposable
{
    private readonly IRecordingRuntime _runtime;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private RecordingSessionOptions? _currentOptions;

    public RecordingSession(IRecordingRuntime runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public RecordingLifecycleState State => _runtime.State;

    public RecordingSessionOptions? CurrentOptions => _currentOptions;

    public async Task StartAsync(RecordingSessionOptions options, CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _runtime.StartAsync(options, cancellationToken).ConfigureAwait(false);
            _currentOptions = options;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _runtime.StopAsync(cancellationToken).ConfigureAwait(false);
            _currentOptions = null;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public Task ChangeAudioDevicesAsync(
        string? microphoneEndpointId,
        string? loopbackRenderEndpointId,
        CancellationToken cancellationToken = default)
    {
        return RestartWithOptionsAsync(options =>
        {
            return options with
            {
                PreferredMicrophoneEndpointId = microphoneEndpointId,
                PreferredLoopbackRenderEndpointId = loopbackRenderEndpointId,
            };
        }, cancellationToken);
    }

    public Task ChangeMonitorAsync(nint monitorHandle, CancellationToken cancellationToken = default)
    {
        return RestartWithOptionsAsync(options => options with { MonitorHandle = monitorHandle }, cancellationToken);
    }

    public void Dispose()
    {
        _runtime.Dispose();
        _mutex.Dispose();
    }

    private async Task RestartWithOptionsAsync(
        Func<RecordingSessionOptions, RecordingSessionOptions> updateOptions,
        CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = _currentOptions;
            if (current is null)
                throw new InvalidOperationException("Recording session is not started.");

            var options = updateOptions(current);
            await _runtime.StopAsync(cancellationToken).ConfigureAwait(false);
            _currentOptions = null;
            try
            {
                await _runtime.StartAsync(options, cancellationToken).ConfigureAwait(false);
                _currentOptions = options;
            }
            catch
            {
                _currentOptions = null;
                throw;
            }
        }
        finally
        {
            _mutex.Release();
        }
    }
}
