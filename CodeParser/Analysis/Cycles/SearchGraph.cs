using CodeParser.Analysis.Shared;
using Contracts.GraphInterface;

namespace CodeParser.Analysis.Cycles;

public class SearchGraph : IGraphRepresentation<SearchNode>
{
    public SearchGraph(List<SearchNode> vertices)
    {
        Vertices = vertices;
    }

    public List<SearchNode> Vertices { get; }

    public uint VertexCount
    {
        get => (uint)Vertices.Count();
    }

    public IReadOnlyCollection<SearchNode> GetNeighbors(SearchNode vertex)
    {
        return vertex.Dependencies;
    }

    public IReadOnlyCollection<SearchNode> GetVertices()
    {
        return Vertices;
    }

    public bool IsEdge(SearchNode source, SearchNode target)
    {
        return source.Dependencies.Contains(target);
    }

    public bool IsVertex(SearchNode vertex)
    {
        return Vertices.Contains(vertex);
    }
}