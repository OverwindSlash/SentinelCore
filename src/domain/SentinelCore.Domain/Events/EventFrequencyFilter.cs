namespace SentinelCore.Domain.Events
{
    public class EventFrequencyFilter<TEvent> where TEvent : DomainEvent
    {
        private readonly int _timeWindowSeconds;
        private readonly TimeSpan _timeWindow;
        private readonly Dictionary<object, DateTime> _eventTimestamps;
        private readonly object _lock = new object();

        public EventFrequencyFilter(int timeWindowSeconds)
        {
            if (timeWindowSeconds < 0)
                throw new ArgumentException("Time window seconds must not less then 0。", nameof(timeWindowSeconds));

            _timeWindowSeconds = timeWindowSeconds;
            _timeWindow = TimeSpan.FromSeconds(timeWindowSeconds);
            _eventTimestamps = new Dictionary<object, DateTime>();
        }

        public bool IsEventPassFilter(TEvent @event)
        {
            if (_timeWindowSeconds == 0)
                return true;

            var key = @event.GetEventKey();
            var now = DateTime.UtcNow;

            lock (_lock)
            {
                if (_eventTimestamps.TryGetValue(key, out DateTime lastTime))
                {
                    if ((now - lastTime) < _timeWindow)
                    {
                        // 在时间窗口内，过滤掉
                        return false;
                    }
                    else
                    {
                        // 超出时间窗口，更新最后时间并发送
                        _eventTimestamps[key] = now;
                        return true;
                    }
                }
                else
                {
                    // 第一次出现，记录时间并发送
                    _eventTimestamps[key] = now;
                    return true;
                }
            }
        }
    }
}
