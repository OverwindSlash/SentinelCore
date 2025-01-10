using Handler.RegionAccess.Events;
using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using SentinelCore.Domain.Abstractions.AnalysisHandler;
using SentinelCore.Domain.Entities.AnalysisDefinitions.Geometrics;
using SentinelCore.Domain.Entities.AnalysisEngine;
using SentinelCore.Domain.Entities.VideoStream;
using SentinelCore.Domain.Events.AnalysisEngine;
using SentinelCore.Service.Pipeline;
using System.Collections.Concurrent;

namespace Handler.RegionAccess.Algorithms
{
    public class RegionAccessAlg : ObjectExpiredSubscriber, IAnalysisHandler
    {
        private readonly AnalysisPipeline _pipeline;
        private readonly string _eventName;

        private readonly string _interestAreaName;
        private readonly List<string> _objTypes;

        private IPublisher<EnterRegionEvent> _enterEventPublisher;
        private IPublisher<LeaveRegionEvent> _leaveEventPublisher;

        public string HandlerName => nameof(RegionAccessAlg);

        private IServiceProvider _serviceProvider;

        private ConcurrentDictionary<string, bool> _objLastInRegionStatus;

        public RegionAccessAlg(AnalysisPipeline pipeline, Dictionary<string, string> preferences)
        {
            _pipeline = pipeline;

            _eventName = preferences["EventName"];
            _interestAreaName = preferences["InterestAreaName"];
            _objTypes = preferences["ObjTypes"].Split(',').ToList();

            _objLastInRegionStatus = new ConcurrentDictionary<string, bool>();
        }

        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _enterEventPublisher = _serviceProvider.GetRequiredService<IPublisher<EnterRegionEvent>>();
            _leaveEventPublisher = _serviceProvider.GetRequiredService<IPublisher<LeaveRegionEvent>>();

            var subscriber = serviceProvider.GetService<ISubscriber<ObjectExpiredEvent>>();
            this.SetSubscriber(subscriber);
        }

        public AnalysisResult Analyze(Frame frame)
        {
            var definition = _pipeline.RegionManager.AnalysisDefinition;

            var interestArea = definition.InterestAreas.First(ia => ia.Name == _interestAreaName);

            foreach (var detectedObject in frame.DetectedObjects)
            {
                if (!detectedObject.IsUnderAnalysis)
                {
                    continue;
                }

                if (!_objTypes.Contains(detectedObject.Label.ToLower()))
                {
                    continue;
                }

                var objectCenter = new NormalizedPoint(frame.Scene.Width, frame.Scene.Height,
                    detectedObject.BottomCenterX, detectedObject.BottomCenterY);

                var isObjInArea = interestArea.IsPointInPolygon(objectCenter);

                if (_objLastInRegionStatus.ContainsKey(detectedObject.Id))
                {
                    bool lastInRegionStatus = _objLastInRegionStatus[detectedObject.Id];

                    bool isEnterRegion = !lastInRegionStatus && isObjInArea;
                    bool isInRegion = lastInRegionStatus && isObjInArea;
                    bool isLeaveRegion = lastInRegionStatus && !isObjInArea;

                    if (isEnterRegion || isInRegion)
                    {
                        detectedObject.SetProperty("EnterRegion", true);
                        detectedObject.SetProperty("LeaveRegion", false);
                    }

                    if (isLeaveRegion)
                    {
                        detectedObject.SetProperty("EnterRegion", false);
                        detectedObject.SetProperty("LeaveRegion", true);
                    }

                    _objLastInRegionStatus[detectedObject.Id] = isObjInArea;
                }
                else
                {
                    _objLastInRegionStatus.TryAdd(detectedObject.Id, isObjInArea);
                }

            }

            return new AnalysisResult(true);
        }

        public override void ProcessEvent(ObjectExpiredEvent @event)
        {
            if (_objLastInRegionStatus.ContainsKey(@event.Id))
            {
                _objLastInRegionStatus.TryRemove(@event.Id, out _);
            }
        }

        public void Dispose()
        {
            
        }
    }
}
