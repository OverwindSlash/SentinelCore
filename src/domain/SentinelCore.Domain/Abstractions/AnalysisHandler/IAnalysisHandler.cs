using SentinelCore.Domain.Entities.AnalysisEngine;
using SentinelCore.Domain.Entities.VideoStream;

namespace SentinelCore.Domain.Abstractions.AnalysisHandler
{
    public interface IAnalysisHandler : IDisposable
    {
        public string HandlerName { get; }

        public void SetServiceProvider(IServiceProvider serviceProvider);

        AnalysisResult Analyze(Frame frame);
    }
}
