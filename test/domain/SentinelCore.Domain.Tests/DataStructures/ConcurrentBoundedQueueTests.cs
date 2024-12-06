using Moq;
using SentinelCore.Domain.DataStructures;

namespace SentinelCore.Domain.Tests.DataStructures;

[TestFixture]
public class ConcurrentBoundedQueueTests
{
    private ConcurrentBoundedQueue<int> _queue;
    private Mock<Action<int>> _mock;
    private Action<int> _cleanupAction;

    [SetUp]
    public void Setup()
    {
        _mock = new Mock<Action<int>>();
        _cleanupAction = _mock.Object;
        _queue = new ConcurrentBoundedQueue<int>(5, _cleanupAction);
    }

    [Test]
    public void TestSingleThreadedEnqueueDequeue()
    {
        _queue.Enqueue(1);
        _queue.Enqueue(2);
        _queue.Enqueue(3);
        
        Assert.That(_queue.Dequeue(), Is.EqualTo(1));
        
        _queue.Enqueue(4);
        
        Assert.That(_queue.Dequeue(), Is.EqualTo(2));
        Assert.That(_queue.Dequeue(), Is.EqualTo(3));
        
        Assert.That(_queue.Count, Is.EqualTo(1));
        Assert.That(_queue.MaxOccupied, Is.EqualTo(3));
    }

    [Test]
    public void TestBoundaryBehaviorWithAutoDequeue()
    {
        for (int i = 0; i < 10; i++)
        {
            _queue.Enqueue(i);
        }
        
        _mock.Verify(cleanAction => cleanAction(It.IsAny<int>()), Times.Exactly(5));

        Assert.That(_queue.Count, Is.EqualTo(5));
        Assert.That(_queue.MaxOccupied, Is.EqualTo(5));
    }

    [Test]
    public void TestEnqueueWithClearInProcess()
    {
        var cleanupQueue = new ConcurrentBoundedQueue<int>(5, cleanup => { Thread.Sleep(500); });

        cleanupQueue.Enqueue(0);
        cleanupQueue.Enqueue(1);
        
        var clearTask = Task.Run(() =>
        {
            cleanupQueue.Clear();
        });
        
        var enqueueTask = Task.Run(() =>
        {
            for (int i = 2; i < 5; i++)
            {
                cleanupQueue.Enqueue(i);
            }
        });
        
        Task.WaitAll(enqueueTask, clearTask);
        
        Assert.That(cleanupQueue.Count, Is.LessThanOrEqualTo(3));
    }
    
    [Test]
    public void TestConcurrencyEnqueueDequeue()
    {
        Task[] tasks = new Task[10];
        
        for (int i = 5; i < 10; i++)
        {
            tasks[i] = Task.Run(() => _queue.Dequeue());
        }
        
        for (int i = 0; i < 5; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                _queue.Enqueue(i);
            });
        }
        
        Task.WaitAll(tasks);
    
        Assert.That(_queue.Count, Is.EqualTo(0));
    }
    
    [Test]
    public async Task TestAsyncEnqueueDequeue()
    {
        await _queue.EnqueueAsync(1);
        var item1 = await _queue.DequeueAsync();
        Assert.That(item1, Is.EqualTo(1));
        
        await _queue.EnqueueAsync(2);
        var item2 = await _queue.DequeueAsync();
        Assert.That(item2, Is.EqualTo(2));

        for (int i = 0; i < 5; i++)
        {
            await _queue.EnqueueAsync(i);
        }

        for (int i = 0; i < 5; i++)
        {
            _ = await _queue.DequeueAsync();
        }
        
        Assert.That(_queue.Count, Is.EqualTo(0));
        Assert.That(_queue.MaxOccupied, Is.EqualTo(5));
    }
    
    [Test]
    public void TestDataStatisticsAccuracy()
    {
        _queue.Enqueue(1);
        _queue.Enqueue(2);
        _queue.Dequeue();
    
        Assert.That(_queue.Count, Is.EqualTo(1));
        Assert.That(_queue.MaxOccupied, Is.EqualTo(2));
    }
}