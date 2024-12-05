using SentinelCore.Domain.Entities.AnalysisDefinitions;
using SentinelCore.Domain.Entities.ObjectDetection;

namespace SentinelCore.Domain.Abstractions.RegionManager
{
    public interface IRegionManager
    {
        public ImageAnalysisDefinition AnalysisDefinition { get; }

        void LoadAnalysisDefinition(string jsonFile, int imageWidth, int imageHeight);
        void CalcRegionProperties(List<DetectedObject> detectedObjects);
    }
}
