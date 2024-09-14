using Contracts.Graph;

namespace CSharpCodeAnalyst.GraphArea;

public class SeparatorCommand : ICodeElementContextCommand
{
    public string Label => throw new NotImplementedException();

    public bool CanHandle(CodeElement item)
    {
        return true;
    }

    public void Invoke(CodeElement item)
    {
    }
}