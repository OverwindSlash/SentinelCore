using System.Collections;
using System.Collections.Concurrent;

namespace SentinelCore.Domain.DataStructures;

public class ConcurrentBoundedQueue<T> : IEnumerable<T>, IConcurrentBoundedQueue<T>
{
    private const int DefaultMaxCapacity = 100;
    private const int CleanupInProcessWaitMs = 30;
    private const int DequeueTimeOut = 500;

    private readonly ConcurrentQueue<T> _queue = new();
    private readonly int _maxCapacity;
    private readonly Action<T> _cleanup;
    private int _maxOccupied;
    
    public int MaxCapacity => _maxCapacity;
    public int MaxOccupied => _maxOccupied;
    public int Count => _queue.Count;

    public ConcurrentBoundedQueue(int maxCapacity = DefaultMaxCapacity, Action<T> cleanup = null)
    {
        if (maxCapacity <= 0)
        {
            throw new ArgumentException("ConcurrentBoundedQueue maximum capacity must be greater than zero.");
        }

        _maxCapacity = maxCapacity;
        _cleanup = cleanup;
    }

    public void Enqueue(T item)
    {
        if (item == null)
        {
            return;
        }
        
        DropExpiredItem();

        _queue.Enqueue(item);
        
        UpdateMaxOccupied();
    }

    private void DropExpiredItem()
    {
        while (_queue.Count >= _maxCapacity && _queue.TryDequeue(out var droppedItem))
        {
            CleanupItem(droppedItem);
        }
    }

    private void UpdateMaxOccupied()
    {
        int count = _queue.Count;
        if (count > _maxOccupied)
        {
            Interlocked.Exchange(ref _maxOccupied, count);
        }
    }

    private void CleanupItem(T item)
    {
        if (_cleanup != null)
        {
            _cleanup(item);
        }
        else
        {
            (item as IDisposable)?.Dispose();
        }
    }

    public T Dequeue()
    {
        var cancellationTokenSource = new CancellationTokenSource(DequeueTimeOut);

        while (!cancellationTokenSource.Token.IsCancellationRequested)
        {
            if (_queue.TryDequeue(out var item))
            {
                return item;
            }

            Task.Delay(CleanupInProcessWaitMs).Wait(); // Empty queue, wait for data
        }

        return default;
    }

    public async Task EnqueueAsync(T item)
    {
        await Task.Run(() => Enqueue(item));
    }

    public async Task<T> DequeueAsync()
    {
        return await Task.Run(() => Dequeue());
    }

    public void Clear()
    {
        for (int i = 0; i < _queue.Count; i++)
        {
            var item = Dequeue();
            CleanupItem(item);
        }
    }

    #region Enumerator

    public IEnumerator<T> GetEnumerator()
    {
        return _queue.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    #endregion
}