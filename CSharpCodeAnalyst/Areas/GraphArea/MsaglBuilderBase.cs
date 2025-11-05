using CodeGraph.Colors;
using CodeGraph.Graph;
using CSharpCodeAnalyst.Areas.GraphArea.Filtering;
using Microsoft.Msagl.Drawing;

namespace CSharpCodeAnalyst.Areas.GraphArea;

internal abstract class MsaglBuilderBase
{

    private static readonly Dictionary<RelationshipType, bool> FlowReversalMap = new()
    {
        // Reverse these for information flow
        { RelationshipType.Handles, true }, // Show flow: Event -> Handler
        { RelationshipType.Implements, true }, // Show flow: Interface -> Implementation  
        { RelationshipType.Overrides, true }, // Show flow: Base -> Override

        // Keep normal direction (already show flow correctly)
        { RelationshipType.Calls, false }, // Caller -> Callee (control flow)
        { RelationshipType.Invokes, false }, // Invoker -> Event (event flow)
        { RelationshipType.Creates, false }, // Creator -> Created (instantiation flow)
        { RelationshipType.Uses, false }, // User -> Used (data flow)
        { RelationshipType.Inherits, false }, // Child -> Parent (inheritance flow)
        { RelationshipType.UsesAttribute, false } // Decorated -> Attribute (metadata flow)
    };

    public abstract Graph CreateGraph(CodeGraph.Graph.CodeGraph codeGraph, PresentationState presentationState,
        bool showInformationFlow, GraphHideFilter hideFilter);

    protected static NodeAttr CreateNodeAttr(CodeElement element, PresentationState presentationState)
    {
        var attr = new NodeAttr
        {
            Id = element.Id,
            FillColor = GetColor(element)
        };

        if (presentationState.IsFlagged(element.Id))
        {
            attr.LineWidth = Constants.FlagLineWidth;
            attr.Color = Constants.FlagColor;
        }
        else if (presentationState.IsSearchHighlighted(element.Id))
        {
            attr.LineWidth = Constants.SearchHighlightLineWidth;
            attr.Color = Constants.SearchHighlightColor;
        }

        return attr;
    }

    protected static EdgeAttr CreateEdgeAttr(string sourceId, string targetId, RelationshipType type, PresentationState presentationState)
    {
        var attr = new EdgeAttr();


        if (type == RelationshipType.Bundled)
        {
            // No unique styling possible when we collapse multiple edges
            // Mark the multi edges with a bold line
            attr.AddStyle(Style.Bold);
        }
        else if (type == RelationshipType.Implements)
        {
            attr.AddStyle(Style.Dotted);
        }

        var key = (sourceId, targetId);
        if (presentationState.IsFlagged(key))
        {
            attr.LineWidth = Constants.FlagLineWidth;
            attr.Color = Constants.FlagColor;
        }


        return attr;
    }

    private static Color GetColor(CodeElement codeElement)
    {
        // External code elements are always gray, regardless of type
        if (codeElement.IsExternal)
        {
            return ToColor(0x808080); // Gray
        }

        // Commonly used schema by IDE's for internal elements
        var rgb = ColorDefinitions.GetRbgOf(codeElement.ElementType);
        return ToColor(rgb);
    }

    private static Color ToColor(int colorValue)
    {
        // Extract RGB components
        var r = colorValue >> 16 & 0xFF;
        var g = colorValue >> 8 & 0xFF;
        var b = colorValue & 0xFF;

        // Create and return the Color object
        return new Color((byte)r, (byte)g, (byte)b);
    }

    protected virtual Node CreateNode(Graph graph, CodeElement codeElement, PresentationState presentationState)
    {
        var node = graph.AddNode(codeElement.Id);
        node.LabelText = codeElement.Name;
        node.UserData = codeElement;
        node.Attr = CreateNodeAttr(codeElement, presentationState);

        return node;
    }


    protected static bool ShouldReverseInFlowMode(CodeGraph.Graph.CodeGraph graph, string sourceId, RelationshipType relationshipType)
    {
        if (relationshipType == RelationshipType.Implements)
        {
            if (graph.Nodes[sourceId].ElementType == CodeElementType.Event)
            {
                return false;
            }
        }

        return FlowReversalMap.GetValueOrDefault(relationshipType, false);
    }

    protected static string GetLabelText(Relationship relationship)
    {
        // Omit the label text for now. The color makes it clear that it is a call relationship
        if (relationship.Type is RelationshipType.Calls or RelationshipType.Invokes)
        {
            return string.Empty;
        }

        // We can see this by the dotted line
        if (relationship.Type is RelationshipType.Implements or RelationshipType.Inherits)
        {
            return string.Empty;
        }

        if (relationship.Type == RelationshipType.Uses)
        {
            return string.Empty;
        }

        if (relationship.Type == RelationshipType.UsesAttribute)
        {
            return string.Empty;
        }

        return relationship.Type.ToString();
    }
}