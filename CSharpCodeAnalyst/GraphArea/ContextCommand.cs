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
    public ContextCommand(string label, Action<CodeElement> action)
    {
        _type = null;
        _action = action;
        Label = label;
    }

    public string Label { get; }

    public bool CanHandle(object item)
    {
        if (item is CodeElement element)
        {
            if (_type == null)
            {
                // Handling all elements
                return true;
            }

            return element.ElementType == _type;
        }

        return false;
    }

    public void Invoke(object item)
    {
        if (item is CodeElement element)
        {
            _action.Invoke(element);
        }
    }
}