using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using OpenCvSharp;
using SentinelCore.Domain.Abstractions.AnalysisHandler;
using SentinelCore.Domain.Entities.AnalysisEngine;
using SentinelCore.Domain.Entities.ObjectDetection;
using SentinelCore.Domain.Entities.VideoStream;
using SentinelCore.Domain.Events.AnalysisEngine;
using SentinelCore.Service.Pipeline;
using System.Collections.Concurrent;

namespace Handler.Smuggling.Algorithms
{
    public class SmugglingAlg : ObjectExpiredSubscriber, IAnalysisHandler
    {
        private readonly AnalysisPipeline _pipeline;
        private readonly string _eventName;
        private IServiceProvider _serviceProvider;

        private readonly int _maxGatheringCount;
        private readonly int _eventSustainSec;
        private readonly int _historyLengthThresh;
        private readonly float _distanceIncreasePercentThresh;
        private readonly float _movingAwayPercentThresh;

        private readonly TtlFlagManager<string> _flagManager;

        private Point _boatPosition;

        private readonly ConcurrentDictionary<long, Queue<Point>> _personHistory = new ConcurrentDictionary<long, Queue<Point>>();

        public string HandlerName => nameof(SmugglingAlg);

        public SmugglingAlg(AnalysisPipeline pipeline, Dictionary<string, string> preferences)
        {
            _pipeline = pipeline;

            _maxGatheringCount = int.Parse(preferences["MaxGatheringCount"]);
            _eventSustainSec = int.Parse(preferences["EventSustainSec"]);
            _historyLengthThresh = int.Parse(preferences["HistoryLengthThresh"]);
            _distanceIncreasePercentThresh = float.Parse(preferences["DistanceIncreasePercentThresh"]);
            _movingAwayPercentThresh = float.Parse(preferences["MovingAwayPercentThresh"]);

            _flagManager = new TtlFlagManager<string>();

            _boatPosition = new Point(0, 0);
        }

        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;

            var subscriber = serviceProvider.GetService<ISubscriber<ObjectExpiredEvent>>();
            this.SetSubscriber(subscriber);
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

            if (isPeopleGathering && isBoatExistence)
            {
                if (CheckPeopleAwayFromBoat(frame))
                {
                    _flagManager.SetValue("people_away_from_boat", true, _eventSustainSec);
                }
                _flagManager.TryGetValue("people_away_from_boat", out var isPeopleAwayFromBoat);
                frame.SetProperty("people_away_from_boat", isPeopleAwayFromBoat);
            }

            return new AnalysisResult(true);
        }

        private bool CheckPeopleGathering(Frame frame)
        {
            List<DetectedObject> detectedPersons = new List<DetectedObject>();

            // 获取所有 Person 目标
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
                
                detectedPersons.Add(detectedObject);
            }

            const float widthFactor = 4.0f;  // 基于每个人物宽度计算聚集阈值
            List<(BoundingBox, List<DetectedObject>)> gatherings = new List<(BoundingBox, List<DetectedObject>)>();  // 每个聚集区域的外部BoundingBox

            foreach (var outerPerson in detectedPersons)
            {
                // 检测人群聚集
                List<DetectedObject> personCluster = new List<DetectedObject>();
                personCluster.Add(outerPerson);

                // 根据当前侦测框获取距离阈值
                float distanceThresh = outerPerson.Width * widthFactor;

                foreach (var innerPerson in detectedPersons)
                {
                    if (outerPerson == innerPerson)
                    {
                        continue;
                    }

                    // 判断当前中心点是否与已有聚集区的某个点的距离小于阈值
                    var distance = CalculateDistance(
                        new Point(outerPerson.CenterX, outerPerson.CenterY), 
                        new Point(innerPerson.CenterX, innerPerson.CenterY));

                    if (distance < distanceThresh)
                    {
                        personCluster.Add(innerPerson);
                    }
                }

                // 如果当前聚集人数没有超过阈值，则继续寻找下一个聚集
                if (personCluster.Count < _maxGatheringCount)
                {
                    continue;
                }

                // 计算当前聚集区域的外部BoundingBox
                float minX = personCluster.Min(p => p.TopLeftX);
                float minY = personCluster.Min(p => p.TopLeftY);
                float maxX = personCluster.Max(p => p.BottomRightX);
                float maxY = personCluster.Max(p => p.BottomRightY);

                float width = maxX - minX;
                float height = maxY - minY;

                var boundingBox = new BoundingBox(
                    labelId: -1,
                    label: "gathering",
                    confidence: 1.0f,
                    x: (int)minX,
                    y: (int)minY,
                    width: (int)width,
                    height: (int)height);
                boundingBox.TrackingId = outerPerson.TrackingId;

                gatherings.Add((boundingBox, personCluster));
            }

            frame.SetProperty("gatherings", gatherings);

            // 返回是否聚集人数超过阈值
            return gatherings.Any(g => g.Item2.Count > _maxGatheringCount);  // _maxGatheringCount为聚集人数的阈值
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
                    _boatPosition = new Point(detectedObject.CenterX, detectedObject.CenterY);
                    return true;
                }
            }

            return false;
        }

        private bool CheckPeopleAwayFromBoat(Frame frame)
        {
            if (_boatPosition is { X: 0, Y: 0 })
            {
                return false;
            }

            var gatherings = frame.GetProperty<List<(BoundingBox, List<DetectedObject>)>>("gatherings");
            if (gatherings != null)
            {
                foreach (var gathering in gatherings)
                {
                    var gatheringBbox = gathering.Item1;
                    var personObjects = gathering.Item2;

                    if (CalculateDistance(new Point(gatheringBbox.CenterX, gatheringBbox.CenterY),
                            _boatPosition) > gatheringBbox.Width * 2)
                    {
                        continue;
                    }

                    int peopleAwayCount = 0;

                    foreach (var person in personObjects)
                    {
                        var personCenter = new Point(person.CenterX, person.CenterY);

                        int personId = person.TrackingId;

                        if (!_personHistory.ContainsKey(personId))
                            _personHistory[personId] = new Queue<Point>();

                        _personHistory[personId].Enqueue(personCenter);

                        // 保持队列长度不超过设定的历史长度
                        if (_personHistory[personId].Count > _historyLengthThresh)
                        {
                            _personHistory[personId].Dequeue();
                        }

                        // 分析是否正在远离参考点
                        if (IsMovingAway(_personHistory[personId]))
                        {
                            peopleAwayCount++;
                        }
                    }

                    if (((float)peopleAwayCount / personObjects.Count) > _movingAwayPercentThresh)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsMovingAway(Queue<Point> history)
        {
            if (history.Count < 2) return false;

            var distances = history.Select(pos => CalculateDistance(pos, _boatPosition)).ToList();

            int increasingDistancesCount = 0;
            for (int i = 1; i < distances.Count; i++)
            {
                if (distances[i] > distances[i - 1])
                {
                    increasingDistancesCount++;
                }
            }

            return (double)increasingDistancesCount / distances.Count >= _distanceIncreasePercentThresh;
        }

        private double CalculateDistance(Point p1, Point p2)
        {
            return Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
        }

        public override void ProcessEvent(ObjectExpiredEvent @event)
        {
            if (_personHistory.ContainsKey(@event.TrackingId))
            {
                _personHistory.TryRemove(@event.TrackingId, out _);
            }
        }
    }
}
