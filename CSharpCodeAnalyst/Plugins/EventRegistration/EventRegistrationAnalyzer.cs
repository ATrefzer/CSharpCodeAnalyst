using Contracts.Graph;

namespace CSharpCodeAnalyst.Plugins.EventRegistration;

/// <summary>
///     Finds imbalances between event registrations and un-registrations.
/// </summary>
internal static class EventRegistrationAnalyzer
{
    internal static List<Result> FindImbalances(CodeGraph originalGraph)
    {
        var relationships = originalGraph.GetAllRelationships().Where(r => r.Type == RelationshipType.Handles).ToHashSet();

        var mismatches = relationships.Where(IsIncomplete);
        var imbalances = new List<Result>();

        foreach (var mismatch in mismatches)
        {
            // Assume imbalance
            var handler = originalGraph.Nodes[mismatch.SourceId];
            var target = originalGraph.Nodes[mismatch.TargetId];
            var locations = mismatch.SourceLocations;
            imbalances.Add(new Result(handler, target, locations));
        }

        return imbalances;

        bool IsIncomplete(Relationship r)
        {
            return !(r.HasAttribute(RelationshipAttribute.EventUnregistration) && r.HasAttribute(RelationshipAttribute.EventRegistration));
        }
    }
}