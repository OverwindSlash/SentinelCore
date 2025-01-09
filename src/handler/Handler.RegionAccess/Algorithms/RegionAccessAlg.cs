using MessagePipe;
using SentinelCore.Domain.Abstractions.AnalysisHandler;
using SentinelCore.Domain.Entities.AnalysisEngine;
using SentinelCore.Domain.Entities.VideoStream;
using SentinelCore.Domain.Events.AnalysisEngine;
using SentinelCore.Service.Pipeline;
using System.Collections.Concurrent;
using Handler.RegionAccess.Events;
using Microsoft.Extensions.DependencyInjection;
using SentinelCore.Domain.Abstractions.EventHandler;

namespace Handler.RegionAccess.Algorithms
{
    public class RegionAccessAlg : ObjectExpiredSubscriber, IAnalysisHandler
    {
        private readonly AnalysisPipeline _pipeline;
        private readonly string _eventName;

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
            _objTypes = preferences["ObjTypes"].Split(',').ToList();
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


            }
        }

        public override void ProcessEvent(ObjectExpiredEvent @event)
        {

        }

        public void Dispose()
        {
            
        }
    }
}
