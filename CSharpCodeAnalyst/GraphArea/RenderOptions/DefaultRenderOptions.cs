using Microsoft.Msagl.Drawing;

namespace CSharpCodeAnalyst.GraphArea.RenderOptions;

public class DefaultRenderOptions : RenderOption
{
    public DefaultRenderOptions()
    {
        Name = "Default";
    }

    public override void Apply(Graph graph)
    {
        graph.Attr.LayerDirection = LayerDirection.TB;
    }
}