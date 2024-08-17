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

public class ContextCommand(string label, CodeElementType type, Action<CodeElement> action) : IContextCommand
{
    public string Label { get; } = label;

    public bool CanHandle(object item)
    {
        if (item is CodeElement element)
        {
            return element.ElementType == type;
        }

        return false;
    }

    public void Invoke(object item)
    {
        if (item is CodeElement element)
        {
            action.Invoke(element);
        }
    }
}