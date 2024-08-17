using Microsoft.Msagl.Drawing;

namespace CSharpCodeAnalyst.GraphArea.RenderOptions;

internal class BottomToTopRenderOptions : RenderOption
{
    public BottomToTopRenderOptions()
    {
        Name = "Bottom to Top";
    }

    public override void Apply(Graph graph)
    {
        graph.Attr.LayerDirection = LayerDirection.BT;
    }
}