namespace SentinelCore.Domain.Events
{
    public abstract class DomainEvent : EventBase
    {
        public string EventName { get; }

        public string Message { get; }

        public string HandlerName { get; }

        protected DomainEvent()
            : this(nameof(DomainEvent), "Unknown", "Unknown")
        { }

        protected DomainEvent(string eventName, string eventMessage, string handlerName)
        {
            EventName = eventName;
            Message = eventMessage;
            HandlerName = handlerName;
        }

        public virtual object GetProperty(string key)
        {
            return null;
        }

        public string CreateLogMessage()
        {
            return $"{DateTime.Now.ToLocalTime()} {GenerateLogContent()}";
        }

        protected abstract string GenerateLogContent();
    }
}
