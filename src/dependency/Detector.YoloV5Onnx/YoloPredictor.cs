using Detector.YoloV5Onnx.Utils;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using Size = System.Drawing.Size;

namespace Detector.YoloV5Onnx
{
    public class YoloPredictor
    {
        private readonly YoloModel _yoloModel;
        private readonly InferenceSession _inferenceSession;
        private readonly ModelMetadata _modelMetadata;

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

            _inferenceSession = new InferenceSession(model, sessionOptions ?? new SessionOptions());
            _modelMetadata = _inferenceSession.ModelMetadata;
        }

        /*public IReadOnlyList<YoloPrediction> Predict(Mat image, float targetConfidence, List<string> targetTypes = null)
        {
            var imageSize = new Size(image.Width, image.Height);

            List<NamedOnnxValue> inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_yoloModel.Input, ExtractPixels(image))
            };

            var onnxOutput = _inferenceSession.Run(inputs, _yoloModel.Outputs);
            List<YoloPrediction> predictions = Suppress(ParseOutput(
                onnxOutput.First().Value as DenseTensor<float>, imageSize,
                targetConfidence, targetTypes));

            onnxOutput.Dispose();

            return predictions;
        }*/

        public IReadOnlyList<YoloPrediction> Predict(Bitmap image, float targetConfidence, List<string> targetTypes = null)
        {
            var imageSize = new Size(image.Width, image.Height);

            List<NamedOnnxValue> inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_yoloModel.Input, ExtractPixels(image))
            };

            var onnxOutput = _inferenceSession.Run(inputs, _yoloModel.Outputs);
            List<YoloPrediction> predictions = Suppress(ParseOutput(
                onnxOutput.First().Value as DenseTensor<float>, imageSize,
                targetConfidence, targetTypes));

            onnxOutput.Dispose();

            return predictions;
        }

        /*private Tensor<float> ExtractPixels(Mat image)
        {
            Mat resizedImg = image.Resize(new OpenCvSharp.Size(_yoloModel.Width, _yoloModel.Height));
            resizedImg = resizedImg.CvtColor(ColorConversionCodes.BGR2RGB);

            int channels = resizedImg.Channels();
            int imgHeight = resizedImg.Rows;
            int imgWidth = resizedImg.Cols;

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
                byte* dataPtr = (byte*)resizedImg.DataPointer;
                int step = (int)resizedImg.Step(); // 一行的字节数

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
        }*/

        private Tensor<float> ExtractPixels(Bitmap image)
        {
            Bitmap resizedImg = ResizeBitmap(image);

            Rectangle rectangle = new Rectangle(0, 0, resizedImg.Width, resizedImg.Height);
            BitmapData bitmapData = resizedImg.LockBits(rectangle, ImageLockMode.ReadOnly, resizedImg.PixelFormat);
            int width = bitmapData.Width;
            int height = bitmapData.Height;
            int bytesPerPixel = Image.GetPixelFormatSize(resizedImg.PixelFormat) / 8;

            DenseTensor<float> tensor = new DenseTensor<float>(new[] { _yoloModel.BatchSize, _yoloModel.Channels, _yoloModel.Height, _yoloModel.Width });

            unsafe
            {
                Span<float> rTensorSpan = tensor.Buffer.Span.Slice(0, height * width);
                Span<float> gTensorSpan = tensor.Buffer.Span.Slice(height * width, height * width);
                Span<float> bTensorSpan = tensor.Buffer.Span.Slice(height * width * 2, height * width);

                byte* scan0 = (byte*)bitmapData.Scan0;
                int stride = bitmapData.Stride;

                for (int y = 0; y < height; y++)
                {
                    byte* row = scan0 + (y * stride);
                    int rowOffset = y * width;

                    for (int x = 0; x < width; x++)
                    {
                        int bIndex = x * bytesPerPixel;
                        int point = rowOffset + x;

                        rTensorSpan[point] = row[bIndex + 2] / 255.0f; //R
                        gTensorSpan[point] = row[bIndex + 1] / 255.0f; //G
                        bTensorSpan[point] = row[bIndex] / 255.0f; //B
                    }
                }

                resizedImg.UnlockBits(bitmapData);
            }

            return tensor;
        }

        private Bitmap ResizeBitmap(Bitmap image)
        {
            if (image.Width == _yoloModel.Width || image.Height == _yoloModel.Height)
            {
                return image;
            }

            PixelFormat format = image.PixelFormat;

            Bitmap output = new Bitmap(_yoloModel.Width, _yoloModel.Height, format);

            (float xRatio, float yRatio) = (_yoloModel.Width / (float)image.Width, _yoloModel.Height / (float)image.Height);
            float ratio = float.Min(xRatio, yRatio);
            (int targetWidth, int targetHeight) = ((int)(image.Width * ratio), (int)(image.Height * ratio));
            (int x, int y) = ((_yoloModel.Width / 2) - (targetWidth / 2), (_yoloModel.Height / 2) - (targetHeight / 2));

            Rectangle roi = new Rectangle(x, y, targetWidth, targetHeight);

            using (Graphics graphics = Graphics.FromImage(output))
            {
                graphics.Clear(Color.FromArgb(0, 0, 0, 0));

                graphics.SmoothingMode = SmoothingMode.None;
                graphics.InterpolationMode = InterpolationMode.Default;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;

                graphics.DrawImage(image, roi);
            }

            return output;
        }

        private YoloPrediction[] ParseOutput(DenseTensor<float> output, Size imageSize, float targetConfidence, List<string> targetTypes)
        {
            unsafe
            {
                List<YoloPrediction> result = new List<YoloPrediction>();

                (int width, int height) = (imageSize.Width, imageSize.Height);
                (float xGain, float yGain) = (_yoloModel.Width / (float)width, _yoloModel.Height / (float)height);
                float gain = Math.Min(xGain, yGain);
                (float xPad, float yPad) = ((_yoloModel.Width - width * gain) / 2, (_yoloModel.Height - height * gain) / 2);
                var spanOutput = output.Buffer.Span;

                for (int i = 0; i < (int)output.Length / _yoloModel.Dimensions; i++)
                {
                    int iOffset = i * _yoloModel.Dimensions;

                    if (spanOutput[iOffset + 4] <= _yoloModel.Confidence)
                        continue;

                    for (int j = 5; j < _yoloModel.Dimensions; j++)
                    {
                        spanOutput[i * _yoloModel.Dimensions + j] *= spanOutput[i * _yoloModel.Dimensions + 4];

                        int objectTypeId = j - 5;

                        if (targetTypes != null && !targetTypes.Contains(_yoloModel.Names[objectTypeId]))
                            continue;

                        if (spanOutput[iOffset + j] < targetConfidence)
                            continue;

                        if (spanOutput[i * _yoloModel.Dimensions + j] <= _yoloModel.MulConfidence)
                            continue;

                        float xMin = ((spanOutput[iOffset + 0] - spanOutput[iOffset + 2] / 2) - xPad) / gain; // Unpad bbox top-left-x to original
                        float yMin = ((spanOutput[iOffset + 1] - spanOutput[iOffset + 3] / 2) - yPad) / gain; // Unpad bbox top-left-y to original
                        float xMax = ((spanOutput[iOffset + 0] + spanOutput[iOffset + 2] / 2) - xPad) / gain; // Unpad bbox bottom-right-x to original
                        float yMax = ((spanOutput[iOffset + 1] + spanOutput[iOffset + 3] / 2) - yPad) / gain; // Unpad bbox bottom-right-y to original

                        xMin = Clamp(xMin, 0, width); // Clip bbox top-left-x to boundaries
                        yMin = Clamp(yMin, 0, height); // Clip bbox top-left-y to boundaries
                        xMax = Clamp(xMax, 0, width - 1); // Clip bbox bottom-right-x to boundaries
                        yMax = Clamp(yMax, 0, height - 1); // Clip bbox bottom-right-y to boundaries

                        YoloPrediction prediction = new YoloPrediction()
                        {
                            TypeId = objectTypeId,
                            Type = _yoloModel.Names[objectTypeId],
                            Confidence = spanOutput[iOffset + j],
                            BoundingBox = new Rectangle((int)xMin, (int)yMin, (int)(xMax - xMin), (int)(yMax - yMin))
                        };

                        result.Add(prediction);
                    }
                }

                return result.ToArray();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float Clamp(float value, float min, float max) => (value < min) ? min : (value > max) ? max : value;

        private List<YoloPrediction> Suppress(YoloPrediction[] predictions)
        {
            List<YoloPrediction> result = new List<YoloPrediction>(predictions);

            foreach (YoloPrediction prediction in predictions)
            {
                foreach (YoloPrediction current in result.ToArray())
                {
                    if (current == prediction)
                        continue;

                    if (Metrics.IntersectionOverUnion(prediction.BoundingBox, current.BoundingBox) >= _yoloModel.Overlap)
                    {
                        if (prediction.Confidence >= current.Confidence)
                        {
                            result.Remove(current);
                        }
                    }
                }
            }

            return result;
        }
    }
}
