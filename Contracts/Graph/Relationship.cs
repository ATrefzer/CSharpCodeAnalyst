using System.Diagnostics;

namespace Contracts.Graph;

[DebuggerDisplay("{Type}")]
public class Relationship
{
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
    public string SourceId { get; }
    public string TargetId { get; }
    public RelationshipType Type { get; }

    public List<SourceLocation> SourceLocations { get; init; } = [];

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