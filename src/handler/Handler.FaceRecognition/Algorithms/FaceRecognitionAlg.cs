using System.Diagnostics;
using FaceONNX;
using Microsoft.ML.OnnxRuntime;
using OpenCvSharp;
using SentinelCore.Domain.Abstractions.AnalysisHandler;
using SentinelCore.Domain.Entities.AnalysisEngine;
using SentinelCore.Domain.Entities.VideoStream;
using SentinelCore.Service.Pipeline;
using Serilog;

namespace Handler.FaceRecognition.Algorithms
{
    public class FaceRecognitionAlg : IAnalysisHandler
    {
        private readonly AnalysisPipeline _pipeline;
        private readonly string _eventName;

        private readonly FaceDetector _faceDetector;
        private readonly Face68LandmarksExtractor _faceLandmarksExtractor;
        private readonly FaceEmbedder _faceEmbedder;
        private readonly Embeddings _faceEmbeddings;

        public string HandlerName => nameof(FaceRecognitionAlg);

        private IServiceProvider _serviceProvider;

        public FaceRecognitionAlg(AnalysisPipeline pipeline, Dictionary<string, string> preferences)
        {
            _pipeline = pipeline;
            _eventName = preferences["EventName"];

            bool useGpu = bool.Parse(preferences["UseGpu"]);
            int gpuId = int.Parse(preferences["GpuId"]);

            using var options = useGpu ? SessionOptions.MakeSessionOptionWithCudaProvider(gpuId) : new SessionOptions();

            Log.Information($"Face recognition use {(useGpu ? "GPU" : "CPU")} device");

            _faceDetector = new FaceDetector(options);
            _faceLandmarksExtractor = new Face68LandmarksExtractor(options);
            _faceEmbedder = new FaceEmbedder(options);
        }

        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public AnalysisResult Analyze(Frame frame)
        {
            var embeddings = GetEmbedding(frame.Scene);

            return new AnalysisResult(true);
        }

        private List<float[]> GetEmbedding(Mat image)
        {
            var startNew = Stopwatch.StartNew();
            var array = GetImageFloatArray(image);
            Console.WriteLine(startNew.ElapsedMilliseconds);
            var faceRectangles = _faceDetector.Forward(array).ToList();
            

            List<float[]> _faceEmbeddings = new List<float[]>();

            foreach (var faceRectangle in faceRectangles)
            {
                if (faceRectangle == null) continue;

                // landmarks
                var points = _faceLandmarksExtractor.Forward(array, faceRectangle.Box);
                var angle = points.RotationAngle;

                // alignment
                var aligned = FaceProcessingExtensions.Align(array, faceRectangle.Box, angle);
                _faceEmbeddings.Add(_faceEmbedder.Forward(aligned));
            }

            return _faceEmbeddings;
        }

        /*static float[][,] GetImageFloatArray(Mat image)
        {
            // 确保图像是三通道的
            if (image.Channels() != 3)
            {
                throw new ArgumentException("图像必须是三通道的 BGR 格式。");
            }

            // 转换图像为32位浮点型，并归一化到0-1
            Mat floatImage = new Mat();
            image.ConvertTo(floatImage, MatType.CV_32FC3, 1.0 / 255.0);

            // 分离通道，OpenCV的顺序是B, G, R
            Mat[] channels = Cv2.Split(floatImage);

            int height = image.Rows;
            int width = image.Cols;

            float[][,] array = new float[3][,]
            {
                new float[height, width],
                new float[height, width],
                new float[height, width]
            };

            for (int c = 0; c < 3; c++)
            {
                // 获取单个通道的数据
                float[] channelData = new float[height * width];
                channels[c].GetArray(out channelData);

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        array[c][y, x] = channelData[y * width + x];
                    }
                }
            }

            return array;
        }*/

        public static float[][,] GetImageFloatArray(Mat image)
        {
            if (image.Channels() != 3)
            {
                throw new ArgumentException("图像必须是三通道的 BGR 格式。");
            }

            int height = image.Rows;
            int width = image.Cols;

            Mat floatImage = new Mat();
            image.ConvertTo(floatImage, MatType.CV_32FC3, 1.0 / 255.0);

            float[][,] array = new float[3][,]
            {
                new float[height, width],
                new float[height, width],
                new float[height, width]
            };

            unsafe
            {
                float* ptr = (float*)floatImage.DataPointer;
                for (int y = 0; y < height; y++)
                {
                    int rowOffset = y * width * 3;
                    for (int x = 0; x < width; x++)
                    {
                        int dataIndex = rowOffset + x * 3;
                        array[0][y, x] = ptr[dataIndex + 0]; // B
                        array[1][y, x] = ptr[dataIndex + 1]; // G
                        array[2][y, x] = ptr[dataIndex + 2]; // R
                    }
                }
            }

            return array;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
