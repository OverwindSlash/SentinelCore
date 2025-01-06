using SentinelCore.Domain.Abstractions.AnalysisHandler;
using SentinelCore.Domain.Entities.AnalysisDefinitions.Geometrics;
using SentinelCore.Domain.Entities.AnalysisEngine;
using SentinelCore.Domain.Entities.VideoStream;
using SentinelCore.Service.Pipeline;

namespace Handler.ObjectDensity.Algorithms
{
    public class ObjectDensityAlg : IAnalysisHandler
    {
        private readonly AnalysisPipeline _pipeline;
        private readonly string _eventName;
        private IServiceProvider _serviceProvider;

        private readonly string _objType;
        private readonly int _maxCount;

        private readonly string _countingRegionStr;
        private NormalizedPolygon _countingRegion;
        

        public string HandlerName => nameof(ObjectDensityAlg);

        public ObjectDensityAlg(AnalysisPipeline pipeline, Dictionary<string, string> preferences)
        {
            _pipeline = pipeline;
            _eventName = preferences["EventName"];
            _objType = preferences["ObjectType"].ToLower();
            _countingRegionStr = preferences["CountingRegion"];
            _maxCount = int.Parse(preferences["MaxCount"]);
            _countingRegion = null;
        }

        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public AnalysisResult Analyze(Frame frame)
        {
            if (_countingRegion == null)
            {
                _countingRegion = new NormalizedPolygon();
                _countingRegion.SetImageSize(frame.Scene.Width, frame.Scene.Height);

                var cords = _countingRegionStr.Split(',');

                var defaultTopLeft = new NormalizedPoint(0, 0);
                defaultTopLeft.SetImageSize(frame.Scene.Width, frame.Scene.Height);
                
                var defaultTopRight = new NormalizedPoint(0, 1);
                defaultTopRight.SetImageSize(frame.Scene.Width, frame.Scene.Height);

                var defaultBottomRight = new NormalizedPoint(1, 1);
                defaultBottomRight.SetImageSize(frame.Scene.Width, frame.Scene.Height);

                var defaultBottomLeft = new NormalizedPoint(1, 0);
                defaultBottomLeft.SetImageSize(frame.Scene.Width, frame.Scene.Height);

                if (cords.Length > 6)   // at least 3 points
                {
                    try
                    {
                        for (int i = 0; i < cords.Length; i += 2)
                        {
                            var point = new NormalizedPoint(double.Parse(cords[i]), double.Parse(cords[i + 1]));
                            _countingRegion.Points.Add(point);
                        }
                    }
                    catch (Exception e)
                    {
                        _countingRegion.Points.Clear();

                        _countingRegion.Points.Add(defaultTopLeft);
                        _countingRegion.Points.Add(defaultTopRight);
                        _countingRegion.Points.Add(defaultBottomRight);
                        _countingRegion.Points.Add(defaultBottomLeft);
                    }
                }
                else
                {
                    _countingRegion.Points.Add(defaultTopLeft);
                    _countingRegion.Points.Add(defaultTopRight);
                    _countingRegion.Points.Add(defaultBottomRight);
                    _countingRegion.Points.Add(defaultBottomLeft);
                }
            }

            int counting = 1;

            foreach (var detectedObject in frame.DetectedObjects)
            {
                if (!detectedObject.IsUnderAnalysis)
                {
                    continue;
                }

                if (detectedObject.Label.ToLower() != _objType)
                {
                    continue;
                }

                var objectCenter = new NormalizedPoint(frame.Scene.Width, frame.Scene.Height, 
                    detectedObject.BottomCenterX, detectedObject.BottomCenterY);

                if (!_countingRegion.IsPointInPolygon(objectCenter))
                {
                    continue;
                }

                if (counting > _maxCount)
                {
                    Console.WriteLine($"WARNING: {detectedObject.Label} number: {counting} in detection region, exceed max thresh: {_maxCount}.");
                }
                else
                {
                    Console.WriteLine($"INFO: {detectedObject.Label} number: {counting} in detection region");
                }

                counting++;
            }

            frame.SetProperty("counting", counting - 1);

            return new AnalysisResult(true);
        }

        public void Dispose()
        {
        }
    }
}
