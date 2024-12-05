using OpenCvSharp;
using SentinelCore.Domain.Entities.ObjectDetection;

namespace SentinelCore.Domain.Abstractions.ObjectTracker
{
    public interface IObjectTracker
    {
        void Track(Mat scene, List<DetectedObject> detectedObjects);
    }
}
