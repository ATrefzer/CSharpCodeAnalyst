using Microsoft.Msagl.Drawing;

namespace CSharpCodeAnalyst.Areas.GraphArea.RenderOptions;

public class LeftToRightRenderOptions : RenderOption
{
    public LeftToRightRenderOptions()
    {
        Name = "Left to Right";
    }

    public override void Apply(Graph graph)
    {
        // Do nothing
        graph.Attr.LayerDirection = LayerDirection.LR;
    }
}