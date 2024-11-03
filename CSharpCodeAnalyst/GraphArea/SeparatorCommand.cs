using Contracts.Graph;

namespace CSharpCodeAnalyst.GraphArea;

public class SeparatorCommand : ICodeElementContextCommand
{
    public string Label
    {
        get => string.Empty;
    }

    public bool CanHandle(CodeElement item)
    {
        return true;
    }

    public void Invoke(CodeElement item)
    {
    }
}