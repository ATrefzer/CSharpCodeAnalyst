using Contracts.Graph;
using CSharpCodeAnalyst.Areas.GraphArea.Filtering;
using Microsoft.Msagl.Drawing;

namespace CSharpCodeAnalyst.Areas.GraphArea;

internal class MsaglFlatBuilder : MsaglBuilderBase
{
    public override Graph CreateGraph(CodeGraph codeGraph, PresentationState presentationState,
        bool showInformationFlow, GraphHideFilter hideFilter)
    {
        return CreateFlatGraph(codeGraph, presentationState, showInformationFlow, hideFilter);
    }

    private static Graph CreateFlatGraph(CodeGraph codeGraph, PresentationState presentationState, bool showInformationFlow, GraphHideFilter hideFilter)
    {
        // Since we start with a fresh graph we don't need to check for existing nodes and edges.

        var graph = new Graph("graph");

        // Add nodes (excluding hidden ones)
        foreach (var codeElement in codeGraph.Nodes.Values)
        {
            if (!hideFilter.ShouldHideElement(codeElement))
            {
                CreateNode(graph, codeElement, presentationState);
            }
        }

        // Add edges and hierarchy
        codeGraph.DfsHierarchy(AddRelationshipsFunc);

        return graph;


        void AddRelationshipsFunc(CodeElement element)
        {
            // Skip relationships from/to hidden elements
            if (hideFilter.ShouldHideElement(element))
            {
                return;
            }

            foreach (var relationship in element.Relationships)
            {
                // Skip hidden relationships
                if (hideFilter.ShouldHideRelationship(relationship))
                {
                    continue;
                }

                // Skip if target is hidden
                var targetElement = codeGraph.Nodes[relationship.TargetId];
                if (hideFilter.ShouldHideElement(targetElement))
                {
                    continue;
                }

                var sourceId = relationship.SourceId;
                var reverse = showInformationFlow && ShouldReverseInFlowMode(codeGraph, sourceId, relationship.Type);
                CreateEdgeForFlatStructure(graph, relationship, reverse, presentationState);
            }

            if (element.Parent != null && !hideFilter.ShouldHideElement(element.Parent))
            {
                CreateContainmentEdge(graph,
                    new Relationship(element.Parent.Id, element.Id, RelationshipType.Containment));
            }
        }
    }

    private static void CreateContainmentEdge(Graph graph, Relationship relationship)
    {
        var edge = graph.AddEdge(relationship.SourceId, relationship.TargetId);
        edge.LabelText = "";

        edge.Attr.Color = Color.LightGray;
        edge.UserData = relationship;
    }

    private static void CreateEdgeForFlatStructure(Graph graph, Relationship relationship, bool reverseEdge, PresentationState state)
    {
        // MSAGL does not allow two same edges with different labels to the same subgraph.

        var sourceId = relationship.SourceId;
        var targetId = relationship.TargetId;
        if (reverseEdge)
        {
            (targetId, sourceId) = (sourceId, targetId);
        }

        var edge = graph.AddEdge(sourceId, targetId);

        edge.LabelText = GetLabelText(relationship);
        edge.Attr = CreateEdgeAttr(sourceId, targetId, relationship.Type, state);

        edge.UserData = relationship;
    }
}