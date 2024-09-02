using Contracts.Graph;

namespace CSharpCodeAnalyst.GraphArea;

public interface IGlobalContextCommand
{
    string Label { get; }

    bool CanHandle(List<CodeElement> markedElements);
    void Invoke(List<CodeElement> markedElements);
}