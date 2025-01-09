using SentinelCore.Domain.Entities.AnalysisDefinitions.Geometrics;

namespace SentinelCore.Domain.Entities.AnalysisDefinitions
{
    public class InterestArea : NormalizedPolygon
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public List<string> RelativeTypes { get; set; }
    }
}
