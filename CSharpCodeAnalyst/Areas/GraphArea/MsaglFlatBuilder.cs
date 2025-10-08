using Contracts.Graph;
using Microsoft.Msagl.Drawing;

namespace CSharpCodeAnalyst.Areas.GraphArea;

internal class MsaglFlatBuilder : MsaglBuilderBase
{
    public override Graph CreateGraph(CodeGraph codeGraph, PresentationState presentationState,
        bool showInformationFlow)
    {
        return CreateFlatGraph(codeGraph, presentationState, showInformationFlow);
    }
    
    private static Graph CreateFlatGraph(CodeGraph codeGraph, PresentationState presentationState, bool showInformationFlow)
    {
        // Since we start with a fresh graph we don't need to check for existing nodes and edges.

        var graph = new Graph("graph");

        // Add nodes
        foreach (var codeElement in codeGraph.Nodes.Values)
        {
            CreateNode(graph, codeElement, presentationState);
        }

        // Add edges and hierarchy
        codeGraph.DfsHierarchy(AddRelationshipsFunc);

        return graph;


        void AddRelationshipsFunc(CodeElement element)
        {
            foreach (var relationship in element.Relationships)
            {
                var sourceId = relationship.SourceId;
                var reverse = showInformationFlow && ShouldReverseInFlowMode(codeGraph, sourceId, relationship.Type);
                CreateEdgeForFlatStructure(graph, relationship, reverse, presentationState);
            }

            if (element.Parent != null)
            {
                CreateContainmentEdge(graph,
                    new Relationship(element.Parent.Id, element.Id, RelationshipType.Containment));
            }
        }
    }

    static void CreateContainmentEdge(Graph graph, Relationship relationship)
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