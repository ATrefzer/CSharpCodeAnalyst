using System.Windows.Input;
using System.Windows.Media;
using CodeGraph.Graph;

namespace CSharpCodeAnalyst.Features.Graph;

public interface IGlobalCommand
{
    string Label { get; }
    ImageSource? Icon { get; }
    Key? Key { get; }

    bool CanHandle(List<CodeElement> selectedElements);
    void Invoke(List<CodeElement> selectedElements);
}