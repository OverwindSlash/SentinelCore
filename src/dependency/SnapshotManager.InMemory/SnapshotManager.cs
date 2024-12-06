using OpenCvSharp;
using SentinelCore.Domain.Abstractions.SnapshotManager;
using SentinelCore.Domain.Entities.ObjectDetection;
using SentinelCore.Domain.Entities.VideoStream;
using System.Collections.Concurrent;

namespace SnapshotManager.InMemory
{
    public class SnapshotManager : ISnapshotManager
    {
        // frameId -> Scene
        private readonly ConcurrentDictionary<long, Mat> _scenesOfFrame;

        // object snapshot list by objId -> (factor, objectMat)
        private readonly ConcurrentDictionary<string, SortedList<float, Mat>> _snapshotsByScore;

        private string _snapshotsDir = "Snapshots";
        private bool _saveBestSnapshot = false;
        private int _maxObjectSnapshots = 10;
        private int _minSnapshotWidth = 40;
        private int _maxSnapshotHeight = 40;

        public string Name => "In-memory snapshot manager";

        public SnapshotManager(Dictionary<string, string> preferences)
        {
            _scenesOfFrame = new ConcurrentDictionary<long, Mat>();
            _snapshotsByScore = new ConcurrentDictionary<string, SortedList<float, Mat>>();

            _snapshotsDir = preferences["SnapshotsDir"];
            _saveBestSnapshot = bool.Parse(preferences["SaveBestSnapshot"]);
            _maxObjectSnapshots = int.Parse(preferences["MaxSnapshots"]);
            _minSnapshotWidth = int.Parse(preferences["MinSnapshotWidth"]);
            _maxSnapshotHeight = int.Parse(preferences["MinSnapshotHeight"]);

            var currentDirectory = Directory.GetCurrentDirectory();
            var combine = Path.Combine(currentDirectory, _snapshotsDir);
            var exists = Directory.Exists(combine);

            if (!exists)
            {
                Directory.CreateDirectory(_snapshotsDir);
            }
        }

        public void ProcessSnapshots(Frame frame)
        {
            AddSceneByFrameId(frame.FrameId, frame);
            AddSnapshotOfObjectById(frame);
        }

        public void AddSceneByFrameId(long frameId, Frame frame)
        {
            if (!_scenesOfFrame.ContainsKey(frameId))
            {
                _scenesOfFrame.TryAdd(frameId, frame.Scene);
            }
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

        public void AddSnapshotOfObjectById(Frame frame)
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

        private float CalculateFactor(DetectedObject obj)
        {
            // Area as order factor.
            return obj.Width * obj.Height;
            // return obj.Width;
        }

        public void AddSnapshotOfObjectById(string objId, float score, Frame frame, BoundingBox bboxs)
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

        /*private void SaveBestSnapshot(string id, Mat highestSnapshot)
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
        }*/

        public void Dispose()
        {
            foreach (Mat scene in _scenesOfFrame.Values)
            {
                scene.Dispose();
            }
        }
    }
}
