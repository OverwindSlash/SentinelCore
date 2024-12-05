namespace SentinelCore.Domain.DataStructures;

public interface IConcurrentBoundedQueue<T>
{
    int Count { get; }
    int MaxCapacity { get; }
    int MaxOccupied { get; }

    void Enqueue(T item);
    T Dequeue();
    Task EnqueueAsync(T item);
    Task<T> DequeueAsync();
    void Clear();
}