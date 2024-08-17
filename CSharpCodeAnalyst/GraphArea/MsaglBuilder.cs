//#define GENERATE_DEBUG_OUTPUT

#if GENERATE_DEBUG_OUTPUT
    using System.Diagnostics;
#endif

using Contracts.Colors;
using Contracts.Graph;
using Microsoft.Msagl.Drawing;

namespace CSharpCodeAnalyst.GraphArea;

/// <summary>
///     Transformation of a CodGraph to Msagl graph structure.
/// </summary>
internal class MsaglBuilder
{
    private readonly Dictionary<(string, string), Edge> _edges = new();

    public Graph CreateGraphFromCodeStructure(CodeGraph codeGraph, bool showFlatGraph)
    {
        if (showFlatGraph)
        {
            return CreateGraphFromCodeStructureFlat(codeGraph);
        }

        return CreateGraphFromCodeStructureHierarchical(codeGraph);
    }

    private Graph CreateGraphFromCodeStructureHierarchical(CodeGraph codeGraph)
    {
        // Since we start with a fresh graph we don't need to check for existing nodes and edges.

        _edges.Clear();

        var graph = new Graph("graph");

        // Pre create all sub-graphs.
        var subGraphs = codeGraph.Nodes.Values
            .Where(n => n.Children.Any())
            .Select(n => new Subgraph(n.Id))
            .ToDictionary(s => s.Id, s => s);


        // Label the sub-graphs
        foreach (var subgraph in subGraphs.Values)
        {
            // Works
            var node = codeGraph.Nodes[subgraph.Id];
            subgraph.Attr.FillColor = Color.AliceBlue;
            subgraph.LabelText = node.Name;
            subgraph.UserData = node;
            subgraph.Attr.FillColor = GetColor(node);
        }

#if GENERATE_DEBUG_OUTPUT
        Debug.WriteLine("Create sub graphs");
        Debug.WriteLine("var dict = new Dictionary<string, Subgraph>();");
        Debug.WriteLine("Subgraph subGraph = null;");
        Debug.WriteLine("Subgraph parentSubGraph = null;");
        Debug.WriteLine("Node newNode = null;");
        Debug.WriteLine("var graph = new Graph(\"test\");");
        foreach (var subGraph in subGraphs.Values)
        {
            Debug.WriteLine($"subGraph = new Subgraph(\"{subGraph.LabelText}\");");
            Debug.WriteLine("dict.Add(subGraph.Id, subGraph);");
        }
#endif


        // Add nodes and sub graphs. Each node that has children becomes a subgraph.
        foreach (var node in codeGraph.Nodes.Values)
        {
            if (subGraphs.TryGetValue(node.Id, out var subGraph))
            {
                // Container nodes
                if (node.Parent == null)
                {
#if GENERATE_DEBUG_OUTPUT
                    Debug.WriteLine($"graph.RootSubgraph.AddSubgraph(dict[\"{subGraph.LabelText}\"]);");
#endif
                    graph.RootSubgraph.AddSubgraph(subGraph);
                }
                else
                {
#if GENERATE_DEBUG_OUTPUT
                    Debug.WriteLine($"parentSubGraph = dict[\"{node.Parent.Name}\"]");
                    Debug.WriteLine($"parentSubGraph.AddSubgraph(dict[\"{subGraph.LabelText}\"]);");
#endif
                    var parentSubGraph = subGraphs[node.Parent.Id];
                    parentSubGraph.AddSubgraph(subGraph);
                }
            }
            else
            {
                // Non container nodes
                var newNode = CreateNode(graph, node);
                if (node.Parent != null)
                {
#if GENERATE_DEBUG_OUTPUT
                    Debug.WriteLine($"newNode = graph.AddNode(\"{newNode.LabelText}\");");
                    Debug.WriteLine($"parentSubGraph = dict[\"{node.Parent.Name}\"];");
                    Debug.WriteLine("parentSubGraph.AddNode(newNode);");
#endif
                    var parentSubGraph = subGraphs[node.Parent.Id];
                    parentSubGraph.AddNode(newNode);
                }
            }
        }

        // Add edges
        codeGraph.DfsHierarchy(AddDependenciesFunc);

        _edges.Clear();
        return graph;


        void AddDependenciesFunc(CodeElement element)
        {
            foreach (var dependency in element.Dependencies)
            {
                CreateEdgeForHierarchicalStructure(graph, dependency);
            }
        }
    }

