using System.Diagnostics;
using CodeGraph.Graph;

// ReSharper disable NotResolvedInText

namespace CodeGraph.Algorithms.Cycles;

[DebuggerDisplay("{OriginalElement.ElementType}: {OriginalElement.Name}")]
public class SearchNode(string id, CodeElement originalElement)
{
    public string Id { get; } = id;
    public CodeElement OriginalElement { get; } = originalElement;
    public HashSet<SearchNode> Dependencies { get; } = new(SearchNodeComparer.Instance);
}