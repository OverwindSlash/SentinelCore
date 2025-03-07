﻿using OpenCvSharp;
using SentinelCore.Domain.Abstractions.SnapshotManager;
using SentinelCore.Domain.Entities.ObjectDetection;
using SentinelCore.Domain.Entities.VideoStream;
using SentinelCore.Domain.Events.AnalysisEngine;
using SentinelCore.Domain.Utils.Extensions;
using System.Collections.Concurrent;

namespace SnapshotManager.InMemory
{
    public class SnapshotManager : FrameAndObjectExpiredSubscriber, ISnapshotManager
    {
        // frameId -> Scene
        private readonly ConcurrentDictionary<long, Mat> _scenesOfFrame;
        // object snapshot list by objId -> (factor, objectMat)
        private readonly ConcurrentDictionary<string, SortedList<float, Mat>> _snapshotsByScore;

        private readonly string _snapshotsDir = "Snapshots";
        private bool _saveBestSnapshot = false;
        private int _maxObjectSnapshots = 10;
        private int _minSnapshotWidth = 40;
        private int _maxSnapshotHeight = 40;

        public string Name => "In-memory snapshot manager";
        public string SnapshotDir => _snapshotsDir;

        public SnapshotManager(Dictionary<string, string> preferences)
        {
            _scenesOfFrame = new ConcurrentDictionary<long, Mat>();
            _snapshotsByScore = new ConcurrentDictionary<string, SortedList<float, Mat>>();

            _snapshotsDir = preferences["SnapshotsDir"];
            _saveBestSnapshot = bool.Parse(preferences["SaveBestSnapshot"]);
            _maxObjectSnapshots = int.Parse(preferences["MaxSnapshots"]);
            _minSnapshotWidth = int.Parse(preferences["MinSnapshotWidth"]);
            _maxSnapshotHeight = int.Parse(preferences["MinSnapshotHeight"]);

            var snapshotFullPath = Path.Combine(Directory.GetCurrentDirectory(), _snapshotsDir);
            snapshotFullPath.EnsureDirExistence();
        }
        
        public void ProcessSnapshots(Frame frame)
        {
            AddSceneByFrameId(frame.FrameId, frame);
            AddSnapshotOfObjectById(frame);
        }

        private void AddSceneByFrameId(long frameId, Frame frame)
        {
            if (!_scenesOfFrame.ContainsKey(frameId))
            {
                _scenesOfFrame.TryAdd(frameId, frame.Scene);
            }
        }

        private void AddSnapshotOfObjectById(Frame frame)
        {
            foreach (var detectedObject in frame.DetectedObjects)
            {
                if (!detectedObject.IsUnderAnalysis)
                {
                    continue;
                }

                Mat snapshot = TakeSnapshot(frame, detectedObject.Bbox.Expand(0.2f));
                detectedObject.Snapshot = snapshot;
                AddSnapshotOfObjectById(detectedObject.Id, CalculateFactor(detectedObject), snapshot);
            }
        }

        public Mat TakeSnapshot(Frame frame, BoundingBox bboxs)
        {
            //return frame.Scene.SubMat(new Rect(bboxs.X, bboxs.Y, bboxs.Width, bboxs.Height)).Clone();

            // 获取当前图像的宽高
            int sceneWidth = frame.Scene.Width;
            int sceneHeight = frame.Scene.Height;

            // 原始Rect
            int x = bboxs.X;
            int y = bboxs.Y;
            int width = bboxs.Width;
            int height = bboxs.Height;

            // 确保X、Y至少从0开始
            x = Math.Max(0, x);
            y = Math.Max(0, y);

            // 如果 X+width 超过图像右边，则进行裁剪
            if (x + width > sceneWidth)
            {
                width = sceneWidth - x;
            }
            // 如果 Y+height 超过图像下边，则进行裁剪
            if (y + height > sceneHeight)
            {
                height = sceneHeight - y;
            }

            // 保险措施：如果裁剪后 width 或 height <= 0，则直接返回空
            if (width <= 0 || height <= 0)
            {
                // 这里可根据业务需求返回null或者一张空的Mat
                return new Mat();
            }

            // 构造新的Rect，保证它在图像内部
            Rect validRect = new Rect(x, y, width, height);

            // 使用有效Rect进行 SubMat 操作，最后 Clone 一份并返回
            return frame.Scene.SubMat(validRect).Clone();
        }

