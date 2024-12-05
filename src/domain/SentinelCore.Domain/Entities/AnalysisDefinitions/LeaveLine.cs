using SentinelCore.Domain.Entities.AnalysisDefinitions.Geometrics;
using System.Text.Json.Serialization;

namespace SentinelCore.Domain.Entities.AnalysisDefinitions;

public class LeaveLine : NormalizedLine
{
    public string Name { get; set; }

    [JsonIgnore]
    public EnterLine EnterLine { get; set; }
}