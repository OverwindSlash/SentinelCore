using SentinelCore.Domain.Events;

namespace Handler.RegionAccess.Events
{
    public class LeaveRegionEvent : DomainEvent
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
