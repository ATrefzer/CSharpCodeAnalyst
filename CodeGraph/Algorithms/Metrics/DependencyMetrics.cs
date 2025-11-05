using CodeGraph.Graph;

namespace CodeGraph.Algorithms.Metrics;

public class InOutDegree(CodeElement element)
{
    public CodeElement Element { get; } = element;
    public int Incoming { get; set; }
    public int Outgoing { get; set; }
}

public static class DependencyMetrics
{
    public static List<InOutDegree> Calculate(Graph.CodeGraph graph)
    {
        // Initialize result with already know outgoing dependencies
        // Including self.
        var result = graph.Nodes.ToDictionary(kvp => kvp.Key, kvp => new InOutDegree(kvp.Value) { Outgoing = kvp.Value.Relationships.Count });

        foreach (var node in graph.Nodes.Values)
        {
            foreach (var relationship in node.Relationships)
            {
                var target = result[relationship.TargetId];
                target.Incoming += 1;
            }
        }

        return result.Values.ToList();
    }
}