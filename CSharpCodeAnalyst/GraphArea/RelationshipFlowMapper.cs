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
        
        // Keep normal direction (already show flow correctly)
        { RelationshipType.Calls, false },         // Caller -> Callee (control flow)
        { RelationshipType.Invokes, false },       // Invoker -> Event (event flow)
        { RelationshipType.Creates, false },       // Creator -> Created (instantiation flow)
        { RelationshipType.Uses, false },          // User -> Used (data flow)
        { RelationshipType.Inherits, false },      // Child -> Parent (inheritance flow)
        { RelationshipType.UsesAttribute, false }, // Decorated -> Attribute (metadata flow)
    };

    public static bool ShouldReverseInFlowMode(RelationshipType type)
    {
        return FlowReversalMap.GetValueOrDefault(type, false);
    }
}