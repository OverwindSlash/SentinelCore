using OpenCvSharp;
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

        private Point _boatPosition;

        public string HandlerName => nameof(SmugglingAlg);

        public SmugglingAlg(AnalysisPipeline pipeline, Dictionary<string, string> preferences)
        {
            _pipeline = pipeline;

            _maxPersonCount = int.Parse(preferences["MaxPersonCount"]);
            _eventSustainSec = int.Parse(preferences["EventSustainSec"]);

            _flagManager = new TtlFlagManager<string>();

            _boatPosition = new Point(0, 0);
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

            if (CheckBoatExistence(frame))
            {
                _flagManager.SetValue("boat_existence", true, _eventSustainSec);
            }
            _flagManager.TryGetValue("boat_existence", out var isBoatExistence);
            frame.SetProperty("boat_existence", isBoatExistence);

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

        private bool CheckBoatExistence(Frame frame)
        {
            foreach (var detectedObject in frame.DetectedObjects)
            {
                if (!detectedObject.IsUnderAnalysis)
                {
                    continue;
                }

                if (detectedObject.Label.ToLower() == "boat")
                {
                    return true;
                }
            }

            return false;
        }

        private double CalculatePersonBoatAverageDistance(Frame frame)
        {
            if (_boatPosition is { X: 0, Y: 0 })
            {
                return -1;
            }

            double totalDistance = 0;
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

                var personCenter = new Point(detectedObject.X + detectedObject.Width / 2, detectedObject.Y + detectedObject.Height / 2);

                totalDistance += CalculateDistance(personCenter, _boatPosition);
                counting++;
            }

            return totalDistance / counting;
        }

        private double CalculateDistance(Point p1, Point p2)
        {
            return Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
        }

        public void Dispose()
        {
            
        }
    }
}
