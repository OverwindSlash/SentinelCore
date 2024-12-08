namespace SentinelCore.Domain.Entities.AnalysisEngine
{
    public class AnalysisResult(bool success)
    {
        public bool Success { get; private set; } = success;
    }
}
