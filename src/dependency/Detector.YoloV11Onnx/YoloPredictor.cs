using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;
using Size = OpenCvSharp.Size;

namespace Detector.YoloV11Onnx
{
    public class YoloPredictor
    {
        private readonly YoloModel _yoloModel;
        private readonly InferenceSession _inferenceSession;
        private readonly ModelMetadata _modelMetadata;

        private Size _originalImageSize;
        private float _resizeScales;

        public ModelMetadata Metadata => _modelMetadata;

        public YoloPredictor(byte[] model, string modelConfig, SessionOptions sessionOptions = null)
        {
            _yoloModel = new YoloModel();
            try
            {
                _yoloModel.LoadFromJson(modelConfig);
            }
            catch
            {
                // Use default values.
            }

            SessionOptions defaultSessionOptions = new SessionOptions();
            // defaultSessionOptions.ExecutionMode = ExecutionMode.ORT_PARALLEL; // 启用并行执行
            // defaultSessionOptions.EnableCpuMemArena = true; // 启用 CPU 内存池
            // defaultSessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL; // 启用所有图优化

            _inferenceSession = new InferenceSession(model, sessionOptions ?? defaultSessionOptions);
            _modelMetadata = _inferenceSession.ModelMetadata;
        }

        public IReadOnlyList<YoloPrediction> Predict(Mat image, float targetConfidence, List<string> targetTypes = null)
        {
            //Stopwatch preProcessSw = Stopwatch.StartNew();
            var processedImage = PreProcess(image);
            // preProcessSw.Stop();
            // Console.WriteLine($"[YOLO_V11]: PreProcess {preProcessSw.Elapsed.TotalMilliseconds}ms.");

            //Stopwatch blob2TensorSw = Stopwatch.StartNew();
            var inputTensor = BlobFromImage(processedImage);
            // blob2TensorSw.Stop();
            // Console.WriteLine($"[YOLO_V11]: BlobFromImage {blob2TensorSw.Elapsed.TotalMilliseconds}ms.");

            //Stopwatch processTensorSw = Stopwatch.StartNew();
            var result = ProcessTensor(inputTensor, targetConfidence, targetTypes);
            // processTensorSw.Stop();
            // Console.WriteLine($"[YOLO_V11]: ProcessTensor {processTensorSw.Elapsed.TotalMilliseconds}ms.");

            return result;
        }

        private Mat PreProcess(Mat originalImage)
        {
            _originalImageSize = new Size(originalImage.Width, originalImage.Height);

            var processedImage = new Mat();

            if (originalImage.Channels() == 3)
            {
                Cv2.CvtColor(originalImage, processedImage, ColorConversionCodes.BGR2RGB);
            }
            else
            {
                Cv2.CvtColor(originalImage, processedImage, ColorConversionCodes.GRAY2RGB);
            }

            switch (_yoloModel.ModelType)
            {
                case ModelType.YOLO_DETECT_V8:
                case ModelType.YOLO_POSE:
                case ModelType.YOLO_DETECT_V8_HALF:
                case ModelType.YOLO_POSE_V8_HALF: // LetterBox
                {
                    if (originalImage.Cols >= originalImage.Rows)
                    {
                        _resizeScales = (float)originalImage.Cols / _yoloModel.Width;
                        processedImage = processedImage.Resize(new Size(_yoloModel.Width, (int)(originalImage.Rows / _resizeScales)));
                    }
                    else
                    {
                        _resizeScales = (float)originalImage.Rows / _yoloModel.Height;
                        processedImage = processedImage.Resize(new Size((int)(originalImage.Cols / _resizeScales), _yoloModel.Height));
                    }

                    // 创建一个填充黑边的新图像
                    Cv2.CopyMakeBorder(
                        processedImage, processedImage,
                        0, _yoloModel.Height - processedImage.Rows, 0, _yoloModel.Width - processedImage.Cols,
                        BorderTypes.Constant, new Scalar(0, 0, 0));
                    break;
                }
                case ModelType.YOLO_CLS: // CenterCrop
                {
                    int h = originalImage.Rows;
                    int w = originalImage.Cols;
                    int m = Math.Min(h, w);
                    int top = (h - m) / 2;
                    int left = (w - m) / 2;

                    // 裁剪中间部分并调整大小
                    Mat croppedImg = new Mat(processedImage, new Rect(left, top, m, m));
                    Cv2.Resize(croppedImg, processedImage, new Size(_yoloModel.Width, _yoloModel.Height));
                    break;
                }
            }

            return processedImage;
        }

