using System.Windows.Media;
using Contracts.Graph;

namespace CSharpCodeAnalyst.Areas.GraphArea;

public interface ICodeElementContextCommand
{
    bool IsVisible { get; set; }
    string Label { get; }
    ImageSource? Icon { get; }
    bool IsDoubleClickable { get; set; }
    bool CanHandle(CodeElement item);
    void Invoke(CodeElement item);
}