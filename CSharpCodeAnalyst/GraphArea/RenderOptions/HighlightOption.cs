namespace CSharpCodeAnalyst.GraphArea.RenderOptions;

public class HighlightOption(HighlightMode mode, string label)
{
    public HighlightMode Mode { get; set; } = mode;

    public override string ToString()
    {
        return label;
    }

    public static HighlightOption Default = new(HighlightMode.EdgeHovered, "Hovered edge");
}