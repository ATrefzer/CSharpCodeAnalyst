using System.Diagnostics;
using Contracts.Graph;
// ReSharper disable NotResolvedInText

namespace CodeParser.Analysis.Shared;

[DebuggerDisplay("{OriginalElement.ElementType}: {OriginalElement.Name}")]
public class SearchNode(string id, CodeElement originalElement)
{
    public string Id { get; set; } = id;
    public CodeElement OriginalElement { get; set; } = originalElement;
    public HashSet<SearchNode> Dependencies { get; } = new(SearchNodeComparer.Instance);
}