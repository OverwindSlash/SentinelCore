using Microsoft.ML.OnnxRuntime;
using OpenCvSharp;
using SentinelCore.Domain.Abstractions.ObjectDetector;
using SentinelCore.Domain.Entities.ObjectDetection;
using Serilog;

namespace Detector.YoloV11Onnx
{
    public class YoloOnnxDetector : IObjectDetector
    {
        private YoloPredictor _predictor;
        private List<string> _targetTypes = new();
        private int _detectionStride = 1;
        private List<string> _names = new();

        private int _detectedCount = 0;

        // ROI detection
        private bool _onlyDetectRoi;
        private int _roiX;
        private int _roiY;
        private int _roiWidth;
        private int _roiHeight;
        private int _minBboxWidth;
        private int _minBboxHeight;
        private Rect _roi;

        public void PrepareEnv(Dictionary<string, string>? envParam = null)
        {

        }

        public void Init(Dictionary<string, string>? initParam = null,
            Dictionary<string, string>? preferences = null)
        {
            Log.Information($"YOlO v11 detector initializing...");

            if (!initParam.TryGetValue("model_path", out var modelPath))
            {
                throw new ArgumentException("initParam does not contain model_path element.");
            }

            if (!initParam.TryGetValue("model_config", out var modelConfig))
            {
                throw new ArgumentException("initParam does not contain model_path element.");
            }

            SessionOptions option = null;
            int gpuId = 0;
            if (initParam.TryGetValue("use_cuda", out var useCuda))
            {
                if (useCuda.ToLower() == "true")
                {
                    if (initParam.TryGetValue("gpu_id", out var value))
                    {
                        gpuId = Int32.Parse(value);
                    }

                    option = SessionOptions.MakeSessionOptionWithCudaProvider(gpuId);
                }
            }

            Log.Information($"Model: {modelPath}, Config: {modelConfig}, Cuda: {useCuda}, GPU: {gpuId}");

            if (!initParam.TryGetValue("target_types", out var targetTypes))
            {
                throw new ArgumentException("initParam does not contain target_types element.");
            }

            if (string.IsNullOrEmpty(targetTypes))
            {
                _targetTypes = null;
            }
            else
            {
                _targetTypes.AddRange(targetTypes.Split(','));
            }

            Log.Information($"Target types: {targetTypes}");

            if (!initParam.TryGetValue("detection_stride", out var strideStr))
            {
                throw new ArgumentException("initParam does not contain detection_stride element.");
            }

            int.TryParse(strideStr, out _detectionStride);

            Log.Information($"Detection stride: {_detectionStride}");

            _predictor = new YoloPredictor(File.ReadAllBytes(modelPath), modelConfig, option);

            GenerateClassNames();

            LoadRoiDefinitions(preferences);

            // Avoid first time-consuming call in test cases.
            Log.Information($"Warm up model...");
            using var mat = new Mat("Images/Traffic_001.jpg", ImreadModes.Color);
            Detect(mat, 0.3F);
            Log.Information($"Warm up complete.");
        }

        private void GenerateClassNames()
        {
            var predictorMetadata = _predictor.Metadata.CustomMetadataMap;
            var namesData = predictorMetadata["names"];
            string[] idAndNames = namesData.Split(',');
            var names = idAndNames.Select(x => x.Split(':')[1]).ToList();
            names = names.Select(x => x.Trim(new[] { '\'', ' ', '}' })).ToList();

            // TODO: Define detection object type in config file.
            _names.Clear();
            _names.AddRange(names);
        }

        private void LoadRoiDefinitions(Dictionary<string, string>? preferences)
        {
            _onlyDetectRoi = bool.Parse(preferences["OnlyDetectRoi"]);
            _roiX = int.Parse(preferences["RoiX"]);
            _roiY = int.Parse(preferences["RoiY"]);
            _roiWidth = int.Parse(preferences["RoiWidth"]);
            _roiHeight = int.Parse(preferences["RoiHeight"]);
            _minBboxWidth = int.Parse(preferences["MinBboxWidth"]);
            _minBboxHeight = int.Parse(preferences["MinBboxHeight"]);

            _roi = new Rect(_roiX, _roiY, _roiWidth, _roiHeight);
        }

        public int GetClassNumber()
        {
            return _predictor.Metadata.CustomMetadataMap.Count;
        }

        public List<BoundingBox> Detect(Mat image, float thresh = 0.5f)
        {
            if (_detectedCount++ % _detectionStride != 0)
            {
                return new List<BoundingBox>();
            }

            var inputImage = GenerateRoiImage(image);

            YoloPrediction[] detectedObjects = _predictor.Predict(inputImage, thresh, _targetTypes).ToArray();

            _detectedCount++;

            return GenerateBoundingBoxes(detectedObjects);
        }

        private Mat GenerateRoiImage(Mat image)
        {
            Mat inputImage = image;
            if (_onlyDetectRoi)
            {
                if ((_roi.X < image.Width && _roi.X + _roi.Width <= image.Width) &&
                    (_roi.Y < image.Height && _roi.Y + _roi.Height <= image.Height))
                {
                    inputImage = new Mat(image, _roi);
                }
            }

            return inputImage;
        }

        private List<BoundingBox> GenerateBoundingBoxes(YoloPrediction[] detectedObjects)
        {
            int roiXOffset = _onlyDetectRoi ? _roiX : 0;
            int roiYOffset = _onlyDetectRoi ? _roiY : 0;

            var boundingBoxes = new List<BoundingBox>();
            foreach (var prediction in detectedObjects)
            {
                var box = prediction.BoundingBox;

                var boundingBox = new BoundingBox(
                    labelId: prediction.TypeId,
                    label: prediction.Type,
                    confidence: prediction.Confidence,
                    x: box.X + roiXOffset,
                    y: box.Y + roiYOffset,
                    width: box.Width,
                    height: box.Height
                );

                boundingBoxes.Add(boundingBox);
            }

            return boundingBoxes;
        }

        public List<BoundingBox> Detect(byte[] imageData, float thresh = 0.5f)
        {
            if (_detectedCount++ % _detectionStride != 0)
            {
                return new List<BoundingBox>();
            }

            using MemoryStream stream = new MemoryStream(imageData);
            var image = Mat.FromStream(stream, ImreadModes.AnyColor);

            var inputImage = GenerateRoiImage(image);
            YoloPrediction[] detectedObjects = _predictor.Predict(inputImage, thresh, _targetTypes).ToArray();

            _detectedCount++;

            return GenerateBoundingBoxes(detectedObjects);
        }

        public List<BoundingBox> Detect(string imageFile, float thresh = 0.5f)
        {
            if (_detectedCount++ % _detectionStride != 0)
            {
                return new List<BoundingBox>();
            }

            var image = Cv2.ImRead(imageFile);

            var inputImage = GenerateRoiImage(image);
            YoloPrediction[] detectedObjects = _predictor.Predict(inputImage, thresh, _targetTypes).ToArray();

            _detectedCount++;

            return GenerateBoundingBoxes(detectedObjects);
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
