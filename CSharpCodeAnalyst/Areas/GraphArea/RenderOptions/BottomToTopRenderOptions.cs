using CSharpCodeAnalyst.Resources;
using Microsoft.Msagl.Drawing;

namespace CSharpCodeAnalyst.Areas.GraphArea.RenderOptions;

internal class BottomToTopRenderOptions : RenderOption
{
    public BottomToTopRenderOptions()
    {
        Name = Strings.Bottom_To_Top_Label;
    }

    public override void Apply(Graph graph)
    {
        graph.Attr.LayerDirection = LayerDirection.BT;
    }
}