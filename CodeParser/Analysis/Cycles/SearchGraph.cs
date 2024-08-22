using CodeParser.Analysis.Shared;
using GraphLib.Contracts;

namespace CodeParser.Analysis.Cycles;

    public class SearchGraph : IGraphRepresentation<SearchNode>
    {
        public SearchGraph(List<SearchNode> vertices) 
        {
            Vertices = vertices;
        }

        public uint VertexCount => (uint)Vertices.Count();

        public List<SearchNode> Vertices { get; }

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
