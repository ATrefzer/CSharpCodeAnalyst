using System.Windows.Media;
using Contracts.Graph;

namespace CSharpCodeAnalyst.Areas.GraphArea;

public class RelationshipContextCommand : IRelationshipContextCommand
{
    private readonly Action<string, string, List<Relationship>> _action;
    private readonly Func<List<Relationship>, bool>? _canExecute;
    private readonly RelationshipType? _type;

    public RelationshipContextCommand(string label, RelationshipType type, Action<string, string, List<Relationship>> action, ImageSource? icon = null)
    {
        _type = type;
        _action = action;
        Label = label;
        Icon = icon;
    }

    /// <summary>
    ///     Generic for all code elements
    /// </summary>
    public RelationshipContextCommand(string label, Action<string, string, List<Relationship>> action,
        Func<List<Relationship>, bool>? canExecute = null, ImageSource? icon = null)
    {
        _type = null;
        _action = action;
        _canExecute = canExecute;
        Label = label;
        Icon = icon;
    }

    public string Label { get; }
    public ImageSource? Icon { get; }

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


    public void Invoke(string sourceId, string targetId, List<Relationship> relationships)
    {
        _action.Invoke(sourceId, targetId, relationships);
    }
}