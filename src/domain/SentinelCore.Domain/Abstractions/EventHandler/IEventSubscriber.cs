using MessagePipe;

namespace SentinelCore.Domain.Abstractions.EventHandler
{
    public interface IEventSubscriber<TEvent> : IDisposable
    {
        void SetSubscriber(ISubscriber<TEvent> subscriber);
        void ProcessEvent(TEvent @event);
    }
}
