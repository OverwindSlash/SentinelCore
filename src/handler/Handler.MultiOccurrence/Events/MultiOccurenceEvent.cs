using System.Text.Json;
using System.Text.Json.Serialization;
using OpenCvSharp;
using SentinelCore.Domain.Events;

namespace Handler.MultiOccurrence.Events
{
    public class MultiOccurenceEvent : DomainEvent
    {
        public string DeviceName { get; }
        public List<string> ObjTypes { get; private set; } = new List<string>();
        public string SnapshotId { get; private set; }

        [JsonIgnore]
        public Mat Snapshot { get; private set; }
        public string EventImagePath { get; set; }

        [JsonIgnore]
        public Mat Scene { get; private set; }
        public string EventScenePath { get; set; }

        public MultiOccurenceEvent(List<string> objTypes, string snapshotId, Mat snapshot, string eventImagePath, Mat scene, string eventScenePath)
            : this("MultiOccurrence Event", "", "UnknownHandler", "UnknownDevice",
                objTypes, snapshotId, snapshot, eventImagePath, scene, eventScenePath)
        {

        }

        public MultiOccurenceEvent(string eventName, string eventMessage, string handlerName,
            string deviceName, List<string> objTypes, string snapshotId, Mat snapshot, string eventImagePath, Mat scene, string eventScenePath)
            : base(eventName, eventMessage, handlerName)
        {
            DeviceName = deviceName;
            ObjTypes = objTypes;
            SnapshotId = snapshotId;
            Snapshot = snapshot;
            EventImagePath = eventImagePath;
            Scene = scene;
            EventScenePath = eventScenePath;
        }

        public override string GenerateJsonMessage()
        {
            return JsonSerializer.Serialize<MultiOccurenceEvent>(this);
        }

        protected override string GenerateLogContent()
        {
            return $"Multi-Occurrence Event: ObjTypes: {ObjTypes}.";
        }
    }
}
