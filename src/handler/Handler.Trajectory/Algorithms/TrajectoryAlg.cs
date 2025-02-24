using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using OpenCvSharp;
using SentinelCore.Domain.Abstractions.AnalysisHandler;
using SentinelCore.Domain.Entities.AnalysisEngine;
using SentinelCore.Domain.Entities.VideoStream;
using SentinelCore.Domain.Events.AnalysisEngine;
using SentinelCore.Service.Pipeline;
using System.Collections.Concurrent;

namespace Handler.Trajectory.Algorithms
{
    public class TrajectoryAlg : ObjectExpiredSubscriber, IAnalysisHandler
    {
        private readonly AnalysisPipeline _pipeline;
        private IServiceProvider _serviceProvider;

        private readonly int _historyLengthThresh;

        private readonly ConcurrentDictionary<string, Queue<Point>> _trackingHistory =
            new ConcurrentDictionary<string, Queue<Point>>();

        public string HandlerName => nameof(TrajectoryAlg);

        public TrajectoryAlg(AnalysisPipeline pipeline, Dictionary<string, string> preferences)
        {
            _pipeline = pipeline;

            _historyLengthThresh = int.Parse(preferences["HistoryLengthThresh"]);
        }

        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;

            var subscriber = serviceProvider.GetService<ISubscriber<ObjectExpiredEvent>>();
            this.SetSubscriber(subscriber);
        }

        public AnalysisResult Analyze(Frame frame)
        {
            foreach (var detectedObject in frame.DetectedObjects)
            {
                if (!detectedObject.IsUnderAnalysis)
                {
                    continue;
                }

                string objectId = detectedObject.Id;

                if (!_trackingHistory.ContainsKey(objectId))
                    _trackingHistory[objectId] = new Queue<Point>();

                _trackingHistory[objectId].Enqueue(new Point(detectedObject.CenterX, detectedObject.CenterY));

                // 保持队列长度不超过设定的历史长度
                if (_trackingHistory[objectId].Count > _historyLengthThresh)
                {
                    _trackingHistory[objectId].Dequeue();
                }
            }

            frame.SetProperty("trajectory", _trackingHistory);

            return new AnalysisResult(true);
        }

        public override void ProcessEvent(ObjectExpiredEvent @event)
        {
            if (_trackingHistory.ContainsKey(@event.Id))
            {
                _trackingHistory.TryRemove(@event.Id, out _);
            }
        }
    }
}