        private Tensor<float> BlobFromImage(Mat iImg)
        {
            int channels = iImg.Channels();
            int imgHeight = iImg.Rows;
            int imgWidth = iImg.Cols;

            DenseTensor<float> tensor = new DenseTensor<float>(new[]
            {
                _yoloModel.BatchSize, _yoloModel.Channels, _yoloModel.Height, _yoloModel.Width
            });

            // 使用 OpenCvSharp 的指针访问 Mat 数据
            unsafe
            {
                // 获取每个通道的 Span
                Span<float> rTensorSpan = tensor.Buffer.Span.Slice(0, imgHeight * imgWidth);
                Span<float> gTensorSpan = tensor.Buffer.Span.Slice(imgHeight * imgWidth, imgHeight * imgWidth);
                Span<float> bTensorSpan = tensor.Buffer.Span.Slice(imgHeight * imgWidth * 2, imgHeight * imgWidth);

                // 获取 Mat 的指针
                byte* dataPtr = (byte*)iImg.DataPointer;
                int step = (int)iImg.Step(); // 一行的字节数

                // 遍历像素并填充到 Tensor
                for (int h = 0; h < imgHeight; h++)
                {
                    byte* row = dataPtr + h * step;
                    int rowOffset = h * imgWidth;

                    for (int w = 0; w < imgWidth; w++)
                    {
                        int pixelOffset = w * channels;
                        int tensorIndex = rowOffset + w;

                        bTensorSpan[tensorIndex] = row[pixelOffset] / 255.0f;        // B通道
                        gTensorSpan[tensorIndex] = row[pixelOffset + 1] / 255.0f;    // G通道
                        rTensorSpan[tensorIndex] = row[pixelOffset + 2] / 255.0f;    // R通道
                    }
                }
            }

            return tensor;
        }

        private List<YoloPrediction> ProcessTensor(Tensor<float> inputTensor, float targetConfidence, List<string> targetTypes = null)
        {
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_yoloModel.Input, inputTensor)
            };

            // 执行推理
            Stopwatch inferenceSw = Stopwatch.StartNew();
            var outputTensor = _inferenceSession.Run(inputs);
            inferenceSw.Stop();
            Console.WriteLine($"[YOLO_V11]: Inference {inferenceSw.Elapsed.TotalMilliseconds}ms.");

            // 获取输出张量数据
            Stopwatch toArraySw = Stopwatch.StartNew();
            DenseTensor<float> resultTensor = (DenseTensor<float>)outputTensor.First().AsTensor<float>();
            var resultArray = resultTensor.ToArray();
            toArraySw.Stop();
            Console.WriteLine($"[YOLO_V11]: ToArray {toArraySw.Elapsed.TotalMilliseconds}ms.");

