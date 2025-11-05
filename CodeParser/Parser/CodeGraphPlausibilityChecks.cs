using System.Diagnostics;

namespace CodeParser.Parser;

internal static class CodeGraphPlausibilityChecks
{
    public static void PlausibilityChecks(CodeGraph.Graph.CodeGraph codeGraph)
    {
        MultipleSourceLocationsInSameLineAreUnlikely(codeGraph);
        RelationshipsHaveNoDeadEnds(codeGraph);
    }

    private static void RelationshipsHaveNoDeadEnds(CodeGraph.Graph.CodeGraph codeGraph)
    {
        var deadEnds = codeGraph.GetAllRelationships().Where(r => !codeGraph.Nodes.ContainsKey(r.SourceId) ||
                                                                  !codeGraph.Nodes.ContainsKey(r.TargetId));

        if (deadEnds.Any())
        {
            Trace.WriteLine("Found relationships with dead ends.");
        }
    }

    private static void MultipleSourceLocationsInSameLineAreUnlikely(CodeGraph.Graph.CodeGraph codeGraph)
    {
        var codeGraphNodes = codeGraph.Nodes.Values;

        foreach (var node in codeGraphNodes)
        {
            var hash = new HashSet<(string, int)>();
            var locations = node.SourceLocations.Select(l => (l.File, l.Line)).ToHashSet();
            foreach (var location in locations)
            {
                if (!hash.Add(location!))
                {
                    Trace.TraceWarning($"Duplicate location found: {location}");
                }
            }


            foreach (var relationship in node.Relationships)
            {
                hash.Clear();
                locations = relationship.SourceLocations.Select(l => (l.File, l.Line)).ToHashSet();
                foreach (var location in locations)
                {
                    if (!hash.Add(location!))
                    {
                        Trace.TraceWarning($"Duplicate location found: {location}");
                    }
                }
            }
        }
    }
}