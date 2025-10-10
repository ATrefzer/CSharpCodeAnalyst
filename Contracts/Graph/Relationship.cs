using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Contracts.Graph;

[DebuggerDisplay("{Type}")]
public class Relationship
{
    [JsonConstructor]
    private Relationship()
    {
        SourceId = string.Empty;
        TargetId = string.Empty;
        Type = RelationshipType.Uses;
    }

    public Relationship(string sourceId, string targetId, RelationshipType type)
    {
        SourceId = sourceId;
        TargetId = targetId;
        Type = type;
    }

    public Relationship(string sourceId, string targetId, RelationshipType type, RelationshipAttribute attributes)
    {
        SourceId = sourceId;
        TargetId = targetId;
        Type = type;
        Attributes = attributes;
    }

    public RelationshipAttribute Attributes { get; set; } = RelationshipAttribute.None;
    public string SourceId { get; private set; }
    public string TargetId { get; private set; }
    public RelationshipType Type { get; private set; }

    public List<SourceLocation> SourceLocations { get; set; } = [];

    public bool HasAttribute(RelationshipAttribute attribute)
    {
        return Attributes.HasFlag(attribute);
    }

    public void SetAttribute(RelationshipAttribute attribute, bool value = true)
    {
        if (value)
        {
            Attributes |= attribute;
        }
        else
        {
            Attributes &= ~attribute;
        }
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Relationship other)
        {
            return false;
        }

        // Yes source, target and type are unique. For this triple we store all source locations.
        return
            SourceId == other.SourceId &&
            TargetId == other.TargetId &&
            Type == other.Type;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(SourceId, TargetId, Type);
    }

    public Relationship Clone()
    {
        var newRelationship = new Relationship(SourceId, TargetId, Type);
        newRelationship.SourceLocations.AddRange(SourceLocations);
        newRelationship.Attributes = Attributes;
        return newRelationship;
    }
}