            if (_yoloModel.ModelType == ModelType.YOLO_DETECT_V8)
            {
                List<int> classIds = new List<int>();
                List<float> confidences = new List<float>();
                List<Rect> boxes = new List<Rect>();

                int signalResultNum = resultTensor.Dimensions[1]; // 类别数 + 坐标数
                int strideNum = resultTensor.Dimensions[2]; // 检测步长

                Stopwatch stopwatchRawData = Stopwatch.StartNew();
                Mat rawData = new Mat(signalResultNum, strideNum, MatType.CV_32F);
                rawData.SetArray(resultArray);
                rawData = rawData.T(); // 转置数据以符合输出格式
                rawData.GetArray<float>(out var data);
                stopwatchRawData.Stop();
                Console.WriteLine($"[YOLO_V11]: Permute {stopwatchRawData.Elapsed.TotalMilliseconds}ms.");

                Stopwatch parseYoloSw = Stopwatch.StartNew();
                Span<float> dataSpan = data; // 将 data 转换为 Span<float>
                for (int i = 0; i < strideNum; ++i)
                {
                    // 提取类别分数并找到最大分数和其索引
                    float maxClassScore = float.MinValue;
                    int classId = -1;

                    // 从偏移量 4 开始遍历类别分数
                    for (int j = 0; j < _yoloModel.Names.Count; ++j)
                    {
                        float score = dataSpan[4 + j];
                        if (score > maxClassScore)
                        {
                            maxClassScore = score;
                            classId = j;
                        }
                    }

                    if (targetTypes != null && !targetTypes.Contains(_yoloModel.Names[classId]))
                    {
                        dataSpan = dataSpan.Slice(signalResultNum);
                        continue;
                    }

                    // 仅当类别分数超过阈值时才进行进一步处理
                    if (maxClassScore > targetConfidence)
                    {
                        confidences.Add(maxClassScore);
                        classIds.Add(classId);

                        // 直接从 dataSpan 获取 bounding box 信息
                        float x = dataSpan[0];
                        float y = dataSpan[1];
                        float w = dataSpan[2];
                        float h = dataSpan[3];

                        int left = (int)((x - 0.5f * w) * _resizeScales);
                        int top = (int)((y - 0.5f * h) * _resizeScales);
                        int width = (int)(w * _resizeScales);
                        int height = (int)(h * _resizeScales);

                        var xMin = Clamp(left, 0, _originalImageSize.Width); // Clip bbox top-left-x to boundaries
                        var yMin = Clamp(top, 0, _originalImageSize.Height); // Clip bbox top-left-y to boundaries
                        var xMax = Clamp(left + width, 0, _originalImageSize.Width - 1); // Clip bbox bottom-right-x to boundaries
                        var yMax = Clamp(top + height, 0, _originalImageSize.Height - 1); // Clip bbox bottom-right-y to boundaries

                        boxes.Add(new Rect(xMin, yMin, xMax - xMin, yMax - yMin));
                    }

                    // 使用 Span 切片操作移动到下一个检测项，而无需创建新数组
                    dataSpan = dataSpan.Slice(signalResultNum);
                }
                parseYoloSw.Stop();
                Console.WriteLine($"[YOLO_V11]: ParseYolo {parseYoloSw.Elapsed.TotalMilliseconds}ms.");

                // 非极大值抑制
                Stopwatch nmsSw = Stopwatch.StartNew();
                CvDnn.NMSBoxes(boxes, confidences,targetConfidence, _yoloModel.Overlap, 
                    out var nmsResult);

                var yoloPredictions = new List<YoloPrediction>();
                foreach (var idx in nmsResult)
                {
                    // if (targetTypes != null && !targetTypes.Contains(_yoloModel.Names[classIds[idx]]))
                    // {
                    //     continue;
                    // }

                    var prediction = new YoloPrediction()
                    {
                        TypeId = classIds[idx],
                        Type = _yoloModel.Names[classIds[idx]],
                        Confidence = confidences[idx],
                        BoundingBox = new Rectangle(boxes[idx].X, boxes[idx].Y, boxes[idx].Width, boxes[idx].Height)
                    };
                    yoloPredictions.Add(prediction);
                }
                nmsSw.Stop();
                Console.WriteLine($"[YOLO_V11]: NMS {nmsSw.Elapsed.TotalMilliseconds}ms.");

                return yoloPredictions;
            }

            return new List<YoloPrediction>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Clamp(int value, int min, int max) => (value < min) ? min : (value > max) ? max : value;
    }
}
