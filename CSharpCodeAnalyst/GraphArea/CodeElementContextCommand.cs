using Contracts.Graph;

namespace CSharpCodeAnalyst.GraphArea;

public class CodeElementContextCommand : ICodeElementContextCommand
{
    private readonly Action<CodeElement> _action;
    private readonly Func<CodeElement, bool>? _canExecute;
    private readonly CodeElementType? _type;

    public CodeElementContextCommand(string label, CodeElementType type, Action<CodeElement> action)
    {
        _type = type;
        _action = action;
        Label = label;
    }

    /// <summary>
    ///     Generic for all code elements
    /// </summary>
    public CodeElementContextCommand(string label, Action<CodeElement> action, Func<CodeElement, bool>? canExecute = null)
    {
        _type = null;
        _action = action;
        _canExecute = canExecute;
        Label = label;
    }

    public string Label { get; }

    public bool CanHandle(CodeElement element)
    {
        var canHandle = false;
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

    public void Invoke(CodeElement element)
    {
        _action.Invoke(element);
    }
}