using Handler.Smuggling.Events;
using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using OpenCvSharp;
using SentinelCore.Domain.Abstractions.AnalysisHandler;
using SentinelCore.Domain.Entities.AnalysisEngine;
using SentinelCore.Domain.Entities.ObjectDetection;
using SentinelCore.Domain.Entities.VideoStream;
using SentinelCore.Domain.Events.AnalysisEngine;
using SentinelCore.Domain.Utils.Extensions;
using SentinelCore.Service.Pipeline;
using System.Collections.Concurrent;

namespace Handler.Smuggling.Algorithms
{
    public class SmugglingAlg : ObjectExpiredSubscriber, IAnalysisHandler
    {
        private readonly AnalysisPipeline _pipeline;
        private readonly string _eventName;
        private IServiceProvider _serviceProvider;

        private IPublisher<SmugglingEvent> _eventPublisher;

        private readonly float _widthBasedApproachFactor;
        private readonly int _maxGatheringCount;
        private readonly int _eventSustainSec;
        private readonly int _historyLengthThresh;
        private readonly float _distanceIncreasePercentThresh;
        private readonly float _movingAwayPercentThresh;

        private readonly TtlFlagManager<string> _flagManager;

        private Point _boatPosition;

        private readonly ConcurrentDictionary<long, Queue<Point>> _personHistory = 
            new ConcurrentDictionary<long, Queue<Point>>();

        public string HandlerName => nameof(SmugglingAlg);

        public SmugglingAlg(AnalysisPipeline pipeline, Dictionary<string, string> preferences)
        {
            _pipeline = pipeline;

            _widthBasedApproachFactor = float.Parse(preferences["WidthBasedApproachFactor"]);
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

            _eventPublisher = _serviceProvider.GetRequiredService<IPublisher<SmugglingEvent>>();
        }

        public AnalysisResult Analyze(Frame frame)
        {
            // 检查是否有人员聚集并设置延迟标记
            if (CheckPeopleGathering(frame))
            {
                _flagManager.SetValue("people_gathering", true, _eventSustainSec);
            }
            _flagManager.TryGetValue("people_gathering", out var isPeopleGathering);
            frame.SetProperty("people_gathering", isPeopleGathering);

            // 检查是否有船舶并设置延迟标记
            if (CheckBoatExistence(frame))
            {
                _flagManager.SetValue("boat_existence", true, 2 * _eventSustainSec);
            }
            _flagManager.TryGetValue("boat_existence", out var isBoatExistence);
            frame.SetProperty("boat_existence", isBoatExistence);

            if (CheckPeopleAwayFromBoat(frame))
            {
                _flagManager.SetValue("people_away_from_boat", true, _eventSustainSec);
            }
            _flagManager.TryGetValue("people_away_from_boat", out var isPeopleAwayFromBoat);
            frame.SetProperty("people_away_from_boat", isPeopleAwayFromBoat);

            if (isPeopleGathering && isBoatExistence && isPeopleAwayFromBoat)
            {
                string eventId = $"smg_{frame.DeviceId}_{frame.TimeStamp.ToString("yyyyMMddHHmmssfff")}";

                string filepath = SaveSmugglingScene(frame, eventId);
                SmugglingEvent smugglingEvent = PublishSmugglingEvent(frame, eventId, filepath);
            }

            return new AnalysisResult(true);
        }

