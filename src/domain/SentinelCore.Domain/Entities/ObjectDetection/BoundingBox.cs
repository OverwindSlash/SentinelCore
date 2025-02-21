using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using OpenCvSharp;
using SentinelCore.Domain.Entities.VideoStream;
using static OpenCvSharp.FileStorage;

namespace SentinelCore.Domain.Entities.ObjectDetection
{
    public class BoundingBox
    {
        public int LabelId { get; }
        public string Label { get; }
        public float Confidence { get; }

        public int X { get; }
        public int Y { get; }
        public int Height { get; }
        public int Width { get; }

        #region ComputeProperties
        [JsonIgnore]
        public int TopLeftX => X;
        [JsonIgnore]
        public int TopLeftY => Y;

        [JsonIgnore]
        public int CenterX => (X + Width / 2);
        [JsonIgnore]
        public int CenterY => (Y + Height / 2);

        [JsonIgnore]
        public int BottomRightX => (X + Width);
        [JsonIgnore]
        public int BottomRightY => (Y + Height);

        [JsonIgnore]
        public int TopCenterX => (X + Width / 2);
        [JsonIgnore]
        public int TopCenterY => Y;

        [JsonIgnore]
        public int BottomCenterX => (X + Width / 2);
        [JsonIgnore]
        public int BottomCenterY => (Y + Height);

        [JsonIgnore]
        public float Left => X;
        [JsonIgnore]
        public float Top => Y;
        [JsonIgnore]
        public float Right => X + Width;
        [JsonIgnore]
        public float Bottom => Y + Height;

        [JsonIgnore] public Rect Rectangle => new Rect(X, Y, Width, Height);
        #endregion

        public int TrackingId { get; set; }

        public BoundingBox(int labelId, string label, float confidence, int x, int y, int height, int width)
        {
            LabelId = labelId;
            Label = label;
            Confidence = confidence;
            X = x;
            Y = y;
            Height = height;
            Width = width;
            TrackingId = 0;
        }

        public BoundingBox CombineBoundingBox(BoundingBox other)
        {
            int minX = Math.Min(TopLeftX, other.TopLeftX);
            int minY = Math.Min(TopLeftY, other.TopLeftY);

            int maxX = Math.Max(BottomRightX, other.BottomRightX);
            int maxY = Math.Max(BottomRightY, other.BottomRightY);

            var boundingBox = new BoundingBox(
                labelId: -1,
                label: $"{Label}_and_{other.Label}",
                confidence: (Confidence + other.Confidence) / 2,
                x: minX,
                y: minY,
                width: maxX - minX,
                height: maxY - minY
            );

            return boundingBox;
        }

        public bool CloseTo(BoundingBox other, double threshold = 0.2)
        {
            // 计算两个边界框的“最近点”
            var closestPoint1 = GetClosestPoint(X, Y, Width, Height, other);
            var closestPoint2 = GetClosestPoint(other.X, other.Y, other.Width, other.Height, this);

            // 计算两个最近点之间的欧几里得距离
            float distance = CalculateDistance(closestPoint1.x, closestPoint1.y, closestPoint2.x, closestPoint2.y);

            // 判断是否接近
            // 使用框的尺寸（Width, Height）作为距离的标准
            return distance < threshold * Math.Max(Width, Height) || distance < threshold * Math.Max(other.Width, other.Height);
        }

        private (int x, int y) GetClosestPoint(int x, int y, int width, int height, BoundingBox other)
        {
            // 获取框的四个边界
            int left = x;
            int right = x + width;
            int top = y;
            int bottom = y + height;

            // 计算目标框的接近点
            int closestX = Math.Max(left, Math.Min(other.X, right));
            int closestY = Math.Max(top, Math.Min(other.Y, bottom));

            return (closestX, closestY);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float CalculateDistance(int x1, int y1, int x2, int y2)
        {
            int dx = x1 - x2;
            int dy = y1 - y2;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public bool Contains(BoundingBox other)
        {
            return Rectangle.Contains(other.Rectangle);
        }

        // Expansion bbox by percentage
        public BoundingBox Expand(float percentage)
        {
            int newWidth = (int)(Width * (1 + percentage));
            int newHeight = (int)(Height * (1 + percentage));
            int newX = X - (newWidth - Width) / 2;
            int newY = Y - (newHeight - Height) / 2;
            return new BoundingBox(LabelId, Label, Confidence, newX, newY, newHeight, newWidth);
        }

        public static BoundingBox FromDetectedObjects(List<DetectedObject> detectedObjects, string label, int trackingId)
        {
            // 计算当前聚集区域的外部BoundingBox
            float minX = detectedObjects.Min(p => p.TopLeftX);
            float minY = detectedObjects.Min(p => p.TopLeftY);
            float maxX = detectedObjects.Max(p => p.BottomRightX);
            float maxY = detectedObjects.Max(p => p.BottomRightY);
            float width = maxX - minX;
            float height = maxY - minY;

            var boundingBox = new BoundingBox(
                labelId: -1,
                label: label,
                confidence: 1.0f,
                x: (int)minX,
                y: (int)minY,
                width: (int)width,
                height: (int)height);
            boundingBox.TrackingId = trackingId;

            return boundingBox;
        }

        public static List<BoundingBox> MergeBoundingBoxes(List<BoundingBox> boundingBoxes, float iouThreshold)
        {
            List<BoundingBox> mergedBoxes = new List<BoundingBox>();

            foreach (var currentBox in boundingBoxes)
            {
                bool merged = false;
                foreach (var existingBox in mergedBoxes)
                {
                    // 计算 IoU
                    float iou = BoundingBox.CalculateIoU(existingBox, currentBox);
                    if (iou > iouThreshold)
                    {
                        // 如果 IoU 超过阈值，合并
                        BoundingBox mergedBox = BoundingBox.Merge(existingBox, currentBox);
                        mergedBoxes.Remove(existingBox);
                        mergedBoxes.Add(mergedBox);
                        merged = true;
                        break;
                    }
                }

                // 如果没有合并，添加新的 BoundingBox
                if (!merged)
                {
                    mergedBoxes.Add(currentBox);
                }
            }

            return mergedBoxes;
        }

        public static BoundingBox Merge(BoundingBox box1, BoundingBox box2)
        {
            int x = Math.Min(box1.X, box2.X);
            int y = Math.Min(box1.Y, box2.Y);
            int width = Math.Max(box1.X + box1.Width, box2.X + box2.Width) - x;
            int height = Math.Max(box1.Y + box1.Height, box2.Y + box2.Height) - y;

            return new BoundingBox(
                labelId: -1,
                label: box1.Label,
                confidence: 1.0f,
                x: x,
                y: y,
                width: width,
                height: height);
        }

        public static float CalculateIoU(BoundingBox box1, BoundingBox box2)
        {
            // 计算交集区域的坐标
            int x1 = Math.Max(box1.X, box2.X);
            int y1 = Math.Max(box1.Y, box2.Y);
            int x2 = Math.Min(box1.X + box1.Width, box2.X + box2.Width);
            int y2 = Math.Min(box1.Y + box1.Height, box2.Y + box2.Height);

            // 计算交集区域的面积
            int intersectionWidth = Math.Max(0, x2 - x1);
            int intersectionHeight = Math.Max(0, y2 - y1);
            int intersectionArea = intersectionWidth * intersectionHeight;

            // 计算并集区域的面积
            int area1 = box1.Width * box1.Height;
            int area2 = box2.Width * box2.Height;
            int unionArea = area1 + area2 - intersectionArea;

            // 计算 IoU
            return (float)intersectionArea / unionArea;
        }
    }
}
