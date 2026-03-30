using CSharpCodeAnalyst.Resources;
using Microsoft.Msagl.Drawing;

namespace CSharpCodeAnalyst.Features.Graph.RenderOptions;

public class DefaultRenderOptions : RenderOption
{
    public DefaultRenderOptions()
    {
        Name = Strings.Default_Label;
    }

    public override void Apply(Microsoft.Msagl.Drawing.Graph graph)
    {
        graph.Attr.LayerDirection = LayerDirection.TB;
    }
}