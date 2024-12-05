using System.Drawing;

namespace Detector.YoloV5Onnx
{
    public class YoloPrediction
    {
        public int TypeId { get; set; }
        public string Type { get; set; }
        public float Confidence { get; set; }

        public Rectangle BoundingBox { get; set; }

        public int X => BoundingBox.X;
        public int Y => BoundingBox.Y;
        public int Width => BoundingBox.Width;
        public int Height => BoundingBox.Height;

        public int TrackingId { get; set; }

        public Point TopLeft => new Point(X, Y);
        public Point TopRight => new Point(X + Width, Y);
        public Point BottomLeft => new Point(X, Y + Height);
        public Point BottomRight => new Point(X + Width, Y + Height);
        public Point Center => new Point(X + Width / 2, Y + Height / 2);

        public List<Point> CornerPoints => new List<Point>() { TopLeft, TopRight, BottomLeft, BottomRight };
    }
}