        public void AddSnapshotOfObjectById(string objId, float score, Mat snapshot)
        {
            if (!_snapshotsByScore.ContainsKey(objId))
            {
                _snapshotsByScore.TryAdd(objId, new SortedList<float, Mat>());
            }

            SortedList<float, Mat> snapshotsById = _snapshotsByScore[objId];
            if (!snapshotsById.ContainsKey(score))
            {
                snapshotsById.Add(score, snapshot);
            }
            else
            {
                snapshotsById[score] = snapshot;
            }

            if (snapshotsById.Count > _maxObjectSnapshots)
            {
                for (int i = 0; i < snapshotsById.Count - _maxObjectSnapshots; i++)
                {
                    // remove tail (lowest score)
                    snapshotsById.RemoveAt(i);
                }
            }
        }

        public Mat GenerateBoxedScene(Mat scene, List<BoundingBox> boundingBoxes)
        {
            Mat boxedScene = scene.Clone();

            foreach (var boundingBox in boundingBoxes)
            {
                boxedScene.Rectangle(boundingBox.Rectangle, Scalar.Crimson, 2);
            }

            return boxedScene;
        }

        private float CalculateFactor(DetectedObject obj)
        {
            return obj.Confidence;
            // Area as order factor.
            // return obj.Width * obj.Height;
            // return obj.Width;
        }

        public Mat GetSceneByFrameId(long frameId)
        {
            if (_scenesOfFrame.ContainsKey(frameId))
            {
                _scenesOfFrame.TryGetValue(frameId, out var scene);
                return scene;
            }

            return new Mat();
        }

        public int GetCachedSceneCount()
        {
            return _scenesOfFrame.Count;
        }

        public SortedList<float, Mat> GetObjectSnapshotsByObjectId(string id)
        {
            if (!_snapshotsByScore.ContainsKey(id))
            {
                return new SortedList<float, Mat>();
            }

            return _snapshotsByScore[id];
        }

        public Mat GetBestSnapshotByObjectId(string id)
        {
            var snapshots = GetObjectSnapshotsByObjectId(id);
            if (snapshots.Count == 0)
            {
                return new Mat();
            }

            var highestScore = snapshots.Keys.Max();
            Mat highestSnapshot = snapshots[highestScore];

            return highestSnapshot;
        }

        public int GetCachedSnapshotCount()
        {
            return _snapshotsByScore.Count;
        }
        
        private void ReleaseSceneByFrameId(long frameId)
        {
            if (_scenesOfFrame.ContainsKey(frameId))
            {
                _scenesOfFrame[frameId].Dispose();

                _scenesOfFrame.TryRemove(frameId, out var mat);
            }
        }

        private void ReleaseSnapshotsByObjectId(string id, bool saveBeforeRelease = true)
        {
            if (!_snapshotsByScore.ContainsKey(id))
            {
                return;
            }

            SortedList<float, Mat> snapshots = _snapshotsByScore[id];

            if (saveBeforeRelease)
            {
                var highestScore = snapshots.Keys.Max();
                Mat highestSnapshot = snapshots[highestScore];

                SaveBestSnapshot(id, highestSnapshot);
            }

            foreach (Mat snapshot in snapshots.Values)
            {
                snapshot.Dispose();
            }

            _snapshotsByScore.TryRemove(id, out var removedSnapshots);
        }

        private void SaveBestSnapshot(string id, Mat highestSnapshot)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            string filename = id.Replace(':', '_');
            if (highestSnapshot.Width > _minSnapshotWidth && highestSnapshot.Height > _maxSnapshotHeight)
            {
                string path = $"{_snapshotsDir}/Best";
                path.EnsureDirExistence();
                var fileSavePath = Path.Combine(path, $"{filename}_{timestamp}.jpg");

                highestSnapshot.SaveImage(fileSavePath);
            }
        }

        public override void ProcessEvent(ObjectExpiredEvent @event)
        {
            Task.Run(() =>
            {
                ReleaseSnapshotsByObjectId(@event.Id, _saveBestSnapshot);
                ReleaseSnapshotsByObjectId($"cb_{@event.Id}", _saveBestSnapshot);
            }).Wait();
        }

        public override void ProcessEvent(FrameExpiredEvent @event)
        {
            Task.Run(() =>
            {
                ReleaseSceneByFrameId(@event.FrameId);
            }).Wait();
        }

        public void Dispose()
        {
            foreach (Mat scene in _scenesOfFrame.Values)
            {
                scene.Dispose();
            }

            base.Dispose();
        }
    }
}
