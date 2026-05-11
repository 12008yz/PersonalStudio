using System.Threading.Channels;

namespace ScreenRecorder.RecordingEngine.MediaFoundation;

/// <summary>
/// Очередь задач кодирования/мультиплексирования с выделенным worker-потоком и backpressure:
/// при заполнении producer ждёт освобождения слота.
/// </summary>
public sealed class BoundedEncoderWorkQueue<TWorkItem> : IAsyncDisposable, IDisposable
{
    private readonly Channel<TWorkItem> _channel;
    private readonly SemaphoreSlim _slots;
    private readonly Func<TWorkItem, CancellationToken, ValueTask> _processItemAsync;
    private readonly Task _workerTask;
    private int _pendingCount;
    private int _stopStarted;

    public BoundedEncoderWorkQueue(
        int capacity,
        Func<TWorkItem, CancellationToken, ValueTask> processItemAsync)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");

        _processItemAsync = processItemAsync ?? throw new ArgumentNullException(nameof(processItemAsync));
        _slots = new SemaphoreSlim(capacity, capacity);
        _channel = Channel.CreateUnbounded<TWorkItem>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        });
        _workerTask = Task.Factory.StartNew(
            WorkerLoopAsync,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();
    }

    public int PendingCount => Volatile.Read(ref _pendingCount);

    public async ValueTask EnqueueAsync(TWorkItem item, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfStopping();

        await _slots.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfStopping();
            await _channel.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
            Interlocked.Increment(ref _pendingCount);
        }
        catch
        {
            _slots.Release();
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _stopStarted, 1) != 0)
        {
            await _workerTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        _channel.Writer.TryComplete();
        await _workerTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _slots.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _slots.Dispose();
    }

    private async Task WorkerLoopAsync()
    {
        var reader = _channel.Reader;
        while (await reader.WaitToReadAsync().ConfigureAwait(false))
        {
            while (reader.TryRead(out var item))
            {
                try
                {
                    await _processItemAsync(item, CancellationToken.None).ConfigureAwait(false);
                }
                finally
                {
                    Interlocked.Decrement(ref _pendingCount);
                    _slots.Release();
                }
            }
        }
    }

    private void ThrowIfStopping()
    {
        if (Volatile.Read(ref _stopStarted) != 0)
            throw new InvalidOperationException("Queue is stopping and does not accept new items.");
    }
}
