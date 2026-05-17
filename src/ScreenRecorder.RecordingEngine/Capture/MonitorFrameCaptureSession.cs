using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.Logging;
using ScreenRecorder.RecordingEngine.Recording;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;

namespace ScreenRecorder.RecordingEngine.Capture;

/// <summary>
/// Windows.Graphics.Capture session for one monitor: stable frame stream + QPC timestamps (видеопайплайн без кодека).
/// </summary>
public sealed class MonitorFrameCaptureSession : IDisposable, IRecordingSessionTimebaseConsumer
{
    private readonly ILogger<MonitorFrameCaptureSession>? _logger;
    private readonly object _gate = new();
    private readonly Stopwatch _stopwatch = new();
    private volatile RecordingSessionTimebase? _sessionTimebase;

    private WinRtGraphicsDevice? _graphics;
    private GraphicsCaptureItem? _item;
    private Direct3D11CaptureFramePool? _pool;
    private GraphicsCaptureSession? _session;

    private int _active;
    private long _frames;
    private long _emptyFrames;
    private long _lastQpc;
    private TimeSpan _lastSystemRelative;
    private SizeInt32 _poolSize;

    /// <summary>
    /// Базовая отметка QPC для сопоставления с <see cref="Direct3D11CaptureFrame.SystemRelativeTime"/>.
    /// Задаётся перед <see cref="GraphicsCaptureSession.StartCapture"/> и сбрасывается после успешного <c>Recreate</c> пула (иначе метка времени кадра и «стенные» часы расходятся).
    /// </summary>
    private long _captureOriginTimestampTicks;

    private double _latencySumMilliseconds;
    private long _latencySampleCount;

    /// <remarks>Не Interlocked-под сумму: допущение один поток колбэка пула для FrameArrived.</remarks>
    private double _lastHandlerLatencyMilliseconds;

    private long _poolRecreateFailures;

    public MonitorFrameCaptureSession(ILogger<MonitorFrameCaptureSession>? logger = null)
    {
        _logger = logger;
    }

    public bool IsRunning => Volatile.Read(ref _active) != 0;

    public event EventHandler<CapturedVideoFrameEventArgs>? FrameCaptured;

    public void BindSessionTimebase(RecordingSessionTimebase timebase) =>
        _sessionTimebase = timebase ?? throw new ArgumentNullException(nameof(timebase));

