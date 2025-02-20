using SentinelCore.Domain.Entities.VideoStream;

namespace SentinelCore.Domain.Entities.ObjectDetection
{
    public class ObjectGroup : DetectedObject
    {
        public List<DetectedObject> GroupObjects { get; private set; }

        public ObjectGroup(Frame frame, List<DetectedObject> groupObjects, string groupLabel, int groupId)
            : base(frame.DeviceId, frame.FrameId, frame.TimeStamp, null)
        {
            this.GroupObjects = groupObjects;
            this.Bbox = BoundingBox.FromDetectedObjects(groupObjects, groupLabel, groupId);
        }
    }
}
