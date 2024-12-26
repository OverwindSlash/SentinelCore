using SentinelCore.Domain.Events;

namespace Handler.Ocr.Events
{
    public class OcrResultEvent : DomainEvent
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
