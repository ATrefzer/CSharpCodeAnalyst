namespace GraphLib.Contracts;

public interface IGraphRepresentationWidthEdgeProperties<TVertex> : IGraphRepresentation<TVertex>
{
    EdgeProperties GetEdgeProperties(TVertex source, TVertex target);

    IGraphRepresentationWidthEdgeProperties<TVertex> Transpose();
}