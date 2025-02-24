using OpenCvSharp;
using SentinelCore.Domain.Entities.ObjectDetection;

namespace SentinelCore.Domain.Abstractions.ObjectDetector
{
    public interface IObjectDetector : IDisposable
    {
        void PrepareEnv(Dictionary<string, string>? envParam = null);
        void Init(Dictionary<string, string>? initParam = null, 
            Dictionary<string, string>? preferences = null);

        int GetClassNumber();
        List<BoundingBox> Detect(Mat image, float thresh = 0.5f);
        List<BoundingBox> Detect(byte[] imageData, float thresh = 0.5f);
        List<BoundingBox> Detect(string imageFile, float thresh = 0.5f);

        void Close();
        void CleanupEnv();
    }
}
