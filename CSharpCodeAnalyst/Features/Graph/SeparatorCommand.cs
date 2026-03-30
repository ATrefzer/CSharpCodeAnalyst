using System.Windows.Media;
using CodeGraph.Graph;

namespace CSharpCodeAnalyst.Features.Graph;

public class SeparatorCommand : ICodeElementContextCommand
{
    public bool IsVisible { get; set; } = true;

    public string Label
    {
        get => string.Empty;
    }

    public ImageSource? Icon
    {
        get => null;
    }

    public bool IsDoubleClickable { get; set; }

    public bool CanHandle(CodeElement item)
    {
        return true;
    }

    public void Invoke(CodeElement item)
    {
    }
}