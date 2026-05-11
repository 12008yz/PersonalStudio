using System.Collections.Concurrent;
using ScreenRecorder.RecordingEngine.MediaFoundation;

namespace ScreenRecorder.RecordingEngine.Tests;

[TestClass]
public sealed class BoundedEncoderWorkQueueTests
{
    [TestMethod]
    public async Task EnqueueAsync_WhenCapacityIsFull_WaitsUntilWorkerReleasesSlot()
    {
        var firstItemStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstItem = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var queue = new BoundedEncoderWorkQueue<int>(
            capacity: 1,
            async (item, ct) =>
            {
                if (item == 1)
                {
                    firstItemStarted.TrySetResult();
                    await releaseFirstItem.Task.WaitAsync(ct).ConfigureAwait(false);
                }
            });

        await queue.EnqueueAsync(1);
        await firstItemStarted.Task;
        Assert.AreEqual(1, queue.PendingCount);

        var secondEnqueue = queue.EnqueueAsync(2).AsTask();
        await Task.Delay(100);
        Assert.IsFalse(secondEnqueue.IsCompleted, "Second enqueue should wait due to backpressure.");

        releaseFirstItem.TrySetResult();
        await secondEnqueue;
        await queue.StopAsync();
    }

    [TestMethod]
    public async Task WorkerLoop_RunsOnDedicatedThread_NotOnCallerThread()
    {
        var callerThreadId = Environment.CurrentManagedThreadId;
        var workerThreadIds = new ConcurrentBag<int>();
        await using var queue = new BoundedEncoderWorkQueue<int>(
            capacity: 4,
            (item, _) =>
            {
                workerThreadIds.Add(Environment.CurrentManagedThreadId);
                return ValueTask.CompletedTask;
            });

        await queue.EnqueueAsync(1);
        await queue.EnqueueAsync(2);
        await queue.StopAsync();

        Assert.IsTrue(workerThreadIds.Count > 0, "Expected worker to process at least one item.");
        foreach (var id in workerThreadIds)
            Assert.AreNotEqual(callerThreadId, id, "Processing must not run on caller thread.");
    }

    [TestMethod]
    public async Task StopAsync_PreventsFurtherEnqueue()
    {
        await using var queue = new BoundedEncoderWorkQueue<int>(
            capacity: 2,
            static (item, _) => ValueTask.CompletedTask);

        await queue.EnqueueAsync(1);
        await queue.StopAsync();

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () => await queue.EnqueueAsync(2));
    }

    [TestMethod]
    public async Task StopAsync_DrainsAlreadyQueuedItems_BeforeCompleting()
    {
        var processed = new ConcurrentBag<int>();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var queue = new BoundedEncoderWorkQueue<int>(
            capacity: 4,
            async (item, _) =>
            {
                if (item == 1)
                    await gate.Task.ConfigureAwait(false);
                processed.Add(item);
            });

        await queue.EnqueueAsync(1);
        await queue.EnqueueAsync(2);
        await queue.EnqueueAsync(3);

        var stoppingTask = queue.StopAsync();
        await Task.Delay(100);
        Assert.IsFalse(stoppingTask.IsCompleted, "StopAsync should wait until queued work is drained.");

        gate.TrySetResult();
        await stoppingTask;

        CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, processed.ToArray());
    }
}
