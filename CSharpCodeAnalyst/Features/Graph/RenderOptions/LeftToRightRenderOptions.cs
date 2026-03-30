using CSharpCodeAnalyst.Resources;
using Microsoft.Msagl.Drawing;

namespace CSharpCodeAnalyst.Features.Graph.RenderOptions;

public class LeftToRightRenderOptions : RenderOption
{
    public LeftToRightRenderOptions()
    {
        Name = Strings.Left_To_Right_Label;
    }

    public override void Apply(Microsoft.Msagl.Drawing.Graph graph)
    {
        // Do nothing
        graph.Attr.LayerDirection = LayerDirection.LR;
    }
}