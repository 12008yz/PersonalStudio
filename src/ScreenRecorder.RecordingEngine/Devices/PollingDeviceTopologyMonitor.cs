using Microsoft.Extensions.Logging;

namespace ScreenRecorder.RecordingEngine.Devices;

public sealed class PollingDeviceTopologyMonitor : IDeviceTopologyMonitor
{
    private readonly object _gate = new();
    private readonly Func<DeviceTopologySnapshot> _snapshotProvider;
    private readonly TimeSpan _pollInterval;
    private readonly ILogger<PollingDeviceTopologyMonitor>? _logger;
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;

    public PollingDeviceTopologyMonitor(
        TimeSpan? pollInterval = null,
        ILogger<PollingDeviceTopologyMonitor>? logger = null)
        : this(DeviceTopologySnapshot.CaptureCurrent, pollInterval ?? TimeSpan.FromSeconds(1), logger)
    {
    }

    internal PollingDeviceTopologyMonitor(
        Func<DeviceTopologySnapshot> snapshotProvider,
        TimeSpan pollInterval,
        ILogger<PollingDeviceTopologyMonitor>? logger = null)
    {
        if (pollInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(pollInterval), "Poll interval must be positive.");

        _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
        _pollInterval = pollInterval;
        _logger = logger;
    }

    public event EventHandler<DeviceTopologyChangedEventArgs>? TopologyChanged;

    public DeviceTopologySnapshot? CurrentSnapshot { get; private set; }

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _loopCts is not null;
            }
        }
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_loopCts is not null)
                throw new InvalidOperationException("Device topology monitor is already running.");

            var cts = new CancellationTokenSource();
            try
            {
                var snapshot = _snapshotProvider();
                _loopCts = cts;
                CurrentSnapshot = snapshot;
                _loopTask = Task.Run(() => LoopAsync(cts.Token));
            }
            catch
            {
                cts.Dispose();
                _loopCts = null;
                _loopTask = null;
                CurrentSnapshot = null;
                throw;
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task? loopTask;
        lock (_gate)
        {
            if (_loopCts is null)
                return;

            _loopCts.Cancel();
            _loopCts.Dispose();
            _loopCts = null;
            loopTask = _loopTask;
            _loopTask = null;
        }

        if (loopTask is not null)
            await loopTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<DeviceTopologyChangeKind> CheckForChangesOnceAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();

        DeviceTopologySnapshot next;
        try
        {
            next = _snapshotProvider();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Polling device topology snapshot failed.");
            throw;
        }

        DeviceTopologySnapshot? previous;
        lock (_gate)
        {
            previous = CurrentSnapshot;
            if (previous is null)
            {
                CurrentSnapshot = next;
                return DeviceTopologyChangeKind.None;
            }
        }

        var changeKind = CalculateChangeKind(previous, next);
        if (changeKind == DeviceTopologyChangeKind.None)
            return changeKind;

        lock (_gate)
        {
            CurrentSnapshot = next;
        }

        TopologyChanged?.Invoke(this, new DeviceTopologyChangedEventArgs(previous, next, changeKind));
        return changeKind;
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
    }

    internal static DeviceTopologyChangeKind CalculateChangeKind(
        DeviceTopologySnapshot previous,
        DeviceTopologySnapshot current)
    {
        var kind = DeviceTopologyChangeKind.None;

        if (!SetEquals(previous.Monitors.Select(m => m.DeviceName), current.Monitors.Select(m => m.DeviceName)))
            kind |= DeviceTopologyChangeKind.MonitorsChanged;

        if (!SetEquals(previous.CaptureEndpoints.Select(d => d.DeviceId), current.CaptureEndpoints.Select(d => d.DeviceId)))
            kind |= DeviceTopologyChangeKind.CaptureEndpointsChanged;

        if (!SetEquals(previous.RenderEndpoints.Select(d => d.DeviceId), current.RenderEndpoints.Select(d => d.DeviceId)))
            kind |= DeviceTopologyChangeKind.RenderEndpointsChanged;

        if (!StringEquals(previous.DefaultCaptureEndpointId, current.DefaultCaptureEndpointId))
            kind |= DeviceTopologyChangeKind.DefaultCaptureEndpointChanged;

        if (!StringEquals(previous.DefaultRenderEndpointId, current.DefaultRenderEndpointId))
            kind |= DeviceTopologyChangeKind.DefaultRenderEndpointChanged;

        return kind;

        static bool StringEquals(string? left, string? right) =>
            string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

        static bool SetEquals(IEnumerable<string> left, IEnumerable<string> right)
        {
            var a = new HashSet<string>(left, StringComparer.OrdinalIgnoreCase);
            var b = new HashSet<string>(right, StringComparer.OrdinalIgnoreCase);
            return a.SetEquals(b);
        }
    }

    private async Task LoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_pollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                await CheckForChangesOnceAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // graceful stop
        }
    }
}
