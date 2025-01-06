using OpenCvSharp;
using SentinelCore.Domain.Entities.ObjectDetection;

namespace SentinelCore.Domain.Entities.VideoStream
{
    public class Frame : IDisposable
    {
        public string DeviceId { get; }
        public long FrameId { get; }
        public long OffsetMilliSec { get; }
        public DateTime TimeStamp { get; }
        public Mat Scene { get; }
        public List<DetectedObject> DetectedObjects { get; }

        private Dictionary<string, object> _customizeProperties = new();

        public Frame(string deviceId, long frameId, long offsetMilliSec, Mat scene)
        {
            DeviceId = deviceId;
            FrameId = frameId;
            OffsetMilliSec = offsetMilliSec;
            TimeStamp = DateTime.Now;
            Scene = scene;
            DetectedObjects = new List<DetectedObject>();
        }

        public void AddDetectedObjects(List<BoundingBox> boundingBoxes)
        {
            foreach (var boundingBox in boundingBoxes)
            {
                var detectedObject = new DetectedObject(
                    deviceId: DeviceId,
                    frameId: FrameId,
                    timeStamp: TimeStamp,
                    bbox: boundingBox
                );

                DetectedObjects.Add(detectedObject);
            }
        }

        public void SetProperty(string key, object value)
        {
            if (!_customizeProperties.ContainsKey(key))
            {
                _customizeProperties.Add(key, value);
            }
            else
            {
                _customizeProperties[key] = value;
            }
        }

        public object GetProperty(string key)
        {
            if (_customizeProperties.ContainsKey(key))
            {
                return _customizeProperties[key];
            }

            return null;
        }

        public void Dispose()
        {
            Scene?.Dispose();
            foreach (DetectedObject detectedObject in DetectedObjects)
            {
                detectedObject.Dispose();
            }
        }
    }
}
