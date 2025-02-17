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
        private readonly int _movingAwayPersonThresh;

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
            _movingAwayPersonThresh = int.Parse(preferences["MovingAwayPersonThresh"]);

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

            if (isPeopleGathering)
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

        // private bool CheckPeopleGathering(Frame frame)
        // {
        //     int counting = 0;
        //
        //     foreach (var detectedObject in frame.DetectedObjects)
        //     {
        //         if (!detectedObject.IsUnderAnalysis)
        //         {
        //             continue;
        //         }
        //
        //         if (detectedObject.Label.ToLower() != "person")
        //         {
        //             continue;
        //         }
        //
        //         counting++;
        //     }
        //
        //     frame.SetProperty("person_count", counting);
        //
        //     return (counting >= _maxGatheringCount);
        // }

        private bool CheckPeopleGathering(Frame frame)
        {
            List<Point> personCenters = new List<Point>();
            List<float> personWidths = new List<float>();

            // 获取所有人员的中心点和宽度
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

                // 计算人员侦测框的中心点
                var centerX = detectedObject.Bbox.X + detectedObject.Bbox.Width / 2;
                var centerY = detectedObject.Bbox.Y + detectedObject.Bbox.Height / 2;
                personCenters.Add(new Point(centerX, centerY));
                personWidths.Add(detectedObject.Bbox.Width);  // 存储每个人物的宽度
            }

            // 使用简单的阈值方法判断是否为聚集
            List<Point> cluster = new List<Point>();
            const float widthFactor = 2.0f;  // 基于每个人物宽度计算聚集阈值

            // 每个聚集区域的外部BoundingBox
            List<(BoundingBox, int)> gatherings = new List<(BoundingBox, int)>();

            // 聚类过程，基于每个人物宽度的阈值判定
            foreach (var outterCenter in personCenters)
            {
                cluster.Add(outterCenter);

                // 获取当前聚集点对应的宽度阈值
                int index = personCenters.IndexOf(outterCenter);
                float gatheringThreshold = personWidths[index] * widthFactor;  // 基于宽度设置阈值

                foreach (var innerCenter in personCenters)
                {
                    if (outterCenter == innerCenter)
                    {
                        continue;
                    }

                    // 判断当前中心点是否与已有聚集区的某个点的距离小于阈值
                    var distance = CalculateDistance(outterCenter, innerCenter);
                    if (distance < gatheringThreshold)
                    {
                        cluster.Add(innerCenter);
                    }
                }

                if (cluster.Count < _maxGatheringCount)
                {
                    cluster.Clear();
                    continue;
                }

                // 计算当前聚集区域的外部BoundingBox
                float minX = cluster.Min(p => p.X);
                float minY = cluster.Min(p => p.Y);
                float maxX = cluster.Max(p => p.X);
                float maxY = cluster.Max(p => p.Y);

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

                gatherings.Add((boundingBox, cluster.Count));

                cluster.Clear();
            }
            
            frame.SetProperty("gatherings", gatherings);

            // 返回是否聚集人数超过阈值
            return gatherings.Any(g => g.Item2 > _maxGatheringCount);  // _maxGatheringCount为聚集人数的阈值
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

            int peopleAwayCount = 0;

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

                var personCenter = new Point(detectedObject.CenterX, detectedObject.CenterY);

                long personId = detectedObject.TrackingId;

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

            return peopleAwayCount > _movingAwayPersonThresh;
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
