using Detector.YoloV11Onnx;
using OpenCvSharp;
using SentinelCore.Domain.Entities.ObjectDetection;
using System.Diagnostics;

namespace Detector.Tests
{
    public class YoloV11OnnxDetectorTests
    {
        private string ModelPath = @"Models/yolo11m.onnx";
        private string ModelConfig = @"Models/yolov11.json";
        private readonly YoloOnnxDetector _detector;

        public YoloV11OnnxDetectorTests()
        {
            _detector = new YoloOnnxDetector();

            _detector.PrepareEnv();
            _detector.Init(new Dictionary<string, string>()
            {
                {"model_path", ModelPath},
                {"model_config", ModelConfig},
                {"use_cuda", "true"}
            });

            // // Avoid first time-consuming call in test cases.
            using var mat = new Mat("Images/Traffic_001.jpg", ImreadModes.Color);
            _detector.Detect(mat, 0.3F);
        }

        [OneTimeTearDown]
        public void CleanUp()
        {
            _detector.Dispose();
        }

        [Test]
        public void TestDetectMat()
        {
            using var mat = new Mat("Images/Traffic_001.jpg", ImreadModes.Color);

            var stopwatch = Stopwatch.StartNew();
            var items = _detector.Detect(mat, 0.1F);
            stopwatch.Stop();
            Console.WriteLine($"detection elapse: {stopwatch.ElapsedMilliseconds}ms");

            //ShowResultImage(items, mat);

            Assert.That(items.Count, Is.EqualTo(16));
        }

        private static void ShowResultImage(List<BoundingBox> items, Mat mat)
        {
            foreach (BoundingBox item in items)
            {
                mat.PutText(item.Label, new Point(item.X, item.Y), HersheyFonts.HersheyPlain, 1.0, Scalar.Aqua);
                mat.Rectangle(new Point(item.X, item.Y), new Point(item.X + item.Width, item.Y + item.Height), Scalar.Aqua);
            }

            Window.ShowImages(mat);
        }

        [Test]
        public void TestDetectMatBytes()
        {
            using var mat = new Mat("Images/Traffic_001.jpg", ImreadModes.Color);

            var imageData = mat.ToBytes();

            var stopwatch = Stopwatch.StartNew();
            var items = _detector.Detect(imageData, 0.1F).ToList();
            stopwatch.Stop();
            Console.WriteLine($"detection elapse: {stopwatch.ElapsedMilliseconds}ms");

            //ShowResultImage(items, mat);

            Assert.That(items.Count, Is.EqualTo(16));
        }

        [Test]
        public void TestDetect2KMatBytes()
        {
            using var mat = new Mat("Images/Pedestrian2K.jpg", ImreadModes.Color);

            var stopwatch = Stopwatch.StartNew();
            var items = _detector.Detect(mat.ToBytes(), 0.7F).ToList();
            stopwatch.Stop();
            Console.WriteLine($"detection elapse: {stopwatch.ElapsedMilliseconds}ms");

            //ShowResultImage(items, mat);

            Assert.That(items.Count, Is.EqualTo(9));
        }

        [Test]
        public void TestDetect2KMatPtr()
        {
            using var mat = new Mat("Images/Pedestrian2K.jpg", ImreadModes.Color);

            var stopwatch = Stopwatch.StartNew();
            var items = _detector.Detect(mat, 0.7F).ToList();
            stopwatch.Stop();
            Console.WriteLine($"detection elapse: {stopwatch.ElapsedMilliseconds}ms");

            //ShowResultImage(items, mat);

            Assert.That(items.Count, Is.EqualTo(9));
        }

        [Test]
        public void TestDetect4KMatBytes()
        {
            using var mat = new Mat("Images/Pedestrian4K.jpg", ImreadModes.Color);

            var stopwatch = Stopwatch.StartNew();
            var items = _detector.Detect(mat.ToBytes(), 0.7F).ToList();
            stopwatch.Stop();
            Console.WriteLine($"detection elapse: {stopwatch.ElapsedMilliseconds}ms");

            //ShowResultImage(items, mat);

            Assert.That(items.Count, Is.EqualTo(9));
        }

        [Test]
        public void TestDetect4KMatPtr()
        {
            using var mat = new Mat("Images/Pedestrian4K.jpg", ImreadModes.Color);

            var stopwatch = Stopwatch.StartNew();
            var items = _detector.Detect(mat, 0.7F).ToList();
            stopwatch.Stop();
            Console.WriteLine($"detection elapse: {stopwatch.ElapsedMilliseconds}ms");

            //ShowResultImage(items, mat);

            Assert.That(items.Count, Is.EqualTo(9));
        }

        [Test]
        public void TestDetectHighwayMatPtr()
        {
            using var mat = new Mat("Images/Traffic_002.jpg", ImreadModes.Color);

            var stopwatch = Stopwatch.StartNew();
            var items = _detector.Detect(mat, 0.1F).ToList();
            stopwatch.Stop();
            Console.WriteLine($"detection elapse: {stopwatch.ElapsedMilliseconds}ms");

            //ShowResultImage(items, mat);

            Assert.That(items.Count, Is.EqualTo(10));
        }

        [Test]
        public void TestDetectHighwayForMotionMatPtr()
        {
            using var mat = new Mat("Images/pl_000001.jpg", ImreadModes.Color);

            var stopwatch = Stopwatch.StartNew();
            var items = _detector.Detect(mat, 0.1F).ToList();
            stopwatch.Stop();
            Console.WriteLine($"detection elapse: {stopwatch.ElapsedMilliseconds}ms");

            //ShowResultImage(items, mat);

            Assert.That(items.Count, Is.EqualTo(11));
        }

        [Test]
        public void TestDetectBechmark()
        {
            using var mat = new Mat("Images/pl_000001.jpg", ImreadModes.Color);

            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < 10; i++)
            {
                var items = _detector.Detect(mat, 0.6F).ToList();
            }
            stopwatch.Stop();

            Console.WriteLine($"detection elapse: {stopwatch.ElapsedMilliseconds / 10}ms");
        }
    }
}
