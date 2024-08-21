using Contracts.Colors;
using Contracts.Graph;
using Microsoft.Msagl.Drawing;

namespace CSharpCodeAnalyst.GraphArea;

/// <summary>
///     Transformation of a CodGraph to Msagl graph structure.
/// </summary>
internal class MsaglBuilder
{
    public Graph CreateGraphFromCodeStructure(CodeGraph codeGraph, PresentationState presentationState,
        bool showFlatGraph)
    {
        if (showFlatGraph)
        {
            return CreateGraphFromCodeStructureFlat(codeGraph);
        }

        return CreateGraphFromCodeStructureHierarchical(codeGraph, presentationState);
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

    private Graph CreateGraphFromCodeStructureHierarchical(CodeGraph codeGraph, PresentationState presentationState)
    {
        var visibleGraph = GetVisibleGraph(codeGraph, presentationState);
        var graph = new Graph("graph");
        var subGraphs = CreateSubGraphs(codeGraph, visibleGraph);

        AddNodesToHierarchicalGraph(graph, visibleGraph, codeGraph, subGraphs);
        AddEdgesToHierarchicalGraph(graph, codeGraph, visibleGraph);

        return graph;
    }

    private CodeGraph GetVisibleGraph(CodeGraph codeGraph, PresentationState state)
    {
        var visibleGraph = new CodeGraph();
        var roots = codeGraph.Nodes.Values.Where(n => n.Parent is null);
        foreach (var root in roots)
        {
            CollectVisibleNodes(root, state, visibleGraph);
        }

        // Graph has no dependencies yet.
        return visibleGraph;
    }

    private void CollectVisibleNodes(CodeElement root, PresentationState state, CodeGraph visibleGraph)
    {
        visibleGraph.IntegrateCodeElementFromOriginal(root);

        if (state.IsCollapsed(root.Id))
        {
            // Children are not visible
            return;
        }

        foreach (var child in root.Children)
        {
            CollectVisibleNodes(child, state, visibleGraph);
        }
    }


    private void AddNodesToHierarchicalGraph(Graph graph, CodeGraph visibleGraph, CodeGraph codeGraph,
        Dictionary<string, Subgraph> subGraphs)
    {
        // Add nodes and sub graphs. Each node that has children becomes a subgraph.
        foreach (var visibleNode in visibleGraph.Nodes.Values)
        {
            if (subGraphs.TryGetValue(visibleNode.Id, out var subGraph))
            {
                // Container nodes
                AddSubgraphToParent(graph, visibleNode, subGraph, subGraphs);
            }
            else
            {
                // Non container nodes

                // We need to assign the node without visibility restrictions.
                // The collapse/expand context menu handler needs the children.
                AddNodeToParent(graph, codeGraph.Nodes[visibleNode.Id], subGraphs);
            }
        }
    }

    private void AddSubgraphToParent(Graph graph, CodeElement visibleNode, Subgraph subGraph,
        Dictionary<string, Subgraph> subGraphs)
    {
        if (visibleNode.Parent == null)
        {
            graph.RootSubgraph.AddSubgraph(subGraph);
        }
        else
        {
            subGraphs[visibleNode.Parent.Id].AddSubgraph(subGraph);
        }
    }

    private void AddNodeToParent(Graph graph, CodeElement node, Dictionary<string, Subgraph> subGraphs)
    {
        var newNode = CreateNode(graph, node);
        if (node.Parent != null)
        {
            subGraphs[node.Parent.Id].AddNode(newNode);
        }
    }

    private void AddEdgesToHierarchicalGraph(Graph graph, CodeGraph codeGraph, CodeGraph visibleGraph)
    {
        var dependencies = GetCollapsedDependencies(codeGraph, visibleGraph);
        foreach (var dependency in dependencies)
        {
            CreateEdgeForHierarchicalStructure(graph, dependency);
        }
    }

    private Dictionary<(string, string), List<Dependency>> GetCollapsedDependencies(CodeGraph codeGraph,
        CodeGraph visibleGraph)
    {
        var allDependencies = codeGraph.GetAllDependencies();
        var dependencies = new Dictionary<(string, string), List<Dependency>>();

        foreach (var dependency in allDependencies)
        {
            // Move edges to collapsed nodes.
            var source = GetHighestVisibleParentOrSelf(dependency.SourceId, codeGraph, visibleGraph);
            var target = GetHighestVisibleParentOrSelf(dependency.TargetId, codeGraph, visibleGraph);

            if (!dependencies.TryGetValue((source, target), out var list))
            {
                list = new List<Dependency>();
                dependencies[(source, target)] = list;
            }

            list.Add(dependency);
        }

        return dependencies;
    }

    /// <summary>
    ///     Pre-creates all sub-graphs
    /// </summary>
    private Dictionary<string, Subgraph> CreateSubGraphs(CodeGraph codeGraph, CodeGraph visibleGraph)
    {
        return visibleGraph.Nodes.Values
            .Where(n => visibleGraph.Nodes[n.Id].Children.Any())
            .ToDictionary(n => n.Id, n => new Subgraph(n.Id)
            {
                LabelText = n.Name,
                UserData = codeGraph.Nodes[n.Id],
                Attr = { FillColor = GetColor(n) }
            });
    }

    private string GetHighestVisibleParentOrSelf(string id, CodeGraph codeGraph, CodeGraph visibleGraph)
    {
        // Assume the parent is always visible!
        var current = codeGraph.Nodes[id];
        while (current != null && visibleGraph.Nodes.Keys.Contains(current.Id) is false)
        {
            current = current.Parent;
        }

        if (current is null)
        {
            throw new NullReferenceException("No visible parent found!");
        }

        return current.Id;
    }

    private void CreateEdgeForHierarchicalStructure(Graph graph,
        KeyValuePair<(string source, string target), List<Dependency>> mappedDependency)
    {
        // MSAGL does not allow two same edges with different labels to the same subgraph.
        // So I collapse them to a single one that carries all the user data.

        var dependencies = mappedDependency.Value;
        if (mappedDependency.Value.Count == 1 && mappedDependency.Key.source == dependencies[0].SourceId &&
            mappedDependency.Key.target == dependencies[0].TargetId)
        {
            // Single, unmapped dependency
            var dependency = dependencies[0];
            var edge = graph.AddEdge(dependency.SourceId, dependency.TargetId);

            edge.LabelText = GetLabelText(dependency);
            if (dependency.Type == DependencyType.Implements)
            {
                edge.Attr.AddStyle(Style.Dotted);
            }

            edge.UserData = new List<Dependency> { dependency };
        }
        else
        {
            // More than one or mapped to collapsed container.
            var edge = graph.AddEdge(mappedDependency.Key.source, mappedDependency.Key.target);

            edge.UserData = dependencies;
            edge.LabelText = dependencies.Count.ToString();

            // No unique styling possible when we collapse multiple edges
            // Mark the multi edges with a bold line
            edge.Attr.AddStyle(Style.Bold);
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