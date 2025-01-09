using SentinelCore.Domain.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Handler.RegionAccess.Events
{
    public class EnterRegionEvent : DomainEvent
    {
        public override string GenerateJsonMessage()
        {
            throw new NotImplementedException();
        }

        protected override string GenerateLogContent()
        {
            throw new NotImplementedException();
        }
    }
}