    public void Start(nint hmonitor)
    {
        lock (_gate)
        {
            if (_session is not null)
                throw new InvalidOperationException("Capture session is already running.");

            try
            {
                _graphics = WinRtGraphicsDevice.CreateHardwareOrWarp();
                _item = GraphicsCaptureItemFactory.CreateForMonitor(hmonitor);
                _item.Closed += OnItemClosed;

                _pool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                    _graphics.WinRt,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    2,
                    _item.Size);

                _poolSize = _item.Size;

                _pool.FrameArrived += OnFrameArrived;

                _session = _pool.CreateCaptureSession(_item);
                _session.IsCursorCaptureEnabled = false;

                _frames = 0;
                _emptyFrames = 0;
                _poolRecreateFailures = 0;
                _latencySumMilliseconds = 0;
                _latencySampleCount = 0;
                _lastHandlerLatencyMilliseconds = double.NaN;
                _stopwatch.Restart();
                Volatile.Write(ref _active, 1);
                _captureOriginTimestampTicks = Stopwatch.GetTimestamp();
                _session.StartCapture();
            }
            catch (Exception ex)
            {
                Volatile.Write(ref _active, 0);
                TeardownLocked();
                _logger?.LogError(
                    ex,
                    "Monitor capture start failed ({Kind}).",
                    ScreenCaptureFailureClassifier.Classify(ex));
                throw;
            }
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            Volatile.Write(ref _active, 0);
            TeardownLocked();
        }
    }

    public FrameCaptureMetrics GetMetrics()
    {
        var n = _latencySampleCount;
        var avgLatencyMs = n > 0 ? _latencySumMilliseconds / n : double.NaN;

        return new FrameCaptureMetrics(
            Interlocked.Read(ref _frames),
            Interlocked.Read(ref _emptyFrames),
            // После Stop() IsRunning == false, но Elapsed хранит итог до остановки; иначе длительность и FPS обнуляются.
            _stopwatch.Elapsed,
            Volatile.Read(ref _lastQpc),
            _lastSystemRelative,
            avgLatencyMs,
            _lastHandlerLatencyMilliseconds,
            Interlocked.Read(ref _poolRecreateFailures));
    }

    public void Dispose()
    {
        lock (_gate)
        {
            Volatile.Write(ref _active, 0);
            TeardownLocked();
        }
    }

    private void OnItemClosed(GraphicsCaptureItem sender, object args)
    {
        _logger?.LogWarning("GraphicsCaptureItem closed (display detached or capture stopped by the system).");
        // Не вызывать Stop() синхронно: во время StartCapture() обработчик может прийти на том же потоке,
        // пока Start() удерживает lock — будет взаимная блокировка.
        ThreadPool.UnsafeQueueUserWorkItem(_ =>
        {
            try
            {
                Stop();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error while stopping after capture item closed.");
            }
        }, null);
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        if (Volatile.Read(ref _active) == 0)
            return;

        try
        {
            using var frame = sender.TryGetNextFrame();
            if (frame is null)
            {
                Interlocked.Increment(ref _emptyFrames);
                return;
            }

            var contentSize = frame.ContentSize;
            var skipLatencySample = false;
            if (contentSize.Width != _poolSize.Width || contentSize.Height != _poolSize.Height)
            {
                var g = _graphics;
                if (g is null)
                    return;

                try
                {
                    sender.Recreate(
                        g.WinRt,
                        DirectXPixelFormat.B8G8R8A8UIntNormalized,
                        2,
                        contentSize);
                    _poolSize = contentSize;
                    _captureOriginTimestampTicks = Stopwatch.GetTimestamp();
                    skipLatencySample = true;
                    _logger?.LogInformation("Direct3D11CaptureFramePool recreated at {Width}x{Height}", contentSize.Width, contentSize.Height);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref _poolRecreateFailures);
                    _logger?.LogError(
                        ex,
                        "Direct3D11CaptureFramePool.Recreate failed for {Width}x{Height} (resolution/DPI change).",
                        contentSize.Width,
                        contentSize.Height);
                }
            }

            var handlerTimestamp = Stopwatch.GetTimestamp();
            var wallSinceCaptureStart = Stopwatch.GetElapsedTime(_captureOriginTimestampTicks, handlerTimestamp);
            var latency = wallSinceCaptureStart - frame.SystemRelativeTime;
            if (latency < TimeSpan.Zero)
            {
                if (latency < TimeSpan.FromMilliseconds(-500))
                    _logger?.LogDebug(
                        "Frame handler latency negative ({LatencyMs} ms); possible clock/origin mismatch; clamped to zero for averages.",
                        latency.TotalMilliseconds);

                latency = TimeSpan.Zero;
            }

            Interlocked.Increment(ref _frames);
            Volatile.Write(ref _lastQpc, handlerTimestamp);
            _lastSystemRelative = frame.SystemRelativeTime;

            var latencyMs = latency.TotalMilliseconds;
            if (!skipLatencySample)
            {
                _latencySumMilliseconds += latencyMs;
                _latencySampleCount++;
                _lastHandlerLatencyMilliseconds = latencyMs;
            }

            var timebase = _sessionTimebase;
            if (timebase is { IsEstablished: true })
            {
                var mediaHns = timebase.VideoCaptureToMediaTimestampHns(handlerTimestamp, latency);
                var captureQpc = RecordingSessionTimebase.EstimateCaptureQpcTicks(handlerTimestamp, latency);
                FrameCaptured?.Invoke(
                    this,
                    new CapturedVideoFrameEventArgs(contentSize, mediaHns, captureQpc, latency));
            }

            // Фаза B: не читаем surface; только метрики и своевременный release кадра.
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "FrameArrived handler failed.");
        }
    }

    private void TeardownLocked()
    {
        _stopwatch.Stop();

        if (_item is not null)
            _item.Closed -= OnItemClosed;

        _session?.Dispose();
        _session = null;

        if (_pool is not null)
        {
            _pool.FrameArrived -= OnFrameArrived;
            _pool.Dispose();
            _pool = null;
        }

        _item = null;
        _graphics?.Dispose();
        _graphics = null;
    }
}
