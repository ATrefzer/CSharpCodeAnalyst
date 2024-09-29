using Contracts.Graph;

namespace CSharpCodeAnalyst.Project;

[Serializable]
public class SerializableRelationship(
    string sourceId,
    string targetId,
    RelationshipType type,
    List<SourceLocation> sourceLocations)
{
    public string SourceId { get; set; } = sourceId;
    public string TargetId { get; set; } = targetId;
    public RelationshipType Type { get; set; } = type;
    public List<SourceLocation> SourceLocations { get; set; } = sourceLocations;
}