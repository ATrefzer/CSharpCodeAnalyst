namespace CSharpCodeAnalyst.Areas.GraphArea.RenderOptions;

public class HighlightOption(HighlightMode mode, string label)
{
    public static HighlightOption Default = new(HighlightMode.EdgeHovered, "Hovered edge");
    public HighlightMode Mode { get; set; } = mode;

    public override string ToString()
    {
        return label;
    }
}