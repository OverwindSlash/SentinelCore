using Handler.MultiOccurrence.Actions;
using Handler.MultiOccurrence.Events;
using MessagePipe;
using OpenCvSharp;
using SentinelCore.Domain.Abstractions.AnalysisHandler;
using SentinelCore.Domain.Abstractions.EventHandler;
using SentinelCore.Domain.Abstractions.SnapshotManager;
using SentinelCore.Domain.Entities.AnalysisEngine;
using SentinelCore.Domain.Entities.ObjectDetection;
using SentinelCore.Domain.Entities.VideoStream;
using SentinelCore.Domain.Events;
using SentinelCore.Domain.Events.AnalysisEngine;
using SentinelCore.Service.Pipeline;

namespace Handler.MultiOccurrence.Algorithms
{
    public class MultiOccurrenceAlg : FrameAndObjectExpiredSubscriber, IAnalysisHandler, IEventPublisher<DomainEvent>
    {
        private readonly AnalysisPipeline _pipeline;

        private IPublisher<DomainEvent> _eventPublisher;

        public string HandlerName => nameof(MultiOccurrenceAlg);

        private readonly string _eventName;
        private readonly string _eventMessage;
        private readonly double _closeThreshold;
        private readonly string _primaryType;
        private readonly List<string> _auxiliaryType;
        private readonly bool _onlyCheckPrimary;

        public MultiOccurrenceAlg(AnalysisPipeline pipeline, Dictionary<string, string> preferences)
        {
            _pipeline = pipeline;

            _eventName = preferences["EventName"];
            _eventMessage = preferences["EventMessage"];
            _closeThreshold = double.Parse(preferences["CloseThreshold"]);
            _primaryType = preferences["PrimaryType"];
            _auxiliaryType = preferences["AuxiliaryType"].Split(',').ToList();
            _onlyCheckPrimary = (_auxiliaryType.Count == 1 && _auxiliaryType[0] == "");
        }

        public AnalysisResult Analyze(Frame frame)
        {
            var snapshotManager = _pipeline.SnapshotManager;

            foreach (var primaryObject in frame.DetectedObjects)
            {
                bool isPrimaryType = string.Compare(primaryObject.Label, _primaryType, StringComparison.InvariantCultureIgnoreCase) == 0;
                if (!isPrimaryType)
                {
                    continue;
                }

                if (_onlyCheckPrimary)
                {
                    string eventId = $"mo_{primaryObject.Id}";
                    BoundingBox bbox = primaryObject.Bbox;
                    float score = bbox.Width;

                    Mat snapshot = snapshotManager.TakeSnapshot(frame, bbox);
                    snapshotManager.AddSnapshotOfObjectById(eventId, score, snapshot);

                    string sceneFilename = $"{eventId}_scene";
                    Mat boxedScene = snapshotManager.GenerateBoxedScene(frame.Scene, new List<BoundingBox>() { bbox });

                    var sceneFilepath = MultiOccurrenceAction.SaveEventImages(snapshotManager.SnapshotDir, sceneFilename, boxedScene);

                    var multiOccurenceEvent = new MultiOccurenceEvent(
                        deviceName: _pipeline.DeviceName,
                        eventName: _eventName,
                        eventMessage: _eventMessage,
                        handlerName: _eventName,
                        objTypes: new List<string>() { primaryObject.Label },
                        snapshotId: eventId,
                        snapshot: null,
                        eventImagePath: string.Empty,
                        scene: boxedScene,
                        eventScenePath: sceneFilepath);
                }
            }

            return new AnalysisResult(true);
        }
      
        public void SetPublisher(IPublisher<DomainEvent> publisher)
        {
            _eventPublisher = publisher;
        }

        public void PublishEvent(DomainEvent @event)
        {
            _eventPublisher.Publish(@event);
        }

        public void Dispose()
        {
            base.Dispose();
        }
    }
}
