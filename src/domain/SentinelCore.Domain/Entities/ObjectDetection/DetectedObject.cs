using OpenCvSharp;
using System.Text.Json.Serialization;

namespace SentinelCore.Domain.Entities.ObjectDetection
{
    public class DetectedObject : IDisposable
    {
        public string DeviceId { get; set; }
        public long FrameId { get; set; }
        public DateTime TimeStamp { get; set; }

        public BoundingBox Bbox { get; set; }
        public Mat Snapshot { get; set; }

        public string Id => $"{Label}:{TrackingId}";
        public int LabelId => Bbox.LabelId;
        public string Label => Bbox.Label;
        public long TrackingId => Bbox.TrackingId;
        public float Confidence => Bbox.Confidence;

        #region ComputeProperties
        [JsonIgnore]
        public int X => Bbox.X;
        [JsonIgnore]
        public int Y => Bbox.Y;
        [JsonIgnore]
        public int Width => Bbox.Width;
        [JsonIgnore]
        public int Height => Bbox.Height;

        [JsonIgnore]
        public int TopLeftX => Bbox.TopLeftX;
        [JsonIgnore]
        public int TopLeftY => Bbox.TopLeftY;

        [JsonIgnore]
        public int CenterX => Bbox.CenterX;
        [JsonIgnore]
        public int CenterY => Bbox.CenterY;

        [JsonIgnore]
        public int BottomRightX => Bbox.BottomRightX;
        [JsonIgnore]
        public int BottomRightY => Bbox.BottomRightY;

        [JsonIgnore]
        public int TopCenterX => Bbox.TopCenterX;
        [JsonIgnore]
        public int TopCenterY => Bbox.TopCenterY;

        [JsonIgnore]
        public int BottomCenterX => Bbox.BottomCenterX;
        [JsonIgnore]
        public int BottomCenterY => Bbox.BottomCenterY;
        #endregion

        public bool IsUnderAnalysis { get; set; }
        public int LaneIndex { get; set; }

        private Dictionary<string, object> _customizeProperties = new();

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

        public DetectedObject(string deviceId, long frameId, DateTime timeStamp, BoundingBox bbox)
        {
            DeviceId = deviceId;
            FrameId = frameId;
            TimeStamp = timeStamp;
            Bbox = bbox;
            IsUnderAnalysis  = true;
            Snapshot = new Mat();
        }

        public void Dispose()
        {
            Snapshot?.Dispose();
        }
    }
}
