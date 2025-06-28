using Contracts.Graph;

namespace CSharpCodeAnalyst.Project;

[Serializable]
public class SerializableRelationship(
    string sourceId,
    string targetId,
    RelationshipType type,
    uint attributes,
    List<SourceLocation> sourceLocations)
{
    public uint Attributes { get; set; } = attributes;
    public string SourceId { get; set; } = sourceId;
    public string TargetId { get; set; } = targetId;
    public RelationshipType Type { get; set; } = type;
    public List<SourceLocation> SourceLocations { get; set; } = sourceLocations;
}