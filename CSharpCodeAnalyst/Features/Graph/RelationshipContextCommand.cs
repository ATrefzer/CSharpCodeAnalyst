using System.Windows.Media;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.Features.Graph;

public class RelationshipContextCommand : IRelationshipContextCommand
{
    private readonly Action<string, string, List<Relationship>> _action;
    private readonly Func<List<Relationship>, bool>? _canExecute;
    private readonly Func<List<Relationship>, bool>? _canEnable;
    private readonly RelationshipType? _type;

    public RelationshipContextCommand(string label, RelationshipType type, Action<string, string, List<Relationship>> action, ImageSource? icon = null)
    {
        _type = type;
        _action = action;
        Label = label;
        Icon = icon;
    }

    public RelationshipContextCommand(string subMenuGroup, string label, RelationshipType type, Action<string, string, List<Relationship>> action, ImageSource? icon = null)
    {
        _type = type;
        _action = action;
        SubMenuGroup = subMenuGroup;
        Label = label;
        Icon = icon;
    }

    /// <summary>
    ///     Generic for all code elements
    /// </summary>
    public RelationshipContextCommand(string subMenuGroup, string label, Action<string, string, List<Relationship>> action,
        Func<List<Relationship>, bool>? canExecute = null, ImageSource? icon = null, Func<List<Relationship>, bool>? canEnable = null)
    {
        _type = null;
        _action = action;
        _canExecute = canExecute;
        _canEnable = canEnable;
        SubMenuGroup = subMenuGroup;
        Label = label;
        Icon = icon;
    }

    public string Label { get; }
    public ImageSource? Icon { get; }
    public string? SubMenuGroup { get; }

    public bool CanHandle(List<Relationship> relationships)
    {
        // This is a dummy relationship to visualize hierarchical relationships in flat graph.
        if (relationships.Any(d => d.Type == RelationshipType.Containment))
        {
            return false;
        }

        if (_type != null)
        {
            if (relationships.Any(d => d.Type != _type))
            {
                return false;
            }
        }

        var canHandle = true;
        if (_canExecute != null)
        {
            // Further restrict the handling
            canHandle = _canExecute.Invoke(relationships);
        }

        return canHandle;
    }

    public bool CanExecute(List<Relationship> relationships)
    {
        return _canEnable?.Invoke(relationships) ?? true;
    }


    public void Invoke(string sourceId, string targetId, List<Relationship> relationships)
    {
        _action.Invoke(sourceId, targetId, relationships);
    }
}