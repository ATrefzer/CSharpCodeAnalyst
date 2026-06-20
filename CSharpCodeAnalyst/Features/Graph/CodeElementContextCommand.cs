using System.Windows.Media;
using CodeGraph.Graph;

namespace CSharpCodeAnalyst.Features.Graph;

public class CodeElementContextCommand : ICodeElementContextCommand
{
    private readonly Action<CodeElement> _action;
    private readonly Func<CodeElement, bool>? _canExecute;
    private readonly Func<CodeElement, bool>? _canEnable;
    private readonly CodeElementType? _type;

    public CodeElementContextCommand(string label, CodeElementType type, Action<CodeElement> action, ImageSource? icon = null)
    {
        _type = type;
        _action = action;
        Label = label;
        Icon = icon;
    }

    /// <summary>
    ///     Generic for all code elements.
    ///     <paramref name="canExecute" /> restricts visibility (hidden when it returns false);
    ///     <paramref name="canEnable" /> keeps the command visible but grays it out when false.
    /// </summary>
    public CodeElementContextCommand(string label, Action<CodeElement> action,
        Func<CodeElement, bool>? canExecute = null, ImageSource? icon = null, Func<CodeElement, bool>? canEnable = null)
    {
        _type = null;
        _action = action;
        _canExecute = canExecute;
        _canEnable = canEnable;
        Label = label;
        Icon = icon;
    }

    public bool IsVisible { get; set; } = true;
    public string Label { get; }
    public ImageSource? Icon { get; }
    public bool IsDoubleClickable { get; set; }

    public bool CanHandle(CodeElement element)
    {
        bool canHandle;
        if (_type == null)
        {
            // Handling all elements
            canHandle = true;
        }
        else
        {
            canHandle = element.ElementType == _type;
        }

        if (_canExecute != null)
        {
            // Further restrict the handling
            canHandle = canHandle && _canExecute.Invoke(element);
        }

        return canHandle;
    }

    public bool CanExecute(CodeElement element)
    {
        return _canEnable?.Invoke(element) ?? true;
    }

    public void Invoke(CodeElement element)
    {
        _action.Invoke(element);
    }
}