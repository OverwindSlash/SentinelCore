using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Handler.Smuggling
{
    public class TtlFlagManager<TKey>
    {
        private readonly ConcurrentDictionary<TKey, TtlValue> _dictionary = new ConcurrentDictionary<TKey, TtlValue>();
        private readonly Timer _timer;

        public TtlFlagManager()
        {
            // 每秒触发一次 TTL 递减
            _timer = new Timer(DoTimerAction, null, 1000, 1000);
        }

        private void DoTimerAction(object state)
        {
            bool result = true;

            foreach (var key in _dictionary.Keys)
            {
                if (_dictionary.TryGetValue(key, out var ttlValue))
                {
                    result &= ttlValue.IsActive;
                }
            }

            // TODO

            foreach (var key in _dictionary.Keys)
            {
                if (_dictionary.TryGetValue(key, out var ttlValue))
                {
                    if (ttlValue.Ttl > 0)
                    {
                        ttlValue.Ttl--;
                        if (ttlValue.Ttl == 0)
                        {
                            ttlValue.IsActive = false;
                        }
                    }
                }
            }
        }

        public void SetValue(TKey key, bool isActive, int ttl)
        {
            _dictionary[key] = new TtlValue { IsActive = isActive, Ttl = ttl };
        }

        public bool TryGetValue(TKey key, out bool isActive)
        {
            if (_dictionary.TryGetValue(key, out var ttlValue))
            {
                isActive = ttlValue.IsActive;
                return true;
            }
            isActive = false;
            return false;
        }

        private class TtlValue
        {
            public bool IsActive { get; set; }
            public int Ttl { get; set; }
        }
    }
}
