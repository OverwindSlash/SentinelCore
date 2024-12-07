using OpenCvSharp;
using SentinelCore.Domain.Entities.ObjectDetection;
using SentinelCore.Domain.Entities.VideoStream;

namespace SentinelCore.Domain.Abstractions.SnapshotManager
{
    public interface ISnapshotManager
    {
        void ProcessSnapshots(Frame frame);

        public Mat GetSceneByFrameId(long frameId);
        public int GetCachedSceneCount();

        public SortedList<float, Mat> GetObjectSnapshotsByObjectId(string objId);
        public int GetCachedSnapshotCount();

        Mat TakeSnapshot(Frame frame, BoundingBox bboxs);
    }
}
