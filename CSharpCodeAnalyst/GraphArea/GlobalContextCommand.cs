using Contracts.Graph;

namespace CSharpCodeAnalyst.GraphArea;

/// <summary>
///     Global command to work on all marked code elements
/// </summary>
public class GlobalContextCommand : IGlobalContextCommand
{
    private readonly Action<List<CodeElement>> _action;
    private readonly Func<List<CodeElement>, bool>? _canExecute;

    public GlobalContextCommand(string label, Action<List<CodeElement>> action)
    {
        _action = action;
        Label = label;
    }

    /// <summary>
    ///     Generic for all code elements
    /// </summary>
    public GlobalContextCommand(string label, Action<List<CodeElement>> action,
        Func<List<CodeElement>, bool>? canExecute = null)
    {
        _action = action;
        _canExecute = canExecute;
        Label = label;
    }

    public string Label { get; }


    public void Invoke(List<CodeElement> markedElements)
    {
        _action.Invoke(markedElements);
    }

    public bool CanHandle(List<CodeElement> markedElements)
    {
        var canHandle = true;

        if (_canExecute != null)
        {
            canHandle = canHandle && _canExecute.Invoke(markedElements);
        }

        return canHandle;
    }
}