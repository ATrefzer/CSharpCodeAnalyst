using Contracts.Graph;

namespace CSharpCodeAnalyst.Analyzer.EventRegistration;

public class EventRegistrationAnalyzer
{
    public static List<EventRegistrationImbalance> FindImbalances(CodeGraph originalGraph)
    {
        var relationships = originalGraph.GetAllRelationships().Where(r => r.Type == RelationshipType.Handles).ToHashSet();

        var mismatches = relationships.Where(IsIncomplete);
        var imbalances = new List<EventRegistrationImbalance>();

        foreach (var mismatch in mismatches)
        {
            // Assume imbalance
            var handler = originalGraph.Nodes[mismatch.SourceId];
            var target = originalGraph.Nodes[mismatch.TargetId];
            var locations = mismatch.SourceLocations;
            imbalances.Add(new EventRegistrationImbalance(handler, target, locations));
        }

        return imbalances;

        bool IsIncomplete(Relationship r)
        {
            return !(r.HasAttribute(RelationshipAttribute.EventUnregistration) && r.HasAttribute(RelationshipAttribute.EventRegistration));
        }
    }
}