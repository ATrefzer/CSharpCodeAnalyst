using System.Windows.Media;
using Contracts.Graph;

namespace CSharpCodeAnalyst.GraphArea;

public interface ICodeElementContextCommand
{
    string Label { get; }
    ImageSource? Icon { get; }

    bool CanHandle(CodeElement item);
    void Invoke(CodeElement item);
}