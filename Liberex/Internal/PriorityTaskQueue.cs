using System.Diagnostics;

namespace Liberex.Internal;

public class PriorityTaskQueue
{
    private readonly Dictionary<TaskPriority, Queue<Func<CancellationToken, ValueTask>>> _queueDictionary = new();
    private readonly SemaphoreSlim _semaphore = new(0);
    private readonly object _locker = new();

    public void Write(Func<CancellationToken, ValueTask> task, TaskPriority priority = TaskPriority.Normal)
    {
        lock (_locker)
        {
            if (_queueDictionary.TryGetValue(priority, out var queue) == false)
            {
                queue = new();
                _queueDictionary[priority] = queue;
            }
            queue.Enqueue(task);
            _semaphore.Release();
        }
    }

    public async ValueTask<Func<CancellationToken, ValueTask>> ReadAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        Func<CancellationToken, ValueTask> item = null;
        for (int i = 2; i >= 0; i--)
        {
            var priority = (TaskPriority)i;
            if (_queueDictionary.TryGetValue(priority, out var queue))
            {
                if (queue.TryDequeue(out item)) break;
            }
        }
        // 实际上不可能为null
        return item;
    }
}

public enum TaskPriority
{
    Low,
    Normal,
    High
}