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
        private List<string> _names = new();

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

            _predictor = new YoloPredictor(File.ReadAllBytes(modelPath), modelConfig, option);

            var predictorMetadata = _predictor.Metadata.CustomMetadataMap;
            var namesData = predictorMetadata["names"];
            string[] idAndNames = namesData.Split(',');
            var names = idAndNames.Select(x => x.Split(':')[1]).ToList();
            names = names.Select(x => x.Trim(new[] { '\'', ' ', '}' })).ToList();

            // TODO: Define detection object type in config file.
            _names.Clear();
            _names.AddRange(names);

            // Avoid first time-consuming call in test cases.
            Log.Information($"Warm up model...");
            using var mat = new Mat("Images/Traffic_001.jpg", ImreadModes.Color);
            Detect(mat, 0.3F);
            Log.Information($"Warm up complete.");
        }

        public int GetClassNumber()
        {
            return _predictor.Metadata.CustomMetadataMap.Count;
        }

        public List<BoundingBox> Detect(Mat image, float thresh = 0.5f)
        {
            YoloPrediction[] detectedObjects = _predictor.Predict(image, thresh, _targetTypes).ToArray();

            return GenerateBoundingBoxes(detectedObjects);
        }

        private List<BoundingBox> GenerateBoundingBoxes(YoloPrediction[] detectedObjects)
        {
            var boundingBoxes = new List<BoundingBox>();
            foreach (var prediction in detectedObjects)
            {
                var box = prediction.BoundingBox;

                var boundingBox = new BoundingBox(
                    labelId: prediction.TypeId,
                    label: prediction.Type,
                    confidence: prediction.Confidence,
                    x: box.X,
                    y: box.Y,
                    width: box.Width,
                    height: box.Height
                );

                boundingBoxes.Add(boundingBox);
            }

            return boundingBoxes;
        }

        public List<BoundingBox> Detect(byte[] imageData, float thresh = 0.5f)
        {
            using MemoryStream stream = new MemoryStream(imageData);

            var image = Mat.FromStream(stream, ImreadModes.AnyColor);

            YoloPrediction[] detectedObjects = _predictor.Predict(image, thresh, _targetTypes).ToArray();

            return GenerateBoundingBoxes(detectedObjects);
        }

        public List<BoundingBox> Detect(string imageFile, float thresh = 0.5f)
        {
            var image = Cv2.ImRead(imageFile);

            YoloPrediction[] detectedObjects = _predictor.Predict(image, thresh, _targetTypes).ToArray();

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
