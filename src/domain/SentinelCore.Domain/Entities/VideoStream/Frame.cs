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

        public T GetProperty<T>(string key)
        {
            if (_customizeProperties.ContainsKey(key))
            {
                return (T)_customizeProperties[key];
            }

            return default(T);
        }

        public static List<BoundingBox> RemoveContainedBoxes(List<BoundingBox> boxes)
        {
            if (boxes == null || boxes.Count == 0)
                return new List<BoundingBox>();

            // 按 Label 分类
            var typeToBoxes = boxes.GroupBy(b => b.Label)
                .ToDictionary(g => g.Key, g => g.ToList());

            var result = new List<BoundingBox>();

            foreach (var kvp in typeToBoxes)
            {
                string label = kvp.Key;
                List<BoundingBox> boxList = kvp.Value;

                // 按面积从大到小排序
                var sortedBoxes = boxList.OrderByDescending(b => b.Width * b.Height).ToList();

                var filtered = new List<BoundingBox>();

                foreach (var box in sortedBoxes)
                {
                    bool isContained = false;
                    foreach (var keptBox in filtered)
                    {
                        if (keptBox.Contains(box))
                        {
                            isContained = true;
                            break;
                        }
                    }

                    if (!isContained)
                    {
                        filtered.Add(box);
                    }
                }

                result.AddRange(filtered);
            }

            return result;
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
