using SentinelCore.Domain.Entities.ObjectDetection;

namespace SentinelCore.Domain.Events.AnalysisEngine
{
    public class ObjectExpiredEvent : EventBase
    {
        public string Id { get; }

        public int LabelId { get; }

        public string Label { get; }

        public long TrackingId { get; }

        public ObjectExpiredEvent(string id, int labelId, string label, long trackingId)
        {
            Id = id;
            LabelId = labelId;
            Label = label;
            TrackingId = trackingId;
        }

        public ObjectExpiredEvent(DetectedObject detectedObject)
        {
            Id = detectedObject.Id;
            LabelId = detectedObject.LabelId;
            Label = detectedObject.Label;
            TrackingId = detectedObject.TrackingId;
        }
    }
}
