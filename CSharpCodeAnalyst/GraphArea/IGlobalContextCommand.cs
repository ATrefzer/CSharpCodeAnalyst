using Contracts.Graph;

namespace CSharpCodeAnalyst.GraphArea;

public interface IGlobalContextCommand
{
    string Label { get; }

    bool CanHandle(List<CodeElement> elements);
    void Invoke(List<CodeElement> elements);
}