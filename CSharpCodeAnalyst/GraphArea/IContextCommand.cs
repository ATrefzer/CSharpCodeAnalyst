using Contracts.Graph;

namespace CSharpCodeAnalyst.GraphArea;

public interface IContextCommand
{
    string Label { get; }

    bool CanHandle(CodeElement item);
    void Invoke(CodeElement item);
}