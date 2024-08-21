using System.Diagnostics;

namespace Contracts.Graph;

[DebuggerDisplay("{Type}")]
public class Dependency(string sourceId, string targetId, DependencyType type)
{
    public string SourceId { get; } = sourceId;
    public string TargetId { get; } = targetId;
    public DependencyType Type { get; } = type;

    public List<SourceLocation> SourceLocations { get; set; } = [];

    public override bool Equals(object? obj)
    {
        if (obj is not Dependency other)
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

    public Dependency Clone()
    {
        var newDependency = new Dependency(SourceId, TargetId, Type);
        newDependency.SourceLocations.AddRange(SourceLocations);
        return newDependency;
    }
}