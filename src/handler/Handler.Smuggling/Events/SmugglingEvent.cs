using Handler.Smuggling.ThirdParty;
using OpenCvSharp;
using SentinelCore.Domain.Events;
using System.Text.Json.Serialization;

namespace Handler.Smuggling.Events
{
    public class SmugglingEvent : DomainEvent
    {
        public string SnapshotId { get; private set; }

        [JsonIgnore]
        public Mat Scene { get; private set; }
        public string EventScenePath { get; set; }

        public override string GenerateJsonMessage()
        {
            return this.GenerateLesSmugglingEventJsonMsg();
        }

        protected override string GenerateLogContent()
        {
            return $"{DateTime.Now.ToLocalTime()}, Device: {DeviceName}, {EventName} occurred.";
        }
    }
}
