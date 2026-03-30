namespace CSharpCodeAnalyst.Features.Graph.RenderOptions;

public abstract class RenderOption
{
    public string Name { get; set; } = string.Empty;

    public override string ToString()
    {
        return Name;
    }

    public abstract void Apply(Microsoft.Msagl.Drawing.Graph graph);
}