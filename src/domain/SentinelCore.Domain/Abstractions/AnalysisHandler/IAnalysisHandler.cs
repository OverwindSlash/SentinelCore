using SentinelCore.Domain.Abstractions.EventHandler;
using SentinelCore.Domain.Entities.AnalysisEngine;
using SentinelCore.Domain.Entities.VideoStream;
using SentinelCore.Domain.Events.AnalysisEngine;

namespace SentinelCore.Domain.Abstractions.AnalysisHandler
{
    public interface IAnalysisHandler : IEventSubscriber<FrameExpiredEvent>, IEventSubscriber<ObjectExpiredEvent>, IDisposable
    {
        public string HandlerName { get; }

        AnalysisResult Analyze(Frame frame);
    }
}
