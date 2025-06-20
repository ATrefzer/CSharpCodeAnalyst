using Contracts.Graph;

namespace CSharpCodeAnalyst.GraphArea;

public static class RelationshipFlowMapper
{
    private static readonly Dictionary<RelationshipType, bool> FlowReversalMap = new()
    {
        // Reverse these for information flow
        { RelationshipType.Handles, true },        // Show flow: Event -> Handler
        { RelationshipType.Implements, true },     // Show flow: Interface -> Implementation  
        { RelationshipType.Overrides, true },      // Show flow: Base -> Override
    };

    public static bool ShouldReverseInFlowMode(RelationshipType type)
    {
        return FlowReversalMap.GetValueOrDefault(type, false);
    }
}