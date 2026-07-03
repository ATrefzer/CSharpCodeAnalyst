using System.Windows.Media;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.Features.Graph;

public interface ICodeElementContextCommand
{
    bool IsVisible { get; set; }
    string Label { get; }
    ImageSource? Icon { get; }
    bool IsDoubleClickable { get; set; }
    bool CanHandle(CodeElement item);

    /// <summary>
    ///     Whether the (visible) command is enabled for this element. Lets a command always
    ///     appear in the menu but render grayed-out when it does not apply (default: enabled).
    /// </summary>
    bool CanExecute(CodeElement item) => true;

    void Invoke(CodeElement item);
}