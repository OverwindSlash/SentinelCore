using MessagePipe;
using SentinelCore.Domain.Abstractions.EventHandler;

namespace SentinelCore.Domain.Events.AnalysisEngine
{
    public class FrameAndObjectExpiredSubscriber : IEventSubscriber<FrameExpiredEvent>, IEventSubscriber<ObjectExpiredEvent>
    {
        private ISubscriber<FrameExpiredEvent> _feSubscriber;
        private IDisposable _disposableFeSubscriber;

        private ISubscriber<ObjectExpiredEvent> _oeSubscriber;
        private IDisposable _disposableOeSubscriber;
        
        public void SetSubscriber(ISubscriber<FrameExpiredEvent> subscriber)
        {
            _feSubscriber = subscriber;
            _disposableFeSubscriber = _feSubscriber.Subscribe(ProcessEvent);
        }

        public virtual void ProcessEvent(FrameExpiredEvent @event)
        {

        }

        public void SetSubscriber(ISubscriber<ObjectExpiredEvent> subscriber)
        {
            _oeSubscriber = subscriber;
            _disposableOeSubscriber = _oeSubscriber.Subscribe(ProcessEvent);
        }

        public virtual void ProcessEvent(ObjectExpiredEvent @event)
        {

        }

        public void Dispose()
        {
            _disposableFeSubscriber.Dispose();
            _disposableOeSubscriber.Dispose();
        }
    }
}
