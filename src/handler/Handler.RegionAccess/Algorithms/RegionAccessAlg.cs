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
    public enum ObjectRegionState
    {
        Outside,
        Entering,
        Inside,
        Leaving
    }

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

        private ConcurrentDictionary<string, ObjectRegionState> _objRegionStates = new ConcurrentDictionary<string, ObjectRegionState>();

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

            var interestArea = definition.InterestAreas.FirstOrDefault(ia => ia.Name == _interestAreaName);
            if (interestArea == null)
            {
                // 处理未找到兴趣区域的情况
                return new AnalysisResult(false);
            }

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

                // 创建对象的四个角点及中心点
                var topLeft = new NormalizedPoint(frame.Scene.Width, frame.Scene.Height,
                    detectedObject.TopLeftX, detectedObject.TopLeftY);

                var topRight = new NormalizedPoint(frame.Scene.Width, frame.Scene.Height,
                    detectedObject.TopLeftX + detectedObject.Width, detectedObject.TopLeftY);

                var bottomRight = new NormalizedPoint(frame.Scene.Width, frame.Scene.Height,
                    detectedObject.TopLeftX + detectedObject.Width, detectedObject.TopLeftY + detectedObject.Height);

                var bottomLeft = new NormalizedPoint(frame.Scene.Width, frame.Scene.Height,
                    detectedObject.TopLeftX, detectedObject.TopLeftY + detectedObject.Height);

                var objectCenter = new NormalizedPoint(frame.Scene.Width, frame.Scene.Height,
                    detectedObject.CenterX, detectedObject.CenterY);

                // 判断对象是否完全在区域内
                bool isFullyInside = interestArea.IsPointInPolygon(topLeft) &&
                                     interestArea.IsPointInPolygon(topRight) &&
                                     interestArea.IsPointInPolygon(bottomRight) &&
                                     interestArea.IsPointInPolygon(bottomLeft);

                // 判断对象是否完全在区域外
                bool isFullyOutside = !interestArea.IsPointInPolygon(topLeft) &&
                                      !interestArea.IsPointInPolygon(topRight) &&
                                      !interestArea.IsPointInPolygon(bottomRight) &&
                                      !interestArea.IsPointInPolygon(bottomLeft);

                // 部分在区域内
                bool isPartiallyInside = !isFullyInside && !isFullyOutside;
                

                // 确定对象中心是否在区域内
                bool isObjInArea = interestArea.IsPointInPolygon(objectCenter);

                // 获取对象的前一状态
                _objRegionStates.TryGetValue(detectedObject.Id, out var previousState);

                ObjectRegionState currentState = previousState;

                // 状态转换逻辑
                if (isFullyInside)
                {
                    switch (previousState)
                    {
                        case ObjectRegionState.Outside:
                        case ObjectRegionState.Leaving:
                            currentState = ObjectRegionState.Entering;
                            break;
                        case ObjectRegionState.Entering:
                        case ObjectRegionState.Inside:
                            currentState = ObjectRegionState.Inside;
                            break;
                        default:
                            currentState = ObjectRegionState.Inside;
                            break;
                    }
                }
                else if (isPartiallyInside)
                {
                    if (isObjInArea)
                    {
                        switch (previousState)
                        {
                            case ObjectRegionState.Outside:
                            case ObjectRegionState.Leaving:
                                currentState = ObjectRegionState.Entering;
                                break;
                            case ObjectRegionState.Entering:
                                currentState = ObjectRegionState.Entering;
                                break;
                            case ObjectRegionState.Inside:
                                currentState = ObjectRegionState.Inside;
                                break;
                            default:
                                currentState = ObjectRegionState.Entering;
                                break;
                        }
                    }
                    else
                    {
                        switch (previousState)
                        {
                            case ObjectRegionState.Inside:
                            case ObjectRegionState.Entering:
                                currentState = ObjectRegionState.Leaving;
                                break;
                            case ObjectRegionState.Leaving:
                                currentState = ObjectRegionState.Leaving;
                                break;
                            case ObjectRegionState.Outside:
                                currentState = ObjectRegionState.Outside;
                                break;
                            default:
                                currentState = ObjectRegionState.Leaving;
                                break;
                        }
                    }
                }
                else // isFullyOutside
                {
                    switch (previousState)
                    {
                        case ObjectRegionState.Inside:
                        case ObjectRegionState.Entering:
                            currentState = ObjectRegionState.Leaving;
                            break;
                        case ObjectRegionState.Leaving:
                        case ObjectRegionState.Outside:
                            currentState = ObjectRegionState.Outside;
                            break;
                        default:
                            currentState = ObjectRegionState.Outside;
                            break;
                    }
                }

                // 根据当前状态设置属性
                switch (currentState)
                {
                    case ObjectRegionState.Entering:
                        detectedObject.SetProperty("EnterRegion", true);
                        detectedObject.SetProperty("InRegion", false);
                        detectedObject.SetProperty("LeaveRegion", false);
                        break;

                    case ObjectRegionState.Inside:
                        detectedObject.SetProperty("EnterRegion", false);
                        detectedObject.SetProperty("InRegion", true);
                        detectedObject.SetProperty("LeaveRegion", false);
                        break;

                    case ObjectRegionState.Leaving:
                        detectedObject.SetProperty("EnterRegion", false);
                        detectedObject.SetProperty("InRegion", false);
                        detectedObject.SetProperty("LeaveRegion", true);
                        break;

                    case ObjectRegionState.Outside:
                    default:
                        detectedObject.SetProperty("EnterRegion", false);
                        detectedObject.SetProperty("InRegion", false);
                        detectedObject.SetProperty("LeaveRegion", false);
                        break;
                }

                // 更新对象的当前状态
                _objRegionStates[detectedObject.Id] = currentState;
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
