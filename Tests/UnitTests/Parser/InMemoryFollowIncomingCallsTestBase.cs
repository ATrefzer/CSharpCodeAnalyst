using CodeGraph.Exploration;
using CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Base for in-memory tests of <see cref="CodeGraphExplorer.FollowIncomingCallsHeuristically" />:
///     parses a single scenario snippet (via <see cref="InMemoryParseTestBase" />), runs the heuristic from
///     a chosen origin and projects the resulting relationships/elements to paths below the namespace.
///     Replaces the FollowHeuristic scenarios that previously ran against the parsed TestSuite solution.
/// </summary>
public abstract class InMemoryFollowIncomingCallsTestBase : InMemoryParseTestBase
{
    protected SearchResult FollowIncomingCalls(string originPath)
    {
        var explorer = new CodeGraphExplorer();
        explorer.LoadCodeGraph(Graph);

        var origin = Graph.Nodes.Values.Single(e => PathOf(e) == originPath);
        return explorer.FollowIncomingCallsHeuristically(origin.Id);
    }

    protected string[] RelationshipsOf(SearchResult result)
    {
        return result.Relationships
            .Select(d => $"{PathOf(Graph.Nodes[d.SourceId])} -({d.Type})-> {PathOf(Graph.Nodes[d.TargetId])}")
            .ToArray();
    }

    protected string[] ElementsOf(SearchResult result)
    {
        return result.Elements.Select(PathOf).ToArray();
    }
}
