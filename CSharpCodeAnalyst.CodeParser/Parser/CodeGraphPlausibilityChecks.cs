using System.Diagnostics;

namespace CSharpCodeAnalyst.CodeParser.Parser;

internal static class CodeGraphPlausibilityChecks
{
    public static void PlausibilityChecks(CodeGraph.Graph.CodeGraph codeGraph)
    {
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

}