using Handler.Ocr.Events;
using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using OpenCvSharp;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models.Local;
using SentinelCore.Domain.Abstractions.AnalysisHandler;
using SentinelCore.Domain.Abstractions.SnapshotManager;
using SentinelCore.Domain.Entities.AnalysisEngine;
using SentinelCore.Domain.Entities.VideoStream;
using SentinelCore.Domain.Events.AnalysisEngine;
using SentinelCore.Service.Pipeline;

namespace Handler.Ocr.Algorithms
{
    public class PaddleSharpOcrAlg : ObjectExpiredSubscriber, IAnalysisHandler
    {
        private readonly AnalysisPipeline _pipeline;

        private IPublisher<OcrResultEvent> _eventPublisher;

        private readonly string _ocrType;
        private readonly PaddleOcrAll _paddleOcrAll;
        private readonly float _scoreThresh;

        public string HandlerName => nameof(PaddleSharpOcrAlg);

        private IServiceProvider _serviceProvider;

        public PaddleSharpOcrAlg(AnalysisPipeline pipeline, Dictionary<string, string> preferences)
        {
            _pipeline = pipeline;

            _ocrType = preferences["TypeToOCR"].ToLower();
            _scoreThresh = float.Parse(preferences["ScoreThresh"]);

            _paddleOcrAll = new PaddleOcrAll(LocalFullModels.ChineseV4)
            {
                AllowRotateDetection = true,
                Enable180Classification = false,
            };
        }

        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _eventPublisher = _serviceProvider.GetRequiredService<IPublisher<OcrResultEvent>>();

            var subscriber = serviceProvider.GetService<ISubscriber<ObjectExpiredEvent>>();
            this.SetSubscriber(subscriber);
        }

        public AnalysisResult Analyze(Frame frame)
        {
            /*foreach (var detectedObject in frame.DetectedObjects)
            {
                if (!detectedObject.IsUnderAnalysis)
                {
                    continue;
                }

                if (detectedObject.Label != _ocrType)
                {
                    continue;
                }

                if (detectedObject.Snapshot.Width == 0)
                {
                    continue;
                }

                PaddleOcrResult result = _paddleOcrAll.Run(detectedObject.Snapshot);
                foreach (PaddleOcrResultRegion region in result.Regions)
                {
                    if (region.Score > _scoreThresh)
                    {
                        Console.WriteLine($"Text: {region.Text}, Score: {region.Score}");
                    }
                }
            }*/

            return new AnalysisResult(true);
        }

        public override void ProcessEvent(ObjectExpiredEvent @event)
        {
            var _snapshotManager = _pipeline.SnapshotManager;

            if (@event.Label != _ocrType)
            {
                return;
            }

            var snapshots = _snapshotManager.GetObjectSnapshotsByObjectId(@event.Id);
            if (snapshots.Count == 0)
            {
                return;
            }

            var highestScore = snapshots.Keys.Max();
            Mat highestSnapshot = snapshots[highestScore];

            PaddleOcrResult result = _paddleOcrAll.Run(highestSnapshot);
            foreach (PaddleOcrResultRegion region in result.Regions)
            {
                if (region.Score > _scoreThresh)
                {
                    Console.WriteLine($"ObjId:{@event.Id} Text: {region.Text}, Score: {region.Score}");
                }
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            _paddleOcrAll.Dispose();
        }
    }
}
