namespace CodeGraph.Algorithms.Cycles;

public class CycleGroup(Graph.CodeGraph codeGraph)
{
    public Graph.CodeGraph CodeGraph { get; } = codeGraph;
    public string Name { get; set; } = string.Empty;
}