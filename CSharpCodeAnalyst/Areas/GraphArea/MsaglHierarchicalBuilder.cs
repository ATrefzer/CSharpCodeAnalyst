using Contracts.Graph;
using CSharpCodeAnalyst.Areas.GraphArea.Filtering;
using Microsoft.Msagl.Drawing;

namespace CSharpCodeAnalyst.Areas.GraphArea;

/// <summary>
///     Transformation of a CodGraph to Msagl graph structure.
///     Don't confuse the presentation state (expanded / collapsed) with the hide filter.
///     The hide filter removes code elements and relationship types completely from the graph.
/// </summary>
internal class MsaglHierarchicalBuilder : MsaglBuilderBase
{
    public override Graph CreateGraph(CodeGraph codeGraph, PresentationState presentationState,
        bool showInformationFlow, GraphHideFilter hideFilter)
    {
        return CreateHierarchicalGraph(codeGraph, presentationState, showInformationFlow, hideFilter);
    }

    private Graph CreateHierarchicalGraph(CodeGraph codeGraph, PresentationState presentationState, bool showInformationFlow, GraphHideFilter hideFilter)
    {
        var visibleGraph = GetVisibleGraph(codeGraph, presentationState, hideFilter);
        var graph = new Graph("graph");
        var subGraphs = CreateSubGraphs(codeGraph, visibleGraph, presentationState);

        AddNodesToHierarchicalGraph(graph, visibleGraph, codeGraph, subGraphs, presentationState);
        AddEdgesToHierarchicalGraph(graph, codeGraph, visibleGraph, showInformationFlow, presentationState, hideFilter);

        return graph;
    }

    private static CodeGraph GetVisibleGraph(CodeGraph codeGraph, PresentationState state, GraphHideFilter hideFilter)
    {
        var visibleGraph = new CodeGraph();
        var roots = codeGraph.Nodes.Values.Where(n => n.Parent is null);
        foreach (var root in roots)
        {
            CollectVisibleNodes(root, state, visibleGraph, hideFilter);
        }

        // Graph has no relationships yet.
        return visibleGraph;
    }

    private static void CollectVisibleNodes(CodeElement root, PresentationState state, CodeGraph visibleGraph, GraphHideFilter hideFilter)
    {
        // Skip hidden elements
        if (hideFilter.ShouldHideElement(root))
        {
            return;
        }

        visibleGraph.IntegrateCodeElementFromOriginal(root);

        if (state.IsCollapsed(root.Id))
        {
            // Children are not visible
            return;
        }

        foreach (var child in root.Children)
        {
            CollectVisibleNodes(child, state, visibleGraph, hideFilter);
        }
    }

