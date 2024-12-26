using SentinelCore.Domain.Abstractions.AnalysisHandler;
using SentinelCore.Domain.Entities.AnalysisEngine;
using SentinelCore.Domain.Entities.VideoStream;

namespace Handler.Ocr.Algorithms
{
    public class PaddleSharpOcrAlg : IAnalysisHandler
    {
        public string HandlerName { get; }

        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            throw new NotImplementedException();
        }

        public AnalysisResult Analyze(Frame frame)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

    }
}
