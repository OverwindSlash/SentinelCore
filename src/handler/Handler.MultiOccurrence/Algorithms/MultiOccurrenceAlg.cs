using Handler.MultiOccurrence.Actions;
using Handler.MultiOccurrence.Events;
using Handler.MultiOccurrence.ThirdParty;
using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using OpenCvSharp;
using SentinelCore.Domain.Abstractions.AnalysisHandler;
using SentinelCore.Domain.Abstractions.MessagePoster;
using SentinelCore.Domain.Abstractions.SnapshotManager;
using SentinelCore.Domain.Entities.AnalysisEngine;
using SentinelCore.Domain.Entities.ObjectDetection;
using SentinelCore.Domain.Entities.VideoStream;
using SentinelCore.Service.Pipeline;

namespace Handler.MultiOccurrence.Algorithms
{
    public class MultiOccurrenceAlg : IAnalysisHandler
    {
        private readonly AnalysisPipeline _pipeline;

        private IPublisher<MultiOccurenceEvent> _eventPublisher;

        public string HandlerName => nameof(MultiOccurrenceAlg);

        private readonly string _eventName;
        private readonly string _eventMessage;
        private readonly double _closeThreshold;
        private readonly string _primaryType;
        private readonly List<string> _auxiliaryType;
        private readonly bool _onlyCheckPrimary;

        private IServiceProvider _serviceProvider;

        public MultiOccurrenceAlg(AnalysisPipeline pipeline, Dictionary<string, string> preferences)
        {
            _pipeline = pipeline;

            _eventName = preferences["EventName"];
            _eventMessage = preferences["EventMessage"];
            _closeThreshold = double.Parse(preferences["CloseThreshold"]);
            _primaryType = preferences["PrimaryType"];
            _auxiliaryType = preferences["AuxiliaryType"].Split(',').ToList();
            _onlyCheckPrimary = _auxiliaryType is [""];
        }

        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _eventPublisher = _serviceProvider.GetRequiredService<IPublisher<MultiOccurenceEvent>>();
        }

        public AnalysisResult Analyze(Frame frame)
        {
            var snapshotManager = _pipeline.SnapshotManager;
            var jsonMessagePosters = _pipeline.JsonMessagePosters;

            foreach (var primaryObject in frame.DetectedObjects)
            {
                bool isPrimaryType = string.Compare(primaryObject.Label, _primaryType, StringComparison.InvariantCultureIgnoreCase) == 0;
                if (!isPrimaryType)
                {
                    continue;
                }
                
                string eventId = $"mo_{primaryObject.Id}";
                if (_onlyCheckPrimary)
                {
                    var bbox = GenerateEventBbox(primaryObject, null, out var score);

                    var boxedScene = SaveSnapshot(frame, snapshotManager, bbox, eventId, score, out var sceneFilepath);

                    var multiOccurenceEvent = PublishMultiOccurenceEvent(primaryObject, null, eventId, boxedScene, sceneFilepath);

                    Console.WriteLine(multiOccurenceEvent.CreateLogMessage());

                    PostRestfulJsonMessage(jsonMessagePosters, multiOccurenceEvent);
                }
                else
                {
                    foreach (var auxiliaryObj in frame.DetectedObjects)
                    {
                        if (primaryObject == auxiliaryObj)
                        {
                            continue;
                        }

                        if (!_auxiliaryType.Contains(auxiliaryObj.Label.ToLower()))
                        {
                            continue;
                        }

                        if (primaryObject.Bbox.CloseTo(auxiliaryObj.Bbox, _closeThreshold))
                        {
                            var bbox = GenerateEventBbox(primaryObject, auxiliaryObj, out var score);

                            var boxedScene = SaveSnapshot(frame, snapshotManager, bbox, eventId, score, out var sceneFilepath);

                            var multiOccurenceEvent = PublishMultiOccurenceEvent(primaryObject, auxiliaryObj, eventId, boxedScene, sceneFilepath);

                            Console.WriteLine(multiOccurenceEvent.CreateLogMessage());

                            PostRestfulJsonMessage(jsonMessagePosters, multiOccurenceEvent);
                        }
                    }
                }
            }

            return new AnalysisResult(true);
        }

        private static BoundingBox GenerateEventBbox(DetectedObject primaryObject, DetectedObject auxiliaryObj, out float score)
        {
            BoundingBox bbox = primaryObject.Bbox;
            if (auxiliaryObj != null)
            {
                bbox = primaryObject.Bbox.CombineBoundingBox(auxiliaryObj.Bbox);
            }
            
            score = bbox.Width;
            return bbox;
        }

        private static Mat SaveSnapshot(Frame frame, ISnapshotManager snapshotManager, BoundingBox bbox, string eventId,
            float score, out string sceneFilepath)
        {
            Mat snapshot = snapshotManager.TakeSnapshot(frame, bbox);
            snapshotManager.AddSnapshotOfObjectById(eventId, score, snapshot);

            string sceneFilename = $"{eventId}_scene";
            Mat boxedScene = snapshotManager.GenerateBoxedScene(frame.Scene, new List<BoundingBox>() { bbox });

            sceneFilepath = MultiOccurrenceAction.SaveEventImages(snapshotManager.SnapshotDir, sceneFilename, boxedScene);
            return boxedScene;
        }

        private MultiOccurenceEvent PublishMultiOccurenceEvent(DetectedObject primaryObject, DetectedObject auxiliaryObj, string eventId, Mat boxedScene, string sceneFilepath)
        {
            var multiOccurenceEvent = new MultiOccurenceEvent(
                deviceName: _pipeline.DeviceName,
                eventName: _eventName,
                eventMessage: _eventMessage,
                handlerName: _eventName,
                objTypes: new List<string>() { primaryObject.Label, auxiliaryObj.Label },
                snapshotId: eventId,
                snapshot: null,
                eventImagePath: string.Empty,
                scene: boxedScene,
                eventScenePath: sceneFilepath);

            _eventPublisher.Publish(multiOccurenceEvent);
            return multiOccurenceEvent;
        }

        private static void PostRestfulJsonMessage(List<IJsonMessagePoster> jsonMessagePosters, MultiOccurenceEvent multiOccurenceEvent)
        {
            foreach (var jsonMessagePoster in jsonMessagePosters)
            {
                var jsonMessage = multiOccurenceEvent.GenerateJsonMessage();
                jsonMessagePoster.PostRestfulJsonMessage(jsonMessage);
            }
        }
        
        public void Dispose()
        {
        }
    }
}
