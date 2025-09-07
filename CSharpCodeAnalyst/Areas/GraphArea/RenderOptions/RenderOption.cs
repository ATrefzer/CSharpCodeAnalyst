using Microsoft.Msagl.Drawing;

namespace CSharpCodeAnalyst.GraphArea.RenderOptions;

public abstract class RenderOption
{
    public string Name { get; set; } = string.Empty;

    public override string ToString()
    {
        return Name;
    }

    public abstract void Apply(Graph graph);
}