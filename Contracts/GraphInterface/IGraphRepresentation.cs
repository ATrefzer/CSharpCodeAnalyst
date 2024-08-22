namespace GraphLib.Contracts;

public interface IGraphRepresentation<TVertex>
{
    uint VertexCount { get; }
    IReadOnlyCollection<TVertex> GetNeighbors(TVertex vertex);
    bool IsVertex(TVertex vertex);
    bool IsEdge(TVertex source, TVertex target);
    IReadOnlyCollection<TVertex> GetVertices();
}