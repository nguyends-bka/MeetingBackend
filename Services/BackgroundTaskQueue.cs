using System.Threading.Channels;

namespace MeetingBackend.Services;

public interface IBackgroundTaskQueue
{
    void QueueBackgroundWorkItem(Func<CancellationToken, Task> workItem);
    ValueTask<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
}

public sealed class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<Func<CancellationToken, Task>> _queue;

    public BackgroundTaskQueue(int capacity = 200)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        };
        _queue = Channel.CreateBounded<Func<CancellationToken, Task>>(options);
    }

    public void QueueBackgroundWorkItem(Func<CancellationToken, Task> workItem)
    {
        if (workItem == null) throw new ArgumentNullException(nameof(workItem));
        if (!_queue.Writer.TryWrite(workItem))
        {
            throw new InvalidOperationException("Background task queue is full.");
        }
    }

    public async ValueTask<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
    {
        var workItem = await _queue.Reader.ReadAsync(cancellationToken);
        return workItem;
    }
}
