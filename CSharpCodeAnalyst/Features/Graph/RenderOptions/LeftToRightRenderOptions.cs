using CSharpCodeAnalyst.Resources;
using Microsoft.Msagl.Drawing;

namespace CSharpCodeAnalyst.Areas.GraphArea.RenderOptions;

public class LeftToRightRenderOptions : RenderOption
{
    public LeftToRightRenderOptions()
    {
        Name = Strings.Left_To_Right_Label;
    }

    public override void Apply(Graph graph)
    {
        // Do nothing
        graph.Attr.LayerDirection = LayerDirection.LR;
    }
}