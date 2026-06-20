using CSharpCodeAnalyst.Resources;

namespace CSharpCodeAnalyst.Features.Graph.RenderOptions;

/// <summary>
///     A selectable graph layout. <see cref="Name" /> is the key sent to the web view
///     (app.js LAYOUTS map); the renderer (Cytoscape) stays the same, only the layout
///     extension that computes node positions changes. Mirrors <see cref="HighlightOption" />.
/// </summary>
public class LayoutOption(string name, string label)
{
    public static readonly LayoutOption Default = new("fcose", Strings.Layout_Force_Label);

    /// <summary>The Cytoscape layout key pushed to JS (e.g. "fcose", "dagre-tb").</summary>
    public string Name { get; } = name;

    public override string ToString()
    {
        return label;
    }
}
