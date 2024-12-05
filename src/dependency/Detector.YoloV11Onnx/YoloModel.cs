using System.Text.Json;

namespace Detector.YoloV11Onnx
{
    public enum ModelType
    {
        YOLO_DETECT_V8,
        YOLO_POSE,
        YOLO_DETECT_V8_HALF,
        YOLO_POSE_V8_HALF,
        YOLO_CLS
    }

    public class YoloModel
    {
        public ModelType ModelType { get; set; } = ModelType.YOLO_DETECT_V8;
        public int Width { get; set; } = 640;
        public int Height { get; set; } = 640;
        public int Depth { get; set; } = 3;
        public int Dimensions { get; set; } = 85;
        public int[] Strides { get; set; } = new int[] { 8, 16, 32 };

        public int[][][] Anchors { get; set; } = new int[][][]
        {
            new int[][] { new int[] { 010, 13 }, new int[] { 016, 030 }, new int[] { 033, 023 } },
            new int[][] { new int[] { 030, 61 }, new int[] { 062, 045 }, new int[] { 059, 119 } },
            new int[][] { new int[] { 116, 90 }, new int[] { 156, 198 }, new int[] { 373, 326 } }
        };

        public int[] Shapes { get; set; } = new int[] { 80, 40, 20 };
        public float Confidence { get; set; } = 0.10f;
        public float MulConfidence { get; set; } = 0.25f;
        public float Overlap { get; set; } = 0.45f;
        public int Channels { get; set; } = 3;
        public int BatchSize { get; set; } = 1;
        public string[] Outputs { get; set; } = new[] { "output0" };
        public string Input { get; set; } = "images";
        public List<string> Names { get; set; }

        public void SaveToJson(string filename)
        {
            string jsonString = JsonSerializer.Serialize(this);
            File.WriteAllText(filename, jsonString);
        }

        public void LoadFromJson(string filename)
        {
            string jsonString = File.ReadAllText(filename);
            var loadedModel = JsonSerializer.Deserialize<YoloModel>(jsonString);

            this.Width = loadedModel.Width;
            this.Height = loadedModel.Height;
            this.Depth = loadedModel.Depth;
            this.Dimensions = loadedModel.Dimensions;
            this.Strides = loadedModel.Strides;
            this.Anchors = loadedModel.Anchors;
            this.Shapes = loadedModel.Shapes;
            this.Confidence = loadedModel.Confidence;
            this.MulConfidence = loadedModel.MulConfidence;
            this.Overlap = loadedModel.Overlap;
            this.Channels = loadedModel.Channels;
            this.BatchSize = loadedModel.BatchSize;
            this.Outputs = loadedModel.Outputs;
            this.Input = loadedModel.Input;
            this.Names = loadedModel.Names;
        }
    }
}
