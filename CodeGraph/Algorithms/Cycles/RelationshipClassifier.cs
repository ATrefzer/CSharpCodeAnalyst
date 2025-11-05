using CodeGraph.Graph;

namespace CodeGraph.Algorithms.Cycles;

public static class RelationshipClassifier
{
    /// <summary>
    ///     The fact that a method overrides another is only interesting when exploring a codebase.
    ///     For the relationship graph it is enough to see that the type inherits from an interface.
    /// </summary>
    public static bool IsRelationshipRelevantForCycle(Graph.CodeGraph codeGraph, Relationship relationship)
    {
        if (relationship.Type == RelationshipType.Handles)
        {
            // This is not a code dependency. It is actually the other direction.
            // This would break the cycle detection.
            return false;
        }

        var source = codeGraph.Nodes[relationship.SourceId];
        var target = codeGraph.Nodes[relationship.TargetId];

        switch (source.ElementType)
        {
            case CodeElementType.Method when target.ElementType is CodeElementType.Method
                                             && relationship.Type == RelationshipType.Implements:
            case CodeElementType.Method when target.ElementType is CodeElementType.Method
                                             && relationship.Type == RelationshipType.Overrides:
            case CodeElementType.Property when target.ElementType is CodeElementType.Property
                                               && relationship.Type == RelationshipType.Implements:
                return false;
            default:
                return true;
        }
    }
}