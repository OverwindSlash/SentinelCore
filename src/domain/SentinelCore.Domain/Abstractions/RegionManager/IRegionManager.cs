using SentinelCore.Domain.Abstractions.EventHandler;
using SentinelCore.Domain.Entities.AnalysisDefinitions;
using SentinelCore.Domain.Entities.ObjectDetection;
using SentinelCore.Domain.Events.AnalysisEngine;

namespace SentinelCore.Domain.Abstractions.RegionManager
{
    public interface IRegionManager : IEventSubscriber<ObjectExpiredEvent>
    {
        public ImageAnalysisDefinition AnalysisDefinition { get; }

        void LoadAnalysisDefinition(string jsonFile, int imageWidth, int imageHeight);
        void CalcRegionProperties(List<DetectedObject> detectedObjects);
    }
}
