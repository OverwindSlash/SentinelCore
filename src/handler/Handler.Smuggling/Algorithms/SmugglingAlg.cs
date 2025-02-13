using SentinelCore.Domain.Abstractions.AnalysisHandler;
using SentinelCore.Domain.Entities.AnalysisDefinitions.Geometrics;
using SentinelCore.Domain.Entities.AnalysisEngine;
using SentinelCore.Domain.Entities.ObjectDetection;
using SentinelCore.Domain.Entities.VideoStream;
using SentinelCore.Service.Pipeline;
using Serilog;

namespace Handler.Smuggling.Algorithms
{
    public class SmugglingAlg : IAnalysisHandler
    {
        private readonly AnalysisPipeline _pipeline;
        private readonly string _eventName;
        private IServiceProvider _serviceProvider;

        private readonly int _maxPersonCount;
        private readonly int _eventSustainSec;

        private readonly TtlFlagManager<string> _flagManager;

        public string HandlerName => nameof(SmugglingAlg);

        public SmugglingAlg(AnalysisPipeline pipeline, Dictionary<string, string> preferences)
        {
            _pipeline = pipeline;

            _maxPersonCount = int.Parse(preferences["MaxPersonCount"]);
            _eventSustainSec = int.Parse(preferences["EventSustainSec"]);

            _flagManager = new TtlFlagManager<string>();
        }

        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public AnalysisResult Analyze(Frame frame)
        {
            if (CheckPeopleGathering(frame))
            {
                _flagManager.SetValue("people_gathering", true, _eventSustainSec);
            }

            _flagManager.TryGetValue("people_gathering", out var isPeopleGathering);
            frame.SetProperty("people_gathering", isPeopleGathering);

            return new AnalysisResult(true);
        }

        private bool CheckPeopleGathering(Frame frame)
        {
            int counting = 0;

            foreach (var detectedObject in frame.DetectedObjects)
            {
                if (!detectedObject.IsUnderAnalysis)
                {
                    continue;
                }

                if (detectedObject.Label.ToLower() != "person")
                {
                    continue;
                }

                counting++;
            }

            frame.SetProperty("person_count", counting);

            return (counting >= _maxPersonCount);
        }

        public void Dispose()
        {
            
        }
    }
}
