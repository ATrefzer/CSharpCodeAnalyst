using System.Windows.Input;
using System.Windows.Media;
using CodeGraph.Graph;

namespace CSharpCodeAnalyst.Areas.GraphArea;

/// <summary>
///     Global command to work on all selected code elements
/// </summary>
public class GlobalCommand : IGlobalCommand
{
    private readonly Action<List<CodeElement>> _action;
    private readonly Func<List<CodeElement>, bool>? _canExecute;

    public GlobalCommand(string label, Action<List<CodeElement>> action, ImageSource? icon = null)
    {
        _action = action;
        Label = label;
        Icon = icon;
    }

    /// <summary>
    ///     Generic for all code elements
    /// </summary>
    public GlobalCommand(string label, Action<List<CodeElement>> action,
        Func<List<CodeElement>, bool>? canExecute, ImageSource? icon = null, Key? key = null)
    {
        _action = action;
        _canExecute = canExecute;
        Label = label;
        Icon = icon;
        Key = key;
    }

    public string Label { get; }
    public ImageSource? Icon { get; }
    public Key? Key { get; }


    public void Invoke(List<CodeElement> selectedElements)
    {
        _action.Invoke(selectedElements);
    }

    public bool CanHandle(List<CodeElement> selectedElements)
    {
        var canHandle = true;

        if (_canExecute != null)
        {
            canHandle = canHandle && _canExecute.Invoke(selectedElements);
        }

        return canHandle;
    }
}