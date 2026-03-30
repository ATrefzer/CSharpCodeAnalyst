using CSharpCodeAnalyst.Resources;
using Microsoft.Msagl.Drawing;

namespace CSharpCodeAnalyst.Features.Graph.RenderOptions;

internal class BottomToTopRenderOptions : RenderOption
{
    public BottomToTopRenderOptions()
    {
        Name = Strings.Bottom_To_Top_Label;
    }

    public override void Apply(Microsoft.Msagl.Drawing.Graph graph)
    {
        graph.Attr.LayerDirection = LayerDirection.BT;
    }
}