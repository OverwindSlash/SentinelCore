﻿using OpenCvSharp;
using SentinelCore.Domain.Abstractions.SnapshotManager;
using SentinelCore.Domain.Entities.ObjectDetection;
using SentinelCore.Domain.Entities.VideoStream;
using System.Collections.Concurrent;
using MessagePipe;
using SentinelCore.Domain.Abstractions.EventHandler;
using SentinelCore.Domain.Events.AnalysisEngine;
using SentinelCore.Domain.Utils.Extensions;

namespace SnapshotManager.InMemory
{
    public class SnapshotManager : ISnapshotManager, IEventSubscriber<ObjectExpiredEvent>, IEventSubscriber<FrameExpiredEvent>
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

        private ISubscriber<ObjectExpiredEvent> _oeSubscriber;
        private IDisposable _disposableOeSubscriber;

        private ISubscriber<FrameExpiredEvent> _feSubscriber;
        private IDisposable _disposableFeSubscriber;

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
            foreach (var obj in frame.DetectedObjects)
            {
                if (!obj.IsUnderAnalysis)
                {
                    continue;
                }

                AddSnapshotOfObjectById(obj.Id, CalculateFactor(obj), frame, obj.Bbox);
            }
        }

        private void AddSnapshotOfObjectById(string objId, float score, Frame frame, BoundingBox bboxs)
        {
            if (!_snapshotsByScore.ContainsKey(objId))
            {
                _snapshotsByScore.TryAdd(objId, new SortedList<float, Mat>());
            }

            Mat snapshot = TakeSnapshot(frame, bboxs);

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

        private float CalculateFactor(DetectedObject obj)
        {
            // Area as order factor.
            return obj.Width * obj.Height;
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

        public int GetCachedSnapshotCount()
        {
            return _snapshotsByScore.Count;
        }

        public Mat TakeSnapshot(Frame frame, BoundingBox bboxs)
        {
            return frame.Scene.SubMat(new Rect(bboxs.X, bboxs.Y, bboxs.Width, bboxs.Height)).Clone();
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

        public void SetSubscriber(ISubscriber<ObjectExpiredEvent> subscriber)
        {
            _oeSubscriber = subscriber;
            _disposableOeSubscriber = _oeSubscriber.Subscribe(ProcessEvent);
        }

        public void ProcessEvent(ObjectExpiredEvent @event)
        {
            Task.Run(() =>
            {
                ReleaseSnapshotsByObjectId(@event.Id, _saveBestSnapshot);
                ReleaseSnapshotsByObjectId($"cb_{@event.Id}", _saveBestSnapshot);
            }).Wait();
        }

        public void SetSubscriber(ISubscriber<FrameExpiredEvent> subscriber)
        {
            _feSubscriber = subscriber;
            _disposableFeSubscriber = _feSubscriber.Subscribe(ProcessEvent);
        }

        public void ProcessEvent(FrameExpiredEvent @event)
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

            _disposableOeSubscriber.Dispose();
            _disposableFeSubscriber.Dispose();
        }
    }
}