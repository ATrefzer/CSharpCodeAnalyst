using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.CodeGraph.Algorithms.Cycles;

public class CycleGroup(Graph.CodeGraph codeGraph, List<CodeElement> vertices)
{
    public Graph.CodeGraph CodeGraph { get; } = codeGraph;

    /// <summary>
    ///     The vertices of the strongly connected component on the lifted search-graph level
    ///     (namespaces, types, or members - all of the same container rank). The detailed
    ///     <see cref="CodeGraph" /> additionally contains their involved children and ancestors,
    ///     so scope questions ("does this cycle lie inside X?") must be asked against these
    ///     vertices, not against the detailed graph.
    /// </summary>
    public List<CodeElement> Vertices { get; } = vertices;

    public string Name { get; set; } = string.Empty;
}