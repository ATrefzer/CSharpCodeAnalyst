using System.Windows.Media;
using Contracts.Graph;

namespace CSharpCodeAnalyst.GraphArea;

public interface IGlobalContextCommand
{
    string Label { get; }
    ImageSource? Icon { get; }

    bool CanHandle(List<CodeElement> selectedElements);
    void Invoke(List<CodeElement> selectedElements);
}