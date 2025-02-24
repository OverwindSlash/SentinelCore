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

            return boundingBoxes;

            //return MergeBoundingBoxes(boundingBoxes, 2.0f);
        }

        private List<BoundingBox> MergeBoundingBoxes(List<BoundingBox> boundingBoxes, float threshold)
        {
            List<BoundingBox> mergedBoundingBoxes = new List<BoundingBox>();

            foreach (var bbox in boundingBoxes)
            {
                bool merged = false;

                for (int i = 0; i < mergedBoundingBoxes.Count; i++)
                {
                    if (bbox.CalculateDistance(mergedBoundingBoxes[i]) < bbox.Width * threshold)
                    {
                        // 合并目标
                        mergedBoundingBoxes[i] = mergedBoundingBoxes[i].CombineBoundingBox(bbox);
                        merged = true;
                        break;
                    }
                }

                if (!merged)
                {
                    mergedBoundingBoxes.Add(bbox);
                }
            }

            if (boundingBoxes.Count != mergedBoundingBoxes.Count)
            {
                return MergeBoundingBoxes(mergedBoundingBoxes, threshold);
            }
            else
            {
                return mergedBoundingBoxes;
            }
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
