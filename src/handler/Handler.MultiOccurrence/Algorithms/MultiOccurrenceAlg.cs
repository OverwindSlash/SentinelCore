using Handler.MultiOccurrence.Actions;
using Handler.MultiOccurrence.Events;
using Handler.MultiOccurrence.ThirdParty;
using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using OpenCvSharp;
using SentinelCore.Domain.Abstractions.AnalysisHandler;
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
            _onlyCheckPrimary = (_auxiliaryType.Count == 1 && _auxiliaryType[0] == "");
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

                if (_onlyCheckPrimary)
                {
                    string eventId = $"mo_{primaryObject.Id}";
                    BoundingBox bbox = primaryObject.Bbox;
                    float score = bbox.Width;

                    var boxedScene = SaveSnapshot(frame, snapshotManager, bbox, eventId, score, out var sceneFilepath);

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

                    _eventPublisher.Publish(multiOccurenceEvent);

                    Console.WriteLine(multiOccurenceEvent.CreateLogMessage());

                    foreach (var jsonMessagePoster in jsonMessagePosters)
                    {
                        var jsonMessage = multiOccurenceEvent.GenerateLesCastingNetJsonMsg();
                        jsonMessagePoster.PostRestfulJsonMessage(jsonMessage);
                    }
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
                            string eventId = $"mo_{primaryObject.Id}";
                            BoundingBox bbox = primaryObject.Bbox.CombineBoundingBox(auxiliaryObj.Bbox);
                            float score = bbox.Width;

                            var boxedScene = SaveSnapshot(frame, snapshotManager, bbox, eventId, score, out var sceneFilepath);

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

                            Console.WriteLine(multiOccurenceEvent.CreateLogMessage());

                            foreach (var jsonMessagePoster in jsonMessagePosters)
                            {
                                var jsonMessage = multiOccurenceEvent.GenerateLesCastingNetJsonMsg();
                                jsonMessagePoster.PostRestfulJsonMessage(jsonMessage);
                            }
                        }
                    }
                }
            }

            return new AnalysisResult(true);
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

        public void Dispose()
        {
        }
    }
}
