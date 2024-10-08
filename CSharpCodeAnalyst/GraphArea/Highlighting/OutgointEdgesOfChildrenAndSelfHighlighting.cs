﻿using Contracts.Graph;
using Microsoft.Msagl.Drawing;

namespace CSharpCodeAnalyst.GraphArea.Highlighting;

internal class OutgointEdgesOfChildrenAndSelfHighlighting : HighlightingBase
{
    public override void Clear(Microsoft.Msagl.WpfGraphControl.GraphViewer? graphViewer)
    {
        ClearAllEdges(graphViewer);
    }

    public override void Highlight(Microsoft.Msagl.WpfGraphControl.GraphViewer? graphViewer,
        IViewerObject? viewerObject, CodeGraph? codeGraph)
    {
        if (graphViewer is null || codeGraph is null)
        {
            return;
        }

        if (viewerObject is not IViewerNode node)
        {
            ClearAllEdges(graphViewer);
            return;
        }

        var ids = new HashSet<string>();
        if (node != null)
        {
            var id = node.Node.Id;
            var vertex = codeGraph.Nodes[id];
            ids = vertex.GetChildrenIncludingSelf();
        }

        var edges = graphViewer.Entities.OfType<IViewerEdge>();
        foreach (var edge in edges)
        {
            var sourceId = edge.Edge.Source;
            if (ids.Contains(sourceId))
            {
                Highlight(edge);
            }
            else
            {
                ClearHighlight(edge);
            }
        }
    }
}