using MessagePipe;
using SentinelCore.Domain.Abstractions.EventHandler;
using SentinelCore.Domain.Abstractions.RegionManager;
using SentinelCore.Domain.Entities.AnalysisDefinitions;
using SentinelCore.Domain.Entities.AnalysisDefinitions.Geometrics;
using SentinelCore.Domain.Entities.ObjectDetection;
using SentinelCore.Domain.Events.AnalysisEngine;
using System.Collections.Concurrent;

namespace RegionManager.DefinitionBased
{
    public class RegionManager : IRegionManager, IEventSubscriber<ObjectExpiredEvent>
    {
        public const int NoNeedToCalculateLane = -1;
        public const int NotInAnyLaneIndex = 0;

        public ImageAnalysisDefinition AnalysisDefinition { get; private set; }

        // Id(type:trackingId) -> trakingId, actually it is a Set.
        private readonly ConcurrentDictionary<string, long> _allTrackingIdsUnderAnalysis;

        private ISubscriber<ObjectExpiredEvent> _subscriber;
        private IDisposable _disposableSubscriber;

        public RegionManager()
        {
            _allTrackingIdsUnderAnalysis = new ConcurrentDictionary<string, long>();
        }

        public void LoadAnalysisDefinition(string jsonFile, int imageWidth, int imageHeight)
        {
            _allTrackingIdsUnderAnalysis.Clear();

            AnalysisDefinition = ImageAnalysisDefinition.LoadFromJson(jsonFile, imageWidth, imageHeight);
        }

        public void CalcRegionProperties(List<DetectedObject> detectedObjects)
        {
            foreach (DetectedObject detectedObject in detectedObjects)
            {
                DetermineAnalyzableObject(detectedObject);
                CalculateLane(detectedObject);
            }
        }

        private void DetermineAnalyzableObject(DetectedObject detectedObject)
        {
            if (AnalysisDefinition.IsObjectAnalyzableRetain &&
                _allTrackingIdsUnderAnalysis.ContainsKey(detectedObject.Id))
            {
                detectedObject.IsUnderAnalysis = true;
                return;
            }

            NormalizedPoint point = new NormalizedPoint(AnalysisDefinition.ImageWidth, AnalysisDefinition.ImageHeight,
                detectedObject.CenterX, detectedObject.CenterY);

            foreach (AnalysisArea analysisArea in AnalysisDefinition.AnalysisAreas)
            {
                if (analysisArea.IsPointInPolygon(point))
                {
                    detectedObject.IsUnderAnalysis = true;
                    break;
                }
            }

            foreach (ExcludedArea excludedArea in AnalysisDefinition.ExcludedAreas)
            {
                if (excludedArea.IsPointInPolygon(point))
                {
                    detectedObject.IsUnderAnalysis = false;
                    break;
                }
            }

            if (detectedObject.IsUnderAnalysis)
            {
                _allTrackingIdsUnderAnalysis.TryAdd(detectedObject.Id, detectedObject.TrackingId);
            }
        }

        private void CalculateLane(DetectedObject detectedObject)
        {
            if (!detectedObject.IsUnderAnalysis)
            {
                return;
            }

            NormalizedPoint point = new NormalizedPoint(AnalysisDefinition.ImageWidth, AnalysisDefinition.ImageHeight,
                detectedObject.CenterX, detectedObject.CenterY);

            foreach (Lane lane in AnalysisDefinition.Lanes)
            {
                if (lane.IsPointInPolygon(point))
                {
                    detectedObject.LaneIndex = lane.Index;
                }
            }
        }

        private void ReleaseAnalyzableObjectById(string id)
        {
            if (_allTrackingIdsUnderAnalysis.ContainsKey(id))
            {
                _allTrackingIdsUnderAnalysis.TryRemove(id, out var value);
            }
        }

        public int GetAnalyzableObjectCount()
        {
            return _allTrackingIdsUnderAnalysis.Count;
        }

        public void SetSubscriber(ISubscriber<ObjectExpiredEvent> subscriber)
        {
            _subscriber = subscriber;
            _disposableSubscriber = _subscriber.Subscribe(ProcessEvent);
        }

        public void ProcessEvent(ObjectExpiredEvent @event)
        {
            Task.Run(() =>
            {
                ReleaseAnalyzableObjectById(@event.Id);
            });
        }

        public void Dispose()
        {
            _disposableSubscriber?.Dispose();
        }
    }
}
