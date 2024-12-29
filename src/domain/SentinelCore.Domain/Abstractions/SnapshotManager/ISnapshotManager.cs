using OpenCvSharp;
using SentinelCore.Domain.Abstractions.EventHandler;
using SentinelCore.Domain.Entities.ObjectDetection;
using SentinelCore.Domain.Entities.VideoStream;
using SentinelCore.Domain.Events.AnalysisEngine;

namespace SentinelCore.Domain.Abstractions.SnapshotManager
{
    public interface ISnapshotManager : IEventSubscriber<ObjectExpiredEvent>, IEventSubscriber<FrameExpiredEvent>
    {
        public string SnapshotDir { get; }

        void ProcessSnapshots(Frame frame);

        public Mat GetSceneByFrameId(long frameId);
        public int GetCachedSceneCount();

        public SortedList<float, Mat> GetObjectSnapshotsByObjectId(string objId);
        public Mat GetBestSnapshotByObjectId(string objId);
        public int GetCachedSnapshotCount();

        Mat TakeSnapshot(Frame frame, BoundingBox bboxs);
        void AddSnapshotOfObjectById(string objId, float score, Mat snapshot);

        Mat GenerateBoxedScene(Mat scene, List<BoundingBox> boundingBoxes);
    }
}
