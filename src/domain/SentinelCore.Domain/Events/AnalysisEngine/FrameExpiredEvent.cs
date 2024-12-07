using SentinelCore.Domain.Entities.VideoStream;

namespace SentinelCore.Domain.Events.AnalysisEngine
{
    public class FrameExpiredEvent : EventBase
    {
        public long FrameId { get; }

        public FrameExpiredEvent(long frameId)
        {
            FrameId = frameId;
        }

        public FrameExpiredEvent(Frame frame)
        {
            FrameId = frame.FrameId;
        }
    }
}
