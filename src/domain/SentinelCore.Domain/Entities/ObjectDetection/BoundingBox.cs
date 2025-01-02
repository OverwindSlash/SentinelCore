using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using OpenCvSharp;

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
    }
}
