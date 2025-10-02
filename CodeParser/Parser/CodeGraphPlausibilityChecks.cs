using System.Diagnostics;
using Contracts.Graph;

namespace CodeParser.Parser;

internal static class CodeGraphPlausibilityChecks
{
    public static void PlausibilityChecks(CodeGraph codeGraph)
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