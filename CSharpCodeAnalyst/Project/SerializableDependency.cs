using Contracts.Graph;

namespace CSharpCodeAnalyst.Project;

[Serializable]
public class SerializableDependency(
    string sourceId,
    string targetId,
    DependencyType type,
    List<SourceLocation> sourceLocations)
{
    public string SourceId { get; set; } = sourceId;
    public string TargetId { get; set; } = targetId;
    public DependencyType Type { get; set; } = type;
    public List<SourceLocation> SourceLocations { get; set; } = sourceLocations;
}