    private Graph CreateGraphFromCodeStructureFlat(CodeGraph codeGraph)
    {
        // Since we start with a fresh graph we don't need to check for existing nodes and edges.

        var graph = new Graph("graph");

        // Add nodes
        foreach (var codeElement in codeGraph.Nodes.Values)
        {
            CreateNode(graph, codeElement);
        }

        // Add edges and hierarchy
        codeGraph.DfsHierarchy(AddDependenciesFunc);

        return graph;


        void AddDependenciesFunc(CodeElement element)
        {
            foreach (var dependency in element.Dependencies)
            {
                CreateEdgeForFlatStructure(graph, dependency);
            }

            if (element.Parent != null)
            {
                CreateContainmentEdge(graph, new Dependency(element.Parent.Id, element.Id, DependencyType.Containment));
            }
        }
    }

    private void CreateEdgeForHierarchicalStructure(Graph graph, Dependency dependency)
    {
        // MSAGL does not allow two same edges with different labels to the same subgraph.
        // So I collapse them to a single one that carries all the user data and merge the labels.

        var key = (dependency.SourceId, dependency.TargetId);
        if (_edges.TryGetValue(key, out var existingEdge))
        {
            var userData = (List<Dependency>)existingEdge.UserData;
            userData.Add(dependency);

            existingEdge.LabelText = "*";

            // No unique styling possible when we collapse multiple edges
            // Mark the multi edges with a bold line
            existingEdge.Attr.AddStyle(Style.Bold);

        }
        else
        {
            var edge = graph.AddEdge(dependency.SourceId, dependency.TargetId);

#if GENERATE_DEBUG_OUTPUT
            var sourceName = codeGraph.Nodes[dependency.SourceId].Name;
            var targetName = codeGraph.Nodes[dependency.TargetId].Name;
            Debug.WriteLine($"graph.AddEdge(\"{sourceName}\", \"{targetName}\");");
#endif
            edge.LabelText = GetLabelText(dependency);
            if (dependency.Type == DependencyType.Implements)
            {
                edge.Attr.AddStyle(Style.Dotted);
            }

            edge.Attr.AddStyle(Style.Rounded);
            edge.UserData = new List<Dependency> { dependency };
            _edges.Add(key, edge);
        }
    }

    private static void CreateEdgeForFlatStructure(Graph graph, Dependency dependency)
    {
        // MSAGL does not allow two same edges with different labels to the same subgraph.

        var edge = graph.AddEdge(dependency.SourceId, dependency.TargetId);

        edge.LabelText = GetLabelText(dependency);

        if (dependency.Type == DependencyType.Implements)
        {
            edge.Attr.AddStyle(Style.Dotted);
        }

        edge.UserData = dependency;
    }

    private static string GetLabelText(Dependency dependency)
    {
        // Omit the label text for now. The color makes it clear that it is a call dependency
        if (dependency.Type == DependencyType.Calls)
        {
            return string.Empty;
        }

        // We can see this by the dotted line
        if (dependency.Type == DependencyType.Implements || dependency.Type == DependencyType.Inherits)
        {
            return string.Empty;
        }

        if (dependency.Type == DependencyType.Uses)
        {
            return string.Empty;
        }

        return dependency.Type.ToString();
    }

    private static void CreateContainmentEdge(Graph graph, Dependency dependency)
    {
        var edge = graph.AddEdge(dependency.SourceId, dependency.TargetId);
        edge.LabelText = "";
        edge.Attr.Color = Color.LightGray;
        edge.UserData = dependency;
    }

    private static Node CreateNode(Graph graph, CodeElement codeElement)
    {
        var node = graph.AddNode(codeElement.Id);
        node.Attr.FillColor = GetColor(codeElement);
        node.LabelText = codeElement.Name;
        node.UserData = codeElement;

        return node;
    }

    private static Color GetColor(CodeElement codeElement)
    {
        // Commonly used schema by IDE's
        var rgb = ColorDefinitions.GetRbgOf(codeElement.ElementType);
        return ToColor(rgb);
    }

    public static Color ToColor(int colorValue)
    {
        // Extract RGB components
        var r = (colorValue >> 16) & 0xFF;
        var g = (colorValue >> 8) & 0xFF;
        var b = colorValue & 0xFF;

        // Create and return the Color object
        return new Color((byte)r, (byte)g, (byte)b);
    }
}