using OpenCvSharp;
using SentinelCore.Domain.Entities.ObjectDetection;
using SentinelCore.Domain.Entities.VideoStream;

namespace SentinelCore.Domain.Abstractions.SnapshotManager
{
    public interface ISnapshotManager
    {
        void ProcessSnapshots(Frame frame);

        void AddSceneByFrameId(long frameId, Frame frame);
        public Mat GetSceneByFrameId(long frameId);
        public int GetCachedSceneCount();

        void AddSnapshotOfObjectById(Frame frame);
        void AddSnapshotOfObjectById(string objId, float score, Frame frame, BoundingBox bboxs);
        public SortedList<float, Mat> GetObjectSnapshotsByObjectId(string objId);
        public int GetCachedSnapshotCount();

        Mat TakeSnapshot(Frame frame, BoundingBox bboxs);
    }
}