    private static void AddNodesToHierarchicalGraph(Graph graph, CodeGraph visibleGraph, CodeGraph codeGraph,
        Dictionary<string, Subgraph> subGraphs, PresentationState presentationState)
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
                AddNodeToParent(graph, codeGraph.Nodes[visibleNode.Id], subGraphs, presentationState);
            }
        }
    }

    private static void AddSubgraphToParent(Graph graph, CodeElement visibleNode, Subgraph subGraph,
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

    private static void AddNodeToParent(Graph graph, CodeElement node, Dictionary<string, Subgraph> subGraphs, PresentationState presentationState)
    {
        var newNode = CreateNode(graph, node, presentationState);
        if (node.Parent != null)
        {
            subGraphs[node.Parent.Id].AddNode(newNode);
        }
    }

    private void AddEdgesToHierarchicalGraph(Graph graph, CodeGraph codeGraph, CodeGraph visibleGraph, bool showInformationFlow, PresentationState state, GraphHideFilter hideFilter)
    {
        var relationships = GetCollapsedRelationships(codeGraph, visibleGraph, showInformationFlow, hideFilter);
        foreach (var relationship in relationships)
        {
            CreateEdgeForHierarchicalStructure(graph, relationship, state);
        }
    }

    private static Dictionary<(string, string), List<Relationship>> GetCollapsedRelationships(CodeGraph codeGraph,
        CodeGraph visibleGraph, bool showInformationFlow, GraphHideFilter hideFilter)
    {
        var allRelationships = codeGraph.GetAllRelationships();
        var relationships = new Dictionary<(string, string), List<Relationship>>();

        foreach (var relationship in allRelationships)
        {
            // Skip hidden relationships
            if (hideFilter.ShouldHideRelationship(relationship))
            {
                continue;
            }

            // Skip relationships where source or target are hidden
            // (they won't have a visible parent in the visibleGraph)
            var sourceElement = codeGraph.Nodes[relationship.SourceId];
            var targetElement = codeGraph.Nodes[relationship.TargetId];

            if (!HasVisibleParentOrSelf(sourceElement, visibleGraph) ||
                !HasVisibleParentOrSelf(targetElement, visibleGraph))
            {
                continue;
            }

            // Move edges to expanded nodes.
            var sourceId = GetHighestVisibleParentOrSelf(relationship.SourceId, codeGraph, visibleGraph);
            var targetId = GetHighestVisibleParentOrSelf(relationship.TargetId, codeGraph, visibleGraph);


            // Reverse edges like "overrides" to better visualize information flow
            // instead of dependencies.
            if (showInformationFlow &&
                ShouldReverseInFlowMode(visibleGraph, sourceId, relationship.Type))
            {
                (targetId, sourceId) = (sourceId, targetId);
            }

            // Skip self-references at collapsed level for relationships inside.
            if (sourceId == targetId && relationship.SourceId != sourceId && relationship.TargetId != targetId)
            {
                continue;
            }

            if (!relationships.TryGetValue((sourceId, targetId), out var list))
            {
                list = [];
                relationships[(sourceId, targetId)] = list;
            }

            list.Add(relationship);
        }

        return relationships;
    }

    /// <summary>
    ///     Pre-creates all sub-graphs
    /// </summary>
    private static Dictionary<string, Subgraph> CreateSubGraphs(CodeGraph codeGraph, CodeGraph visibleGraph, PresentationState state)
    {
        return visibleGraph.Nodes.Values
            .Where(n => visibleGraph.Nodes[n.Id].Children.Any())
            .ToDictionary(n => n.Id, n => new Subgraph(n.Id)
            {
                LabelText = n.Name,
                UserData = codeGraph.Nodes[n.Id],
                Attr = CreateNodeAttr(n, state)
            });
    }

    private static string GetHighestVisibleParentOrSelf(string id, CodeGraph codeGraph, CodeGraph visibleGraph)
    {
        // Assume the parent is always visible!
        var current = codeGraph.Nodes[id];
        while (current != null && !visibleGraph.Nodes.Keys.Contains(current.Id))
        {
            current = current.Parent;
        }

        if (current is null)
        {
            throw new NullReferenceException("No visible parent found!");
        }

        return current.Id;
    }

    private static bool HasVisibleParentOrSelf(CodeElement element, CodeGraph visibleGraph)
    {
        var current = element;
        while (current != null)
        {
            if (visibleGraph.Nodes.ContainsKey(current.Id))
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }

    private static void CreateEdgeForHierarchicalStructure(Graph graph,
        KeyValuePair<(string source, string target), List<Relationship>> mappedRelationships, PresentationState state)
    {
        // MSAGL does not allow two same edges with different labels to the same subgraph.
        // So I collapse them to a single one that carries all the user data.

        string sourceId;
        string targetId;
        Edge edge;

        var relationships = mappedRelationships.Value;
        if (mappedRelationships.Value.Count == 1 && mappedRelationships.Key.source == relationships[0].SourceId &&
            mappedRelationships.Key.target == relationships[0].TargetId)
        {
            // Single, unmapped relationship
            var relationship = relationships[0];

            sourceId = relationship.SourceId;
            targetId = relationship.TargetId;
            edge = graph.AddEdge(sourceId, targetId);

            edge.LabelText = GetLabelText(relationship);

            edge.Attr = CreateEdgeAttr(sourceId, targetId, relationship.Type, state);

            edge.UserData = new List<Relationship> { relationship };
        }
        else
        {
            sourceId = mappedRelationships.Key.source;
            targetId = mappedRelationships.Key.target;

            // More than one or mapped to collapsed container.
            // Connect the highest visible not collapsed elements (or self)
            edge = graph.AddEdge(sourceId, targetId);

            edge.UserData = relationships;
            edge.LabelText = relationships.Count.ToString();

            edge.Attr = CreateEdgeAttr(sourceId, targetId, RelationshipType.Bundled, state);
        }
    }

    private static bool IsMethod(CodeGraph codeGraph, string id)
    {
        return codeGraph.Nodes[id].ElementType == CodeElementType.Method;
    }
}