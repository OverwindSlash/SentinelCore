using MessagePipe;

namespace SentinelCore.Domain.Abstractions.EventHandler
{
    public interface IEventPublisher<TEvent>
    {
        void SetPublisher(IPublisher<TEvent> publisher);
        void PublishEvent(TEvent @event);
    }
}
