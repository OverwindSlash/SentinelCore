namespace SentinelCore.Domain.Events
{
    public abstract class DomainEvent : EventBase
    {
        public string DeviceName { get; set; }

        public string EventName { get; }

        public string Message { get; }

        public string HandlerName { get; }

        protected DomainEvent()
            : this("UnknownDevice", nameof(DomainEvent), "Unknown", "Unknown")
        { }

        protected DomainEvent(string deviceName, string eventName, string eventMessage, string handlerName)
        {
            DeviceName = deviceName;
            EventName = eventName;
            Message = eventMessage;
            HandlerName = handlerName;
        }

        public virtual string GetEventKey()
        {
            return $"{DeviceName}_{EventName}";
        }

        public abstract string GenerateJsonMessage();

        public string CreateLogMessage()
        {
            return $"{DateTime.Now.ToLocalTime()} {GenerateLogContent()}";
        }

        protected abstract string GenerateLogContent();
    }
}
