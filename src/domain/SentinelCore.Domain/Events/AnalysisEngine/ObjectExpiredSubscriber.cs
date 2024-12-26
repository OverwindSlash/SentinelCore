using MessagePipe;
using SentinelCore.Domain.Abstractions.EventHandler;

namespace SentinelCore.Domain.Events.AnalysisEngine
{
    public class ObjectExpiredSubscriber : IEventSubscriber<ObjectExpiredEvent>
    {
        private ISubscriber<ObjectExpiredEvent> _oeSubscriber;
        private IDisposable _disposableOeSubscriber;

        public void SetSubscriber(ISubscriber<ObjectExpiredEvent> subscriber)
        {
            _oeSubscriber = subscriber;
            _disposableOeSubscriber = _oeSubscriber.Subscribe(ProcessEvent);
        }

        public virtual void ProcessEvent(ObjectExpiredEvent @event)
        {

        }

        public virtual void Dispose()
        {
            _disposableOeSubscriber.Dispose();
        }
    }
}
