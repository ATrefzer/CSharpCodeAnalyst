using Contracts.Graph;

namespace CSharpCodeAnalyst.GraphArea;

public class DependencyContextCommand : IDependencyContextCommand
{
    private readonly Action<List<Dependency>> _action;
    private readonly Func<List<Dependency>, bool>? _canExecute;
    private readonly DependencyType? _type;

    public DependencyContextCommand(string label, DependencyType type, Action<List<Dependency>> action)
    {
        _type = type;
        _action = action;
        Label = label;
    }

    /// <summary>
    ///     Generic for all code elements
    /// </summary>
    public DependencyContextCommand(string label, Action<List<Dependency>> action,
        Func<List<Dependency>, bool>? canExecute = null)
    {
        _type = null;
        _action = action;
        _canExecute = canExecute;
        Label = label;
    }

    public string Label { get; }

    public bool CanHandle(List<Dependency> dependencies)
    {
        // This is a dummy dependency to visualize hierarchical relationships in flat graph.
        if (dependencies.Any(d => d.Type == DependencyType.Containment))
        {
            return false;
        }

        if (_type != null)
        {
            if (dependencies.All(d => d.Type == _type) is false)
            {
                return false;
            }
        }

        var canHandle = true;
        if (_canExecute != null)
        {
            // Further restrict the handling
            canHandle = _canExecute.Invoke(dependencies);
        }

        return canHandle;
    }


    public void Invoke(List<Dependency> dependencies)
    {
        _action.Invoke(dependencies);
    }
}