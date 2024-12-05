using SentinelCore.Domain.Entities.AnalysisDefinitions.Geometrics;
using System.Text.Json.Serialization;

namespace SentinelCore.Domain.Entities.AnalysisDefinitions;

public class EnterLine : NormalizedLine
{
    public string Name { get; set; }

    [JsonIgnore]
    public LeaveLine LeaveLine { get; set; }
}