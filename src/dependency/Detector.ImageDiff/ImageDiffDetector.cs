using OpenCvSharp;
using SentinelCore.Domain.Abstractions.ObjectDetector;
using SentinelCore.Domain.Entities.ObjectDetection;
using SentinelCore.Domain.Entities.VideoStream;
using Serilog;

namespace Detector.ImageDiff
{
    public class ImageDiffDetector : IObjectDetector
    {
        private int _roiX;
        private int _roiY;
        private int _roiWidth;
        private int _roiHeight;
        private float _diffThresh;
        private int _minBboxWidth;
        private int _minBboxHeight;

        private Rect _roi;
        private Mat _lastImage;

        public void PrepareEnv(Dictionary<string, string>? envParam = null)
        {
            
        }

        public void Init(Dictionary<string, string>? initParam = null, 
            Dictionary<string, string>? preferences = null)
        {
            Log.Information($"Image difference detector initializing...");

            _roiX = int.Parse(preferences["ImageDiff.RoiX"]);
            _roiY = int.Parse(preferences["ImageDiff.RoiY"]);
            _roiWidth = int.Parse(preferences["ImageDiff.RoiWidth"]);
            _roiHeight = int.Parse(preferences["ImageDiff.RoiHeight"]);
            _diffThresh = float.Parse(preferences["ImageDiff.DiffThresh"]);
            _minBboxWidth = int.Parse(preferences["ImageDiff.MinBboxWidth"]);
            _minBboxHeight = int.Parse(preferences["ImageDiff.MinBboxHeight"]);

            _roi = new Rect(_roiX, _roiY, _roiWidth, _roiHeight);
        }

        public int GetClassNumber()
        {
            return 1;
        }

        public List<BoundingBox> Detect(Mat image, float thresh = 0.5f)
        {
            if (_lastImage == null)
            {
                _lastImage = image.Clone();
                return new List<BoundingBox>();
            }

            // 截取感兴趣区域 (ROI)
            Mat lastRoi = new Mat(_lastImage, _roi);
            Mat currentRoi = new Mat(image, _roi);

            // 转为灰度图，便于比较
            Mat gray1 = new Mat();
            Mat gray2 = new Mat();
            Cv2.CvtColor(lastRoi, gray1, ColorConversionCodes.BGR2GRAY);
            Cv2.CvtColor(currentRoi, gray2, ColorConversionCodes.BGR2GRAY);

            // 计算两帧之间的差异
            Mat diff = new Mat();
            Cv2.Absdiff(gray1, gray2, diff);

            // 计算变化率
            Mat diffThresholded = new Mat();
            Cv2.Threshold(diff, diffThresholded, _diffThresh, 255, ThresholdTypes.Binary);

            // Cv2.ImShow("Frame with Bounding Boxes", diffThresholded);
            // Cv2.WaitKey(0);

            // 查找变化区域
            var contours = Cv2.FindContoursAsArray(diffThresholded, 
                RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            // 对每个变化区域绘制边界框
            List<BoundingBox> boundingBoxes = new List<BoundingBox>();
            foreach (var contour in contours)
            {
                Rect boundingBox = Cv2.BoundingRect(contour);

                if (boundingBox.Width < _minBboxWidth || 
                    boundingBox.Height < _minBboxHeight)
                {
                    continue;
                }

                var bbox = new BoundingBox(
                    labelId: 1,
                    label: "object",
                    confidence: 1,
                    x: boundingBox.X + _roiX,
                    y: boundingBox.Y + _roiY,
                    width: boundingBox.Width,
                    height: boundingBox.Height);

                boundingBoxes.Add(bbox);
            }

            _lastImage.Dispose();
            _lastImage = image.Clone();

            //return boundingBoxes;

            return MergeCloseBoxes(boundingBoxes, 2.0f);
        }

        public List<BoundingBox> MergeCloseBoxes(List<BoundingBox> boundingBoxes, float distanceThreshold)
        {
            if (boundingBoxes.Count == 0)
            {
                return boundingBoxes;
            }

            List<BoundingBox> boxes = boundingBoxes;
            int count = boxes.Count;
            UnionFind uf = new UnionFind(count);

            // 构建并查集关系
            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    var distance = CalculateDistance(boxes[i], boxes[j]);
                    var thresh1 = boxes[i].Width * distanceThreshold;
                    var thresh2 = boxes[j].Width * distanceThreshold;

                    if (distance < thresh1 || distance < thresh2)
                    {
                        uf.Union(i, j);
                    }
                }
            }

            // 按根节点分组
            Dictionary<int, List<BoundingBox>> groups = new Dictionary<int, List<BoundingBox>>();
            for (int i = 0; i < count; i++)
            {
                int root = uf.Find(i);
                if (!groups.ContainsKey(root))
                    groups[root] = new List<BoundingBox>();

                groups[root].Add(boxes[i]);
            }

            // 合并每组BoundingBox
            List<BoundingBox> result = new List<BoundingBox>();
            foreach (var group in groups.Values)
            {
                result.Add(MergeGroup(group));
            }

            return MergeCloseBoxes(result, distanceThreshold);
        }

        private float CalculateDistance(BoundingBox a, BoundingBox b)
        {
            // 计算中心点坐标
            float aCenterX = a.X + a.Width / 2f;
            float aCenterY = a.Y + a.Height / 2f;
            float bCenterX = b.X + b.Width / 2f;
            float bCenterY = b.Y + b.Height / 2f;

            // 计算欧氏距离
            float dx = aCenterX - bCenterX;
            float dy = aCenterY - bCenterY;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        private BoundingBox MergeGroup(List<BoundingBox> group)
        {
            // 计算合并后的边界
            int minX = group.Min(b => b.X);
            int minY = group.Min(b => b.Y);
            int maxX = group.Max(b => b.X + b.Width);
            int maxY = group.Max(b => b.Y + b.Height);

            // 获取置信度最高的标签
            var best = group.OrderByDescending(b => b.Confidence).First();

            var bbox = new BoundingBox(
                labelId: 1,
                label: "object",
                confidence: 1,
                x: minX,
                y: minY,
                width: maxX - minX,
                height: maxY - minY);

            return bbox;
        }

        public List<BoundingBox> Detect(byte[] imageData, float thresh = 0.5f)
        {
            using MemoryStream stream = new MemoryStream(imageData);

            var image = Mat.FromStream(stream, ImreadModes.AnyColor);

            return Detect(image, thresh);
        }

        public List<BoundingBox> Detect(string imageFile, float thresh = 0.5f)
        {
            var image = Cv2.ImRead(imageFile);

            return Detect(image, thresh);
        }

        public void Close()
        {

        }

        public void CleanupEnv()
        {
            
        }

        public void Dispose()
        {

        }
    }
}
