using CodeGraph.Algorithms.Cycles;
using CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Base for in-memory cycle tests: parses a snippet (via <see cref="InMemoryParseTestBase" />), runs
///     <see cref="CycleFinder.FindCycleGroups" /> on the resulting graph and projects each detected cycle
///     group to node/edge paths below the namespace (so assertions read like "ClassA._fieldB -> ClassB").
///     Replaces the project-scoped Core.Cycles approval fixture; every source file's cycles are asserted in
///     their own fixture.
/// </summary>
public abstract class InMemoryCycleParseTestBase : InMemoryParseTestBase
{
    protected List<(HashSet<string> Nodes, HashSet<string> Edges)> CycleGroups()
    {
        return CycleFinder.FindCycleGroups(Graph)
            .Select(group => (
                // Resolve each node back to the main graph: a cycle sub-graph only keeps Parent links
                // for ancestors that are themselves in the cycle, which would truncate nested paths.
                group.CodeGraph.Nodes.Values.Select(n => PathOf(Graph.Nodes[n.Id])).ToHashSet(),
                group.CodeGraph.GetAllRelationships()
                    .Select(r => $"{PathOf(Graph.Nodes[r.SourceId])} -> {PathOf(Graph.Nodes[r.TargetId])}")
                    .ToHashSet()))
            .ToList();
    }

    /// <summary>Asserts the graph contains exactly one cycle group with the given nodes and edges.</summary>
    protected void AssertSingleCycle(string[] nodes, string[] edges)
    {
        var groups = CycleGroups();
        Assert.That(groups, Has.Count.EqualTo(1), "expected exactly one cycle group");
        Assert.That(groups[0].Nodes, Is.EquivalentTo(nodes));
        Assert.That(groups[0].Edges, Is.EquivalentTo(edges));
    }

    /// <summary>Asserts the total number of detected cycle groups.</summary>
    protected void AssertCycleGroupCount(int expected)
    {
        Assert.That(CycleGroups(), Has.Count.EqualTo(expected));
    }

    /// <summary>Asserts some detected cycle group matches the given nodes and edges exactly.</summary>
    protected void AssertContainsCycle(string[] nodes, string[] edges)
    {
        var found = CycleGroups().Any(g => g.Nodes.SetEquals(nodes) && g.Edges.SetEquals(edges));
        Assert.That(found, Is.True, "expected cycle group not found");
    }
}
