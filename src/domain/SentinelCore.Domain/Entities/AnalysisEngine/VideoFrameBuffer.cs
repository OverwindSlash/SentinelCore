using SentinelCore.Domain.DataStructures;
using SentinelCore.Domain.Entities.VideoStream;

namespace SentinelCore.Domain.Entities.AnalysisEngine
{
    public class VideoFrameBuffer : ConcurrentBoundedQueue<Frame>
    {
        public VideoFrameBuffer(int bufferSize)
            : base(bufferSize)
        {

        }
    }
}
