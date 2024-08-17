using Contracts.Graph;

namespace CodeParser.Analysis.Cycles;

public static class DependencyClassifier
{
    /// <summary>
    ///     The fact that a method overrides another is only interesting when exploring a codebase.
    ///     For the dependency graph it is enough to see that the type inherits from an interface.
    /// </summary>
    public static bool IsDependencyRelevantForCycle(CodeGraph codeGraph, Dependency dependency)
    {
        var source = codeGraph.Nodes[dependency.SourceId];
        var target = codeGraph.Nodes[dependency.TargetId];

        switch (source.ElementType)
        {
            case CodeElementType.Method when target.ElementType is CodeElementType.Method
                                             && dependency.Type == DependencyType.Implements:
            case CodeElementType.Method when target.ElementType is CodeElementType.Method
                                             && dependency.Type == DependencyType.Overrides:
            case CodeElementType.Property when target.ElementType is CodeElementType.Property
                                               && dependency.Type == DependencyType.Implements:
                return false;
            default:
                return true;
        }
    }
}