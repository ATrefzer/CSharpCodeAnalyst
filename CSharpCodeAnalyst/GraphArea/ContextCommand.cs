using Contracts.Graph;

namespace CSharpCodeAnalyst.GraphArea;

public class SeparatorCommand : IContextCommand
{
    public string Label => throw new NotImplementedException();

    public bool CanHandle(object item)
    {
        return true;
    }

    public void Invoke(object item)
    {
    }
}

public class ContextCommand : IContextCommand
{
    private readonly Action<CodeElement> _action;
    private readonly Func<CodeElement, bool>? _canExecute;
    private readonly CodeElementType? _type;

    public ContextCommand(string label, CodeElementType type, Action<CodeElement> action)
    {
        _type = type;
        _action = action;
        Label = label;
    }

    /// <summary>
    /// Generic for all code elements
    /// </summary>
    public ContextCommand(string label, Action<CodeElement> action, Func<CodeElement, bool>? canExecute = null)
    {
        _type = null;
        _action = action;
        _canExecute = canExecute;
        Label = label;
    }

    public string Label { get; }

    public bool CanHandle(object item)
    {
        var canHandle = false;
        if (item is CodeElement element)
        {
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
        }

        return canHandle;
    }

    public void Invoke(object item)
    {
        if (item is CodeElement element)
        {
            _action.Invoke(element);
        }
    }
}