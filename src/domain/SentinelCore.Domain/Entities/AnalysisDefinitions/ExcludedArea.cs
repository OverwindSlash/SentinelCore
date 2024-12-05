using SentinelCore.Domain.Entities.AnalysisDefinitions.Geometrics;

namespace SentinelCore.Domain.Entities.AnalysisDefinitions;

public class ExcludedArea : NormalizedPolygon
{
    public string Name { get; set; }
}