        private bool CheckPeopleGathering(Frame frame)
        {
            // 获取所有 Person 目标
            List<DetectedObject> detectedPersons = 
                frame.FilterObjectsUnderAnalysis(obj => obj.Label.ToLower() == "person");

            // 检测人群聚集
            List<ObjectGroup> allPersonGatherings = new List<ObjectGroup>();
            foreach (var outerPerson in detectedPersons)
            {
                List<DetectedObject> personCluster = new List<DetectedObject>();
                personCluster.Add(outerPerson);

                // 根据两人之间的距离判断是否属于同一聚集
                personCluster.AddRange(detectedPersons.
                    Where(innerPerson => outerPerson != innerPerson).
                    Where(innerPerson => outerPerson.Bbox.CloseTo(innerPerson.Bbox, _widthBasedApproachFactor)));

                // 如果当前聚集人数没有超过阈值，则继续寻找下一个聚集
                if (personCluster.Count < _maxGatheringCount)
                {
                    continue;
                }

                // 创建人群聚集对象
                var currentPersonGathering = new ObjectGroup(frame, personCluster, "gathering", outerPerson.TrackingId);

                // 去重有包含关系的人群聚集对象
                bool isCurrentGatheringInnerBbox = false;
                ObjectGroup toRemoveGathering = null; 

                foreach (var candidateGathering in allPersonGatherings)
                {
                    // 如果当前聚集包含在候选聚集内，则跳过
                    if (candidateGathering.Bbox.Contains(currentPersonGathering.Bbox))
                    {
                        isCurrentGatheringInnerBbox = true;
                        break;
                    }

                    // 如果候选聚集包含在当前聚集内，则移除候选聚集
                    if (currentPersonGathering.Bbox.Contains(candidateGathering.Bbox))
                    {
                        toRemoveGathering = candidateGathering;
                    }
                }

                if (!isCurrentGatheringInnerBbox)
                {
                    allPersonGatherings.Add(currentPersonGathering);
                }

                if (toRemoveGathering != null)
                {
                    allPersonGatherings.Remove(toRemoveGathering);
                }
            }

            frame.SetProperty("gatherings", allPersonGatherings);

            // 返回是否聚集人数超过阈值
            return allPersonGatherings.Any(g => g.GroupObjects.Count > _maxGatheringCount);  // _maxGatheringCount为聚集人数的阈值
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

            bool isAnyPeopleGatheringFlee = false;

            var gatherings = frame.GetProperty<List<ObjectGroup>>("gatherings");
            if (gatherings != null)
            {
                // 正在远离船舶的 Person 的 Id 集合
                HashSet<int> fleePersons = new HashSet<int>();

                // 获取所有 Person 目标
                List<DetectedObject> detectedPersons =
                    frame.FilterObjectsUnderAnalysis(obj => obj.Label.ToLower() == "person");

                // 将 Person 按照 TrackingId 放入历史队列, 并计算是否远离
                foreach (var person in detectedPersons)
                {
                    int personId = person.TrackingId;

                    if (!_personHistory.ContainsKey(personId))
                        _personHistory[personId] = new Queue<Point>();

                    _personHistory[personId].Enqueue(new Point(person.CenterX, person.CenterY));

                    // 保持队列长度不超过设定的历史长度
                    if (_personHistory[personId].Count > _historyLengthThresh)
                    {
                        _personHistory[personId].Dequeue();
                    }
                    
                    // 分析是否正在远离船舶
                    if (IsMovingAway(_personHistory[personId]))
                    {
                        fleePersons.Add(personId);
                    }
                }
                
                // 计算每个人群聚集里有多少比例的人正远离船
                foreach (var gathering in gatherings)
                {
                    int fleePersonCount = 0;
                    foreach (var person in gathering.GroupObjects)
                    {
                        if (fleePersons.Contains(person.TrackingId))
                        {
                            fleePersonCount++;
                        }
                    }

                    bool isGatheringFlee = ((float)fleePersonCount / gathering.GroupObjects.Count) > _movingAwayPercentThresh;
                    
                    gathering.SetProperty("isFlee", isGatheringFlee);

                    isAnyPeopleGatheringFlee |= isGatheringFlee;
                }
            }

            return isAnyPeopleGatheringFlee;
        }

        private bool IsMovingAway(Queue<Point> history)
        {
            if (history.Count < _historyLengthThresh / 2) return false;
        
            var distances = history.Select(pos => CalculateDistance(pos, _boatPosition)).ToList();
        
            int increasingDistancesCount = 0;
            for (int i = 1; i < distances.Count; i++)
            {
                if (distances[i] - distances[i - 1] > 0)
                {
                    increasingDistancesCount++;
                }

                //Console.Write(distances[i].ToString("F1") + " ");
            }
            //Console.Write("\n");
        
            return (double)increasingDistancesCount / distances.Count >= _distanceIncreasePercentThresh;
        }

        private string SaveSmugglingScene(Frame frame, string eventId)
        {
            var gatherings = frame.GetProperty<List<ObjectGroup>>("gatherings");
            if (gatherings.Count == 0)
            {
                return string.Empty;
            }

            var cloneScene = frame.Scene.Clone();

            foreach (var detectedObject in frame.DetectedObjects)
            {
                if (!detectedObject.IsUnderAnalysis)
                {
                    continue;
                }

                if (detectedObject.Label == "person")
                {
                    cloneScene.Circle(new Point(detectedObject.CenterX, detectedObject.CenterY), 5, Scalar.Aqua);
                }
            }

            var boundingBoxes = gatherings.ConvertAll(g => g.Bbox);
            var mergeBoxes = BoundingBox.MergeBoundingBoxes(boundingBoxes, 0.3f);

            foreach (var bbox in mergeBoxes)
            {
                cloneScene.Rectangle(new Point(bbox.X, bbox.Y), new Point(bbox.X + bbox.Width, bbox.Y + bbox.Height), Scalar.Crimson);
            }

            var snapshotManager = _pipeline.SnapshotManager;
            string filename = $"{eventId}.jpg";

            var path = Path.Combine(snapshotManager.SnapshotDir, "Events");
            path.EnsureDirExistence();
            var filepath = Path.Combine(path, filename);

            Task.Run(() =>
            {
                cloneScene.SaveImage(filepath);
            });

            return filepath;
        }

        private SmugglingEvent PublishSmugglingEvent(Frame frame, string eventId, string filepath)
        {
            var smugglingEvent = new SmugglingEvent(eventId, frame.Scene, filepath);
            _eventPublisher.Publish(smugglingEvent);

            return smugglingEvent;
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
