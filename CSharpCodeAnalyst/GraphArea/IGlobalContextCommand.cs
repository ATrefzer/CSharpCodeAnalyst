using Contracts.Graph;

namespace CSharpCodeAnalyst.GraphArea;

public interface IGlobalContextCommand
{
    string Label { get; }

    bool CanHandle(List<CodeElement> selectedElements);
    void Invoke(List<CodeElement> selectedElements);
}