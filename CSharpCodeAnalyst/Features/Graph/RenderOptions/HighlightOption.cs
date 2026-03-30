using CSharpCodeAnalyst.Resources;

namespace CSharpCodeAnalyst.Areas.GraphArea.RenderOptions;

public class HighlightOption(HighlightMode mode, string label)
{
    public static readonly HighlightOption Default = new(HighlightMode.EdgeHovered, Strings.Hovered_Edge_Label);
    public HighlightMode Mode { get; set; } = mode;

    public override string ToString()
    {
        return label;
    }
}