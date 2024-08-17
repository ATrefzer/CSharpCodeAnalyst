using System.Runtime.CompilerServices;

namespace CodeParser.Analysis.Shared;

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