using SentinelCore.Domain.Entities.AnalysisDefinitions.Geometrics;

namespace SentinelCore.Domain.Entities.AnalysisDefinitions;

public class AnalysisArea : NormalizedPolygon
{
    public string Name { get; set; }
}