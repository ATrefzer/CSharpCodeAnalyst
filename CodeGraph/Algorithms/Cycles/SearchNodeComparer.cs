using System.Runtime.CompilerServices;

namespace CodeGraph.Algorithms.Cycles;

public class SearchNodeComparer : IEqualityComparer<SearchNode>
{
    public static SearchNodeComparer Instance { get; } = new();

    public bool Equals(SearchNode? x, SearchNode? y)
    {
        return ReferenceEquals(x, y);
    }

    public int GetHashCode(SearchNode? obj)
    {
        return obj is null ? 0 : RuntimeHelpers.GetHashCode(obj);
    }
}